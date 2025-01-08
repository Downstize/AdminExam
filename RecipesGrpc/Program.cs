using EasyNetQ;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;
using Polly;
using Prometheus;
using RecipesGrpc;
using RecipesGrpc.Message;
using RecipesGrpc.Model;
using RecipesGrpc.Services;

var builder = WebApplication.CreateBuilder(args);

// Настройка DbContext
builder.Services.AddDbContext<RecipesDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("PostgresConnection")));

// Регистрация зависимостей
builder.Services.AddSingleton<RedisCacheService>();
builder.Services.AddScoped<RecipesServiceImpl>();

// Подключение RabbitMQ
builder.Services.AddSingleton<IBus>(_ =>
{
    var amqpConnection = builder.Configuration.GetConnectionString("AutoRabbitMQ");
    var bus = RabbitHutch.CreateBus(amqpConnection);
    Console.WriteLine("Подключение к RabbitMQ установлено!");
    return bus;
});

// Настройка логирования
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(5085, listenOptions =>
    {
        listenOptions.Protocols = HttpProtocols.Http2;
    });
    options.ListenAnyIP(5086, listenOptions =>
    {
        listenOptions.Protocols = HttpProtocols.Http1; // Prometheus
    });
});

// Настройка gRPC
builder.Services.AddGrpc();

var app = builder.Build();

// Логика RabbitMQ подписки
var bus = app.Services.GetRequiredService<IBus>();

try
{
    var subscriberId = $"RecipesGrpc@{Environment.MachineName}";

    for (int i = 0; i < 5; i++) // Максимум 5 попыток подписки
    {
        try
        {
            // Подписка на сообщения RabbitMQ
            await bus.PubSub.SubscribeAsync<RecipeMessage>(subscriberId, async message =>
            {
                switch (message)
                {
                    case { Id: 0 }:
                        await HandleCreateRecipeMessage(message, app.Services);
                        break;
                    case { Id: > 0, Name: null }:
                        await HandleDeleteRecipeMessage(message, app.Services);
                        break;
                    default:
                        await HandleUpdateRecipeMessage(message, app.Services);
                        break;
                }
            });

            Console.WriteLine("Успешная подписка на сообщения RabbitMQ.");
            break; // Если подписка успешна, выходим из цикла
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Попытка {i + 1} подписаться на RabbitMQ не удалась: {ex.Message}");
            await Task.Delay(TimeSpan.FromSeconds(5)); // Ждем 5 секунд перед следующей попыткой
        }
    }
}
catch (Exception ex)
{
    Console.WriteLine($"Не удалось подписаться на сообщения RabbitMQ: {ex.Message}");
    throw;
}



static async Task HandleCreateRecipeMessage(RecipeMessage message, IServiceProvider services)
{
    using var scope = services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<RecipesDbContext>();
    var redisCacheService = scope.ServiceProvider.GetRequiredService<RedisCacheService>();

    var recipe = new RecipeModel
    {
        Name = message.Name,
        Ingredients = message.Ingredients,
        PrepTime = message.PrepTime,
        CookTime = message.CookTime,
        Instructions = message.Instructions
    };

    await dbContext.Recipes.AddAsync(recipe);
    await dbContext.SaveChangesAsync();

    // Обновляем кэш всех рецептов
    var allRecipes = await dbContext.Recipes.ToListAsync();
    await redisCacheService.SetCacheAsync("all_recipes", allRecipes, TimeSpan.FromMinutes(10));

    Console.WriteLine($"Создан рецепт: {recipe.Name}");
}



static async Task HandleUpdateRecipeMessage(RecipeMessage message, IServiceProvider services)
{
    using var scope = services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<RecipesDbContext>();
    var redisCacheService = scope.ServiceProvider.GetRequiredService<RedisCacheService>();

    var recipe = await dbContext.Recipes.FindAsync(message.Id);
    if (recipe != null)
    {
        // Обновляем данные рецепта
        recipe.Name = message.Name;
        recipe.Ingredients = message.Ingredients;
        recipe.PrepTime = message.PrepTime;
        recipe.CookTime = message.CookTime;
        recipe.Instructions = message.Instructions;

        dbContext.Recipes.Update(recipe);
        await dbContext.SaveChangesAsync();

        // Удаляем кеш для конкретного рецепта
        var cacheKey = $"recipe_{message.Id}";
        await redisCacheService.RemoveCacheAsync(cacheKey);

        // Обновляем кеш для всех рецептов
        var allRecipes = await dbContext.Recipes.ToListAsync();
        await redisCacheService.SetCacheAsync("all_recipes", allRecipes, TimeSpan.FromMinutes(10));

        Console.WriteLine($"Обновлен рецепт: {recipe.Name}");
    }
    else
    {
        Console.WriteLine($"Рецепт с ID {message.Id} не найден.");
    }
}


static async Task HandleDeleteRecipeMessage(RecipeMessage message, IServiceProvider services)
{
    using var scope = services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<RecipesDbContext>();
    var redisCacheService = scope.ServiceProvider.GetRequiredService<RedisCacheService>();

    var recipe = await dbContext.Recipes.FindAsync(message.Id);
    if (recipe != null)
    {
        dbContext.Recipes.Remove(recipe);
        await dbContext.SaveChangesAsync();

        // Удаляем кеш для конкретного рецепта
        var cacheKey = $"recipe_{message.Id}";
        await redisCacheService.RemoveCacheAsync(cacheKey);

        // Обновляем кеш для всех рецептов
        var allRecipes = await dbContext.Recipes.ToListAsync();
        await redisCacheService.SetCacheAsync("all_recipes", allRecipes, TimeSpan.FromMinutes(10));

        Console.WriteLine($"Удален рецепт: {recipe.Name}");
    }
    else
    {
        Console.WriteLine($"Рецепт с ID {message.Id} не найден.");
    }
}


// Безопасное завершение работы с RabbitMQ
app.Lifetime.ApplicationStopping.Register(() =>
{
    Console.WriteLine("Завершение работы с RabbitMQ...");
    bus.Dispose();
    Console.WriteLine("RabbitMQ завершил работу.");
});

app.MapMetrics();

// Выполнение миграции базы данных с использованием Polly
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<RecipesDbContext>();

    var retryPolicy = Policy
        .Handle<Exception>()
        .WaitAndRetry(5, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
            (exception, timeSpan, retryCount, context) =>
            {
                Console.WriteLine($"Попытка {retryCount} выполнить миграцию через {timeSpan.Seconds} секунд из-за ошибки: {exception.Message}");
            });

    retryPolicy.Execute(() => dbContext.Database.Migrate());
}

// Регистрация gRPC сервиса
app.MapGrpcService<RecipesServiceImpl>();
app.MapGet("/", () => "gRPC Server is running!");

app.Run();

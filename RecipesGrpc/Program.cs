using EasyNetQ;
using Microsoft.EntityFrameworkCore;
using Polly;
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

// Регистрация логики RabbitMQ
builder.Services.AddSingleton<RabbitMqPublisher>();

// Настройка логирования
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

// Настройка gRPC
builder.Services.AddGrpc();

var app = builder.Build();

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
using EasyNetQ;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;
using Polly;
using Prometheus;
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
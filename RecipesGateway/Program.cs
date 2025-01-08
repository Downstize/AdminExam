using Prometheus;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using RecipesGrpc;
using Serilog;
using Serilog.Sinks.Http;
using EasyNetQ;
using RecipesGateway.Logs;

// Настраиваем Serilog
var logger = new LoggerConfiguration()
    .WriteTo.Console() // Логи в консоль
    .WriteTo.Http(
        "http://logstash:5044", // Адрес Logstash
        queueLimitBytes: null, // Без ограничения на размер очереди
        textFormatter: new Serilog.Formatting.Json.JsonFormatter() // Формат JSON
    )
    .CreateLogger();

Log.Logger = logger;

try
{
    Log.Information("Starting up the service");

    var builder = WebApplication.CreateBuilder(args);

    // Настраиваем Serilog как логгер для приложения
    builder.Host.UseSerilog();

    // Регистрируем gRPC клиент
    builder.Services.AddGrpcClient<Recipes.RecipesClient>(o =>
    {
        o.Address = new Uri(builder.Configuration["GrpcSettings:DomainServiceUrl"]);
    });

    // Подключение RabbitMQ
    builder.Services.AddSingleton<IBus>(_ =>
    {
        var amqpConnection = builder.Configuration.GetConnectionString("AutoRabbitMQ");
        var bus = RabbitHutch.CreateBus(amqpConnection);
        Console.WriteLine("Подключение к RabbitMQ установлено!");
        return bus;
    });

    // Регистрация логики RabbitMQ
    builder.Services.AddSingleton<LogProcessor>();

    // Добавляем контроллеры
    builder.Services.AddControllers();

    // Добавляем поддержку Swagger (опционально)
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();

    // Настройка Kestrel для HTTP/1.1 и HTTP/2
    builder.WebHost.ConfigureKestrel(options =>
    {
        options.ListenAnyIP(5002, listenOptions =>
        {
            listenOptions.Protocols = HttpProtocols.Http2; // HTTP/2 для gRPC
        });

        options.ListenAnyIP(8080, listenOptions =>
        {
            listenOptions.Protocols = HttpProtocols.Http1AndHttp2;
        });

        options.ListenAnyIP(5003, listenOptions =>
        {
            listenOptions.Protocols = HttpProtocols.Http1; // HTTP/1.1 для метрик
        });
    });

    var app = builder.Build();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseRouting();
    app.UseHttpMetrics();
    app.UseAuthorization();

    app.MapMetrics();
    app.MapControllers();

    // Запускаем LogProcessor для обработки логов из RabbitMQ
    var logProcessor = app.Services.GetRequiredService<LogProcessor>();
    logProcessor.StartListening();

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "The application failed to start correctly");
}
finally
{
    Log.CloseAndFlush();
}

using Prometheus;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using RecipesGrpc;
using Serilog;
using EasyNetQ;
using RecipesGateway.Logs;

var logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.Http(
        "http://logstash:5044",
        queueLimitBytes: null,
        textFormatter: new Serilog.Formatting.Json.JsonFormatter()
    )
    .CreateLogger();

Log.Logger = logger;

try
{
    Log.Information("Starting up the service");

    var builder = WebApplication.CreateBuilder(args);
    
    builder.Host.UseSerilog();
    
    builder.Services.AddGrpcClient<Recipes.RecipesClient>(o =>
    {
        o.Address = new Uri(builder.Configuration["GrpcSettings:DomainServiceUrl"]);
    });
    
    builder.Services.AddSingleton<IBus>(_ =>
    {
        var amqpConnection = builder.Configuration.GetConnectionString("AutoRabbitMQ");
        var bus = RabbitHutch.CreateBus(amqpConnection);
        Console.WriteLine("Подключение к RabbitMQ установлено!");
        return bus;
    });
    
    builder.Services.AddSingleton<LogProcessor>();
    
    builder.Services.AddControllers();
    
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();
    
    builder.WebHost.ConfigureKestrel(options =>
    {
        options.ListenAnyIP(5002, listenOptions =>
        {
            listenOptions.Protocols = HttpProtocols.Http2;
        });

        options.ListenAnyIP(8080, listenOptions =>
        {
            listenOptions.Protocols = HttpProtocols.Http1AndHttp2;
        });

        options.ListenAnyIP(5003, listenOptions =>
        {
            listenOptions.Protocols = HttpProtocols.Http1;
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

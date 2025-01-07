using Prometheus;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using RecipesGrpc;

var builder = WebApplication.CreateBuilder(args);

// Регистрируем gRPC клиент
builder.Services.AddGrpcClient<Recipes.RecipesClient>(o =>
{
    o.Address = new Uri(builder.Configuration["GrpcSettings:DomainServiceUrl"]);
});

// Добавляем контроллеры
builder.Services.AddControllers();

// Добавляем поддержку Swagger (опционально)
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Настройка Kestrel для HTTP/1.1 и HTTP/2
builder.WebHost.ConfigureKestrel(options =>
{
    // Порт для основного функционала (например, gRPC)
    options.ListenAnyIP(5002, listenOptions =>
    {
        listenOptions.Protocols = HttpProtocols.Http2; // HTTP/2 для gRPC
    });
    
    options.ListenAnyIP(8080, listenOptions =>
    {
        listenOptions.Protocols = HttpProtocols.Http1AndHttp2;
    });

    // Порт для Prometheus метрик
    options.ListenAnyIP(5003, listenOptions =>
    {
        listenOptions.Protocols = HttpProtocols.Http1; // HTTP/1.1 для метрик
    });
});

var app = builder.Build();

// Используем Swagger в режиме разработки
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Настройка метрик
app.UseRouting(); // Убедитесь, что UseRouting идет до MapMetrics
app.UseHttpMetrics(); // Добавляет автоматический сбор HTTP метрик

app.UseAuthorization();

// Регистрируем эндпоинты метрик
app.MapMetrics(); // Эндпоинт /metrics для Prometheus

app.MapControllers();

app.Run();
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

var app = builder.Build();

// Используем Swagger в режиме разработки
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Добавляем поддержку маршрутизации и контроллеров
app.UseRouting();

app.UseAuthorization();

app.MapControllers();

app.Run();
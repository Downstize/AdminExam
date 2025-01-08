using EasyNetQ;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using RecipesGrpc;
using RecipesGrpc.Message;
using RecipesGrpc.Model;
using RecipesGrpc.Services;

public class RecipesServiceImpl : Recipes.RecipesBase
{
    private readonly ILogger<RecipesServiceImpl> _logger;
    private readonly RedisCacheService _redisCacheService;
    private readonly RecipesDbContext _dbContext;
    private readonly IBus _rabbitMqBus;

    public RecipesServiceImpl(
        ILogger<RecipesServiceImpl> logger,
        RecipesDbContext dbContext,
        RedisCacheService redisCacheService,
        IBus rabbitMqBus)
    {
        _logger = logger;
        _dbContext = dbContext;
        _redisCacheService = redisCacheService;
        _rabbitMqBus = rabbitMqBus;
    }

    public override async Task<CreateRecipeResponse> CreateRecipe(CreateRecipeRequest request, ServerCallContext context)
{
    try
    {
        _logger.LogInformation("CreateRecipe called with Name: {Name}", request.Name);

        // Сохраняем рецепт в базе данных
        var recipe = new RecipeModel
        {
            Name = request.Name,
            Ingredients = request.Ingredients,
            PrepTime = request.PrepTime,
            CookTime = request.CookTime,
            Instructions = request.Instructions
        };

        await _dbContext.Recipes.AddAsync(recipe);
        await _dbContext.SaveChangesAsync();

        // Обновляем кэш всех рецептов
        var allRecipes = await _dbContext.Recipes.ToListAsync();
        await _redisCacheService.SetCacheAsync("all_recipes", allRecipes, TimeSpan.FromMinutes(10));

        // Отправляем сообщение в RabbitMQ
        var recipeMessage = new RecipeMessage
        {
            Id = recipe.Id,
            Name = recipe.Name,
            Ingredients = recipe.Ingredients,
            PrepTime = recipe.PrepTime,
            CookTime = recipe.CookTime,
            Instructions = recipe.Instructions
        };
        await _rabbitMqBus.PubSub.PublishAsync(recipeMessage, "recipes.create");

        _logger.LogInformation("Recipe created with ID: {Id} and message sent to RabbitMQ.", recipe.Id);

        // Возвращаем ID созданного рецепта
        return new CreateRecipeResponse
        {
            Id = recipe.Id
        };
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error in CreateRecipe for Name: {Name}", request.Name);
        throw new RpcException(new Status(StatusCode.Internal, "An internal error occurred in CreateRecipe"));
    }
}


    public override async Task<UpdateRecipeResponse> UpdateRecipe(UpdateRecipeRequest request, ServerCallContext context)
    {
        try
        {
            _logger.LogInformation("UpdateRecipe called with ID: {Id}", request.Id);

            // Формируем сообщение и публикуем его в RabbitMQ
            var recipeMessage = new RecipeMessage
            {
                Id = request.Id,
                Name = request.Name,
                Ingredients = request.Ingredients,
                PrepTime = request.PrepTime,
                CookTime = request.CookTime,
                Instructions = request.Instructions
            };
            await _rabbitMqBus.PubSub.PublishAsync(recipeMessage, "recipes.update");

            _logger.LogInformation("Recipe update message sent to RabbitMQ.");

            return new UpdateRecipeResponse { Success = true };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in UpdateRecipe for ID: {Id}", request.Id);
            throw;
        }
    }

    public override async Task<DeleteRecipeResponse> DeleteRecipe(DeleteRecipeRequest request, ServerCallContext context)
    {
        try
        {
            _logger.LogInformation("DeleteRecipe called with ID: {Id}", request.Id);

            // Формируем сообщение и публикуем его в RabbitMQ
            var recipeMessage = new RecipeMessage
            {
                Id = request.Id
            };
            await _rabbitMqBus.PubSub.PublishAsync(recipeMessage, "recipes.delete");

            _logger.LogInformation("Recipe delete message sent to RabbitMQ.");

            return new DeleteRecipeResponse { Success = true };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in DeleteRecipe for ID: {Id}", request.Id);
            throw;
        }
    }



    public override async Task<GetRecipeResponse> GetRecipe(GetRecipeRequest request, ServerCallContext context)
    {
        try
        {
            _logger.LogInformation("GetRecipe called with ID: {Id}", request.Id);

            var cacheKey = $"recipe_{request.Id}";
            var cachedRecipe = await _redisCacheService.GetCacheAsync<RecipeModel>(cacheKey);

            if (cachedRecipe != null)
            {
                _logger.LogInformation("GetRecipe cache hit for Recipe ID: {Id}", request.Id);
                await PublishLogToRabbitMQ("GetRecipe", request.Id, "Cache hit");

                // Немедленно возвращаем кешированные данные
                return new GetRecipeResponse
                {
                    Recipe = new Recipe
                    {
                        Id = cachedRecipe.Id,
                        Name = cachedRecipe.Name,
                        Ingredients = cachedRecipe.Ingredients,
                        PrepTime = cachedRecipe.PrepTime,
                        CookTime = cachedRecipe.CookTime,
                        Instructions = cachedRecipe.Instructions
                    }
                };
            }

            // Добавляем задержку для демонстрации работы с некешированными данными
            _logger.LogInformation("Cache miss for Recipe ID: {Id}. Simulating delay.", request.Id);
            await Task.Delay(TimeSpan.FromSeconds(3));

            var recipe = await _dbContext.Recipes.FindAsync(request.Id);
            if (recipe == null)
            {
                _logger.LogWarning("GetRecipe failed: Recipe with ID {Id} not found", request.Id);
                await PublishLogToRabbitMQ("GetRecipe", request.Id, "Recipe not found");
                throw new RpcException(new Status(StatusCode.NotFound, $"Recipe with ID {request.Id} not found."));
            }

            await _redisCacheService.SetCacheAsync(cacheKey, recipe, TimeSpan.FromMinutes(10));
            await PublishLogToRabbitMQ("GetRecipe", request.Id, "Recipe retrieved from database");

            _logger.LogInformation("GetRecipe succeeded for Recipe ID: {Id}", request.Id);

            return new GetRecipeResponse
            {
                Recipe = new Recipe
                {
                    Id = recipe.Id,
                    Name = recipe.Name,
                    Ingredients = recipe.Ingredients,
                    PrepTime = recipe.PrepTime,
                    CookTime = recipe.CookTime,
                    Instructions = recipe.Instructions
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetRecipe for Recipe ID: {Id}", request.Id);
            await PublishLogToRabbitMQ("GetRecipe", request.Id, $"Error: {ex.Message}");
            throw;
        }
    }


    public override async Task<ListRecipesResponse> ListRecipes(ListRecipesRequest request, ServerCallContext context)
    {
        try
        {
            _logger.LogInformation("ListRecipes called");

            var cacheKey = "all_recipes";
            var cachedRecipes = await _redisCacheService.GetCacheAsync<List<RecipeModel>>(cacheKey);

            if (cachedRecipes != null)
            {
                _logger.LogInformation("ListRecipes cache hit. Returning cached recipes.");
                await PublishLogToRabbitMQ("ListRecipes", null, "Cache hit for ListRecipes");

                var response = new ListRecipesResponse();
                response.Recipes.AddRange(cachedRecipes.Select(r => new Recipe
                {
                    Id = r.Id,
                    Name = r.Name,
                    Ingredients = r.Ingredients,
                    PrepTime = r.PrepTime,
                    CookTime = r.CookTime,
                    Instructions = r.Instructions
                }));

                return response;
            }

            _logger.LogInformation("Cache miss for ListRecipes. Simulating delay.");
            await Task.Delay(TimeSpan.FromSeconds(3)); // Задержка для демонстрации работы без кеша

            var recipes = await _dbContext.Recipes.ToListAsync();
            if (recipes == null || recipes.Count == 0)
            {
                _logger.LogWarning("ListRecipes found no recipes.");
                await PublishLogToRabbitMQ("ListRecipes", null, "No recipes found in database.");
                return new ListRecipesResponse(); // Возвращаем пустой ответ
            }

            // Сохраняем результат в кэш
            await _redisCacheService.SetCacheAsync(cacheKey, recipes, TimeSpan.FromMinutes(10));
            _logger.LogInformation("ListRecipes succeeded: Found {Count} recipes", recipes.Count);

            await PublishLogToRabbitMQ("ListRecipes", null, $"Found {recipes.Count} recipes");

            var responseWithDbData = new ListRecipesResponse();
            responseWithDbData.Recipes.AddRange(recipes.Select(r => new Recipe
            {
                Id = r.Id,
                Name = r.Name,
                Ingredients = r.Ingredients,
                PrepTime = r.PrepTime,
                CookTime = r.CookTime,
                Instructions = r.Instructions
            }));

            return responseWithDbData;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in ListRecipes");
            await PublishLogToRabbitMQ("ListRecipes", null, $"Error: {ex.Message}");
            throw;
        }
    }


    private async Task PublishLogToRabbitMQ(string action, int? recipeId, string message)
    {
        var logMessage = new LogMessage
        {
            Timestamp = DateTime.UtcNow,
            Action = action,
            RecipeId = recipeId,
            Message = message,
            Service = "RecipesService"
        };

        await _rabbitMqBus.PubSub.PublishAsync(logMessage, "logs_queue");
    }

}

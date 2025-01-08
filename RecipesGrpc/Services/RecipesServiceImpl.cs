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

            var recipe = new RecipeModel
            {
                Name = request.Name,
                Ingredients = request.Ingredients,
                PrepTime = request.PrepTime,
                CookTime = request.CookTime,
                Instructions = request.Instructions
            };

            _dbContext.Recipes.Add(recipe);
            await _dbContext.SaveChangesAsync();

            var cacheKey = $"recipe_{recipe.Id}";
            await _redisCacheService.SetCacheAsync(cacheKey, recipe, TimeSpan.FromMinutes(10));

            await PublishLogToRabbitMQ("CreateRecipe", recipe.Id, "Recipe created successfully");

            _logger.LogInformation("CreateRecipe succeeded for Recipe ID: {Id}", recipe.Id);

            return new CreateRecipeResponse { Id = recipe.Id };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in CreateRecipe for Name: {Name}", request.Name);
            await PublishLogToRabbitMQ("CreateRecipe", null, $"Error: {ex.Message}");
            throw new RpcException(new Status(StatusCode.Internal, "An internal error occurred in CreateRecipe"));
        }
    }

    public override async Task<UpdateRecipeResponse> UpdateRecipe(UpdateRecipeRequest request, ServerCallContext context)
    {
        try
        {
            _logger.LogInformation("UpdateRecipe called with ID: {Id}", request.Id);

            var recipe = await _dbContext.Recipes.FindAsync(request.Id);
            if (recipe == null)
            {
                _logger.LogWarning("UpdateRecipe failed: Recipe with ID {Id} not found", request.Id);
                await PublishLogToRabbitMQ("UpdateRecipe", request.Id, "Recipe not found");
                throw new RpcException(new Status(StatusCode.NotFound, $"Recipe with ID {request.Id} not found."));
            }

            recipe.Name = request.Name;
            recipe.Ingredients = request.Ingredients;
            recipe.PrepTime = request.PrepTime;
            recipe.CookTime = request.CookTime;
            recipe.Instructions = request.Instructions;

            await _dbContext.SaveChangesAsync();

            var cacheKey = $"recipe_{recipe.Id}";
            await _redisCacheService.SetCacheAsync(cacheKey, recipe, TimeSpan.FromMinutes(10));

            await PublishLogToRabbitMQ("UpdateRecipe", recipe.Id, "Recipe updated successfully");

            _logger.LogInformation("UpdateRecipe succeeded for Recipe ID: {Id}", recipe.Id);

            return new UpdateRecipeResponse { Success = true };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in UpdateRecipe for ID: {Id}", request.Id);
            await PublishLogToRabbitMQ("UpdateRecipe", request.Id, $"Error: {ex.Message}");
            throw;
        }
    }

    public override async Task<DeleteRecipeResponse> DeleteRecipe(DeleteRecipeRequest request, ServerCallContext context)
    {
        try
        {
            _logger.LogInformation("DeleteRecipe called with ID: {Id}", request.Id);

            var recipe = await _dbContext.Recipes.FindAsync(request.Id);
            if (recipe == null)
            {
                _logger.LogWarning("DeleteRecipe failed: Recipe with ID {Id} not found", request.Id);
                await PublishLogToRabbitMQ("DeleteRecipe", request.Id, "Recipe not found");
                throw new RpcException(new Status(StatusCode.NotFound, $"Recipe with ID {request.Id} not found."));
            }

            _dbContext.Recipes.Remove(recipe);
            await _dbContext.SaveChangesAsync();

            var cacheKey = $"recipe_{recipe.Id}";
            await _redisCacheService.RemoveCacheAsync(cacheKey);

            await PublishLogToRabbitMQ("DeleteRecipe", recipe.Id, "Recipe deleted successfully");

            _logger.LogInformation("DeleteRecipe succeeded for Recipe ID: {Id}", recipe.Id);

            return new DeleteRecipeResponse { Success = true };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in DeleteRecipe for ID: {Id}", request.Id);
            await PublishLogToRabbitMQ("DeleteRecipe", request.Id, $"Error: {ex.Message}");
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

            var recipes = await _dbContext.Recipes.ToListAsync();

            _logger.LogInformation("ListRecipes succeeded: Found {Count} recipes", recipes.Count);

            await PublishLogToRabbitMQ("ListRecipes", null, $"Found {recipes.Count} recipes");

            var response = new ListRecipesResponse();
            response.Recipes.AddRange(recipes.Select(r => new Recipe
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

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

        await _rabbitMqBus.PubSub.PublishAsync(new RecipeMessage
        {
            Id = recipe.Id,
            Name = recipe.Name,
            Ingredients = recipe.Ingredients,
            PrepTime = recipe.PrepTime,
            CookTime = recipe.CookTime,
            Instructions = recipe.Instructions
        });

        return new CreateRecipeResponse { Id = recipe.Id };
    }

    public override async Task<UpdateRecipeResponse> UpdateRecipe(UpdateRecipeRequest request, ServerCallContext context)
    {
        var recipe = await _dbContext.Recipes.FindAsync(request.Id);
        if (recipe == null)
        {
            throw new RpcException(new Status(StatusCode.NotFound, $"Рецепт с ID {request.Id} не найден."));
        }

        recipe.Name = request.Name;
        recipe.Ingredients = request.Ingredients;
        recipe.PrepTime = request.PrepTime;
        recipe.CookTime = request.CookTime;
        recipe.Instructions = request.Instructions;

        await _dbContext.SaveChangesAsync();

        var cacheKey = $"recipe_{recipe.Id}";
        await _redisCacheService.SetCacheAsync(cacheKey, recipe, TimeSpan.FromMinutes(10));

        await _rabbitMqBus.PubSub.PublishAsync(new RecipeMessage
        {
            Id = recipe.Id,
            Name = recipe.Name,
            Ingredients = recipe.Ingredients,
            PrepTime = recipe.PrepTime,
            CookTime = recipe.CookTime,
            Instructions = recipe.Instructions
        });

        return new UpdateRecipeResponse { Success = true };
    }

    public override async Task<DeleteRecipeResponse> DeleteRecipe(DeleteRecipeRequest request, ServerCallContext context)
    {
        var recipe = await _dbContext.Recipes.FindAsync(request.Id);
        if (recipe == null)
        {
            throw new RpcException(new Status(StatusCode.NotFound, $"Рецепт с ID {request.Id} не найден."));
        }

        _dbContext.Recipes.Remove(recipe);
        await _dbContext.SaveChangesAsync();

        var cacheKey = $"recipe_{recipe.Id}";
        await _redisCacheService.RemoveCacheAsync(cacheKey);

        await _rabbitMqBus.PubSub.PublishAsync(new RecipeMessage
        {
            Id = recipe.Id
        });

        return new DeleteRecipeResponse { Success = true };
    }

    public override async Task<GetRecipeResponse> GetRecipe(GetRecipeRequest request, ServerCallContext context)
    {
        var cacheKey = $"recipe_{request.Id}";
        var cachedRecipe = await _redisCacheService.GetCacheAsync<RecipeModel>(cacheKey);

        if (cachedRecipe != null)
        {
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
            throw new RpcException(new Status(StatusCode.NotFound, $"Рецепт с ID {request.Id} не найден."));
        }

        await _redisCacheService.SetCacheAsync(cacheKey, recipe, TimeSpan.FromMinutes(10));

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


    public override async Task<ListRecipesResponse> ListRecipes(ListRecipesRequest request, ServerCallContext context)
    {
        _logger.LogInformation("ListRecipes called");
        var recipes = await _dbContext.Recipes.ToListAsync();
        _logger.LogInformation($"Found {recipes.Count} recipes");

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

        _logger.LogInformation("Returning response for ListRecipes");
        return response;
    }

}

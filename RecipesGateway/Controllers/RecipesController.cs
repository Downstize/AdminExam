using Grpc.Net.Client;
using Microsoft.AspNetCore.Mvc;
using RecipesGrpc;

[Route("api/[controller]")]
[ApiController]
public class RecipesController : ControllerBase
{
    private readonly Recipes.RecipesClient _client;
    private readonly ILogger<RecipesController> _logger;

    public RecipesController(IConfiguration configuration, ILogger<RecipesController> logger)
    {
        var grpcChannel = GrpcChannel.ForAddress(configuration["GrpcSettings:DomainServiceUrl"]);
        _client = new Recipes.RecipesClient(grpcChannel);
        _logger = logger;
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetRecipe(int id)
    {
        _logger.LogInformation("GetRecipe called with ID: {Id}", id);

        try
        {
            var request = new GetRecipeRequest { Id = id };
            var response = await _client.GetRecipeAsync(request);

            _logger.LogInformation("GetRecipe succeeded for ID: {Id}", id);
            return Ok(response.Recipe);
        }
        catch (Grpc.Core.RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.NotFound)
        {
            _logger.LogWarning("GetRecipe failed for ID: {Id}. Reason: {Reason}", id, ex.Status.Detail);
            return NotFound(new { Message = $"Recipe with ID {id} not found." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in GetRecipe for ID: {Id}", id);
            return StatusCode(500, new { Message = "An internal server error occurred." });
        }
    }

    [HttpGet]
    public async Task<IActionResult> ListRecipes()
    {
        _logger.LogInformation("ListRecipes called");

        try
        {
            var response = await _client.ListRecipesAsync(new ListRecipesRequest());

            _logger.LogInformation("ListRecipes succeeded. Found {Count} recipes", response.Recipes.Count);
            return Ok(response.Recipes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in ListRecipes");
            return StatusCode(500, new { Message = "An internal server error occurred." });
        }
    }

    [HttpPost]
    public async Task<IActionResult> CreateRecipe([FromBody] CreateRecipeRequest request)
    {
        _logger.LogInformation("CreateRecipe called with Name: {Name}", request.Name);

        try
        {
            var response = await _client.CreateRecipeAsync(request);

            _logger.LogInformation("CreateRecipe succeeded with ID: {Id}", response.Id);
            return CreatedAtAction(nameof(GetRecipe), new { id = response.Id }, response);
        }
        catch (Grpc.Core.RpcException ex)
        {
            _logger.LogError(ex, "CreateRecipe failed with gRPC error. Status: {Status}, Detail: {Detail}", ex.StatusCode, ex.Status.Detail);
            return StatusCode(500, new { Message = ex.Status.Detail });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in CreateRecipe");
            return StatusCode(500, new { Message = "An internal server error occurred." });
        }
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateRecipe(int id, [FromBody] UpdateRecipeRequest request)
    {
        _logger.LogInformation("UpdateRecipe called with ID: {Id}", id);

        if (id != request.Id)
        {
            _logger.LogWarning("UpdateRecipe failed: ID in URL ({UrlId}) does not match ID in body ({BodyId})", id, request.Id);
            return BadRequest(new { Message = "ID in the URL does not match ID in the body." });
        }

        try
        {
            var response = await _client.UpdateRecipeAsync(request);

            if (response.Success)
            {
                _logger.LogInformation("UpdateRecipe succeeded for ID: {Id}", id);
                return NoContent();
            }
            else
            {
                _logger.LogWarning("UpdateRecipe failed: Update operation unsuccessful for ID: {Id}", id);
                return BadRequest(new { Message = "Failed to update recipe." });
            }
        }
        catch (Grpc.Core.RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.NotFound)
        {
            _logger.LogWarning("UpdateRecipe failed for ID: {Id}. Reason: {Reason}", id, ex.Status.Detail);
            return NotFound(new { Message = $"Recipe with ID {id} not found." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in UpdateRecipe for ID: {Id}", id);
            return StatusCode(500, new { Message = "An internal server error occurred." });
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteRecipe(int id)
    {
        _logger.LogInformation("DeleteRecipe called with ID: {Id}", id);

        try
        {
            var response = await _client.DeleteRecipeAsync(new DeleteRecipeRequest { Id = id });

            if (response.Success)
            {
                _logger.LogInformation("DeleteRecipe succeeded for ID: {Id}", id);
                return NoContent();
            }
            else
            {
                _logger.LogWarning("DeleteRecipe failed: Delete operation unsuccessful for ID: {Id}", id);
                return BadRequest(new { Message = "Failed to delete recipe." });
            }
        }
        catch (Grpc.Core.RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.NotFound)
        {
            _logger.LogWarning("DeleteRecipe failed for ID: {Id}. Reason: {Reason}", id, ex.Status.Detail);
            return NotFound(new { Message = $"Recipe with ID {id} not found." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in DeleteRecipe for ID: {Id}", id);
            return StatusCode(500, new { Message = "An internal server error occurred." });
        }
    }
}

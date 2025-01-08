using Grpc.Net.Client;
using Microsoft.AspNetCore.Mvc;
using RecipesGrpc;

[Route("api/[controller]")]
[ApiController]
public class RecipesController : ControllerBase
{
    private readonly Recipes.RecipesClient _client;
    private readonly ILogger<RecipesController> _logger;

    public RecipesController(IConfiguration configuration,ILogger<RecipesController> logger)
    {
        var grpcChannel = GrpcChannel.ForAddress(configuration["GrpcSettings:DomainServiceUrl"]);
        _client = new Recipes.RecipesClient(grpcChannel);
        _logger = logger;
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetRecipe(int id)
    {
        try
        {
            var request = new GetRecipeRequest { Id = id };
            var response = await _client.GetRecipeAsync(request);
            return Ok(response.Recipe);
        }
        catch (Grpc.Core.RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.NotFound)
        {
            return NotFound(new { Message = $"Recipe with ID {id} not found." });
        }
    }

    [HttpGet]
    public async Task<IActionResult> ListRecipes()
    {
        var response = await _client.ListRecipesAsync(new ListRecipesRequest());
        return Ok(response.Recipes);
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
            _logger.LogError(ex, "CreateRecipe failed with unexpected error.");
            return StatusCode(500, new { Message = "An internal server error occurred." });
        }
    }


    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateRecipe(int id, [FromBody] UpdateRecipeRequest request)
    {
        if (id != request.Id)
        {
            return BadRequest(new { Message = "ID in the URL does not match ID in the body." });
        }

        try
        {
            var response = await _client.UpdateRecipeAsync(request);
            return response.Success ? NoContent() : BadRequest(new { Message = "Failed to update recipe." });
        }
        catch (Grpc.Core.RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.NotFound)
        {
            return NotFound(new { Message = $"Recipe with ID {id} not found." });
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteRecipe(int id)
    {
        try
        {
            var response = await _client.DeleteRecipeAsync(new DeleteRecipeRequest { Id = id });
            return response.Success ? NoContent() : BadRequest(new { Message = "Failed to delete recipe." });
        }
        catch (Grpc.Core.RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.NotFound)
        {
            return NotFound(new { Message = $"Recipe with ID {id} not found." });
        }
    }
}

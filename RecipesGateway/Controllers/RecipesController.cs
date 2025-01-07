using Grpc.Net.Client;
using Microsoft.AspNetCore.Mvc;
using RecipesGrpc;

[Route("api/[controller]")]
[ApiController]
public class RecipesController : ControllerBase
{
    private readonly Recipes.RecipesClient _client;

    public RecipesController(IConfiguration configuration)
    {
        var grpcChannel = GrpcChannel.ForAddress(configuration["GrpcSettings:DomainServiceUrl"]);
        _client = new Recipes.RecipesClient(grpcChannel);
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
        var response = await _client.CreateRecipeAsync(request);
        return CreatedAtAction(nameof(GetRecipe), new { id = response.Id }, response);
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

namespace RecipesGrpc.Message;

public class RecipeMessage
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Ingredients { get; set; }
    public int PrepTime { get; set; }
    public int CookTime { get; set; }
    public string Instructions { get; set; }
}

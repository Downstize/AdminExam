namespace RecipesGrpc.Model;

using System.ComponentModel.DataAnnotations;

public class RecipeModel
{
    [Key]
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Ingredients { get; set; } = string.Empty;
    public int PrepTime { get; set; }
    public int CookTime { get; set; }
    public string Instructions { get; set; } = string.Empty;
}

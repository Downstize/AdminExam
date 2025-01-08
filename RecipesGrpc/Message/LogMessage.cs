namespace RecipesGrpc.Message;

public class LogMessage
{
    public DateTime Timestamp { get; set; }
    public string Action { get; set; }
    public int? RecipeId { get; set; }
    public string Message { get; set; }
    public string Service { get; set; }
}

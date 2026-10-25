namespace AIGamingOptimizer.Models;

public sealed class AiOptions
{
    public string ApiKey { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "https://api.openai.com/v1";
    public string Model { get; set; } = "gpt-4o-mini";
    public string AuthStyle { get; set; } = "Auto";
}

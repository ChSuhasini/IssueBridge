namespace IssueBridge.Api.Assistant.Model;

public class AnthropicOptions
{
    public const string SectionName = "Anthropic";

    public required string ApiKey { get; set; }
    public required string Model { get; set; }
    public string ApiBaseUrl { get; set; } = "https://api.anthropic.com/v1/";
}

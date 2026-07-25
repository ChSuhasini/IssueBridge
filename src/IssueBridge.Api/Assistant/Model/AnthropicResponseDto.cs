using System.Text.Json;
using System.Text.Json.Serialization;

namespace IssueBridge.Api.Assistant.Model;

public class AnthropicResponseDto
{
    public string? Id { get; set; }
    public string? Role { get; set; }

    // Left as raw elements rather than a typed union — each block's shape
    // depends on its own "type" field ("text" vs "tool_use"), which
    // AnthropicModelClient.ParseResponse inspects directly.
    public List<JsonElement> Content { get; set; } = new();

    [JsonPropertyName("stop_reason")]
    public string? StopReason { get; set; }
}

public class AnthropicErrorResponseDto
{
    public AnthropicErrorDetailDto? Error { get; set; }
}

public class AnthropicErrorDetailDto
{
    public string? Type { get; set; }
    public string? Message { get; set; }
}

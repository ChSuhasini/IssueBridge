using System.Text.Json.Serialization;

namespace IssueBridge.Api.Assistant.Model;

public class AnthropicRequestDto
{
    public required string Model { get; set; }

    [JsonPropertyName("max_tokens")]
    public int MaxTokens { get; set; } = 1024;

    public List<AnthropicToolDefinitionDto>? Tools { get; set; }

    public required List<AnthropicRequestMessageDto> Messages { get; set; }
}

public class AnthropicRequestMessageDto
{
    public required string Role { get; set; }

    // Either a List<object> of content blocks built by the agent loop, or a
    // List<JsonElement> echoed straight from a prior AnthropicResponseDto —
    // both serialize correctly since System.Text.Json resolves object-typed
    // members by their runtime type.
    public required object Content { get; set; }
}

public class AnthropicToolDefinitionDto
{
    public required string Name { get; set; }
    public required string Description { get; set; }

    [JsonPropertyName("input_schema")]
    public required System.Text.Json.Nodes.JsonObject InputSchema { get; set; }
}

public class TextContentBlockDto
{
    public string Type => "text";
    public required string Text { get; set; }
}

public class ToolResultContentBlockDto
{
    public string Type => "tool_result";

    [JsonPropertyName("tool_use_id")]
    public required string ToolUseId { get; set; }

    public required string Content { get; set; }

    [JsonPropertyName("is_error")]
    public bool? IsError { get; set; }
}

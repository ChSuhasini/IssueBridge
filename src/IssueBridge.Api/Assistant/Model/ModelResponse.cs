namespace IssueBridge.Api.Assistant.Model;

// Provider-agnostic result of one model turn. RawAssistantContent is opaque to
// everything except the provider client that produced it — the agent loop just
// hands it back on the next ModelMessage without interpreting it.
public class ModelResponse
{
    public required ModelStopReason StopReason { get; init; }
    public string? Text { get; init; }
    public List<ModelToolCall> ToolCalls { get; init; } = new();
    public string? ErrorMessage { get; init; }
    public object? RawAssistantContent { get; init; }

    public static ModelResponse Failed(string errorMessage) =>
        new() { StopReason = ModelStopReason.Error, ErrorMessage = errorMessage };

    public static ModelResponse Empty() =>
        new() { StopReason = ModelStopReason.Empty };
}

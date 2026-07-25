namespace IssueBridge.Api.Assistant.Model;

// Content is deliberately `object`, not a fixed shape: it's either a list of
// content blocks the agent loop builds itself (text / tool_result), or the raw
// content a provider's own response returned (echoed back verbatim so the
// provider can keep track of its own tool_use turn). The agent loop never
// inspects it — only the provider client that produced/consumes it does.
public class ModelMessage
{
    public required string Role { get; init; } // "user" | "assistant"
    public required object Content { get; init; }
}

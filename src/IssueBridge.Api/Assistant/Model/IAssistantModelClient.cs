using IssueBridge.Api.Assistant.Tools;

namespace IssueBridge.Api.Assistant.Model;

// Behind this interface so the model provider (currently Anthropic) can be
// swapped without touching the agent loop. Must never throw — any failure
// (network, non-2xx, malformed body) is reported via ModelResponse.Failed so
// the agent loop can handle it as a normal, bounded outcome.
public interface IAssistantModelClient
{
    Task<ModelResponse> SendAsync(
        IReadOnlyList<ModelMessage> messages,
        IReadOnlyList<IAssistantTool> tools,
        CancellationToken cancellationToken);
}

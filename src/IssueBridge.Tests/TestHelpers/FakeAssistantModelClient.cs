using IssueBridge.Api.Assistant.Model;
using IssueBridge.Api.Assistant.Tools;

namespace IssueBridge.Tests.TestHelpers;

// A scripted stand-in for IAssistantModelClient: returns queued responses in
// order, regardless of what messages it was sent. Records every call's
// message list so a test can inspect the conversation the agent loop built.
public class FakeAssistantModelClient : IAssistantModelClient
{
    private readonly Queue<ModelResponse> _responses;

    public List<IReadOnlyList<ModelMessage>> CallHistory { get; } = new();

    public FakeAssistantModelClient(params ModelResponse[] responses)
    {
        _responses = new Queue<ModelResponse>(responses);
    }

    public Task<ModelResponse> SendAsync(
        IReadOnlyList<ModelMessage> messages,
        IReadOnlyList<IAssistantTool> tools,
        CancellationToken cancellationToken)
    {
        CallHistory.Add(messages);

        if (_responses.Count == 0)
        {
            throw new InvalidOperationException("FakeAssistantModelClient ran out of scripted responses.");
        }

        return Task.FromResult(_responses.Dequeue());
    }
}

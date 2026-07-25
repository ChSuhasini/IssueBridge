using System.Text.Json;
using IssueBridge.Api.Assistant.Model;
using IssueBridge.Api.Assistant.Tools;

namespace IssueBridge.Api.Assistant;

// Runs one question through a bounded model <-> tool loop. Every tool call is
// executed exclusively through AssistantToolExecutor — this class never talks
// to the database, generates SQL, or calls a write endpoint. No conversation
// history is kept between requests; each AskAsync call starts fresh.
public class AssistantAgent
{
    private const int MaxIterations = 3;

    private readonly IAssistantModelClient _modelClient;
    private readonly AssistantToolExecutor _toolExecutor;

    public AssistantAgent(IAssistantModelClient modelClient, AssistantToolExecutor toolExecutor)
    {
        _modelClient = modelClient;
        _toolExecutor = toolExecutor;
    }

    public async Task<AssistantAskResult> AskAsync(string question, CancellationToken cancellationToken)
    {
        var tools = _toolExecutor.Tools.ToList();
        var toolsCalled = new List<string>();

        var messages = new List<ModelMessage>
        {
            new()
            {
                Role = "user",
                Content = new List<object> { new TextContentBlockDto { Text = question } }
            }
        };

        for (var iteration = 1; iteration <= MaxIterations; iteration++)
        {
            var response = await _modelClient.SendAsync(messages, tools, cancellationToken);

            switch (response.StopReason)
            {
                case ModelStopReason.Error:
                    return AssistantAskResult.Failed(
                        AssistantRequestStatus.ModelError,
                        toolsCalled,
                        response.ErrorMessage ?? "The model provider returned an error.");

                case ModelStopReason.Empty:
                    return AssistantAskResult.Failed(
                        AssistantRequestStatus.EmptyResponse,
                        toolsCalled,
                        "The model returned no usable content.");

                case ModelStopReason.EndTurn:
                    return AssistantAskResult.Succeeded(response.Text ?? string.Empty, toolsCalled);

                case ModelStopReason.ToolUse:
                    messages.Add(new ModelMessage { Role = "assistant", Content = response.RawAssistantContent! });
                    messages.Add(new ModelMessage
                    {
                        Role = "user",
                        Content = await RunToolCallsAsync(response.ToolCalls, toolsCalled, cancellationToken)
                    });
                    break;
            }
        }

        return AssistantAskResult.Failed(
            AssistantRequestStatus.IterationLimitReached,
            toolsCalled,
            "The assistant could not produce a final answer within the tool-call limit.");
    }

    private async Task<List<object>> RunToolCallsAsync(
        IReadOnlyList<ModelToolCall> toolCalls,
        List<string> toolsCalled,
        CancellationToken cancellationToken)
    {
        var toolResultBlocks = new List<object>();

        foreach (var call in toolCalls)
        {
            toolsCalled.Add(call.Name);
            var result = await _toolExecutor.ExecuteAsync(call.Name, call.Arguments, cancellationToken);

            toolResultBlocks.Add(new ToolResultContentBlockDto
            {
                ToolUseId = call.Id,
                Content = FormatForModel(result),
                IsError = result.Status != ToolExecutionStatus.Success
            });
        }

        return toolResultBlocks;
    }

    private static string FormatForModel(ToolExecutionResult result) =>
        result.Status == ToolExecutionStatus.Success
            ? JsonSerializer.Serialize(result.Data)
            : result.ErrorMessage ?? "Tool execution failed.";
}

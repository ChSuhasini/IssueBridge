using System.Text.Json;
using IssueBridge.Api.Assistant;
using IssueBridge.Api.Assistant.Model;
using IssueBridge.Api.Assistant.Tools;
using IssueBridge.Tests.TestHelpers;
using Xunit;

namespace IssueBridge.Tests;

public class AssistantAgentTests
{
    private static JsonElement Json(string json) => JsonDocument.Parse(json).RootElement;

    private static ModelResponse ToolUseResponse(string toolCallId, string toolName, string argsJson = "{}") =>
        new()
        {
            StopReason = ModelStopReason.ToolUse,
            ToolCalls = [new ModelToolCall { Id = toolCallId, Name = toolName, Arguments = Json(argsJson) }],
            RawAssistantContent = new { placeholder = true }
        };

    private static ModelResponse TextResponse(string text) =>
        new() { StopReason = ModelStopReason.EndTurn, Text = text, RawAssistantContent = new { placeholder = true } };

    private static AssistantToolExecutor CreateRealToolExecutor(IssueBridge.Api.Data.IssueBridgeDbContext db) =>
        new([
            new GetOpenIssuesTool(db),
            new GetHighPriorityIssuesTool(db),
            new GetDashboardSummaryTool(db),
            new GetIssueDetailsTool(db),
            new GetIssuesByAssigneeTool(db)
        ]);

    // Proves a question the model can answer directly never touches the tool layer.
    [Fact]
    public async Task AskAsync_DirectTextResponse_ReturnsSuccessWithNoToolsCalled()
    {
        using var factory = new SqliteInMemoryContextFactory();
        await using var db = factory.CreateContext();
        var modelClient = new FakeAssistantModelClient(TextResponse("Hello, no tools needed."));
        var agent = new AssistantAgent(modelClient, CreateRealToolExecutor(db));

        var result = await agent.AskAsync("hi", CancellationToken.None);

        Assert.Equal(AssistantRequestStatus.Success, result.Status);
        Assert.Equal("Hello, no tools needed.", result.Answer);
        Assert.Empty(result.ToolsCalled);
    }

    // Proves the full round trip: tool_use is executed through the real
    // executor, and the tool_result is correctly wired back into the next
    // request before the model's final answer is returned.
    [Fact]
    public async Task AskAsync_ToolUseFollowedByFinalText_ExecutesToolAndReturnsFinalAnswer()
    {
        using var factory = new SqliteInMemoryContextFactory();
        await using var db = factory.CreateContext();
        var modelClient = new FakeAssistantModelClient(
            ToolUseResponse("call_1", "get_dashboard_summary"),
            TextResponse("You have 0 open issues."));
        var agent = new AssistantAgent(modelClient, CreateRealToolExecutor(db));

        var result = await agent.AskAsync("what's the status?", CancellationToken.None);

        Assert.Equal(AssistantRequestStatus.Success, result.Status);
        Assert.Equal("You have 0 open issues.", result.Answer);
        Assert.Equal(["get_dashboard_summary"], result.ToolsCalled);

        Assert.Equal(2, modelClient.CallHistory.Count);
        var secondCallMessages = modelClient.CallHistory[1];
        Assert.Equal(3, secondCallMessages.Count);
        var toolResultBlocks = Assert.IsType<List<object>>(secondCallMessages[2].Content);
        var block = Assert.IsType<ToolResultContentBlockDto>(Assert.Single(toolResultBlocks));
        Assert.Equal("call_1", block.ToolUseId);
        Assert.False(block.IsError);
    }

    // Proves invalid tool arguments don't crash the loop — the error is fed
    // back to the model, which gets a chance to recover with a final answer.
    [Fact]
    public async Task AskAsync_InvalidToolArguments_MarksToolResultAsErrorAndRecovers()
    {
        using var factory = new SqliteInMemoryContextFactory();
        await using var db = factory.CreateContext();
        var modelClient = new FakeAssistantModelClient(
            ToolUseResponse("call_1", "get_issue_details", "{}"), // missing required issueNumber
            TextResponse("I couldn't look that up."));
        var agent = new AssistantAgent(modelClient, CreateRealToolExecutor(db));

        var result = await agent.AskAsync("tell me about issue 5", CancellationToken.None);

        Assert.Equal(AssistantRequestStatus.Success, result.Status);
        Assert.Equal("I couldn't look that up.", result.Answer);
        Assert.Equal(["get_issue_details"], result.ToolsCalled);

        var secondCallMessages = modelClient.CallHistory[1];
        var toolResultBlocks = Assert.IsType<List<object>>(secondCallMessages[2].Content);
        var block = Assert.IsType<ToolResultContentBlockDto>(Assert.Single(toolResultBlocks));
        Assert.True(block.IsError);
        Assert.NotEmpty(block.Content);
    }

    // Proves a hallucinated/unknown tool name is rejected safely by the
    // executor and surfaced to the model as an error, not a crash.
    [Fact]
    public async Task AskAsync_UnknownTool_MarksToolResultAsErrorAndRecovers()
    {
        using var factory = new SqliteInMemoryContextFactory();
        await using var db = factory.CreateContext();
        var modelClient = new FakeAssistantModelClient(
            ToolUseResponse("call_1", "delete_everything"),
            TextResponse("That's not something I can do."));
        var agent = new AssistantAgent(modelClient, CreateRealToolExecutor(db));

        var result = await agent.AskAsync("delete everything", CancellationToken.None);

        Assert.Equal(AssistantRequestStatus.Success, result.Status);
        Assert.Equal("That's not something I can do.", result.Answer);

        var secondCallMessages = modelClient.CallHistory[1];
        var toolResultBlocks = Assert.IsType<List<object>>(secondCallMessages[2].Content);
        var block = Assert.IsType<ToolResultContentBlockDto>(Assert.Single(toolResultBlocks));
        Assert.True(block.IsError);
    }

    // Proves an Anthropic API failure is reported as a bounded ModelError
    // result rather than an unhandled exception.
    [Fact]
    public async Task AskAsync_ModelApiFailure_ReturnsModelErrorStatus()
    {
        using var factory = new SqliteInMemoryContextFactory();
        await using var db = factory.CreateContext();
        var modelClient = new FakeAssistantModelClient(ModelResponse.Failed("simulated API failure"));
        var agent = new AssistantAgent(modelClient, CreateRealToolExecutor(db));

        var result = await agent.AskAsync("hi", CancellationToken.None);

        Assert.Equal(AssistantRequestStatus.ModelError, result.Status);
        Assert.Contains("simulated API failure", result.Answer);
        Assert.Empty(result.ToolsCalled);
    }

    // Proves a response with no text and no tool calls is reported as
    // EmptyResponse rather than a Success with a blank answer.
    [Fact]
    public async Task AskAsync_EmptyResponse_ReturnsEmptyResponseStatus()
    {
        using var factory = new SqliteInMemoryContextFactory();
        await using var db = factory.CreateContext();
        var modelClient = new FakeAssistantModelClient(ModelResponse.Empty());
        var agent = new AssistantAgent(modelClient, CreateRealToolExecutor(db));

        var result = await agent.AskAsync("hi", CancellationToken.None);

        Assert.Equal(AssistantRequestStatus.EmptyResponse, result.Status);
    }

    // Proves the loop is genuinely capped: three tool_use turns exhaust the
    // limit and return a controlled result. FakeAssistantModelClient would
    // throw on a 4th call since only 3 responses are queued, so this also
    // proves the agent never exceeds the cap.
    [Fact]
    public async Task AskAsync_IterationCapExceeded_ReturnsIterationLimitReachedWithoutThrowing()
    {
        using var factory = new SqliteInMemoryContextFactory();
        await using var db = factory.CreateContext();
        var modelClient = new FakeAssistantModelClient(
            ToolUseResponse("call_1", "get_open_issues"),
            ToolUseResponse("call_2", "get_open_issues"),
            ToolUseResponse("call_3", "get_open_issues"));
        var agent = new AssistantAgent(modelClient, CreateRealToolExecutor(db));

        var result = await agent.AskAsync("keep going", CancellationToken.None);

        Assert.Equal(AssistantRequestStatus.IterationLimitReached, result.Status);
        Assert.Equal(3, result.ToolsCalled.Count);
    }
}

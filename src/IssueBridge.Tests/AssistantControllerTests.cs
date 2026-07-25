using IssueBridge.Api.Assistant;
using IssueBridge.Api.Assistant.Model;
using IssueBridge.Api.Assistant.Tools;
using IssueBridge.Api.Controllers;
using IssueBridge.Api.Dtos;
using IssueBridge.Tests.TestHelpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace IssueBridge.Tests;

public class AssistantControllerTests
{
    private static AssistantToolExecutor CreateRealToolExecutor(IssueBridge.Api.Data.IssueBridgeDbContext db) =>
        new([
            new GetOpenIssuesTool(db),
            new GetHighPriorityIssuesTool(db),
            new GetDashboardSummaryTool(db),
            new GetIssueDetailsTool(db),
            new GetIssuesByAssigneeTool(db)
        ]);

    // Proves a successful ask writes exactly one AssistantQueryLog row, and
    // that row contains only the safe summary fields — never raw tool data,
    // issue bodies, notes, or the model prompt.
    [Fact]
    public async Task Ask_SuccessfulRequest_WritesExactlyOneTelemetryRow()
    {
        using var factory = new SqliteInMemoryContextFactory();
        await using var db = factory.CreateContext();
        var modelClient = new FakeAssistantModelClient(
            new ModelResponse { StopReason = ModelStopReason.EndTurn, Text = "All quiet.", RawAssistantContent = new { } });
        var agent = new AssistantAgent(modelClient, CreateRealToolExecutor(db));
        var controller = new AssistantController(agent, db);

        var actionResult = await controller.Ask(new AssistantAskRequestDto { Question = "What's the status?" }, CancellationToken.None);

        var response = Assert.IsType<AssistantAskResponseDto>(Assert.IsType<ActionResult<AssistantAskResponseDto>>(actionResult).Value);
        Assert.Equal("All quiet.", response.Answer);
        Assert.Equal("Success", response.Status);

        var logs = await db.AssistantQueryLogs.ToListAsync();
        var log = Assert.Single(logs);
        Assert.Equal("What's the status?", log.Question);
        Assert.Equal("Success", log.Status);
        Assert.Null(log.ToolNamesCalled);
        Assert.Null(log.ErrorCategory);
        Assert.True(log.DurationMs >= 0);
    }

    // Proves a tool-using request logs the tool name(s) actually called.
    [Fact]
    public async Task Ask_ToolUsingRequest_LogsToolNamesCalled()
    {
        using var factory = new SqliteInMemoryContextFactory();
        await using var db = factory.CreateContext();
        var modelClient = new FakeAssistantModelClient(
            new ModelResponse
            {
                StopReason = ModelStopReason.ToolUse,
                ToolCalls =
                [
                    new ModelToolCall
                    {
                        Id = "call_1",
                        Name = "get_dashboard_summary",
                        Arguments = System.Text.Json.JsonDocument.Parse("{}").RootElement
                    }
                ],
                RawAssistantContent = new { }
            },
            new ModelResponse { StopReason = ModelStopReason.EndTurn, Text = "0 open issues.", RawAssistantContent = new { } });
        var agent = new AssistantAgent(modelClient, CreateRealToolExecutor(db));
        var controller = new AssistantController(agent, db);

        await controller.Ask(new AssistantAskRequestDto { Question = "status?" }, CancellationToken.None);

        var log = await db.AssistantQueryLogs.SingleAsync();
        Assert.Equal("get_dashboard_summary", log.ToolNamesCalled);
    }

    // Proves an empty question is rejected before the agent or the log is ever touched.
    [Fact]
    public async Task Ask_EmptyQuestion_ReturnsBadRequestAndWritesNoTelemetry()
    {
        using var factory = new SqliteInMemoryContextFactory();
        await using var db = factory.CreateContext();
        var modelClient = new FakeAssistantModelClient();
        var agent = new AssistantAgent(modelClient, CreateRealToolExecutor(db));
        var controller = new AssistantController(agent, db);

        var actionResult = await controller.Ask(new AssistantAskRequestDto { Question = "   " }, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(Assert.IsType<ActionResult<AssistantAskResponseDto>>(actionResult).Result);
        Assert.Empty(await db.AssistantQueryLogs.ToListAsync());
    }
}

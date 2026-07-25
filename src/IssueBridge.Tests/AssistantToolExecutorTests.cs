using System.Text.Json;
using IssueBridge.Api.Assistant.Dtos;
using IssueBridge.Api.Assistant.Tools;
using IssueBridge.Api.Data;
using IssueBridge.Api.Dtos;
using IssueBridge.Api.Models;
using IssueBridge.Tests.TestHelpers;
using Xunit;

namespace IssueBridge.Tests;

public class AssistantToolExecutorTests
{
    private static readonly JsonElement EmptyArgs = JsonDocument.Parse("{}").RootElement;

    private static async Task SeedIssueAsync(
        IssueBridgeDbContext db,
        int number,
        string title,
        string state = "open",
        LocalStatus status = LocalStatus.NotStarted,
        Priority priority = Priority.Medium,
        string? assignedTo = null,
        string? notes = null)
    {
        var issue = new Issue
        {
            GitHubIssueId = number,
            Number = number,
            Title = title,
            State = state,
            GitHubUrl = $"https://github.com/owner/repo/issues/{number}",
            GitHubCreatedAt = DateTimeOffset.UtcNow,
            GitHubUpdatedAt = DateTimeOffset.UtcNow,
            LastSyncedAt = DateTimeOffset.UtcNow
        };
        db.Issues.Add(issue);
        await db.SaveChangesAsync();

        db.LocalTaskInfos.Add(new LocalTaskInfo
        {
            IssueId = issue.Id,
            LocalStatus = status,
            Priority = priority,
            AssignedTo = assignedTo,
            Notes = notes,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();
    }

    private static AssistantToolExecutor CreateExecutor(IssueBridgeDbContext db, params IAssistantTool[] extraTools)
    {
        IAssistantTool[] tools =
        [
            new GetOpenIssuesTool(db),
            new GetHighPriorityIssuesTool(db),
            new GetDashboardSummaryTool(db),
            new GetIssueDetailsTool(db),
            new GetIssuesByAssigneeTool(db),
            .. extraTools
        ];
        return new AssistantToolExecutor(tools);
    }

    // Proves get_open_issues only returns GitHub-open issues, ignoring local status entirely.
    [Fact]
    public async Task GetOpenIssues_ReturnsOnlyIssuesWithGitHubStateOpen()
    {
        using var factory = new SqliteInMemoryContextFactory();
        await using var db = factory.CreateContext();
        await SeedIssueAsync(db, number: 1, title: "Open one", state: "open");
        await SeedIssueAsync(db, number: 2, title: "Closed one", state: "closed");

        var executor = CreateExecutor(db);
        var result = await executor.ExecuteAsync("get_open_issues", EmptyArgs);

        Assert.Equal(ToolExecutionStatus.Success, result.Status);
        var issues = Assert.IsType<List<IssueSummaryDto>>(result.Data);
        Assert.Single(issues);
        Assert.Equal(1, issues[0].Number);
    }

    // Proves get_high_priority_issues filters on LocalTaskInfo.Priority, not GitHub data.
    [Fact]
    public async Task GetHighPriorityIssues_ReturnsOnlyHighPriorityIssues()
    {
        using var factory = new SqliteInMemoryContextFactory();
        await using var db = factory.CreateContext();
        await SeedIssueAsync(db, number: 1, title: "Urgent", priority: Priority.High);
        await SeedIssueAsync(db, number: 2, title: "Normal", priority: Priority.Medium);

        var executor = CreateExecutor(db);
        var result = await executor.ExecuteAsync("get_high_priority_issues", EmptyArgs);

        var issues = Assert.IsType<List<IssueSummaryDto>>(result.Data);
        Assert.Single(issues);
        Assert.Equal(1, issues[0].Number);
    }

    // Proves get_dashboard_summary computes the same aggregate counts as DashboardController.
    [Fact]
    public async Task GetDashboardSummary_ComputesCorrectCounts()
    {
        using var factory = new SqliteInMemoryContextFactory();
        await using var db = factory.CreateContext();
        await SeedIssueAsync(db, number: 1, title: "A", status: LocalStatus.NotStarted, priority: Priority.High);
        await SeedIssueAsync(db, number: 2, title: "B", status: LocalStatus.InProgress);
        await SeedIssueAsync(db, number: 3, title: "C", status: LocalStatus.Done);

        var executor = CreateExecutor(db);
        var result = await executor.ExecuteAsync("get_dashboard_summary", EmptyArgs);

        var summary = Assert.IsType<DashboardSummaryDto>(result.Data);
        Assert.Equal(1, summary.Open);
        Assert.Equal(1, summary.InProgress);
        Assert.Equal(1, summary.Done);
        Assert.Equal(1, summary.HighPriority);
    }

    // Proves get_issue_details returns full fields (including Notes) for an existing issue.
    [Fact]
    public async Task GetIssueDetails_ReturnsFullDetailsForExistingIssue()
    {
        using var factory = new SqliteInMemoryContextFactory();
        await using var db = factory.CreateContext();
        await SeedIssueAsync(db, number: 7, title: "Investigate flaky test", assignedTo: "Alice", notes: "Repro'd on CI only");

        var executor = CreateExecutor(db);
        var args = JsonDocument.Parse("""{"issueNumber": 7}""").RootElement;
        var result = await executor.ExecuteAsync("get_issue_details", args);

        var dto = Assert.IsType<IssueDto>(result.Data);
        Assert.Equal("Investigate flaky test", dto.Title);
        Assert.Equal("Alice", dto.AssignedTo);
        Assert.Equal("Repro'd on CI only", dto.Notes);
    }

    // Proves a nonexistent issue number is a valid empty result, not an error.
    [Fact]
    public async Task GetIssueDetails_UnknownIssueNumber_ReturnsSuccessWithNullData()
    {
        using var factory = new SqliteInMemoryContextFactory();
        await using var db = factory.CreateContext();

        var executor = CreateExecutor(db);
        var args = JsonDocument.Parse("""{"issueNumber": 999}""").RootElement;
        var result = await executor.ExecuteAsync("get_issue_details", args);

        Assert.Equal(ToolExecutionStatus.Success, result.Status);
        Assert.Null(result.Data);
        Assert.Contains("999", result.Summary);
    }

    // Proves malformed arguments are rejected centrally as InvalidArguments, not an exception or a DB call.
    [Theory]
    [InlineData("{}")]
    [InlineData("""{"issueNumber": "not-a-number"}""")]
    [InlineData("""{"issueNumber": -1}""")]
    public async Task GetIssueDetails_InvalidArguments_ReturnsInvalidArgumentsStatus(string argsJson)
    {
        using var factory = new SqliteInMemoryContextFactory();
        await using var db = factory.CreateContext();

        var executor = CreateExecutor(db);
        var args = JsonDocument.Parse(argsJson).RootElement;
        var result = await executor.ExecuteAsync("get_issue_details", args);

        Assert.Equal(ToolExecutionStatus.InvalidArguments, result.Status);
        Assert.NotNull(result.ErrorMessage);
    }

    // Proves get_issues_by_assignee filters correctly and rejects an empty assignee.
    [Fact]
    public async Task GetIssuesByAssignee_ReturnsOnlyMatchingIssues()
    {
        using var factory = new SqliteInMemoryContextFactory();
        await using var db = factory.CreateContext();
        await SeedIssueAsync(db, number: 1, title: "Alice's task", assignedTo: "Alice");
        await SeedIssueAsync(db, number: 2, title: "Bob's task", assignedTo: "Bob");
        await SeedIssueAsync(db, number: 3, title: "Unassigned task");

        var executor = CreateExecutor(db);
        var args = JsonDocument.Parse("""{"assignedTo": "Alice"}""").RootElement;
        var result = await executor.ExecuteAsync("get_issues_by_assignee", args);

        var issues = Assert.IsType<List<IssueSummaryDto>>(result.Data);
        Assert.Single(issues);
        Assert.Equal(1, issues[0].Number);
    }

    [Fact]
    public async Task GetIssuesByAssignee_MissingArgument_ReturnsInvalidArgumentsStatus()
    {
        using var factory = new SqliteInMemoryContextFactory();
        await using var db = factory.CreateContext();

        var executor = CreateExecutor(db);
        var result = await executor.ExecuteAsync("get_issues_by_assignee", EmptyArgs);

        Assert.Equal(ToolExecutionStatus.InvalidArguments, result.Status);
    }

    // Proves the executor rejects tool names the registry doesn't know about,
    // rather than throwing or silently no-op'ing.
    [Fact]
    public async Task ExecuteAsync_UnknownToolName_ReturnsUnknownToolStatus()
    {
        using var factory = new SqliteInMemoryContextFactory();
        await using var db = factory.CreateContext();

        var executor = CreateExecutor(db);
        var result = await executor.ExecuteAsync("delete_all_issues", EmptyArgs);

        Assert.Equal(ToolExecutionStatus.UnknownTool, result.Status);
    }

    // Proves the read-only guardrail is enforced structurally by the executor,
    // not just by every current tool happening to be read-only.
    [Fact]
    public async Task ExecuteAsync_ToolMarkedNotReadOnly_ReturnsUnsupportedOperationStatus()
    {
        using var factory = new SqliteInMemoryContextFactory();
        await using var db = factory.CreateContext();

        var executor = CreateExecutor(db, new FakeWriteTool());
        var result = await executor.ExecuteAsync("delete_issue", EmptyArgs);

        Assert.Equal(ToolExecutionStatus.UnsupportedOperation, result.Status);
    }

    // Proves an unexpected exception from inside a tool (e.g. a DB error) is
    // caught centrally and turned into ExecutionError, instead of propagating
    // and crashing the request.
    [Fact]
    public async Task ExecuteAsync_ToolThrowsUnexpectedException_ReturnsExecutionErrorStatus()
    {
        using var factory = new SqliteInMemoryContextFactory();
        await using var db = factory.CreateContext();

        var executor = CreateExecutor(db, new FakeThrowingTool());
        var result = await executor.ExecuteAsync("throwing_tool", EmptyArgs);

        Assert.Equal(ToolExecutionStatus.ExecutionError, result.Status);
        Assert.NotNull(result.ErrorMessage);
    }

    private sealed class FakeWriteTool : IAssistantTool
    {
        public string Name => "delete_issue";
        public string Description => "Test-only tool that would mutate data if it were ever allowed to run.";
        public bool IsReadOnly => false;
        public System.Text.Json.Nodes.JsonObject InputSchema => new();

        public Task<ToolExecutionResult> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Should never be called — the executor must reject this before execution.");
    }

    private sealed class FakeThrowingTool : IAssistantTool
    {
        public string Name => "throwing_tool";
        public string Description => "Test-only tool that simulates an unexpected failure, e.g. a DB error.";
        public bool IsReadOnly => true;
        public System.Text.Json.Nodes.JsonObject InputSchema => new();

        public Task<ToolExecutionResult> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Simulated database failure.");
    }
}

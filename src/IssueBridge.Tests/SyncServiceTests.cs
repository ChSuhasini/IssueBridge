using System.Net;
using System.Text.RegularExpressions;
using IssueBridge.Api.GitHub;
using IssueBridge.Api.Models;
using IssueBridge.Tests.TestHelpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace IssueBridge.Tests;

public class SyncServiceTests
{
    private static IGitHubIssuesClient CreateGitHubClient(Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        var handler = new FakeHttpMessageHandler(responder);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.github.com/") };
        var options = Options.Create(new GitHubOptions { Owner = "owner", Repo = "repo", Token = "fake-token" });
        return new GitHubIssuesClient(httpClient, options);
    }

    private static int PageFromRequest(HttpRequestMessage request)
    {
        var match = Regex.Match(request.RequestUri!.Query, @"\bpage=(\d+)");
        return match.Success ? int.Parse(match.Groups[1].Value) : 1;
    }

    // Proves dedup logic works: syncing a GitHub issue we've never seen
    // before creates exactly one Issue row plus its default LocalTaskInfo row.
    [Fact]
    public async Task NewGitHubIssue_CreatesExactlyOneIssueAndDefaultLocalTaskInfo()
    {
        using var factory = new SqliteInMemoryContextFactory();
        var updatedAt = DateTimeOffset.Parse("2026-01-01T00:00:00Z");

        var gitHubClient = CreateGitHubClient(request =>
        {
            var page = PageFromRequest(request);
            var items = page == 1
                ? new[] { GitHubResponseBuilder.Issue(id: 1001, number: 1, title: "First issue", updatedAt) }
                : Array.Empty<GitHubIssueDto>();
            return GitHubResponseBuilder.JsonPage(items);
        });

        await using var context = factory.CreateContext();
        var syncService = new SyncService(context, gitHubClient);

        var result = await syncService.SyncAsync();

        Assert.True(result.Success);
        Assert.Equal(1, result.Created);
        Assert.Equal(0, result.Updated);
        Assert.Equal(0, result.Skipped);

        await using var verifyContext = factory.CreateContext();
        Assert.Equal(1, await verifyContext.Issues.CountAsync());
        Assert.Equal(1, await verifyContext.LocalTaskInfos.CountAsync());
        var localInfo = await verifyContext.LocalTaskInfos.SingleAsync();
        Assert.Equal(LocalStatus.NotStarted, localInfo.LocalStatus);
    }

    // Proves idempotency: syncing the exact same GitHub state twice must not
    // create duplicate rows or otherwise corrupt data on the second pass.
    [Fact]
    public async Task ResyncingUnchangedIssue_ProducesNoDuplicatesAndNoDataLoss()
    {
        using var factory = new SqliteInMemoryContextFactory();
        var updatedAt = DateTimeOffset.Parse("2026-01-01T00:00:00Z");

        var gitHubClient = CreateGitHubClient(request =>
        {
            var page = PageFromRequest(request);
            var items = page == 1
                ? new[] { GitHubResponseBuilder.Issue(id: 2001, number: 5, title: "Stable issue", updatedAt) }
                : Array.Empty<GitHubIssueDto>();
            return GitHubResponseBuilder.JsonPage(items);
        });

        await using (var context = factory.CreateContext())
        {
            var syncService = new SyncService(context, gitHubClient);
            await syncService.SyncAsync(); // first sync
        }

        SyncResult secondResult;
        await using (var context = factory.CreateContext())
        {
            var syncService = new SyncService(context, gitHubClient);
            secondResult = await syncService.SyncAsync(); // second sync, identical data
        }

        Assert.True(secondResult.Success);
        Assert.Equal(0, secondResult.Created);
        Assert.Equal(0, secondResult.Updated);
        Assert.Equal(1, secondResult.Skipped);

        await using var verifyContext = factory.CreateContext();
        Assert.Equal(1, await verifyContext.Issues.CountAsync());
        Assert.Equal(1, await verifyContext.LocalTaskInfos.CountAsync());
    }

    // Proves the two-table separation actually holds: when GitHub content
    // changes upstream, the Issue row updates but a team's local edits
    // (assignee, notes, status, priority) are left completely untouched.
    [Fact]
    public async Task UpstreamIssueChange_UpdatesGitHubFieldsButPreservesLocalTaskInfo()
    {
        using var factory = new SqliteInMemoryContextFactory();
        var originalUpdatedAt = DateTimeOffset.Parse("2026-01-01T00:00:00Z");
        var newUpdatedAt = DateTimeOffset.Parse("2026-02-01T00:00:00Z");
        var localEditTime = DateTimeOffset.Parse("2026-01-15T00:00:00Z");

        // Seed an issue that already went through one sync and has local edits.
        await using (var seedContext = factory.CreateContext())
        {
            seedContext.Issues.Add(new Api.Models.Issue
            {
                GitHubIssueId = 3001,
                Number = 9,
                Title = "Original title",
                Body = "Original body",
                State = "open",
                Labels = "bug",
                GitHubUrl = "https://github.com/owner/repo/issues/9",
                GitHubCreatedAt = originalUpdatedAt,
                GitHubUpdatedAt = originalUpdatedAt,
                LastSyncedAt = originalUpdatedAt,
                LocalTaskInfo = new LocalTaskInfo
                {
                    AssignedTo = "Alice",
                    LocalStatus = LocalStatus.InProgress,
                    Notes = "Do not touch this note",
                    Priority = Priority.High,
                    UpdatedAt = localEditTime
                }
            });
            await seedContext.SaveChangesAsync();
        }

        var gitHubClient = CreateGitHubClient(request =>
        {
            var page = PageFromRequest(request);
            var items = page == 1
                ? new[] { GitHubResponseBuilder.Issue(id: 3001, number: 9, title: "Updated title from GitHub", newUpdatedAt) }
                : Array.Empty<GitHubIssueDto>();
            return GitHubResponseBuilder.JsonPage(items);
        });

        SyncResult result;
        await using (var context = factory.CreateContext())
        {
            var syncService = new SyncService(context, gitHubClient);
            result = await syncService.SyncAsync();
        }

        Assert.True(result.Success);
        Assert.Equal(0, result.Created);
        Assert.Equal(1, result.Updated);
        Assert.Equal(0, result.Skipped);

        await using var verifyContext = factory.CreateContext();
        var issue = await verifyContext.Issues.Include(i => i.LocalTaskInfo).SingleAsync();

        Assert.Equal("Updated title from GitHub", issue.Title);
        Assert.Equal(newUpdatedAt, issue.GitHubUpdatedAt);

        Assert.Equal("Alice", issue.LocalTaskInfo!.AssignedTo);
        Assert.Equal(LocalStatus.InProgress, issue.LocalTaskInfo.LocalStatus);
        Assert.Equal("Do not touch this note", issue.LocalTaskInfo.Notes);
        Assert.Equal(Priority.High, issue.LocalTaskInfo.Priority);
        Assert.Equal(localEditTime, issue.LocalTaskInfo.UpdatedAt);
    }

    // Proves failure handling: if the GitHub fetch fails partway through
    // pagination, sync fails cleanly with zero rows written — no partial sync.
    [Fact]
    public async Task GitHubFailureMidPagination_LeavesNoPartialWrites()
    {
        using var factory = new SqliteInMemoryContextFactory();
        var updatedAt = DateTimeOffset.Parse("2026-01-01T00:00:00Z");

        // Page 1 returns a full page (100 items), which forces the client to
        // request page 2 — where we simulate the failure.
        var page1Items = Enumerable.Range(1, 100)
            .Select(i => GitHubResponseBuilder.Issue(id: 4000 + i, number: i, title: $"Issue {i}", updatedAt))
            .ToArray();

        var gitHubClient = CreateGitHubClient(request =>
        {
            var page = PageFromRequest(request);
            return page == 1
                ? GitHubResponseBuilder.JsonPage(page1Items)
                : GitHubResponseBuilder.Failure(HttpStatusCode.InternalServerError);
        });

        await using var context = factory.CreateContext();
        var syncService = new SyncService(context, gitHubClient);

        var result = await syncService.SyncAsync();

        Assert.False(result.Success);
        Assert.NotNull(result.Error);
        Assert.Equal(0, result.Created);
        Assert.Equal(0, result.Updated);
        Assert.Equal(0, result.Skipped);

        await using var verifyContext = factory.CreateContext();
        Assert.Equal(0, await verifyContext.Issues.CountAsync());
        Assert.Equal(0, await verifyContext.LocalTaskInfos.CountAsync());
    }
}

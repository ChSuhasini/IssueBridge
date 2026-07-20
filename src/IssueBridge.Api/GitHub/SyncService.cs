using System.Diagnostics;
using IssueBridge.Api.Data;
using IssueBridge.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace IssueBridge.Api.GitHub;

public class SyncService : ISyncService
{
    private readonly IssueBridgeDbContext _db;
    private readonly IGitHubIssuesClient _gitHubClient;

    public SyncService(IssueBridgeDbContext db, IGitHubIssuesClient gitHubClient)
    {
        _db = db;
        _gitHubClient = gitHubClient;
    }

    public async Task<SyncResult> SyncAsync(CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        // Fetch everything before touching the database. If this throws, we
        // return immediately with zero writes made — no partial sync state.
        IReadOnlyList<GitHubIssueDto> fetched;
        try
        {
            fetched = await _gitHubClient.FetchAllIssuesAsync(cancellationToken);
        }
        catch (GitHubSyncException ex)
        {
            stopwatch.Stop();
            return new SyncResult { Success = false, Error = ex.Message, DurationMs = stopwatch.ElapsedMilliseconds };
        }

        int created = 0, updated = 0, skipped = 0;
        var now = DateTimeOffset.UtcNow;

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var existingIssues = await _db.Issues.ToDictionaryAsync(i => i.GitHubIssueId, cancellationToken);

            foreach (var dto in fetched)
            {
                var labels = string.Join(",", dto.Labels.Select(l => l.Name));

                if (existingIssues.TryGetValue(dto.Id, out var existing))
                {
                    if (existing.GitHubUpdatedAt != dto.UpdatedAt)
                    {
                        // Update GitHub-owned fields only — LocalTaskInfo is never touched here.
                        existing.Title = dto.Title;
                        existing.Body = dto.Body;
                        existing.State = dto.State;
                        existing.Labels = labels;
                        existing.GitHubUrl = dto.HtmlUrl;
                        existing.GitHubCreatedAt = dto.CreatedAt;
                        existing.GitHubUpdatedAt = dto.UpdatedAt;
                        existing.LastSyncedAt = now;
                        updated++;
                    }
                    else
                    {
                        skipped++;
                    }
                }
                else
                {
                    _db.Issues.Add(new Issue
                    {
                        GitHubIssueId = dto.Id,
                        Number = dto.Number,
                        Title = dto.Title,
                        Body = dto.Body,
                        State = dto.State,
                        Labels = labels,
                        GitHubUrl = dto.HtmlUrl,
                        GitHubCreatedAt = dto.CreatedAt,
                        GitHubUpdatedAt = dto.UpdatedAt,
                        LastSyncedAt = now,
                        LocalTaskInfo = new LocalTaskInfo
                        {
                            LocalStatus = LocalStatus.NotStarted,
                            Priority = Priority.Medium,
                            UpdatedAt = now
                        }
                    });
                    created++;
                }
            }

            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            stopwatch.Stop();
            return new SyncResult
            {
                Success = false,
                Error = $"Sync failed while writing to the database: {ex.Message}",
                DurationMs = stopwatch.ElapsedMilliseconds
            };
        }

        stopwatch.Stop();
        return new SyncResult
        {
            Success = true,
            Created = created,
            Updated = updated,
            Skipped = skipped,
            DurationMs = stopwatch.ElapsedMilliseconds
        };
    }
}

namespace IssueBridge.Api.Models;

// Read-only mirror of a GitHub issue. Every field here is overwritten wholesale
// on sync — nothing on this entity is user-editable via the API.
public class Issue
{
    public int Id { get; set; }

    public long GitHubIssueId { get; set; }
    public int Number { get; set; }
    public required string Title { get; set; }
    public string? Body { get; set; }
    public required string State { get; set; } // "open" | "closed"
    public string? Labels { get; set; } // comma-separated label names
    public required string GitHubUrl { get; set; }
    public DateTimeOffset GitHubCreatedAt { get; set; }
    public DateTimeOffset GitHubUpdatedAt { get; set; }
    public DateTimeOffset LastSyncedAt { get; set; }

    public LocalTaskInfo? LocalTaskInfo { get; set; }
}

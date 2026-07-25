namespace IssueBridge.Api.Models;

// One row per POST /api/assistant/ask request. Deliberately excludes raw tool
// results, issue bodies/notes, model prompts, and secrets — enough to audit
// what was asked, which tools ran, and whether it succeeded, nothing more.
public class AssistantQueryLog
{
    public int Id { get; set; }
    public required string Question { get; set; }
    public string? ToolNamesCalled { get; set; } // comma-separated, e.g. "get_open_issues,get_issue_details"
    public required string Status { get; set; }
    public long DurationMs { get; set; }
    public string? ErrorCategory { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

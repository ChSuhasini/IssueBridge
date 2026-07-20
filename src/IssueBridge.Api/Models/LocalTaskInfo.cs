namespace IssueBridge.Api.Models;

// Team-owned workflow layer, 1:1 with Issue. Sync never writes to this table
// except to insert the default row the first time an Issue is created.
public class LocalTaskInfo
{
    public int IssueId { get; set; }
    public Issue? Issue { get; set; }

    public string? AssignedTo { get; set; }
    public LocalStatus LocalStatus { get; set; } = LocalStatus.NotStarted;
    public string? Notes { get; set; }
    public Priority Priority { get; set; } = Priority.Medium;
    public DateTimeOffset UpdatedAt { get; set; }
}

using IssueBridge.Api.Models;

namespace IssueBridge.Api.Assistant.Dtos;

// Deliberately excludes Body/Notes — list-returning tools should stay concise
// (both for the model's context and for future telemetry). get_issue_details
// is the one tool that returns full IssueDto with those fields included.
public class IssueSummaryDto
{
    public required int Number { get; set; }
    public required string Title { get; set; }
    public required string State { get; set; }
    public LocalStatus LocalStatus { get; set; }
    public Priority Priority { get; set; }
    public string? AssignedTo { get; set; }

    public static IssueSummaryDto FromEntity(Issue issue)
    {
        var local = issue.LocalTaskInfo;
        return new IssueSummaryDto
        {
            Number = issue.Number,
            Title = issue.Title,
            State = issue.State,
            LocalStatus = local?.LocalStatus ?? LocalStatus.NotStarted,
            Priority = local?.Priority ?? Priority.Medium,
            AssignedTo = local?.AssignedTo
        };
    }
}

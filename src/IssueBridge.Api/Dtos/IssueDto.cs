using IssueBridge.Api.Models;

namespace IssueBridge.Api.Dtos;

public class IssueDto
{
    public int Id { get; set; }
    public int Number { get; set; }
    public required string Title { get; set; }
    public string? Body { get; set; }
    public required string State { get; set; }
    public string? Labels { get; set; }
    public required string GitHubUrl { get; set; }
    public DateTimeOffset GitHubCreatedAt { get; set; }
    public DateTimeOffset GitHubUpdatedAt { get; set; }
    public DateTimeOffset LastSyncedAt { get; set; }

    public string? AssignedTo { get; set; }
    public LocalStatus LocalStatus { get; set; }
    public string? Notes { get; set; }
    public Priority Priority { get; set; }
    public DateTimeOffset LocalUpdatedAt { get; set; }

    public static IssueDto FromEntity(Issue issue)
    {
        var local = issue.LocalTaskInfo;
        return new IssueDto
        {
            Id = issue.Id,
            Number = issue.Number,
            Title = issue.Title,
            Body = issue.Body,
            State = issue.State,
            Labels = issue.Labels,
            GitHubUrl = issue.GitHubUrl,
            GitHubCreatedAt = issue.GitHubCreatedAt,
            GitHubUpdatedAt = issue.GitHubUpdatedAt,
            LastSyncedAt = issue.LastSyncedAt,
            AssignedTo = local?.AssignedTo,
            LocalStatus = local?.LocalStatus ?? LocalStatus.NotStarted,
            Notes = local?.Notes,
            Priority = local?.Priority ?? Priority.Medium,
            LocalUpdatedAt = local?.UpdatedAt ?? issue.LastSyncedAt
        };
    }
}

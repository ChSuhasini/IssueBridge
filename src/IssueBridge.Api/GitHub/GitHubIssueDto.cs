using System.Text.Json.Serialization;

namespace IssueBridge.Api.GitHub;

// Shape of a single element from GET /repos/{owner}/{repo}/issues.
// Only the fields we actually use are mapped — GitHub's payload has many more.
public class GitHubIssueDto
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("number")]
    public int Number { get; set; }

    [JsonPropertyName("title")]
    public required string Title { get; set; }

    [JsonPropertyName("body")]
    public string? Body { get; set; }

    [JsonPropertyName("state")]
    public required string State { get; set; }

    [JsonPropertyName("html_url")]
    public required string HtmlUrl { get; set; }

    [JsonPropertyName("created_at")]
    public DateTimeOffset CreatedAt { get; set; }

    [JsonPropertyName("updated_at")]
    public DateTimeOffset UpdatedAt { get; set; }

    [JsonPropertyName("labels")]
    public List<GitHubLabelDto> Labels { get; set; } = new();

    // Present only when the "issue" is actually a pull request — GitHub's
    // issues endpoint returns both, and we need to skip PRs.
    [JsonPropertyName("pull_request")]
    public object? PullRequest { get; set; }
}

public class GitHubLabelDto
{
    [JsonPropertyName("name")]
    public required string Name { get; set; }
}

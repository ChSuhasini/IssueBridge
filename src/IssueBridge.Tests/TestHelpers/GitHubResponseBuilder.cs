using System.Net;
using System.Net.Http.Json;
using IssueBridge.Api.GitHub;

namespace IssueBridge.Tests.TestHelpers;

public static class GitHubResponseBuilder
{
    public static GitHubIssueDto Issue(long id, int number, string title, DateTimeOffset updatedAt, params string[] labels)
    {
        return new GitHubIssueDto
        {
            Id = id,
            Number = number,
            Title = title,
            Body = $"Body for issue {number}",
            State = "open",
            HtmlUrl = $"https://github.com/owner/repo/issues/{number}",
            CreatedAt = updatedAt,
            UpdatedAt = updatedAt,
            Labels = labels.Select(l => new GitHubLabelDto { Name = l }).ToList()
        };
    }

    public static HttpResponseMessage JsonPage(IReadOnlyList<GitHubIssueDto> items)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(items)
        };
    }

    public static HttpResponseMessage Failure(HttpStatusCode statusCode)
    {
        return new HttpResponseMessage(statusCode);
    }
}

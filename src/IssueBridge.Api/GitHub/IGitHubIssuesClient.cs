namespace IssueBridge.Api.GitHub;

public interface IGitHubIssuesClient
{
    // Fetches every page of issues for the configured repo (state=all),
    // filtering out pull requests. Throws GitHubSyncException on any failure —
    // never returns a partial list silently.
    Task<IReadOnlyList<GitHubIssueDto>> FetchAllIssuesAsync(CancellationToken cancellationToken = default);
}

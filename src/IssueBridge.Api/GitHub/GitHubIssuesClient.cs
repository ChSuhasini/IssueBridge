using System.Net.Http.Json;
using Microsoft.Extensions.Options;

namespace IssueBridge.Api.GitHub;

public class GitHubIssuesClient : IGitHubIssuesClient
{
    private const int PageSize = 100;

    private readonly HttpClient _httpClient;
    private readonly GitHubOptions _options;

    public GitHubIssuesClient(HttpClient httpClient, IOptions<GitHubOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public async Task<IReadOnlyList<GitHubIssueDto>> FetchAllIssuesAsync(CancellationToken cancellationToken = default)
    {
        var allIssues = new List<GitHubIssueDto>();
        var page = 1;

        while (true)
        {
            var requestUri = $"repos/{_options.Owner}/{_options.Repo}/issues?state=all&per_page={PageSize}&page={page}";

            HttpResponseMessage response;
            try
            {
                response = await _httpClient.GetAsync(requestUri, cancellationToken);
            }
            catch (HttpRequestException ex)
            {
                throw new GitHubSyncException($"Network error while fetching page {page} of issues.", ex);
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new GitHubSyncException(
                    $"GitHub API returned {(int)response.StatusCode} ({response.ReasonPhrase}) while fetching page {page}.");
            }

            List<GitHubIssueDto>? pageItems;
            try
            {
                pageItems = await response.Content.ReadFromJsonAsync<List<GitHubIssueDto>>(cancellationToken: cancellationToken);
            }
            catch (System.Text.Json.JsonException ex)
            {
                throw new GitHubSyncException($"Failed to parse GitHub response on page {page}.", ex);
            }

            if (pageItems is null || pageItems.Count == 0)
            {
                break;
            }

            allIssues.AddRange(pageItems.Where(i => i.PullRequest is null));

            if (pageItems.Count < PageSize)
            {
                break; // last page
            }

            page++;
        }

        return allIssues;
    }
}

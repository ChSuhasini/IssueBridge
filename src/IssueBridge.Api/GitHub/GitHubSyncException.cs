namespace IssueBridge.Api.GitHub;

// Thrown when the GitHub fetch fails for any reason (network error, non-2xx
// response, bad token, rate limit). Caught by SyncService before any DB write
// happens, so a failure here can never leave partial data behind.
public class GitHubSyncException : Exception
{
    public GitHubSyncException(string message) : base(message)
    {
    }

    public GitHubSyncException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

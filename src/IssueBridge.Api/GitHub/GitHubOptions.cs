namespace IssueBridge.Api.GitHub;

public class GitHubOptions
{
    public const string SectionName = "GitHub";

    public required string Owner { get; set; }
    public required string Repo { get; set; }
    public required string Token { get; set; }
    public string ApiBaseUrl { get; set; } = "https://api.github.com/";
}

namespace IssueBridge.Api.GitHub;

public class SyncResult
{
    public bool Success { get; set; }
    public int Created { get; set; }
    public int Updated { get; set; }
    public int Skipped { get; set; }
    public long DurationMs { get; set; }
    public string? Error { get; set; }
}

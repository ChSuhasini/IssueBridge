namespace IssueBridge.Api.Assistant.Tools;

// Data is the full structured result meant to be fed back to the model. Summary
// is a short, telemetry-safe description (no issue bodies/notes) — this is what
// AssistantQueryLog will store once that table exists; Data never gets logged.
public class ToolExecutionResult
{
    public required ToolExecutionStatus Status { get; init; }
    public object? Data { get; init; }
    public string? Summary { get; init; }
    public string? ErrorMessage { get; init; }

    public static ToolExecutionResult Ok(object? data, string summary) =>
        new() { Status = ToolExecutionStatus.Success, Data = data, Summary = summary };
}

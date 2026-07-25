namespace IssueBridge.Api.Assistant;

public class AssistantAskResult
{
    public required AssistantRequestStatus Status { get; init; }
    public required string Answer { get; init; }
    public required List<string> ToolsCalled { get; init; }

    public static AssistantAskResult Succeeded(string answer, List<string> toolsCalled) =>
        new() { Status = AssistantRequestStatus.Success, Answer = answer, ToolsCalled = toolsCalled };

    public static AssistantAskResult Failed(AssistantRequestStatus status, List<string> toolsCalled, string answer) =>
        new() { Status = status, Answer = answer, ToolsCalled = toolsCalled };
}

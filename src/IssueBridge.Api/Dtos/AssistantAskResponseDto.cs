namespace IssueBridge.Api.Dtos;

public class AssistantAskResponseDto
{
    public required string Answer { get; set; }
    public required List<string> ToolsCalled { get; set; }
    public required string Status { get; set; }
    public required long DurationMs { get; set; }
}

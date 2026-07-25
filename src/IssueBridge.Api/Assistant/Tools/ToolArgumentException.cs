namespace IssueBridge.Api.Assistant.Tools;

// Thrown by a tool's ExecuteAsync when the model-supplied arguments are missing,
// malformed, or out of range. AssistantToolExecutor catches this centrally and
// turns it into an InvalidArguments result — individual tools never need to
// build that result shape themselves.
public class ToolArgumentException : Exception
{
    public ToolArgumentException(string message) : base(message)
    {
    }
}

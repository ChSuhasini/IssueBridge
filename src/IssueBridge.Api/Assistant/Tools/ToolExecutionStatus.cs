namespace IssueBridge.Api.Assistant.Tools;

public enum ToolExecutionStatus
{
    Success,
    UnknownTool,
    InvalidArguments,
    UnsupportedOperation,
    ExecutionError
}

namespace IssueBridge.Api.Assistant;

public enum AssistantRequestStatus
{
    Success,
    IterationLimitReached,
    ModelError,
    EmptyResponse
}

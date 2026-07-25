using System.Text.Json;

namespace IssueBridge.Api.Assistant.Tools;

// The only place a model-chosen tool name + raw JSON arguments turns into an
// actual DB query. Every execution path funnels through here, so this is also
// the only place that needs to guard against unknown tool names, malformed
// arguments, and any tool that isn't explicitly marked read-only.
public class AssistantToolExecutor
{
    private readonly IReadOnlyDictionary<string, IAssistantTool> _tools;

    public AssistantToolExecutor(IEnumerable<IAssistantTool> tools)
    {
        _tools = tools.ToDictionary(t => t.Name);
    }

    public IReadOnlyCollection<IAssistantTool> Tools => _tools.Values.ToList();

    public async Task<ToolExecutionResult> ExecuteAsync(
        string toolName,
        JsonElement arguments,
        CancellationToken cancellationToken = default)
    {
        if (!_tools.TryGetValue(toolName, out var tool))
        {
            return new ToolExecutionResult
            {
                Status = ToolExecutionStatus.UnknownTool,
                ErrorMessage = $"Unknown tool '{toolName}'."
            };
        }

        if (!tool.IsReadOnly)
        {
            return new ToolExecutionResult
            {
                Status = ToolExecutionStatus.UnsupportedOperation,
                ErrorMessage = $"Tool '{toolName}' is not permitted: only read-only tools may be executed."
            };
        }

        try
        {
            return await tool.ExecuteAsync(arguments, cancellationToken);
        }
        catch (ToolArgumentException ex)
        {
            return new ToolExecutionResult
            {
                Status = ToolExecutionStatus.InvalidArguments,
                ErrorMessage = ex.Message
            };
        }
        catch (Exception)
        {
            // Anything a tool didn't anticipate (DB error, null ref, etc.) — never
            // let this escape to the caller. Message is intentionally generic;
            // the exception itself is what a real logger would capture later.
            return new ToolExecutionResult
            {
                Status = ToolExecutionStatus.ExecutionError,
                ErrorMessage = $"Tool '{toolName}' failed unexpectedly."
            };
        }
    }
}

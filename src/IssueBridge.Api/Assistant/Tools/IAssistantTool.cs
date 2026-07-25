using System.Text.Json;
using System.Text.Json.Nodes;

namespace IssueBridge.Api.Assistant.Tools;

public interface IAssistantTool
{
    string Name { get; }
    string Description { get; }

    // Every current tool is true. AssistantToolExecutor checks this before
    // running anything, so a future tool that forgets to be read-only is
    // rejected structurally rather than by convention.
    bool IsReadOnly { get; }

    // JSON Schema (object/properties/required) describing the arguments this
    // tool accepts — this is what gets sent to the model as the tool definition.
    JsonObject InputSchema { get; }

    Task<ToolExecutionResult> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken);
}

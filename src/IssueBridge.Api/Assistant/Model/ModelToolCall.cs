using System.Text.Json;

namespace IssueBridge.Api.Assistant.Model;

public class ModelToolCall
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required JsonElement Arguments { get; init; }
}

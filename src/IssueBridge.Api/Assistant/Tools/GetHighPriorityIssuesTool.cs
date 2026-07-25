using System.Text.Json;
using System.Text.Json.Nodes;
using IssueBridge.Api.Assistant.Dtos;
using IssueBridge.Api.Data;
using IssueBridge.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace IssueBridge.Api.Assistant.Tools;

public class GetHighPriorityIssuesTool : IAssistantTool
{
    private readonly IssueBridgeDbContext _db;

    public GetHighPriorityIssuesTool(IssueBridgeDbContext db)
    {
        _db = db;
    }

    public string Name => "get_high_priority_issues";
    public string Description => "Lists issues whose local Priority is set to High.";
    public bool IsReadOnly => true;

    public JsonObject InputSchema => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject(),
        ["required"] = new JsonArray()
    };

    public async Task<ToolExecutionResult> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var issues = await _db.Issues
            .Include(i => i.LocalTaskInfo)
            .Where(i => i.LocalTaskInfo!.Priority == Priority.High)
            .ToListAsync(cancellationToken);

        var summaries = issues.Select(IssueSummaryDto.FromEntity).ToList();
        return ToolExecutionResult.Ok(summaries, $"{summaries.Count} high-priority issue(s).");
    }
}

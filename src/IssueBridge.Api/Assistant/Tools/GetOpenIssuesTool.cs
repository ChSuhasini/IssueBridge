using System.Text.Json;
using System.Text.Json.Nodes;
using IssueBridge.Api.Assistant.Dtos;
using IssueBridge.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace IssueBridge.Api.Assistant.Tools;

public class GetOpenIssuesTool : IAssistantTool
{
    private readonly IssueBridgeDbContext _db;

    public GetOpenIssuesTool(IssueBridgeDbContext db)
    {
        _db = db;
    }

    public string Name => "get_open_issues";
    public string Description => "Lists GitHub issues that are still open (GitHub state == \"open\"), regardless of local status.";
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
            .Where(i => i.State == "open")
            .ToListAsync(cancellationToken);

        var summaries = issues.Select(IssueSummaryDto.FromEntity).ToList();
        return ToolExecutionResult.Ok(summaries, $"{summaries.Count} open issue(s).");
    }
}

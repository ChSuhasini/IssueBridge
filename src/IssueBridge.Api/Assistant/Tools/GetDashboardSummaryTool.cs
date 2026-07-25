using System.Text.Json;
using System.Text.Json.Nodes;
using IssueBridge.Api.Data;
using IssueBridge.Api.Dtos;
using IssueBridge.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace IssueBridge.Api.Assistant.Tools;

public class GetDashboardSummaryTool : IAssistantTool
{
    private readonly IssueBridgeDbContext _db;

    public GetDashboardSummaryTool(IssueBridgeDbContext db)
    {
        _db = db;
    }

    public string Name => "get_dashboard_summary";
    public string Description => "Returns aggregate counts: Open, In Progress, Done, and High Priority issue counts.";
    public bool IsReadOnly => true;

    public JsonObject InputSchema => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject(),
        ["required"] = new JsonArray()
    };

    public async Task<ToolExecutionResult> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var localInfos = await _db.LocalTaskInfos.ToListAsync(cancellationToken);

        var summary = new DashboardSummaryDto
        {
            Open = localInfos.Count(l => l.LocalStatus == LocalStatus.NotStarted),
            InProgress = localInfos.Count(l => l.LocalStatus == LocalStatus.InProgress),
            Done = localInfos.Count(l => l.LocalStatus == LocalStatus.Done),
            HighPriority = localInfos.Count(l => l.Priority == Priority.High)
        };

        return ToolExecutionResult.Ok(
            summary,
            $"Open={summary.Open}, InProgress={summary.InProgress}, Done={summary.Done}, HighPriority={summary.HighPriority}.");
    }
}

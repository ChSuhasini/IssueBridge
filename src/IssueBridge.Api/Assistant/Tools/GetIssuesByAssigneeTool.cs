using System.Text.Json;
using System.Text.Json.Nodes;
using IssueBridge.Api.Assistant.Dtos;
using IssueBridge.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace IssueBridge.Api.Assistant.Tools;

public class GetIssuesByAssigneeTool : IAssistantTool
{
    private readonly IssueBridgeDbContext _db;

    public GetIssuesByAssigneeTool(IssueBridgeDbContext db)
    {
        _db = db;
    }

    public string Name => "get_issues_by_assignee";
    public string Description => "Lists issues currently assigned to a specific person (exact match on the local AssignedTo field).";
    public bool IsReadOnly => true;

    public JsonObject InputSchema => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["assignedTo"] = new JsonObject
            {
                ["type"] = "string",
                ["description"] = "The assignee's name, matching what's stored in AssignedTo."
            }
        },
        ["required"] = new JsonArray("assignedTo")
    };

    public async Task<ToolExecutionResult> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        if (arguments.ValueKind != JsonValueKind.Object ||
            !arguments.TryGetProperty("assignedTo", out var assignedToProp) ||
            assignedToProp.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(assignedToProp.GetString()))
        {
            throw new ToolArgumentException("'assignedTo' is required and must be a non-empty string.");
        }

        var assignedTo = assignedToProp.GetString()!;

        var issues = await _db.Issues
            .Include(i => i.LocalTaskInfo)
            .Where(i => i.LocalTaskInfo!.AssignedTo == assignedTo)
            .ToListAsync(cancellationToken);

        var summaries = issues.Select(IssueSummaryDto.FromEntity).ToList();
        return ToolExecutionResult.Ok(summaries, $"{summaries.Count} issue(s) assigned to {assignedTo}.");
    }
}

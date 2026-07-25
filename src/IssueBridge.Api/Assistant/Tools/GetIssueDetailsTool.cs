using System.Text.Json;
using System.Text.Json.Nodes;
using IssueBridge.Api.Data;
using IssueBridge.Api.Dtos;
using Microsoft.EntityFrameworkCore;

namespace IssueBridge.Api.Assistant.Tools;

public class GetIssueDetailsTool : IAssistantTool
{
    private readonly IssueBridgeDbContext _db;

    public GetIssueDetailsTool(IssueBridgeDbContext db)
    {
        _db = db;
    }

    public string Name => "get_issue_details";
    public string Description =>
        "Returns full details (title, body, state, notes, assignee, priority) for a single issue by its GitHub issue number.";
    public bool IsReadOnly => true;

    public JsonObject InputSchema => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["issueNumber"] = new JsonObject
            {
                ["type"] = "integer",
                ["description"] = "The GitHub issue number, e.g. 12 for issue #12."
            }
        },
        ["required"] = new JsonArray("issueNumber")
    };

    public async Task<ToolExecutionResult> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        if (arguments.ValueKind != JsonValueKind.Object ||
            !arguments.TryGetProperty("issueNumber", out var issueNumberProp) ||
            issueNumberProp.ValueKind != JsonValueKind.Number ||
            !issueNumberProp.TryGetInt32(out var issueNumber) ||
            issueNumber <= 0)
        {
            throw new ToolArgumentException("'issueNumber' is required and must be a positive integer.");
        }

        var issue = await _db.Issues
            .Include(i => i.LocalTaskInfo)
            .FirstOrDefaultAsync(i => i.Number == issueNumber, cancellationToken);

        if (issue is null)
        {
            return ToolExecutionResult.Ok(null, $"No issue found with number {issueNumber}.");
        }

        var dto = IssueDto.FromEntity(issue);
        return ToolExecutionResult.Ok(
            dto,
            $"Issue #{issueNumber}: \"{dto.Title}\" ({dto.State}, {dto.LocalStatus}, {dto.Priority}).");
    }
}

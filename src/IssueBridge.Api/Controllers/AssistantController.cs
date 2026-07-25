using System.Diagnostics;
using IssueBridge.Api.Assistant;
using IssueBridge.Api.Data;
using IssueBridge.Api.Dtos;
using IssueBridge.Api.Models;
using Microsoft.AspNetCore.Mvc;

namespace IssueBridge.Api.Controllers;

[ApiController]
[Route("api/assistant")]
public class AssistantController : ControllerBase
{
    private readonly AssistantAgent _agent;
    private readonly IssueBridgeDbContext _db;

    public AssistantController(AssistantAgent agent, IssueBridgeDbContext db)
    {
        _agent = agent;
        _db = db;
    }

    [HttpPost("ask")]
    public async Task<ActionResult<AssistantAskResponseDto>> Ask(
        [FromBody] AssistantAskRequestDto request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Question))
        {
            return BadRequest("Question must not be empty.");
        }

        var stopwatch = Stopwatch.StartNew();
        var result = await _agent.AskAsync(request.Question, cancellationToken);
        stopwatch.Stop();

        await LogAsync(request.Question, result, stopwatch.ElapsedMilliseconds, cancellationToken);

        return new AssistantAskResponseDto
        {
            Answer = result.Answer,
            ToolsCalled = result.ToolsCalled,
            Status = result.Status.ToString(),
            DurationMs = stopwatch.ElapsedMilliseconds
        };
    }

    private async Task LogAsync(string question, AssistantAskResult result, long durationMs, CancellationToken cancellationToken)
    {
        _db.AssistantQueryLogs.Add(new AssistantQueryLog
        {
            Question = question,
            ToolNamesCalled = result.ToolsCalled.Count > 0 ? string.Join(",", result.ToolsCalled) : null,
            Status = result.Status.ToString(),
            DurationMs = durationMs,
            ErrorCategory = result.Status == AssistantRequestStatus.Success ? null : result.Status.ToString(),
            CreatedAt = DateTimeOffset.UtcNow
        });

        await _db.SaveChangesAsync(cancellationToken);
    }
}

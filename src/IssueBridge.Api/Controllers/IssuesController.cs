using IssueBridge.Api.Data;
using IssueBridge.Api.Dtos;
using IssueBridge.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IssueBridge.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class IssuesController : ControllerBase
{
    private readonly IssueBridgeDbContext _db;

    public IssuesController(IssueBridgeDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<List<IssueDto>>> GetIssues(
        [FromQuery] LocalStatus? status,
        [FromQuery] string? assignee,
        [FromQuery] Priority? priority,
        CancellationToken cancellationToken)
    {
        var query = _db.Issues.Include(i => i.LocalTaskInfo).AsQueryable();

        if (status is not null)
        {
            query = query.Where(i => i.LocalTaskInfo!.LocalStatus == status);
        }

        if (!string.IsNullOrWhiteSpace(assignee))
        {
            query = query.Where(i => i.LocalTaskInfo!.AssignedTo == assignee);
        }

        if (priority is not null)
        {
            query = query.Where(i => i.LocalTaskInfo!.Priority == priority);
        }

        // SQLite can't translate ORDER BY on DateTimeOffset server-side, so sort client-side.
        var issues = await query.ToListAsync(cancellationToken);
        return issues
            .OrderByDescending(i => i.GitHubUpdatedAt)
            .Select(IssueDto.FromEntity)
            .ToList();
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<IssueDto>> GetIssue(int id, CancellationToken cancellationToken)
    {
        var issue = await _db.Issues
            .Include(i => i.LocalTaskInfo)
            .FirstOrDefaultAsync(i => i.Id == id, cancellationToken);

        return issue is null ? NotFound() : IssueDto.FromEntity(issue);
    }

    [HttpPut("{id:int}/local")]
    public async Task<ActionResult<IssueDto>> UpdateLocal(int id, UpdateLocalTaskInfoDto dto, CancellationToken cancellationToken)
    {
        var issue = await _db.Issues
            .Include(i => i.LocalTaskInfo)
            .FirstOrDefaultAsync(i => i.Id == id, cancellationToken);

        if (issue is null)
        {
            return NotFound();
        }

        // Every Issue gets a LocalTaskInfo row on creation during sync, but
        // fall back to creating one here rather than assuming it always exists.
        var local = issue.LocalTaskInfo ??= new LocalTaskInfo { IssueId = issue.Id };

        local.AssignedTo = dto.AssignedTo;
        local.LocalStatus = dto.LocalStatus;
        local.Notes = dto.Notes;
        local.Priority = dto.Priority;
        local.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        return IssueDto.FromEntity(issue);
    }
}

using IssueBridge.Api.Data;
using IssueBridge.Api.Dtos;
using IssueBridge.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IssueBridge.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DashboardController : ControllerBase
{
    private readonly IssueBridgeDbContext _db;

    public DashboardController(IssueBridgeDbContext db)
    {
        _db = db;
    }

    [HttpGet("summary")]
    public async Task<ActionResult<DashboardSummaryDto>> GetSummary(CancellationToken cancellationToken)
    {
        var localInfos = await _db.LocalTaskInfos.ToListAsync(cancellationToken);

        return new DashboardSummaryDto
        {
            Open = localInfos.Count(l => l.LocalStatus == LocalStatus.NotStarted),
            InProgress = localInfos.Count(l => l.LocalStatus == LocalStatus.InProgress),
            Done = localInfos.Count(l => l.LocalStatus == LocalStatus.Done),
            HighPriority = localInfos.Count(l => l.Priority == Priority.High)
        };
    }
}

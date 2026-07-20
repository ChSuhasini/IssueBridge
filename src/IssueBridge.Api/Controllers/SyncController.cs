using IssueBridge.Api.GitHub;
using Microsoft.AspNetCore.Mvc;

namespace IssueBridge.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SyncController : ControllerBase
{
    private readonly ISyncService _syncService;

    public SyncController(ISyncService syncService)
    {
        _syncService = syncService;
    }

    [HttpPost]
    public async Task<ActionResult<SyncResult>> Sync(CancellationToken cancellationToken)
    {
        var result = await _syncService.SyncAsync(cancellationToken);
        return result.Success ? Ok(result) : StatusCode(StatusCodes.Status502BadGateway, result);
    }
}

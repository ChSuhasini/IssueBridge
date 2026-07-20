namespace IssueBridge.Api.GitHub;

public interface ISyncService
{
    Task<SyncResult> SyncAsync(CancellationToken cancellationToken = default);
}

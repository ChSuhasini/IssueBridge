import { useState } from "react";
import { triggerSync } from "../api";
import type { SyncResult } from "../types";
import "./SyncButton.css";

interface Props {
  onSyncComplete: () => void;
}

export function SyncButton({ onSyncComplete }: Props) {
  const [syncing, setSyncing] = useState(false);
  const [lastResult, setLastResult] = useState<SyncResult | null>(null);

  async function handleClick() {
    setSyncing(true);
    setLastResult(null);
    try {
      const result = await triggerSync();
      setLastResult(result);
      if (result.success) {
        onSyncComplete();
      }
    } catch (err) {
      setLastResult({
        success: false,
        created: 0,
        updated: 0,
        skipped: 0,
        durationMs: 0,
        error: err instanceof Error ? err.message : "Sync request failed",
      });
    } finally {
      setSyncing(false);
    }
  }

  return (
    <div className="sync-control">
      <button className="sync-button" onClick={handleClick} disabled={syncing}>
        {syncing ? "Syncing…" : "Sync Now"}
      </button>
      {lastResult && (
        <div className={`sync-result ${lastResult.success ? "sync-result-ok" : "sync-result-error"}`}>
          {lastResult.success
            ? `Synced: ${lastResult.created} created, ${lastResult.updated} updated, ${lastResult.skipped} skipped (${lastResult.durationMs}ms)`
            : `Sync failed: ${lastResult.error ?? "unknown error"}`}
        </div>
      )}
    </div>
  );
}

import { useCallback, useEffect, useState } from "react";
import { fetchDashboardSummary, fetchIssues, updateLocalTaskInfo } from "./api";
import { EditPanel } from "./components/EditPanel";
import { IssueTable } from "./components/IssueTable";
import { SummaryCards } from "./components/SummaryCards";
import { SyncButton } from "./components/SyncButton";
import type { DashboardSummary, Issue, IssueFilters, UpdateLocalTaskInfoRequest } from "./types";
import "./App.css";

function App() {
  const [issues, setIssues] = useState<Issue[]>([]);
  const [summary, setSummary] = useState<DashboardSummary | null>(null);
  const [filters, setFilters] = useState<IssueFilters>({});
  const [loading, setLoading] = useState(true);
  const [selectedIssue, setSelectedIssue] = useState<Issue | null>(null);
  const [error, setError] = useState<string | null>(null);

  const loadData = useCallback(async (currentFilters: IssueFilters) => {
    setLoading(true);
    setError(null);
    try {
      const [issuesData, summaryData] = await Promise.all([fetchIssues(currentFilters), fetchDashboardSummary()]);
      setIssues(issuesData);
      setSummary(summaryData);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to load data");
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    loadData(filters);
  }, [filters, loadData]);

  async function handleSaveLocal(id: number, data: UpdateLocalTaskInfoRequest) {
    await updateLocalTaskInfo(id, data);
    await loadData(filters);
  }

  return (
    <div className="app-shell">
      <header className="app-header">
        <h1>IssueBridge</h1>
        <SyncButton onSyncComplete={() => loadData(filters)} />
      </header>

      {error && <div className="app-error">{error}</div>}

      <SummaryCards summary={summary} loading={loading} />

      <IssueTable
        issues={issues}
        loading={loading}
        filters={filters}
        onFiltersChange={setFilters}
        onSelect={setSelectedIssue}
      />

      {selectedIssue && (
        <EditPanel issue={selectedIssue} onClose={() => setSelectedIssue(null)} onSave={handleSaveLocal} />
      )}
    </div>
  );
}

export default App;

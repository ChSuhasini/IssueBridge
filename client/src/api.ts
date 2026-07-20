import type {
  DashboardSummary,
  Issue,
  IssueFilters,
  LocalStatus,
  Priority,
  SyncResult,
  UpdateLocalTaskInfoRequest,
} from "./types";

const API_BASE = "/api";

async function handleJson<T>(response: Response): Promise<T> {
  if (!response.ok) {
    const body = await response.text().catch(() => "");
    throw new Error(`Request failed (${response.status}): ${body || response.statusText}`);
  }
  return response.json() as Promise<T>;
}

export async function fetchIssues(filters: IssueFilters = {}): Promise<Issue[]> {
  const params = new URLSearchParams();
  if (filters.status !== undefined) params.set("status", LocalStatusName(filters.status));
  if (filters.priority !== undefined) params.set("priority", PriorityName(filters.priority));
  if (filters.assignee) params.set("assignee", filters.assignee);

  const query = params.toString();
  const response = await fetch(`${API_BASE}/issues${query ? `?${query}` : ""}`);
  return handleJson<Issue[]>(response);
}

export async function fetchDashboardSummary(): Promise<DashboardSummary> {
  const response = await fetch(`${API_BASE}/dashboard/summary`);
  return handleJson<DashboardSummary>(response);
}

export async function updateLocalTaskInfo(id: number, data: UpdateLocalTaskInfoRequest): Promise<Issue> {
  const response = await fetch(`${API_BASE}/issues/${id}/local`, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(data),
  });
  return handleJson<Issue>(response);
}

export async function triggerSync(): Promise<SyncResult> {
  const response = await fetch(`${API_BASE}/sync`, { method: "POST" });
  // Sync intentionally returns 502 on failure with a structured SyncResult body,
  // so we parse the JSON either way rather than treating non-2xx as fatal here.
  return response.json() as Promise<SyncResult>;
}

function LocalStatusName(status: LocalStatus): string {
  return ["NotStarted", "InProgress", "Done"][status];
}

function PriorityName(priority: Priority): string {
  return ["Low", "Medium", "High"][priority];
}

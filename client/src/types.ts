export enum LocalStatus {
  NotStarted = 0,
  InProgress = 1,
  Done = 2,
}

export enum Priority {
  Low = 0,
  Medium = 1,
  High = 2,
}

export interface Issue {
  id: number;
  number: number;
  title: string;
  body: string | null;
  state: string;
  labels: string | null;
  gitHubUrl: string;
  gitHubCreatedAt: string;
  gitHubUpdatedAt: string;
  lastSyncedAt: string;
  assignedTo: string | null;
  localStatus: LocalStatus;
  notes: string | null;
  priority: Priority;
  localUpdatedAt: string;
}

export interface DashboardSummary {
  open: number;
  inProgress: number;
  done: number;
  highPriority: number;
}

export interface SyncResult {
  success: boolean;
  created: number;
  updated: number;
  skipped: number;
  durationMs: number;
  error: string | null;
}

export interface UpdateLocalTaskInfoRequest {
  assignedTo: string | null;
  localStatus: LocalStatus;
  notes: string | null;
  priority: Priority;
}

export interface IssueFilters {
  status?: LocalStatus;
  assignee?: string;
  priority?: Priority;
}

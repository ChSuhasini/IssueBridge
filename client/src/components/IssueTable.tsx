import { LocalStatus, Priority, type Issue, type IssueFilters } from "../types";
import "./IssueTable.css";

interface Props {
  issues: Issue[];
  loading: boolean;
  filters: IssueFilters;
  onFiltersChange: (filters: IssueFilters) => void;
  onSelect: (issue: Issue) => void;
}

const statusLabels: Record<LocalStatus, string> = {
  [LocalStatus.NotStarted]: "Not Started",
  [LocalStatus.InProgress]: "In Progress",
  [LocalStatus.Done]: "Done",
};

const priorityLabels: Record<Priority, string> = {
  [Priority.Low]: "Low",
  [Priority.Medium]: "Medium",
  [Priority.High]: "High",
};

export function IssueTable({ issues, loading, filters, onFiltersChange, onSelect }: Props) {
  return (
    <div className="issue-table-wrap">
      <div className="issue-filters">
        <select
          value={filters.status ?? ""}
          onChange={(e) =>
            onFiltersChange({
              ...filters,
              status: e.target.value === "" ? undefined : (Number(e.target.value) as LocalStatus),
            })
          }
        >
          <option value="">All statuses</option>
          <option value={LocalStatus.NotStarted}>Not Started</option>
          <option value={LocalStatus.InProgress}>In Progress</option>
          <option value={LocalStatus.Done}>Done</option>
        </select>

        <select
          value={filters.priority ?? ""}
          onChange={(e) =>
            onFiltersChange({
              ...filters,
              priority: e.target.value === "" ? undefined : (Number(e.target.value) as Priority),
            })
          }
        >
          <option value="">All priorities</option>
          <option value={Priority.Low}>Low</option>
          <option value={Priority.Medium}>Medium</option>
          <option value={Priority.High}>High</option>
        </select>

        <input
          type="text"
          placeholder="Filter by assignee"
          value={filters.assignee ?? ""}
          onChange={(e) => onFiltersChange({ ...filters, assignee: e.target.value || undefined })}
        />
      </div>

      <div className="issue-table-scroll">
        <table className="issue-table">
          <thead>
            <tr>
              <th>#</th>
              <th>Title</th>
              <th>GitHub State</th>
              <th>Assignee</th>
              <th>Status</th>
              <th>Priority</th>
            </tr>
          </thead>
          <tbody>
            {loading && (
              <tr>
                <td colSpan={6} className="issue-table-empty">
                  Loading…
                </td>
              </tr>
            )}
            {!loading && issues.length === 0 && (
              <tr>
                <td colSpan={6} className="issue-table-empty">
                  No issues match these filters.
                </td>
              </tr>
            )}
            {!loading &&
              issues.map((issue) => (
                <tr key={issue.id} onClick={() => onSelect(issue)} className="issue-row">
                  <td>{issue.number}</td>
                  <td className="issue-title-cell">{issue.title}</td>
                  <td>
                    <span className={`github-state github-state-${issue.state}`}>{issue.state}</span>
                  </td>
                  <td>{issue.assignedTo || <span className="issue-empty-cell">Unassigned</span>}</td>
                  <td>{statusLabels[issue.localStatus]}</td>
                  <td>
                    <span className={`priority-badge priority-${issue.priority}`}>{priorityLabels[issue.priority]}</span>
                  </td>
                </tr>
              ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}

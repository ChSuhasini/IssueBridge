import { useState } from "react";
import { LocalStatus, Priority, type Issue, type UpdateLocalTaskInfoRequest } from "../types";
import "./EditPanel.css";

interface Props {
  issue: Issue;
  onClose: () => void;
  onSave: (id: number, data: UpdateLocalTaskInfoRequest) => Promise<void>;
}

export function EditPanel({ issue, onClose, onSave }: Props) {
  const [assignedTo, setAssignedTo] = useState(issue.assignedTo ?? "");
  const [localStatus, setLocalStatus] = useState(issue.localStatus);
  const [notes, setNotes] = useState(issue.notes ?? "");
  const [priority, setPriority] = useState(issue.priority);
  const [saving, setSaving] = useState(false);

  async function handleSave() {
    setSaving(true);
    try {
      await onSave(issue.id, {
        assignedTo: assignedTo.trim() || null,
        localStatus,
        notes: notes.trim() || null,
        priority,
      });
      onClose();
    } finally {
      setSaving(false);
    }
  }

  return (
    <div className="edit-panel-overlay" onClick={onClose}>
      <div className="edit-panel" onClick={(e) => e.stopPropagation()}>
        <div className="edit-panel-header">
          <div>
            <div className="edit-panel-number">#{issue.number}</div>
            <h2 className="edit-panel-title">{issue.title}</h2>
          </div>
          <button className="edit-panel-close" onClick={onClose} aria-label="Close">
            ×
          </button>
        </div>

        <a className="edit-panel-github-link" href={issue.gitHubUrl} target="_blank" rel="noreferrer">
          View on GitHub ↗
        </a>

        {issue.body && <p className="edit-panel-body">{issue.body}</p>}

        <div className="edit-panel-field">
          <label>Assigned to</label>
          <input value={assignedTo} onChange={(e) => setAssignedTo(e.target.value)} placeholder="Unassigned" />
        </div>

        <div className="edit-panel-field">
          <label>Status</label>
          <select value={localStatus} onChange={(e) => setLocalStatus(Number(e.target.value) as LocalStatus)}>
            <option value={LocalStatus.NotStarted}>Not Started</option>
            <option value={LocalStatus.InProgress}>In Progress</option>
            <option value={LocalStatus.Done}>Done</option>
          </select>
        </div>

        <div className="edit-panel-field">
          <label>Priority</label>
          <select value={priority} onChange={(e) => setPriority(Number(e.target.value) as Priority)}>
            <option value={Priority.Low}>Low</option>
            <option value={Priority.Medium}>Medium</option>
            <option value={Priority.High}>High</option>
          </select>
        </div>

        <div className="edit-panel-field">
          <label>Notes</label>
          <textarea value={notes} onChange={(e) => setNotes(e.target.value)} rows={5} placeholder="Internal notes…" />
        </div>

        <div className="edit-panel-actions">
          <button className="edit-panel-cancel" onClick={onClose} disabled={saving}>
            Cancel
          </button>
          <button className="edit-panel-save" onClick={handleSave} disabled={saving}>
            {saving ? "Saving…" : "Save"}
          </button>
        </div>
      </div>
    </div>
  );
}

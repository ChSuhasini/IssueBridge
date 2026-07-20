import type { DashboardSummary } from "../types";
import "./SummaryCards.css";

interface Props {
  summary: DashboardSummary | null;
  loading: boolean;
}

interface Tile {
  label: string;
  value: number | null;
  accentVar: string;
  icon: string;
}

export function SummaryCards({ summary, loading }: Props) {
  const tiles: Tile[] = [
    { label: "Open", value: summary?.open ?? null, accentVar: "--accent-blue", icon: "○" },
    { label: "In Progress", value: summary?.inProgress ?? null, accentVar: "--accent-aqua", icon: "◐" },
    { label: "Done", value: summary?.done ?? null, accentVar: "--status-good", icon: "✓" },
    { label: "High Priority", value: summary?.highPriority ?? null, accentVar: "--status-critical", icon: "!" },
  ];

  return (
    <div className="summary-cards">
      {tiles.map((tile) => (
        <div className="stat-tile" key={tile.label} style={{ ["--tile-accent" as string]: `var(${tile.accentVar})` }}>
          <div className="stat-tile-icon" aria-hidden="true">
            {tile.icon}
          </div>
          <div className="stat-tile-value">{loading ? "—" : tile.value}</div>
          <div className="stat-tile-label">{tile.label}</div>
        </div>
      ))}
    </div>
  );
}

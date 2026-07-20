# IssueBridge — Project Plan

## Context

IssueBridge is a portfolio project: a local mirror of GitHub issues with a team-owned workflow layer (status, assignee, notes, priority) that GitHub itself can never overwrite. The point isn't "call the GitHub API" — it's proving sync idempotency, dedup, and clean separation between external (GitHub-owned) data and internal (team-owned) data hold up under real conditions (re-syncs, upstream changes, API failures).

Starting point: completely empty environment. No .NET SDK installed yet (verified — `dotnet --version` not found). No GitHub PAT yet. No prior experience with ASP.NET Core, EF Core, or React tooling. Node/npm/git are already installed. Because of this, the plan is phased with a checkpoint after each phase — nothing moves to the next phase until the previous one is confirmed working.

**Stack decisions already made:**
- Database: SQLite
- Deployment target: undecided — build and prove it locally first, decide on hosting later
- GitHub source repo/token: not set up yet

## Repo layout

```
IssueBridge/IssueBridge/        (git root)
  IssueBridge.sln
  src/
    IssueBridge.Api/            ASP.NET Core Web API
    IssueBridge.Tests/          xUnit test project
  client/                       React app (Vite + TS)
  README.md
  plan.md                       (this file)
```

## Data model (the core design decision)

Two tables, deliberately kept separate so sync can never clobber team edits:

- **`Issue`** — read-only mirror of GitHub. Columns: `Id` (PK), `GitHubIssueId`, `Number`, `Title`, `Body`, `State` (open/closed), `Labels`, `GitHubUrl`, `GitHubCreatedAt`, `GitHubUpdatedAt`, `LastSyncedAt`. Every field here is overwritten wholesale on sync — nothing here is user-editable via the API.
- **`LocalTaskInfo`** — team-owned, 1:1 with `Issue` via `IssueId` FK. Columns: `IssueId` (PK/FK), `AssignedTo`, `LocalStatus` (NotStarted/InProgress/Done — independent of GitHub's open/closed), `Notes`, `Priority`, `UpdatedAt`. Sync **never** writes to this table except to `INSERT` a default row (`NotStarted`, unassigned) the first time an `Issue` is created.

This separation is enforced structurally: sync code only ever touches `Issue`; the local-edit endpoint only ever touches `LocalTaskInfo`. That way it can't accidentally regress.

## Phase 0 — Environment setup (do this first, together)

1. Install .NET 8 SDK (LTS) — via the official installer or `winget install Microsoft.DotNet.SDK.8`. Verify with `dotnet --version`.
2. Create a GitHub Personal Access Token (fine-grained, read-only `Issues` scope on a repo you own) — walk through the exact GitHub UI steps.
3. Pick a test repo: either an existing small repo you own, or create a scratch repo with ~10-15 sample issues (mix of open/closed, some with labels) so sync has real pagination to exercise.
4. Store the PAT via `dotnet user-secrets` (never committed, never in appsettings.json).

**Checkpoint:** `dotnet --version` succeeds, PAT created, test repo identified.

## Phase 1 — Backend skeleton

- `dotnet new sln`, `dotnet new webapi -o src/IssueBridge.Api`, `dotnet new xunit -o src/IssueBridge.Tests`, wire both into the sln.
- Add `Microsoft.EntityFrameworkCore.Sqlite` + `Microsoft.EntityFrameworkCore.Design`.
- Define `Issue` and `LocalTaskInfo` entities + `IssueBridgeDbContext`, create the initial EF Core migration, confirm `dotnet ef database update` creates `issuebridge.db` with both tables.

**Checkpoint:** API runs (`dotnet run`), Swagger loads, SQLite file exists with the two tables.

## Phase 2 — GitHub sync

- `GitHubIssuesClient`: typed `HttpClient` (via `IHttpClientFactory`) calling `GET /repos/{owner}/{repo}/issues?state=all&per_page=100&page=N` with the PAT in the `Authorization` header, paginating by incrementing `page` until an empty array is returned. Skip entries that have a `pull_request` key (GitHub's issues endpoint includes PRs).
- Sync algorithm: fetch **all** pages into memory first, validate the full fetch succeeded, *then* write to the DB in a single transaction — so a failure partway through never leaves a half-synced state.
  - New `GitHubIssueId` → insert `Issue` + default `LocalTaskInfo` row.
  - Existing, `GitHubUpdatedAt` changed → update only `Issue` columns.
  - Existing, unchanged → skip (no write).
- Failure handling: any `HttpRequestException` / non-2xx response aborts the sync before any DB write, returns a structured error (status code, message) — no partial writes.
- `POST /api/sync` → returns `{ created, updated, skipped, durationMs }` or a clean error payload.

**Checkpoint:** Trigger sync against the test repo via Swagger, confirm counts match reality, re-run and confirm second run reports all-skipped.

## Phase 3 — CRUD + dashboard endpoints

- `GET /api/issues?status=&assignee=&priority=` — joins `Issue` + `LocalTaskInfo`, filters.
- `GET /api/issues/{id}` — single issue with its local info.
- `PUT /api/issues/{id}/local` — updates only `LocalTaskInfo` fields (assignee, status, notes, priority); 404 if the issue doesn't exist.
- `GET /api/dashboard/summary` — aggregate counts (Open, In Progress, Done, High Priority) computed from `LocalTaskInfo.LocalStatus` / `Priority`.

**Checkpoint:** Exercise all endpoints via Swagger against synced data.

## Phase 4 — Tests (xUnit)

Using EF Core's SQLite in-memory provider (real SQLite semantics, isolated per test) and a stubbed `HttpMessageHandler` to fake GitHub responses (no real network calls in tests):

1. New GitHub issue → sync → exactly one `Issue` row + one default `LocalTaskInfo` row.
2. Re-sync with identical fake response → row counts unchanged, no duplicate `Issue` rows (idempotency).
3. GitHub issue's title/body changed upstream + local edits already made (assignee/notes set) → after sync, `Issue` fields reflect the update but `LocalTaskInfo` fields are untouched (core separation guarantee).
4. Fake `HttpMessageHandler` throws / returns 500 mid-pagination → sync returns a clean error, DB has zero new rows (transaction rollback verified).

**Checkpoint:** `dotnet test` — all four pass, each test's purpose documented in one line tying back to the risk it guards against.

## Phase 5 — Frontend (React + Vite)

- `npm create vite@latest client -- --template react-ts`.
- Dashboard page: summary cards (Open/In Progress/Done/High Priority) + filterable issue table.
- Edit panel (side panel or modal): assignee, status, notes, priority — calls `PUT /api/issues/{id}/local`.
- Sync button: calls `POST /api/sync`, shows loading state, then a result toast (counts or error).
- Simple `fetch`-based API client, no state library needed at this scale.

**Checkpoint:** Full loop works in the browser — click Sync, see issues populate, edit one, refresh, confirm the edit survived a second sync.

## Phase 6 — README + deployment

- README documents: architecture (two-table principle), how to run locally, actual numbers from a real test run (issue count synced, sync duration, what happened when the token was invalidated or network was cut mid-sync), and the "Next Improvement Steps" section (background sync, auth, rate-limit backoff, deleted-issue handling, webhooks).
- Revisit deployment target (Azure App Service vs Render/Railway) once the app is proven working locally.

## Verification approach throughout

Each phase ends with a manual checkpoint before moving to the next — no phase starts until the previous one is confirmed working. Phase 4's automated tests are the one place we lean on `dotnet test` rather than manual clicking.

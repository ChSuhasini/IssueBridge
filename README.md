# IssueBridge

A small internal tool that mirrors GitHub issues locally and lets a team layer their own workflow (status, assignment, notes, priority) on top — without needing GitHub itself to support that workflow.

GitHub is the source of truth for the code and issue text. It's a poor tool for a team's internal workflow around that issue — who's *actually* working on something this week, internal notes that shouldn't be public, a priority ranking GitHub's labels don't cleanly express, a single dashboard of what's open/urgent without opening GitHub. IssueBridge solves that specific gap.

## The core design principle

Two tables, deliberately kept separate so a sync can never destroy a team's own work:

- **`Issues`** — a read-only mirror of GitHub. Every field is overwritten wholesale on sync; nothing here is user-editable through the API.
- **`LocalTaskInfo`** — the team's own layer (assignee, local status, notes, priority). Sync *never* writes to this table except to insert the default row the first time an issue is created.

Sync code only ever touches `Issues`. The local-edit endpoint only ever touches `LocalTaskInfo`. This separation is proven, not just asserted — see [Testing](#testing) below.

## Architecture

```
React (Vite)  →  ASP.NET Core Web API  →  SQLite
localhost:5173     localhost:5080          issuebridge.db
```

- **Backend**: ASP.NET Core Web API (.NET 8), EF Core + SQLite
- **Frontend**: React + TypeScript (Vite)
- **Sync**: a typed `HttpClient` calls the GitHub REST API directly (no SDK/Octokit) — pagination, auth, and error handling are hand-rolled deliberately, since that's the actual skill being demonstrated
- **Tests**: xUnit, using a real SQLite `:memory:` connection (not the EF Core InMemory provider) so transaction behavior matches production, and a stubbed `HttpMessageHandler` so no test ever calls the real GitHub API

## Project layout

```
IssueBridge/
  IssueBridge.sln
  src/
    IssueBridge.Api/      ASP.NET Core Web API
      Models/              Issue, LocalTaskInfo, LocalStatus, Priority
      Data/                IssueBridgeDbContext
      GitHub/              GitHubIssuesClient, SyncService, DTOs
      Controllers/         SyncController, IssuesController, DashboardController
      Dtos/
    IssueBridge.Tests/     xUnit tests
  client/                  React app (Vite + TS)
```

## Setup

### Prerequisites
- .NET 8 SDK
- Node.js + npm
- A GitHub repo you own, and a fine-grained Personal Access Token scoped to that repo with **read-only Issues** permission

### 1. Configure secrets (never committed)

```bash
cd src/IssueBridge.Api
dotnet user-secrets init
dotnet user-secrets set "GitHub:Token" "your-pat-here"
dotnet user-secrets set "GitHub:Owner" "your-github-username"
dotnet user-secrets set "GitHub:Repo" "your-repo-name"
```

### 2. Create the database

```bash
dotnet tool install --global dotnet-ef   # first time only
dotnet ef database update
```

### 3. Run the backend

```bash
cd src/IssueBridge.Api
dotnet run --urls "http://localhost:5080"
```
Swagger UI: http://localhost:5080/swagger

### 4. Run the frontend

```bash
cd client
npm install
npm run dev
```
App: http://localhost:5173 (dev server proxies `/api` to `localhost:5080`)

## API endpoints

| Method | Route | Purpose |
|---|---|---|
| `POST` | `/api/sync` | Fetch all issues from GitHub, upsert `Issues`, never touch `LocalTaskInfo` except to default new rows |
| `GET` | `/api/issues?status=&assignee=&priority=` | List issues, optionally filtered |
| `GET` | `/api/issues/{id}` | Single issue with its local info |
| `PUT` | `/api/issues/{id}/local` | Update assignee/status/notes/priority — the only endpoint that writes to `LocalTaskInfo` |
| `GET` | `/api/dashboard/summary` | Aggregate counts: Open, In Progress, Done, High Priority |

## Testing

Four xUnit tests target the actual risk in a sync system — not coverage for its own sake:

| Test | What it proves |
|---|---|
| `NewGitHubIssue_CreatesExactlyOneIssueAndDefaultLocalTaskInfo` | Dedup logic works — the #1 bug risk in any sync system |
| `ResyncingUnchangedIssue_ProducesNoDuplicatesAndNoDataLoss` | Idempotency — syncing twice doesn't corrupt anything |
| `UpstreamIssueChange_UpdatesGitHubFieldsButPreservesLocalTaskInfo` | The two-table separation actually holds under a real upstream change |
| `GitHubFailureMidPagination_LeavesNoPartialWrites` | Failure handling — a fetch failure aborts *before* any DB write, verified via transaction rollback |

```
dotnet test
```
```
Passed!  - Failed: 0, Passed: 4, Skipped: 0, Total: 4, Duration: ~1s
```

## Real test results (against a live 8-issue GitHub repo)

All numbers below are from actual runs against [`IssueBridge-Test`](https://github.com/ChSuhasini/IssueBridge-Test), not estimates.

**First sync** (empty local DB → 8 GitHub issues):
```
{"success":true,"created":8,"updated":0,"skipped":0,"durationMs":2987}
```

**Immediate re-sync** (idempotency check — nothing changed upstream):
```
{"success":true,"created":0,"updated":0,"skipped":8,"durationMs":612}
```

**Local edit survives a sync** — issue #8 was assigned to a team member, set to "In Progress", "High" priority, with a note. Re-syncing afterward left every local field byte-for-byte unchanged (`localUpdatedAt` timestamp didn't even move), while GitHub-owned fields were still refreshed normally.

**Invalid token** (deliberately corrupted the PAT):
```
{"success":false,"created":0,"updated":0,"skipped":0,"durationMs":589,
 "error":"GitHub API returned 401 (Unauthorized) while fetching page 1."}
```
HTTP response: `502 Bad Gateway` with the structured error body above. Zero rows written.

**Network failure** (pointed the client at an unreachable/non-existent host, simulating no internet):
```
{"success":false,"created":0,"updated":0,"skipped":0,"durationMs":179,
 "error":"Network error while fetching page 1 of issues."}
```
Failed fast (~180ms, DNS resolution failure), zero rows written, same clean structured-error contract as the auth failure.

In both failure cases the sync fetches **all** pages into memory first and only writes to the database in a single transaction afterward — so a failure at any point during the GitHub fetch (even mid-pagination on page 2, 3, ...) can never leave a half-synced database. This is exercised directly by the `GitHubFailureMidPagination_LeavesNoPartialWrites` test.

## Next improvement steps

Documenting what's deliberately *not* built, and why:

- **Background scheduled sync** instead of a manual button — would need an `IHostedService` and a choice between polling every N minutes (simple, some staleness) vs. GitHub webhooks (real-time, but needs a publicly reachable endpoint — harder to stand up as a v1)
- **Authentication + multi-user roles** — this is single-user today; a real team needs login and per-user permissions (e.g. so "AssignedTo" can be validated against real accounts)
- **Rate-limit backoff/caching** — GitHub's REST API rate-limits unauthenticated/authenticated requests; a production system would inspect `X-RateLimit-Remaining` and delay/retry rather than just failing the sync
- **Handling deleted GitHub issues** — currently unhandled; a deleted issue on GitHub just stops appearing in the sync response, but its local row survives forever. A reconciliation pass (mark issues not seen in the latest sync as "possibly deleted") would close this gap
- **Webhooks instead of polling** — more real-time than any polling interval, but requires public hosting and webhook-signature verification

## Deployment

Not yet deployed — the app has been fully built and verified locally (backend, frontend, and the full sync/edit/re-sync loop, tested against a real GitHub repo). Deployment to a public URL (Render or Azure App Service are the leading candidates) is the next step once time allows.

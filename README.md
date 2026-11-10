# Grant Task Tracker

A small full-stack application for grants officers to manage grants and their
tasks: view grants with pending-task counts, add tasks to open grants, complete
tasks, close grants when the rules allow it, and spot overdue work.

- **Backend:** ASP.NET Core (.NET 10) Minimal API, EF Core + SQLite,
  MiniValidation, Serilog, OpenAPI/Swagger, ProblemDetails, `TimeProvider`,
  health checks, NUnit.
- **Frontend:** React 19 + TypeScript, Vite, shadcn/ui, Tailwind CSS, TanStack
  Query, React Hook Form + Zod, React Router, Axios, date-fns, lucide-react,
  Vitest + React Testing Library + MSW.

Requirements are in [`SPECS.md`](SPECS.md). The design is in
[`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md), the test strategy in
[`TESTING.md`](TESTING.md), and how AI was used in [`AI_USAGE.md`](AI_USAGE.md).

## Quick start

Prerequisites: **.NET SDK 10.0.102** or later (pinned by `global.json`) and
**Node.js 22** or later (developed on 26.13).

```bash
npm run setup          # restore .NET tools and packages, install frontend packages
npm test               # run every test (backend, then frontend)

npm run dev:backend    # API on http://localhost:5080 (Swagger: /swagger)
npm run dev:frontend   # UI on http://localhost:5173 (proxies /api to :5080)
```

Open http://localhost:5173. In Development the database is recreated and
reseeded on every start, so seed dates stay relative to today.

### Configuration

| Setting (environment variable) | Default | Meaning |
| ------------------------------ | ------- | ------- |
| `ConnectionStrings:GrantTracker` (`ConnectionStrings__GrantTracker`) | `Data Source=granttracker.db` | SQLite file |
| `Business:UtcOffset` (`Business__UtcOffset`) | `+00:00` | Fixed UTC offset that defines the business day ("today"). Accepts `+7`, `-8`, `+05:30`, … from `-12:00` to `+14:00`. An invalid value stops startup with a message naming the setting. |
| `Database:ResetOnStartup` | `false` (`true` in Development) | Delete and recreate the database on start |
| `Database:Seed` | `true` | Seed the SPECS §9 data when the database is empty |

Example: `Business__UtcOffset=+7 npm run dev:backend`.

## Design decisions (SPECS §13)

1. **Entity types and IDs.** IDs are `int` identity columns: simple, readable
   in URLs, and enough for one database. Enums are stored as strings.
   `Grant.Version` (`Guid`) is the concurrency token.
2. **Dates.** `ApplicationDate`, `DueDate` and `CompletedDate` are `DateOnly`,
   because every rule is in calendar days. The JSON format is `yyyy-MM-dd`.
   "Today" comes from `BusinessClock`. It converts `TimeProvider`'s instant
   using the configured **fixed UTC offset**. Business logic never reads a
   clock: domain methods take `today` as a parameter (BR-6).
3. **Completing an already-completed task** returns `200` with the task
   unchanged, including the original `CompletedDate`. Closing a closed grant
   also returns `200` unchanged. Both are safe to retry.
4. **Concurrency (OP-2):** optimistic concurrency with a grant-level version
   token. See [Concurrency](#concurrency-op-2).
5. **Idempotency (OP-3):** a required `Idempotency-Key` header on task
   creation, kept for 24 hours. See [Unreliable networks](#unreliable-networks-op-3).
6. **Error shape.** Every error is RFC 9457 `application/problem+json` with a
   stable `code` and a `traceId`. Validation errors add
   `errors: { field: [messages] }`. See the table below.
7. **Operational needs.** OP-2 and OP-3 are implemented. OP-4 and OP-5 are
   partly implemented. OP-1 and OP-6 are deferred. See
   [Before production](#before-production).

Other decisions:

- Business endpoints live under `/api` (e.g. `/api/grants`), a documented
  change from the exercise's routes, so the dev proxy forwards one prefix.
- UI scope follows the spec, not every mockup element. There is no grant
  creation, no task description, and no edit/delete/search/Reports/Settings.
- A refused grant close shows the **server's** reason
  ("…has 1 pending High-priority task"), not the mockup's "overdue" text.
- Overdue tasks on closed grants are listed, because BR-5 allows closing a
  grant that still has pending Low or Medium tasks.

The full decision log is in
[`docs/plans/completed/grant-task-tracker.md`](docs/plans/completed/grant-task-tracker.md).

## API

| Method | Route | Success | Errors |
| ------ | ----- | ------- | ------ |
| GET | `/api/grants?status=Open\|Closed` | 200 grants with `pendingTaskCount` | 400 unknown status |
| GET | `/api/grants/{id}` | 200 grant with tasks (each with server-computed `isOverdue`) | 404 |
| POST | `/api/grants/{id}/tasks` + `Idempotency-Key` header | 201 task (replays add `Idempotent-Replayed: true`) | 400, 404, 409, 422 |
| PUT | `/api/tasks/{id}/complete` | 200 task | 404, 409 |
| PUT | `/api/grants/{id}/close` | 200 grant | 404, 409 |
| GET | `/api/tasks/overdue` | 200, ordered `DueDate`, then `Id` (most overdue first), with `daysOverdue` | — |
| GET | `/health/live`, `/health/ready` | 200 | 503 when the database or schema is unavailable |

| Status | `code` | When |
| ------ | ------ | ---- |
| 400 | `VALIDATION_FAILED` | BR-2, BR-3, missing fields, malformed JSON, unknown enum values |
| 400 | `IDEMPOTENCY_KEY_MISSING` / `IDEMPOTENCY_KEY_INVALID` | No key, or a key over 100 characters |
| 404 | `GRANT_NOT_FOUND` / `TASK_NOT_FOUND` / `NOT_FOUND` | Unknown grant, task or route |
| 409 | `GRANT_CLOSED` | BR-1 |
| 409 | `GRANT_HAS_PENDING_HIGH_PRIORITY_TASKS` | BR-5 |
| 409 | `CONCURRENCY_CONFLICT` | Someone else changed the grant between this request's read and write |
| 422 | `IDEMPOTENCY_KEY_REUSED` | Same key, different body or grant |
| 5xx | `UNEXPECTED_ERROR` | Unhandled server error |

## Concurrency (OP-2)

BR-5 spans several rows: a grant must not be closed while any of its
High-priority tasks is pending. Checking one row's version would not
protect it. So **every change to a grant or any of its tasks goes through a
`Grant` method that assigns a new `Grant.Version`**. That covers adding a
task, completing a task and closing a grant.

The task change and the version update are saved in one transaction as
`UPDATE Grants … WHERE Id = @id AND Version = @old`. If another officer
changed the grant after this request read it, nothing is updated, the
transaction rolls back, and the API returns `409 CONCURRENCY_CONFLICT`.

- Closing a grant and adding a High task both update the grant row, so
  whichever commits second fails. A closed grant can never gain a pending
  High task. The same holds for BR-1.
- If two completions of the same task race, the loser reloads once, sees the
  task already completed, and returns `200` with the original date.
- The UI shows any 409 with the server's message and refreshes the data.

Limitations:

- Writes to the same grant are serialised. One officer may occasionally get a
  409 and retry, which is acceptable for about 100 officers.
- SQLite allows one writer at a time for the whole database. Writers wait
  (busy timeout) rather than fail. WAL mode lets reads continue during writes.
- The guarantee depends on every write going through `Grant`'s methods. The
  entities have private setters, and a test checks that every write path
  changes `Version`.

## Unreliable networks (OP-3)

- **Task creation** requires an `Idempotency-Key`. The UI generates one per
  submission and reuses it for every retry of the same values: automatic
  retries after network errors, and the officer pressing Save again.
  - The server stores the key, a SHA-256 hash of the grant id and request
    body, and the created task, in the same transaction as the task.
  - A repeat returns the same task. A different body with the same key
    returns 422.
  - Rejected requests are not stored, so they can be retried.
  - If two identical requests race, the loser replays the winner's task.
  - Keys expire after **24 hours**. Expired records are purged at startup
    and before each new record is written.
- **Completion** is idempotent (BR-4), so the UI retries it on network errors.
- **Visible failures.** When the server cannot be reached (no response, a
  timeout, or a bare 502/503 from the proxy), a banner says *"Cannot reach the
  server. Check your connection and try again."* It has a Try again button
  and clears on the next successful request. Every other API error is shown:
  validation messages on their form fields, business-rule errors in the
  dialog or a toast, and load errors in an alert.

## Assumptions

- No authentication: every user is a trusted officer, and there is no audit
  of who did what.
- One business day definition (one UTC offset) for the whole deployment.
- Task titles are trimmed before validation and storage.
- Completing a task on a closed grant is allowed. The spec does not forbid
  it, and BR-5 allows closed grants to keep Low or Medium tasks.
- Client-side validation uses the browser's date for early feedback only.
  Near midnight the server's business date may differ, and the server's
  message is then shown on the field.

## Before production

| Need | Status | What remains |
| ---- | ------ | ------------ |
| **OP-1** Multiple instances and restarts | Deferred | Move to PostgreSQL or SQL Server (SQLite is a single-node file). The version token and idempotency table already work across instances. Run migrations as a deployment step instead of at startup, and turn off `ResetOnStartup`. Move the idempotency purge to a scheduled job. |
| **OP-4** Logging and diagnostics | Partial | Serilog writes structured JSON request logs, and every error response carries a `traceId`. Still needed: a central sink (Seq, ELK or Application Insights), OpenTelemetry tracing, retention policy, and alerts on 5xx and 409 rates. |
| **OP-5** Health checks | Partial | `/health/live` and `/health/ready` exist; readiness queries the schema. Still needed: wire them to orchestrator probes, and alert on them. |
| **OP-6** Metrics | Deferred | OpenTelemetry metrics (tasks created and completed per day, overdue count), and query timing on `/api/tasks/overdue`, backed by the existing `(CompletedDate, DueDate)` index. |
| Security | Not started | Authentication and authorisation (e.g. OIDC), HTTPS, CORS for a separately hosted UI, rate limiting. |
| Frontend | — | Code-split the bundle (one 680 kB minified / 216 kB gzipped chunk; Vite warns above 500 kB), add browser end-to-end tests, serve the UI behind the same origin as the API. |

## Repository layout

```
backend/   GrantTracker.slnx — Domain, Data, Api + Domain.Tests, Api.IntegrationTests
frontend/  Vite React app (src/app, api, features/grants, features/tasks, pages, test)
docs/      ARCHITECTURE.md, plans/ (execution plan + decision log), mockups/
AGENTS.md, CLAUDE.md, .claude/, .agents/   AI agent instructions and tooling
```

## AI agent harness

This repository uses an agent harness (`AGENTS.md`, `docs/WORKFLOW.md`). To
reinstall it:

```bash
curl -fsSL "https://raw.githubusercontent.com/hoangnb24/repository-harness/main/scripts/install-harness.sh?$(date +%s)" | bash -s -- --claude --yes
```

# Architecture — Grant Task Tracker

This document describes the proposed architecture for the Grant Task
Tracker defined in [`SPECS.md`](../SPECS.md). Choices that go beyond an
explicit requirement are *proposed design decisions*; the reasoning is
given so they can be challenged.

## 1. Guiding principles

- **Proportionate.** The exercise is small: three backend projects, one
  frontend app, no repository/unit-of-work wrappers over EF Core, no
  mediator, no CQRS.
- **Business rules are pure and testable.** BR-1 to BR-6 live in a
  domain project with no dependency on HTTP, EF Core, or the clock.
  "Today" is passed in as a `DateOnly`.
- **The server is the source of truth.** The client validates for
  usability; the server validates for correctness and computes
  `isOverdue` using its own `TimeProvider`.
- **One error shape.** Every non-2xx response is RFC 9457
  `ProblemDetails` with a stable machine-readable `code`.

## 2. Tech stack

### 2.1 Backend

| Concern              | Choice                                                    |
| -------------------- | --------------------------------------------------------- |
| Runtime              | .NET 10 (LTS)                                             |
| Language             | C#                                                        |
| Web framework        | ASP.NET Core Web API — Minimal APIs with route groups     |
| ORM                  | Entity Framework Core                                     |
| Database             | SQLite (`Microsoft.EntityFrameworkCore.Sqlite`)           |
| Request validation   | MiniValidation (DataAnnotations on request DTOs)          |
| Logging              | Serilog (`Serilog.AspNetCore`, console sink, JSON format) |
| API documentation    | `Microsoft.AspNetCore.OpenApi` + Swagger UI               |
| Error handling       | `AddProblemDetails()` + `IExceptionHandler`               |
| Dependency injection | Built-in ASP.NET Core DI                                  |
| Date/time            | `TimeProvider` (`FakeTimeProvider` in tests)              |
| Health checks        | ASP.NET Core Health Checks + EF Core DbContext check      |
| Unit tests           | NUnit                                                     |
| Integration tests    | NUnit + `WebApplicationFactory<Program>` + SQLite         |

*Why Minimal APIs:* MiniValidation is designed for them, the endpoint
count is small (6), and route groups keep them organised without
controller ceremony.

### 2.2 Frontend

| Concern            | Choice                                        |
| ------------------ | --------------------------------------------- |
| Framework          | React + TypeScript (strict mode)              |
| Build tool         | Vite                                          |
| UI components      | shadcn/ui                                     |
| Styling            | Tailwind CSS                                  |
| Server state       | TanStack Query                                |
| Client/UI state    | `useState` / `useContext`                     |
| Forms              | React Hook Form                               |
| Form validation    | Zod + `@hookform/resolvers`                   |
| Routing            | React Router                                  |
| HTTP client        | Axios                                         |
| Dates              | date-fns                                      |
| Icons              | lucide-react                                  |
| Tests              | Vitest + React Testing Library + jsdom        |
| HTTP mocking       | MSW (Mock Service Worker) — *proposed addition* |

*Why React Router for one screen:* the selected grant lives in the URL
(`/grants/:grantId`), so refresh, back/forward, and deep links work
without extra state code.

## 3. Repository layout

```
/
├── backend/
│   ├── GrantTracker.slnx
│   ├── Directory.Build.props          # nullable, warnings-as-errors, LangVersion
│   ├── Directory.Packages.props       # central NuGet version management
│   ├── src/
│   │   ├── GrantTracker.Domain/
│   │   ├── GrantTracker.Data/
│   │   └── GrantTracker.Api/
│   └── tests/
│       ├── GrantTracker.Domain.Tests/
│       └── GrantTracker.Api.IntegrationTests/
├── frontend/
│   ├── package.json                   # scripts: dev, build, test, lint
│   ├── index.html
│   ├── vite.config.ts                 # React plugin, `@/` alias, /api proxy → backend
│   ├── vitest.config.ts               # jsdom environment, setup file
│   ├── tsconfig.json                  # references app + node configs
│   ├── tsconfig.app.json              # strict mode, `@/*` path alias
│   ├── tsconfig.node.json             # for Vite/Vitest config files
│   ├── eslint.config.js
│   ├── components.json                # shadcn/ui config
│   ├── public/
│   └── src/
│       ├── main.tsx
│       ├── index.css                  # Tailwind entry + shadcn/ui theme variables
│       ├── app/                       # providers, router, query client
│       ├── api/                       # Axios client, error normalisation, DTO types
│       ├── features/
│       │   ├── grants/                # grant queries/mutations + components
│       │   └── tasks/                 # task queries/mutations, Zod schema + components
│       ├── pages/                     # GrantTasksPage (the single screen)
│       ├── components/
│       │   └── ui/                    # shadcn/ui generated components
│       ├── lib/                       # cn(), date helpers
│       └── test/                      # Vitest setup, MSW handlers, render helpers
├── docs/
│   └── ARCHITECTURE.md
├── global.json                        # pins the .NET SDK
├── package.json                       # root scripts: `npm test` runs everything
├── .editorconfig
├── .gitignore
├── AGENTS.md
├── AI_USAGE.md
├── README.md
├── SPECS.md
└── TESTING.md
```

The file-level structure inside each project is in §4 (backend) and §9
(frontend).

`npm test` at the root runs `dotnet test backend` and then
`npm --prefix frontend test -- --run`, which satisfies the "one command"
testing requirement.

## 4. Backend architecture

### 4.1 Projects and dependencies

```
GrantTracker.Api  ──►  GrantTracker.Data  ──►  GrantTracker.Domain
        │                                            ▲
        └────────────────────────────────────────────┘
```

| Project      | Responsibility                                                                  | Depends on     |
| ------------ | ------------------------------------------------------------------------------- | -------------- |
| `Domain`     | Entities, enums, business rules BR-1 to BR-6, domain errors                     | nothing        |
| `Data`       | `AppDbContext`, EF configurations, migrations, seeding, idempotency table       | Domain         |
| `Api`        | HTTP endpoints, DTOs, use-case services, transactions, idempotency, error mapping | Domain, Data |

This split maps directly onto the spec's "separate API, business logic,
and data access" requirement (SPECS §11):

- Domain rules can be unit-tested in milliseconds with no database.
- EF Core mapping and migrations are isolated from HTTP concerns.
- Use-case services sit in the API project, organised by feature. A
  separate Application project was considered and rejected: there is
  one host and six use cases, so it would add a project without adding
  a boundary that anything relies on.
- Services use `AppDbContext` directly. `DbContext` is already a unit of
  work and a repository, so a repository layer would add only
  indirection.

### 4.2 `GrantTracker.Domain`

```
GrantTracker.Domain/
├── Grants/
│   ├── Grant.cs                 # AddTask(), Close(), Touch()
│   └── GrantStatus.cs           # Open, Closed
├── Tasks/
│   ├── GrantTask.cs             # Complete(), IsOverdue(today)
│   └── TaskPriority.cs          # Low, Medium, High
└── Common/
    ├── DomainException.cs       # Code, Kind (Validation | Conflict), Field?
    └── ErrorCodes.cs            # stable string constants
```

- The entity is named `GrantTask` to avoid clashing with
  `System.Threading.Tasks.Task`. The API and UI still call it "task".
- Rule placement:

  | Rule | Enforced in                               | Failure                  |
  | ---- | ----------------------------------------- | ------------------------ |
  | BR-1 | `Grant.AddTask` — grant must be `Open`    | Conflict `GRANT_CLOSED`  |
  | BR-2 | `Grant.AddTask(…, today)`                 | Validation on `dueDate`  |
  | BR-3 | `Grant.AddTask` — trimmed, 1–100 chars    | Validation on `title`    |
  | BR-4 | `GrantTask.Complete(today)` — no-op if already completed | — (idempotent) |
  | BR-5 | `Grant.Close()` — no pending `High` tasks | Conflict `GRANT_HAS_PENDING_HIGH_PRIORITY_TASKS` |
  | BR-6 | `GrantTask.IsOverdue(today)` — `CompletedDate is null && DueDate < today` | — |

- Domain methods take `DateOnly today` as a parameter and never read a
  clock, which satisfies BR-6's testability requirement by
  construction.
- Rule violations throw `DomainException`. Exceptions were chosen over
  a `Result<T>` type because there is a single global handler that maps
  them, and it keeps service code linear. If the domain grows, a
  `Result` type would be the next step.

### 4.3 `GrantTracker.Data`

```
GrantTracker.Data/
├── AppDbContext.cs
├── Configurations/
│   ├── GrantConfiguration.cs           # Version as concurrency token, unique ReferenceNumber
│   ├── GrantTaskConfiguration.cs       # Title max 100, enum as string, index (CompletedDate, DueDate)
│   └── IdempotencyRecordConfiguration.cs
├── Idempotency/
│   └── IdempotencyRecord.cs
├── Migrations/
├── Seeding/
│   └── DatabaseSeeder.cs               # uses TimeProvider; dates relative to startup
└── DataServiceCollectionExtensions.cs  # AddData(connectionString)
```

- Enums are stored as strings, so the database stays readable.
- `ApplicationDate`, `DueDate`, and `CompletedDate` are `DateOnly`.
  SQLite stores them as `yyyy-MM-dd` text, which sorts correctly.
- Cascade delete from `Grant` to `GrantTask` enforces "a task cannot
  exist without its grant".
- Migrations are applied at startup. With `Database:ResetOnStartup=true`
  (the default in Development), the database is recreated and reseeded
  on every start, so seed dates always stay relative to the current day
  (SPECS §6.1). With it set to `false`, the seeder runs only when the
  database is empty.

### 4.4 `GrantTracker.Api`

```
GrantTracker.Api/
├── Program.cs                          # composition root and pipeline
├── Features/
│   ├── Grants/
│   │   ├── GrantEndpoints.cs           # MapGrantEndpoints(this RouteGroupBuilder)
│   │   ├── GrantService.cs             # List, Get, Close
│   │   └── GrantContracts.cs           # GrantSummaryDto, GrantDetailDto
│   └── Tasks/
│       ├── TaskEndpoints.cs
│       ├── TaskService.cs              # Add, Complete, ListOverdue
│       └── TaskContracts.cs            # CreateTaskRequest, TaskDto, OverdueTaskDto
├── Common/
│   ├── Errors/
│   │   ├── DomainExceptionHandler.cs   # IExceptionHandler → ProblemDetails
│   │   └── ConcurrencyExceptionHandler.cs
│   ├── Validation/
│   │   └── ValidationFilter.cs         # endpoint filter using MiniValidator
│   ├── Idempotency/
│   │   └── IdempotencyService.cs
│   └── Time/
│       └── BusinessClock.cs            # Today => DateOnly from TimeProvider
├── appsettings.json
└── appsettings.Development.json
```

Services are registered as scoped. `BusinessClock` is the only class
that reads `TimeProvider`. It converts "now" to a business `DateOnly`
using a configured time zone (`Business:TimeZone`), so "today" does not
depend on the server's locale.

### 4.5 Request pipeline

```
Serilog request logging
  → Exception handler (ProblemDetails)
  → CORS (Development only; Vite proxy is the default)
  → /api route group
      → ValidationFilter (MiniValidation → 400 ValidationProblem)
      → Endpoint → Service → Domain + AppDbContext
  → /health/live, /health/ready
  → /openapi/v1.json, /swagger (Development)
```

## 5. API design

All business endpoints sit under `/api`. This is a documented change
from the exercise's routes: it lets the Vite dev proxy forward one
prefix and keeps health/OpenAPI routes separate.

| Method | Route                       | Success                    | Errors                         |
| ------ | --------------------------- | -------------------------- | ------------------------------ |
| GET    | `/api/grants?status=Open`   | 200 `GrantSummaryDto[]`    | 400 invalid status             |
| GET    | `/api/grants/{id}`          | 200 `GrantDetailDto`       | 404                            |
| POST   | `/api/grants/{id}/tasks`    | 201 `TaskDto`              | 400, 404, 409, 422             |
| PUT    | `/api/tasks/{id}/complete`  | 200 `TaskDto`              | 404, 409                       |
| PUT    | `/api/grants/{id}/close`    | 200 `GrantDetailDto`       | 404, 409                       |
| GET    | `/api/tasks/overdue`        | 200 `OverdueTaskDto[]`     | —                              |
| GET    | `/health/live`              | 200                        | —                              |
| GET    | `/health/ready`             | 200 (DB reachable)         | 503                            |

- `TaskDto` includes a server-computed `isOverdue`. `OverdueTaskDto`
  adds `grantId`, `grantReferenceNumber`, and `daysOverdue`.
- Overdue ordering is `DueDate ASC, Id ASC`: the oldest due date is the
  most overdue, and `Id` makes ties deterministic.
- **Completing an already-completed task** returns `200` with the
  task unchanged, including the original `CompletedDate` (BR-4, OP-3).
- **Closing an already-closed grant** returns `200` unchanged, for the
  same retry-safety reason.

### 5.1 Error contract

Every error is `application/problem+json`:

```json
{
  "type": "https://granttracker/errors/grant-has-pending-high-priority-tasks",
  "title": "Grant cannot be closed",
  "status": 409,
  "detail": "Grant GR-100001 has 1 pending High-priority task.",
  "code": "GRANT_HAS_PENDING_HIGH_PRIORITY_TASKS",
  "traceId": "00-…"
}
```

Validation errors use `ValidationProblemDetails`, which adds
`"errors": { "title": ["…"], "dueDate": ["…"] }`, so the frontend can
attach each message to its form field.

| Code                                     | Status | Source                     |
| ---------------------------------------- | ------ | -------------------------- |
| `VALIDATION_FAILED`                      | 400    | BR-2, BR-3, malformed input |
| `IDEMPOTENCY_KEY_MISSING`                | 400    | OP-3                       |
| `GRANT_NOT_FOUND` / `TASK_NOT_FOUND`     | 404    |                            |
| `GRANT_CLOSED`                           | 409    | BR-1                       |
| `GRANT_HAS_PENDING_HIGH_PRIORITY_TASKS`  | 409    | BR-5                       |
| `CONCURRENCY_CONFLICT`                   | 409    | OP-2                       |
| `IDEMPOTENCY_KEY_REUSED`                 | 422    | OP-3 (same key, different body) |

## 6. Concurrency strategy (OP-2)

**Decision: optimistic concurrency with a grant-level version token.**

- `Grant` has a `Version` (`Guid`) column configured with
  `IsConcurrencyToken()`.
- **Every write that touches a grant or its tasks calls
  `grant.Touch()`**, which assigns a new `Version`. This covers adding a
  task, completing a task, and closing a grant.
- The task insert or update and the grant's version update are saved in
  one `SaveChanges` call, which is a single transaction. EF issues
  `UPDATE Grants SET Version = @new WHERE Id = @id AND Version = @old`.
  If another request changed the grant first, zero rows are affected,
  EF throws `DbUpdateConcurrencyException`, and the whole transaction
  rolls back.
- The API maps that exception to `409 CONCURRENCY_CONFLICT`. The
  frontend shows "This grant was changed by someone else", refetches,
  and lets the officer retry.

**Why this protects BR-5 and not just a single row.** Both sides of the
race (closing the grant, and adding a High task) must update the same
grant row. Whichever commits second sees a stale version and fails, so
it is impossible to commit a Closed grant with a pending High task.
Touching the grant on every task write also protects BR-1: a task can't
be added to a grant that was closed concurrently. It also prevents lost
updates when two officers complete the same task.

**Limitations to state in the README:**

- Writes to the same grant are effectively serialised, which is
  acceptable for 100 officers.
- SQLite allows one writer at a time across the whole database.
  Microsoft.Data.Sqlite retries on `SQLITE_BUSY` until the command
  timeout. WAL mode is enabled so reads don't block writes.
- The invariant holds only if every write path calls `Touch()`. An
  integration test per write path guards against regressions.

**Tests:**

- A deterministic test opens two `AppDbContext` instances on the same
  SQLite database and loads the grant in both. It closes the grant in
  one, adds a High task in the other, and asserts that the second save
  throws and the database still satisfies BR-5.
- An API-level test fires both requests in parallel and asserts that
  exactly one succeeds and the other returns 409 or a BR-5 conflict.

## 7. Idempotent task creation (OP-3)

**Decision: a required `Idempotency-Key` header on
`POST /api/grants/{id}/tasks`.**

1. The client generates a UUID (`crypto.randomUUID()`) for each distinct
   submission and reuses it for every retry of that submission.
2. The server computes a SHA-256 hash of `grantId` plus the
   canonicalised request body.
3. It then looks the key up in `IdempotencyRecords`:
   - **Found, same hash:** replay the stored `201` response and add an
     `Idempotent-Replayed: true` header. No new task is created.
   - **Found, different hash:** return `422 IDEMPOTENCY_KEY_REUSED`.
   - **Not found:** run the use case, then insert the task and the
     idempotency record (key, hash, status, response JSON, `CreatedAt`)
     in the same `SaveChanges`.
4. If two identical requests race, the record's primary key makes the
   second insert fail. The server catches that, reloads the record, and
   replays it.
5. Only successful responses are stored. A rejected request can be
   corrected and retried with the same key only if the body is
   unchanged; otherwise the client generates a new key.
6. **Retention is 24 hours.** Expired records are deleted at startup and
   opportunistically when new records are written. In production this
   would move to a scheduled job.

## 8. Date and time handling

- `TimeProvider.System` is registered in production code;
  `FakeTimeProvider` (from `Microsoft.Extensions.TimeProvider.Testing`)
  replaces it in tests.
- `BusinessClock.Today` is the only conversion from an instant to a
  calendar date. Services pass `today` into domain methods.
- The JSON API exchanges dates as `yyyy-MM-dd` strings.
- `CompletedDate` is a `DateOnly`, because the spec describes it as a
  date and BR-4/BR-6 reason in calendar days. If an audit timestamp is
  needed later, add `CompletedAt` (`DateTimeOffset`) alongside it.

## 9. Frontend architecture

```
frontend/
├── index.html
├── vite.config.ts                  # /api proxy → backend in dev
├── vitest.config.ts
├── components.json                 # shadcn/ui config
├── tsconfig.json
└── src/
    ├── main.tsx
    ├── app/
    │   ├── App.tsx
    │   ├── providers.tsx           # QueryClientProvider, RouterProvider
    │   ├── router.tsx              # "/" → first grant, "/grants/:grantId"
    │   └── queryClient.ts          # global error handling, retry policy
    ├── api/
    │   ├── client.ts               # Axios instance (baseURL /api, timeout)
    │   ├── errors.ts               # toApiError(): normalise Axios errors
    │   └── types.ts                # DTO types mirroring the API
    ├── features/
    │   ├── grants/
    │   │   ├── api.ts              # useGrants, useGrant, useCloseGrant, query keys
    │   │   └── components/
    │   │       ├── GrantSelector.tsx
    │   │       └── GrantHeader.tsx # status, close button
    │   └── tasks/
    │       ├── api.ts              # useAddTask, useCompleteTask
    │       ├── schema.ts           # Zod schema for Add Task
    │       ├── useIdempotencyKey.ts
    │       └── components/
    │           ├── TaskList.tsx
    │           ├── TaskRow.tsx     # overdue highlight, Complete button
    │           └── AddTaskForm.tsx
    ├── pages/
    │   └── GrantTasksPage.tsx      # the single screen
    ├── components/
    │   ├── ui/                     # shadcn/ui generated components
    │   ├── ApiErrorAlert.tsx
    │   └── ConnectionBanner.tsx
    ├── lib/
    │   ├── utils.ts                # cn()
    │   └── dates.ts                # date-fns helpers
    └── test/
        ├── setup.ts                # RTL + jest-dom + MSW server
        ├── handlers.ts             # MSW handlers
        └── renderWithProviders.tsx
```

Tests sit next to the code they cover (for example,
`AddTaskForm.test.tsx`).

### 9.1 Error handling

`api/errors.ts` turns every Axios failure into one discriminated union:

```ts
type ApiError =
  | { kind: "network" }                                     // no response / timeout
  | { kind: "validation"; fieldErrors: Record<string, string[]> }
  | { kind: "business"; status: number; code: string; detail: string }
  | { kind: "notFound"; detail: string }
  | { kind: "unexpected"; status?: number };
```

- **`network`:** `ConnectionBanner` shows "Cannot reach the server.
  Check your connection and try again." It is driven by TanStack Query's
  global `QueryCache` / `MutationCache` `onError`.
- **`validation`:** the error is mapped onto React Hook Form fields with
  `setError`.
- **`business` / `notFound` / `unexpected`:** `ApiErrorAlert` shows the
  server's `detail`. No error is swallowed.

### 9.2 Retries

- Queries retry twice, but only on network errors.
- Complete Task retries on network errors, which is safe because the
  endpoint is idempotent.
- Add Task retries on network errors with the same `Idempotency-Key`.
  `useIdempotencyKey` keeps a key until the submission succeeds or the
  form values change.
- After every mutation, `grant` and `grants` queries are invalidated so
  pending counts and the task list refresh.

### 9.3 Client-side validation

The Zod schema mirrors BR-2 and BR-3: a trimmed title of 1–100
characters, a due date no earlier than today, and a due date no earlier
than the grant's application date. It gives early feedback only; the
server stays authoritative because the client's clock may be wrong.

## 10. Testing architecture

| Level                     | Project / location                   | Covers                                                             |
| ------------------------- | ------------------------------------ | ------------------------------------------------------------------ |
| Domain unit               | `GrantTracker.Domain.Tests`          | BR-1 to BR-6 with fixed `today`; BR-3 boundary titles (0, whitespace, 100, 101) |
| API + database integration | `GrantTracker.Api.IntegrationTests` | Status codes, ProblemDetails shape, overdue ordering, BR-4 replay, idempotency replay/mismatch, concurrency (§6), seeding |
| Frontend component        | `frontend/src/**/*.test.tsx`         | Form validation, overdue highlighting, API error display, connection banner |

- Integration tests use `WebApplicationFactory<Program>` with a fresh
  SQLite database per test: a temporary file, deleted afterwards, so
  multiple connections can share it for concurrency tests.
  `FakeTimeProvider` is injected, so tests never depend on the real
  date or on execution order.
- The EF Core InMemory provider is **not** used, because it does not
  model transactions or concurrency (SPECS §11).

## 11. Operational needs

| Need | Status in this design | Notes |
| ---- | --------------------- | ----- |
| OP-2 Concurrency | **Implemented** | §6 |
| OP-3 Unreliable network | **Implemented** | §7, §9.1, §9.2 |
| OP-4 Logging | Partially | Serilog structured request logs plus `traceId` in ProblemDetails. Before production: central sink (Seq/ELK/App Insights), correlation across services, log retention policy. |
| OP-5 Health checks | Partially | `/health/live` and `/health/ready` with a DbContext check. Before production: wire to the orchestrator's probes and add alerting. |
| OP-1 Multiple instances | Deferred | Move to a server database (PostgreSQL / SQL Server). The concurrency token and idempotency table already work across instances. Remove reset-on-startup and run migrations as a deployment step. |
| OP-6 Metrics | Deferred | OpenTelemetry metrics (tasks created/completed per day, overdue count); query timing on `/tasks/overdue`, backed by the `(CompletedDate, DueDate)` index. |

# Execution Plan: Grant Task Tracker

Date: 2026-11-08

## Status

Active

## Outcome

A runnable Grant Task Tracker that meets [SPECS.md](../../../SPECS.md):

- An ASP.NET Core + SQLite API that enforces BR-1 to BR-6, OP-2
  (concurrency) and OP-3 (safe retries).
- A single React screen for working on a grant's tasks.
- One command that runs every test.
- The `README.md`, `TESTING.md` and `AI_USAGE.md` the submission requires.

## Context

- Requirements: [SPECS.md](../../../SPECS.md).
- Architecture: [docs/ARCHITECTURE.md](../../ARCHITECTURE.md) covers the stack,
  layout, error contract, concurrency and idempotency. It already includes the
  decisions below.
- UI reference: [docs/mockups/](../../mockups/). The mockups guide layout
  only. D1–D3 below limit what gets built.
- Dev machine on 2026-11-08: .NET SDK 10.0.102 is installed. Node.js is not.

## Scope

In scope:

- Six business endpoints under `/api`, plus health checks and OpenAPI.
- BR-1 to BR-6, with unit and integration tests.
- OP-2 and OP-3 implemented. OP-4 and OP-5 partly (Serilog, health checks).
  OP-1 and OP-6 documented only.
- One frontend screen: a grant list with All/Open/Closed tabs; a grant header
  with summary cards and a Close Grant button; a task table with overdue
  highlighting and a Complete button; an Add Task dialog; toasts; and a
  connection banner.
- A configurable business UTC offset (D6).
- `README.md`, `TESTING.md`, `AI_USAGE.md`.

Out of scope (D1–D3):

- Creating grants (the mockup's Add Grant button and dialog).
- A task description field.
- Editing, deleting or viewing a single task; the Grant Details, Documents
  and Activity tabs; search boxes; the task status filter; Reports and
  Settings navigation; the notification bell; the user menu; authentication.

## Approach

Build the backend from the inside out: domain, then data, then API, then the
operational work. Write tests first at each layer. The frontend can start once
it is scaffolded. It works against MSW mocks of the documented API contract
until the real endpoints exist.

Each task in Progress is about one commit. SPECS §12 forbids squashing the
history.

- Critical path: 0.2 → 0.3 → Phase 1 → 2.1 → Phase 3 → Phase 4 → 5.1 → 5.2 →
  Phase 7.
- In parallel: 0.4 → Phase 6. This needs Node.js.

### Business date (D6)

- Setting: `Business:UtcOffset` (environment variable `Business__UtcOffset`).
- Formats: `±H`, `±HH` or `±HH:mm`, for example `+1`, `+7`, `-8` or `+05:30`.
  Range `-12:00` to `+14:00`. Default `+00:00`.
- The value is checked at startup (`ValidateOnStart`). An invalid value stops
  the app with a message naming the setting.
- `BusinessClock.Today` converts `TimeProvider.GetUtcNow()` to the offset and
  takes the date.
- A fixed offset ignores daylight saving. The README must say so.
- The seeder gets `today` from `BusinessClock`, so seed dates and overdue
  checks use the same calendar.

## Risks And Recovery

- **Node.js is missing.** Phase 6 and the root `npm test` are blocked until a
  current Node LTS is installed.
- **A write path forgets `Touch()`, so the BR-5 race reappears.** Only `Grant`
  methods can make changes, and they call `Touch()` themselves. Test 5.1(c)
  checks this.
- **Two completions of the same task race, and one gets a 409 for an action
  that succeeded.** Completion reloads and retries once after a concurrency
  conflict (G1).
- **Two identical POSTs race and fail on the grant's `Version` before the
  idempotency key check.** Handle both failures, look up the key and replay
  (G2).
- **The client and server disagree about "today".** The browser's date and
  the configured offset can differ by up to a day around midnight. Client
  validation is only a hint; the server's 400 is shown on the field.
- **SQLite allows one writer at a time.** That is acceptable for 100 officers
  and must be documented. OP-1 needs a server database.
- **Recovery:** every task is its own commit, so revert that commit. The
  development database can be thrown away (`Database:ResetOnStartup=true` in
  Development); delete the `.db` file to reset it.

## Progress

### Phase 0 – Setup

- [x] **0.1** Decide D1–D6 and record them in this plan.
- [x] **0.2** Root files: `global.json` (SDK 10.0.102, `rollForward:
  latestFeature`), `.editorconfig`, `.gitignore` (`bin/`, `obj/`,
  `node_modules/`, `dist/`, `*.db*`), root `package.json`.
  *Done when:* `dotnet --version` in the repo reports 10.0.x.
- [x] **0.3** Backend skeleton: `GrantTracker.slnx`, `Directory.Build.props`
  (nullable, warnings as errors), `Directory.Packages.props`, three source
  projects and two test projects referencing each other as in Architecture
  §4.1, and `dotnet-ef` as a local tool.
  *Done when:* `dotnet build backend` and `dotnet test backend` pass.
- [x] **0.4** Frontend skeleton: Vite React-TS (strict), Tailwind, shadcn/ui,
  `@/` alias, `/api` proxy, Vitest + RTL + jsdom + MSW, ESLint.
  *Done when:* build, lint and a smoke test pass.

### Phase 1 – Domain (test-first, no database)

- [x] **1.1** `Grant`, `GrantTask`, the enums, `DomainException` and
  `ErrorCodes`. `Grant` is the only way to change a grant or its tasks: its
  `AddTask`, `CompleteTask` and `Close` methods call a private `Touch()`.
  *Done when:* it compiles and the constructor tests pass.
- [x] **1.2** BR-1, BR-2 and BR-3 in `Grant.AddTask(title, dueDate, priority,
  today)`.
  *Done when:* tests cover a closed grant; a past due date; a due date before
  the application date; a due date of today (allowed); and titles that are
  null, empty, whitespace, 100 characters (allowed), 101 characters, or need
  trimming.
- [x] **1.3** BR-4 in `Grant.CompleteTask(taskId, today)`.
  *Done when:* the first call sets `CompletedDate` to `today`; a second call
  with a later `today` keeps the original date and leaves `Version` unchanged.
- [x] **1.4** BR-5 in `Grant.Close()`.
  *Done when:* a pending High task blocks closing with
  `GRANT_HAS_PENDING_HIGH_PRIORITY_TASKS`; completed High tasks and pending
  Low or Medium tasks do not block it; closing a closed grant does nothing.
- [x] **1.5** BR-6 in `GrantTask.IsOverdue(today)`.
  *Done when:* due yesterday → overdue; due today → not overdue; completed
  with a past due date → not overdue.

### Phase 2 – Data

- [x] **2.1** `AppDbContext` and its configurations: `Version` as a
  concurrency token, unique `ReferenceNumber`, `Title` up to 100 characters,
  enums stored as strings, `DateOnly` dates, cascade delete, an index on
  `(CompletedDate, DueDate)`. Add `IdempotencyRecord`, the first migration and
  WAL mode.
  *Done when:* the migration applies to an empty SQLite file.
- [x] **2.2** `DatabaseSeeder(context, today)` and `Database:ResetOnStartup`.
  *Done when:* a test with a fixed `today` checks the 4 grants, the 6 tasks,
  their relative dates, and each case SPECS §9 asks the seed data to show.

### Phase 3 – API foundation

- [x] **3.1** `Program.cs`: Serilog, ProblemDetails and the exception
  handlers, `TimeProvider`, `BusinessClock` with `Business:UtcOffset`, JSON
  settings (enums as strings, `yyyy-MM-dd` dates), OpenAPI and Swagger UI, the
  `/api` group with `ValidationFilter`, `/health/live` and `/health/ready`.
  *Done when:* the app starts and `/swagger` and both health endpoints respond.
- [x] **3.2** Test fixture: `WebApplicationFactory<Program>`, a temporary
  SQLite file per test, `FakeTimeProvider`, and an offset each test can set.
  *Done when:* a health-check test and an error-format test pass.
- [x] **3.3** Business date tests (D6).
  *Done when:*
  - At 2026-11-08T20:00Z, `+1` and `-8` give 2026-11-08 and `+7` gives
    2026-11-09.
  - At 23:30Z, `+1` gives 2026-11-09.
  - At 05:00Z, `-8` gives 2026-11-07.
  - `+15`, `abc` and `+7:5` stop the app at startup.

### Phase 4 – Endpoints (each with integration tests)

- [x] **4.1** `GET /api/grants?status=` and `GET /api/grants/{id}`.
  *Done when:* pending counts are right; `isOverdue` is set; a bad status
  returns 400; an unknown grant returns 404; at 2026-11-08T20:00Z a task due
  2026-11-08 is overdue with `+7` and not with `-8`.
- [x] **4.2** `POST /api/grants/{id}/tasks`.
  *Done when:* it returns 201 with the task (no `Location`; see Decisions); 400 with field errors
  for BR-2 and BR-3; 404; 409 `GRANT_CLOSED`.
- [x] **4.3** `PUT /api/tasks/{id}/complete`.
  *Done when:* it returns 200; repeating it after moving the clock forward
  keeps the original date; an unknown task returns 404.
- [x] **4.4** `PUT /api/grants/{id}/close`.
  *Done when:* it returns 200; 409 under BR-5; 200 for a grant that is already
  closed; 404.
- [x] **4.5** `GET /api/tasks/overdue`.
  *Done when:* results are sorted by `DueDate` then `Id`; `daysOverdue` is
  set; completed tasks and tasks due today are left out; tasks on closed
  grants are included (G4).

### Phase 5 – Concurrency (OP-2) and safe retries (OP-3)

- [x] **5.1** OP-2: `DbUpdateConcurrencyException` maps to 409
  `CONCURRENCY_CONFLICT`. Completion reloads and retries once after a
  conflict (G1).
  *Done when:*
  - (a) A test with two `DbContext`s races closing a grant against adding a
    High task: the second save fails and BR-5 still holds.
  - (b) API tests of the same race, made deterministic with an EF Core
    interceptor that sends the competing request between read and write,
    plus a parallel smoke test: exactly one request succeeds.
  - (c) Every write path changes `Version`.
  - (d) Two simultaneous completions of one task both return 200 with the
    same `CompletedDate`.
- [x] **5.2** OP-3: `Idempotency-Key` on task creation.
  *Done when:*
  - A missing key returns 400.
  - A repeat replays the stored response with `Idempotent-Replayed: true` and
    creates no task.
  - The same key with a different body returns 422.
  - After 25 hours the key counts as new.
  - Parallel identical POSTs create exactly one task (G2).

### Phase 6 – Frontend

Can start after 0.4, against MSW mocks.

- [x] **6.1** `api/client.ts`, `api/types.ts`, `toApiError()`.
  *Done when:* there is a unit test for each error kind: network, timeout,
  400, 404, 409 and 500.
- [x] **6.2** App shell: providers; a router where `/` opens the first grant
  and `/grants/:grantId` opens a given grant; a query client that retries
  network errors only and has a global `onError`; `ConnectionBanner`; toasts.
  *Done when:* a network failure shows the banner.
- [x] **6.3** Grant list with status tabs and counts (D5); `GrantHeader` with
  summary cards and a Close Grant button.
  *Done when:* a BR-5 409 shows the server's `detail` (D4).
- [x] **6.4** `TaskList` and `TaskRow`: priority and status badges, overdue
  highlighting from the server's `isOverdue`, and a Complete button on pending
  tasks only.
  *Done when:* an overdue row is highlighted; completed rows have no Complete
  button; completing shows a success toast.
- [x] **6.5** `AddTaskForm` dialog: React Hook Form and a Zod schema that
  mirrors BR-2 and BR-3; `useIdempotencyKey`; server field errors applied with
  `setError`; business errors in an alert; disabled on closed grants.
  *Done when:* client validation works; a server 400 lands on the right
  fields; a retry reuses the same key.

### Phase 7 – Wrap-up

- [x] **7.1** Root `npm test` runs `dotnet test backend` and then the frontend
  tests.
  *Done when:* it passes from a fresh clone.
- [ ] **7.2** *(Partly done 2026-11-09: API + Vite run; every endpoint, the BR-5 refusal, idempotent replay and the 502-when-API-down case checked through the proxy. Visual click-through in a browser still to do by the owner.)* Manual end-to-end check against the seeded database: every
  in-scope mockup flow, plus the banner when the server is stopped.
  *Done when:* each flow is seen working in a browser.
- [x] **7.3** `TESTING.md`, including a planted bug. For example, change `<`
  to `<=` in `IsOverdue`, or remove `Touch()` from `AddTask`, and record which
  test fails.
  *Done when:* the failing output is recorded and the bug is reverted.
- [x] **7.4** `README.md`: setup, run and test steps; answers to the seven
  open decisions in SPECS §13; the status-code table; how concurrency works
  and its limits; the UTC offset setting and its daylight-saving limit; how
  OP-1, OP-4, OP-5 and OP-6 would be finished before production. Keep the
  existing Harness install section.
- [ ] **7.5** *(Drafted; owner sections marked "To be completed by the author".)* `AI_USAGE.md`. The owner must supply their own contributions and
  the AI-mistake example. Use the log at the end of this plan for candidates.
- [ ] **7.6** *(Text files scanned: no personal details. The mockup image still needs redacting by the owner; the original stays in git history unless history is rewritten.)* Remove secrets and personal details. The mockup
  `grant-tracker-add-grant.jpeg` shows a real name in the user menu and must
  be redacted or replaced. Then move this plan to `docs/plans/completed/`.

## Decisions

All dated 2026-11-08 and agreed with the repository owner.

- **D1:** Grants cannot be created. The mockup's Add Grant has no requirement
  or endpoint behind it.
- **D2:** Tasks have no description field. It is not in the Task model (SPECS
  §3.2).
- **D3:** The other mockup extras listed under Out of scope are not built.
- **D4:** When closing a grant fails, the UI shows the server's `detail`,
  which uses the BR-5 wording. The mockup's "overdue task" text is not used.
- **D5:** The grant selector is the mockup's list panel with All/Open/Closed
  tabs. The counts come from one unfiltered `GET /api/grants`. The API's
  `?status=` filter is still built and tested.
- **D6:** "Today" uses a configurable fixed UTC offset, `Business:UtcOffset`
  (for example `+1`, `+7`, `-8`), defaulting to `+00:00`. This replaces the
  IANA `Business:TimeZone` setting that the architecture first proposed.
- **G1:** Completing a task retries once after a concurrency conflict, so two
  simultaneous completions both return 200.
- **G2:** When identical idempotent POSTs race, both a duplicate-key failure
  and a grant-version conflict are handled; the server then looks up the key
  and replays.
- **G3:** Every change goes through a `Grant` method, which calls `Touch()`
  itself. This replaces `GrantTask.Complete` as the public way to complete a
  task.
- **G4:** The overdue list includes tasks on closed grants, because the spec
  says "across all grants" and BR-5 allows closing a grant with pending Low or
  Medium tasks.
- **Seeder date:** the seeder gets `today` from `BusinessClock` instead of
  reading `TimeProvider` itself.

Decided during implementation (2026-11-08 to 2026-11-09):

- **IDs** are integers (SPECS §13 decision 1).
- **`Grant.CompleteTask(task, today)`** takes the task instead of its id:
  new tasks all have id 0 until saved, and the seeder completes tasks
  before saving.
- **Seed data** is built through the domain methods, with tasks added
  "as of" the grant's application date, so the seed obeys BR-1 to BR-5.
- **Idempotency records** store a reference to the created task, not a
  JSON snapshot. Replays return the task's current state. This keeps
  task and record in one `SaveChanges` and made the race testable.
- **No `Location` header** on 201: there is no single-task endpoint.
- **No CORS policy**: the Vite proxy is the only development path.
- **`IDEMPOTENCY_KEY_INVALID`** (400) for keys over 100 characters, and
  generic `NOT_FOUND` / `UNEXPECTED_ERROR` codes so every error has a code.
- **Frontend (2026-11-09):** oxlint (Vite template default) instead of
  ESLint; a native `<select>` for priority (Radix Select is unreliable in
  jsdom); `next-themes` dropped from the generated toaster (no dark mode);
  a 5xx without ProblemDetails counts as "server unreachable", because the
  Vite proxy returns a bare 502 when the API is down; one 680 kB bundle is
  accepted for a single internal screen.
- **Race tests** use a test-only `SaveChangesInterceptor` registered with
  EF Core's `ConfigureDbContext`, so no production hook is needed.

D6, G1–G4 and the seeder date are now part of
[docs/ARCHITECTURE.md](../../ARCHITECTURE.md).

### AI usage log (for AI_USAGE.md)

Record notable AI mistakes, corrections and owner contributions here as they
happen.

- 2026-11-08, AI mistake: while proving the concurrency tests catch
  regressions, the agent mutated `TaskService.cs`, ran the tests, then
  restored it with `git checkout`. The file had uncommitted work (the G1
  retry), so the restore silently deleted it. Caught by checking
  `git status` afterwards; the retry was re-applied and the full suite
  re-run. Correction: commit before mutation checks, then restore with
  `git checkout`.
- 2026-11-08, AI mistake: the first draft of the BR-1 test added an
  `internal CloseForTest()` method to production code because `Close()`
  did not exist yet. Replaced by moving the test to the BR-5 commit.
- 2026-11-09, AI-written test passed for the wrong reason: sonner's global
  toast store leaked a "Task completed" toast into the next test, so the
  network-retry test passed with no retry. Fixed by clearing toasts after
  each test; proven by removing the retry and watching the test fail.
- 2026-11-09, a selector test held a DOM element from before a redirect
  re-rendered the page; it now waits for the redirected page.
- 2026-11-08, design catch: completing tasks by id inside the domain
  would not work for unsaved tasks (all id 0); changed to pass the task.

## Validation

- Focused proof: `dotnet test backend/tests/GrantTracker.Domain.Tests`;
  `npm --prefix frontend test -- --run`.
- Integration and end-to-end proof:
  `dotnet test backend/tests/GrantTracker.Api.IntegrationTests`; the manual
  browser run in 7.2.
- Repository-required checks: root `npm test` from a fresh clone;
  `dotnet build backend` with warnings as errors;
  `npm --prefix frontend run lint` and `npm --prefix frontend run build`.

## Result

Pending.

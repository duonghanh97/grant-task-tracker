# Testing Strategy

All tests run with one command from the repository root:

```bash
npm run setup   # once: restores .NET tools/packages and installs frontend packages
npm test        # backend (NUnit) then frontend (Vitest)
```

Current result: **181 tests pass**. That is 36 domain, 100 API/database
integration and 45 frontend tests. They pass from a fresh clone and under
`TZ=Pacific/Kiritimati` (UTC+14) and `TZ=America/Los_Angeles` (UTC−8).

## 1. What is tested at each level, and why

| Level | Location | Tests | What it proves |
| ----- | -------- | ----- | -------------- |
| Domain unit | `backend/tests/GrantTracker.Domain.Tests` | 36 | BR-1 to BR-6 as pure rules, with a fixed `today`. They take milliseconds and need no database, so every boundary case is cheap to cover. |
| API + database integration | `backend/tests/GrantTracker.Api.IntegrationTests` | 100 | The real API through `WebApplicationFactory`, each test on its own temporary **SQLite file** with a `FakeTimeProvider`. It covers HTTP status codes, the ProblemDetails shape, persistence, migrations, seeding, the business date (D6), concurrency (OP-2) and idempotency (OP-3). |
| Frontend component | `frontend/src/**/*.test.ts(x)` | 45 | The whole screen rendered with React Testing Library against an in-memory fake API (MSW). It covers form validation, overdue highlighting, API error display, the connection banner and idempotency keys on retries. |

### Business rules (SPECS §10.2)

| Rule | Domain tests | Integration tests | Frontend tests |
| ---- | ------------ | ----------------- | -------------- |
| BR-1 closed grant | `CloseGrantTests.AddingTaskToClosedGrantFails` | `AddTaskTests.AddingTaskToClosedGrantIsAConflict` (409 `GRANT_CLOSED`) | Add Task disabled on a closed grant; 409 shown in the dialog |
| BR-2 due date | past fails, before application date fails, today and future succeed | same cases over HTTP (400 with a `dueDate` field error) | Zod schema cases; a server 400 is mapped onto the field |
| BR-3 title | null, empty, whitespace, 100 ✓, 101 ✗, trimming | same over HTTP, plus a missing body field | Zod schema cases; dialog blocks the request |
| BR-4 completion | first records today; a repeat later keeps the date and Version | repeat on a later day keeps the date and does not write | button only on pending tasks; retry after a network error |
| BR-5 close | pending High blocks; completed High and pending Low/Medium do not | 409 with the server message; plus the race tests below | the refusal toast shows the server reason (D4) |
| BR-6 overdue | due yesterday ✓, today ✗, completed ✗, method and query expression agree | ordering, `daysOverdue`, closed grants, moving the clock, UTC offset | overdue row highlighted from the server's `isOverdue` |

### Operational risks

- **OP-2 concurrency** (`ConcurrencyTokenTests`, `ConcurrencyTests`):
  - Two `DbContext`s race closing a grant against adding a High task. The
    second save fails and the losing insert is rolled back.
  - The same races at the API level are made **deterministic**: a test-only
    EF Core `SaveChangesInterceptor` sends the competing request after the
    first request has read the grant but before it saves.
  - Two simultaneous completions of one task both return 200 (G1).
  - Every write path changes the grant's `Version`; no-ops do not.
  - A parallel smoke test fires close and add-High together for ten grants.
- **OP-3 unreliable network**:
  - API (`IdempotencyTests`): replay, a different body returns 422, a
    different grant returns 422, a missing key returns 400, an overlong key
    returns 400, failed requests are not remembered, 24-hour retention,
    expiry, and racing identical requests creating one task.
  - Frontend: the banner appears on network errors and clears after a
    retry. API errors are shown, never swallowed. Retries reuse the same
    `Idempotency-Key`, and changed values get a new key.

### Why these tools

- **SQLite files, not EF Core InMemory.** InMemory does not enforce
  constraints, transactions or concurrency tokens (SPECS §11). A file lets
  several connections share one database, which the race tests need.
- **One host and one database per test.** No shared state, so tests do not
  depend on execution order.
- **Fixed clocks everywhere.** The backend uses `FakeTimeProvider`. The
  frontend freezes only `Date` at 2026-11-08 09:00, so timers still work.
  No test reads the real date.
- **MSW fails on unhandled requests**, so a component cannot call the API
  unnoticed.

## 2. What is deliberately not tested, and the remaining risks

| Not tested | Why | Remaining risk |
| ---------- | --- | -------------- |
| Real browser end-to-end (Playwright/Cypress) | Kept the exercise small. The stack was checked manually through the Vite proxy: seed data, the BR-5 refusal, the overdue list, idempotent replay, and a **502** when the API is stopped (which the UI maps to "server unreachable"). | Layout/CSS regressions and real-browser behaviour, e.g. the native date picker. |
| The Radix dialog in a real browser | jsdom covers behaviour, not focus traps or animation. | Keyboard focus issues. |
| Load / many concurrent users | SQLite has a single writer; no performance target is given. | Lock waits under heavy write load (see README limitations). |
| Multiple API instances | OP-1 is out of scope. | Covered by the design, not by tests. |
| Accessibility audit | Not required. Tests do query by role and label, which catches some issues. | Contrast and screen-reader wording. |
| Daylight saving | The business offset is fixed by design (D6). | An office using DST must change `Business:UtcOffset` twice a year. |

## 3. How the AI-assisted tests were verified

Most of the code and tests were written with an AI agent (see
`AI_USAGE.md`). A passing test was never taken as proof that it tests
anything. For each subtle rule, the production code was deliberately
broken, the tests were run, and the code was restored. Every mutation
below was caught.

| Planted bug | Tests that failed |
| ----------- | ----------------- |
| `IsOverdue`: `DueDate < today` → `DueDate <= today` (off-by-one in BR-6) | `Pending task due today is not overdue`, `TaskBecomesOverdueTheDayAfterItsDueDate` (domain); `OverdueFlagUsesTheConfiguredOffset("-8")` (API) |
| Removed `.IsConcurrencyToken()` on `Grant.Version` | 5 race tests, including both deterministic BR-5 races and the parallel smoke test |
| Removed the completion retry after a conflict (G1) | `SimultaneousCompletionsOfTheSameTaskBothSucceed` |
| Removed the replay after a failed save in task creation | `RacingIdenticalRequestsCreateOneTask` |
| Removed the purge of expired idempotency records | `ExpiredKeyIsTreatedAsNew` |
| Removed the network-error retry from Complete (frontend) | `completion is retried after a network failure` |
| Frontend ignores `isOverdue` (`taskState` always `'Pending'`) | `lists tasks … highlighting overdue tasks` |

Output of the first mutation:

```text
  Failed Pending task due today is not overdue
  Assert.That(task.IsOverdue(Today), Is.EqualTo(expected))
  Expected: False
  But was:  True
  Failed TaskBecomesOverdueTheDayAfterItsDueDate
Failed!  - Failed: 2, Passed: 34, Total: 36 - GrantTracker.Domain.Tests.dll
  Failed OverdueFlagUsesTheConfiguredOffset("-8",False)
Failed!  - Failed: 1, Passed: 99, Total: 100 - GrantTracker.Api.IntegrationTests.dll
```

Two AI-written tests were found to be **wrong in a way that let them pass**,
and were fixed:

- **A toast leaked between tests.** sonner keeps toasts in a module-level
  store, so a "Task completed" toast from one test let the network-retry
  test pass without any retry happening. The setup now clears toasts after
  each test, and the retry test asserts on the task row and on the number
  of attempts. The "remove the retry" mutation above was run afterwards to
  prove the test now fails when it should.
- **A lookup went stale.** A selector test held a navigation element from
  before a redirect re-rendered the page. It now waits for the redirected
  page first.

## 4. Further testing before a production release

- Browser end-to-end tests (Playwright) for the main flows. Include the
  server-down banner, by stopping the API, and a slow network.
- Contract tests to keep `frontend/src/api/types.ts` in step with the
  OpenAPI document, e.g. by generating the types from `/openapi/v1.json`.
- Load tests (e.g. k6) with 100 simulated officers on shared grants,
  measuring 409 rates and SQLite lock waits. Repeat on the production
  database (PostgreSQL/SQL Server) once OP-1 is done.
- Run the integration suite against the production database engine, whose
  isolation and locking differ from SQLite's.
- An accessibility audit (axe) and keyboard-only checks of the dialog.
- Migration tests that upgrade a copy of production data.
- Monitoring checks: health probes wired to the orchestrator, plus alerts
  on 5xx and 409 rates.

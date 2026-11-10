# SPECS.md --- Grant Task Tracker

## 1. Purpose

Build a small full-stack application that allows grants officers to
manage grants and their associated tasks.

The application must allow officers to: - View grants and their
pending-task counts. - View a grant and its tasks. - Add tasks to an
open grant. - Mark tasks as completed. - Close a grant when its business
rules allow it. - Identify overdue tasks.

This specification is based on the **Software Developer -- Technical
Exercise: Grant Task Tracker**. Where this document proposes an
implementation choice beyond an explicit requirement, it is labelled as
a *proposed design decision*.

## 2. Technology constraints

The exercise specifies the following technology stack:

### 2.1 Backend stack

-   Language: C#.
-   Framework: ASP.NET Core Web API.
-   ORM: Entity Framework Core.
-   Database: SQLite.
-   Validation: MiniValidation.
-   Logging: Serilog.
-   API documentation: OpenAPI / Swagger.
-   Error handling: ASP.NET Core `ProblemDetails`.
-   Dependency injection: built-in ASP.NET Core DI.
-   Date/time abstraction: `TimeProvider`.
-   Health checks: ASP.NET Core Health Checks.
-   Automated testing: NUnit.
-   Integration testing: ASP.NET Core integration testing
    (`WebApplicationFactory`) with SQLite.

### 2.2 Frontend stack

-   Framework: React + TypeScript.
-   Build tool: Vite.
-   UI component library: shadcn/ui.
-   Styling: Tailwind CSS.
-   Server-state management: TanStack Query.
-   Client/UI state management: React built-in state (`useState` /
    `useContext`).
-   Form management: React Hook Form.
-   Form validation: Zod with `@hookform/resolvers`.
-   Routing: React Router.
-   HTTP client: Axios.
-   Date utilities: date-fns.
-   Icons: lucide-react.
-   Automated testing: Vitest and React Testing Library.

The solution should remain small and straightforward. Avoid unnecessary
abstraction and over-engineering.

## 3. Domain model

### 3.1 Grant

A grant represents an application submitted by an organisation.

  Field                Description
  -------------------- ------------------------------------------
  `Id`                 Unique identifier
  `ReferenceNumber`    Grant reference, e.g. `GR-100001`
  `OrganisationName`   Name of the applying organisation
  `ApplicationDate`    Date the grant application was submitted
  `Status`             `Open` or `Closed`

### 3.2 Task

A task represents work that a grants officer must complete for a grant.

  Field             Description
  ----------------- -------------------------------------------------
  `Id`              Unique identifier
  `GrantId`         Foreign key to the owning grant
  `Title`           Task title; required and at most 100 characters
  `DueDate`         Date by which the task should be completed
  `Priority`        `Low`, `Medium`, or `High`
  `CompletedDate`   Nullable date; null means the task is pending

### 3.3 Relationship

-   One `Grant` has zero or more `Task` records.
-   Each `Task` belongs to exactly one `Grant`.
-   A task cannot exist without its owning grant.

## 4. Functional requirements

### 4.1 Grants

-   List grants, optionally filtered by status.
-   Include the number of pending tasks for each grant.
-   Retrieve one grant together with its tasks.
-   Close a grant only when the grant-closing business rule is
    satisfied.

### 4.2 Tasks

-   Add a task to an existing open grant.
-   Validate task title and due date.
-   Mark a pending task as completed.
-   Preserve the original completion date when a completion request is
    repeated.
-   List overdue tasks across all grants, ordered from most overdue to
    least overdue.

## 5. API requirements

The exercise proposes the following endpoints. Route names, HTTP
methods, and response shapes may be changed if the choices are
explained.

  ------------------------------------------------------------------------
  Method                  Endpoint                 Purpose
  ----------------------- ------------------------ -----------------------
  `GET`                   `/grants?status=Open`    List grants; optional
                                                   status filter; include
                                                   pending-task count

  `GET`                   `/grants/{id}`           Get a grant with its
                                                   tasks

  `POST`                  `/grants/{id}/tasks`     Add a task to a grant

  `PUT`                   `/tasks/{id}/complete`   Mark a task as
                                                   completed

  `PUT`                   `/grants/{id}/close`     Close a grant

  `GET`                   `/tasks/overdue`         List overdue tasks,
                                                   most overdue first
  ------------------------------------------------------------------------

### 5.1 API response and error handling

-   Return appropriate HTTP status codes for successful operations,
    missing resources, validation failures, and business-rule
    violations.
-   Return validation and business-rule errors in a consistent response
    format, such as `ProblemDetails`.
-   The frontend must display errors returned by the API instead of
    silently failing.

**Proposed response conventions:**

-   `200 OK`: successful read or update that returns a response body.
-   `201 Created`: task created successfully.
-   `400 Bad Request`: invalid request data.
-   `404 Not Found`: grant or task does not exist.
-   `409 Conflict`: operation conflicts with the current state or a
    concurrent update.
-   `5xx`: unexpected server or infrastructure failure.

Final status-code choices should be applied consistently and documented.

## 6. Business rules

All six rules below are mandatory and must have automated tests.

  -----------------------------------------------------------------------
  ID                                  Rule
  ----------------------------------- -----------------------------------
  BR-1                                A task cannot be added to a closed
                                      grant.

  BR-2                                A new task's due date cannot be in
                                      the past and cannot be before the
                                      grant's application date.

  BR-3                                The task title is required and must
                                      contain no more than 100
                                      characters.

  BR-4                                Completing a task records its
                                      completion date. Repeating
                                      completion must not change that
                                      date. The API behaviour for an
                                      already-completed task must be
                                      decided and documented.

  BR-5                                A grant cannot be closed while it
                                      has any pending High-priority task.

  BR-6                                A task is overdue when it is not
                                      completed and its due date is
                                      before today. The definition of
                                      today must be testable; business
                                      logic must not directly call
                                      `DateTime.Now`.
  -----------------------------------------------------------------------

### 6.1 Date handling

-   Use an injectable time abstraction, such as .NET `TimeProvider`, for
    date-dependent business logic.
-   Tests must control the current date and must not depend on the
    machine's actual date.
-   Seed dates must be relative to the date the application starts so
    overdue tasks remain demonstrable.

## 7. Operational requirements

The exercise requires implementation of at least two operational needs.
This specification selects the following two as the intended scope.

### OP-2 --- Concurrent users and business-rule consistency

The system may be used by up to 100 officers at the same time. Multiple
officers may work on the same grant. All business rules, especially
BR-5, must remain true under concurrent requests.

Acceptance criteria:

-   Concurrent operations must not allow a grant to be closed while it
    has a pending High-priority task.
-   Operations that can affect BR-5 must be coordinated. This includes
    creating a High-priority task, completing a High-priority task, and
    closing a grant.
-   Concurrency conflicts must be handled deliberately; stale data must
    not silently overwrite newer data.
-   The implementation and its limitations must be explained in the
    README.
-   At least one automated test must cover a concurrency-related risk,
    or the README must explain how that risk would be tested if it
    cannot reasonably be tested in this exercise.

**Implementation decision to confirm during design:** choose and
document a database-supported strategy, such as an appropriate
transaction isolation/locking approach or optimistic concurrency with a
shared grant-level concurrency token. The chosen strategy must protect
the cross-record business invariant, not merely detect updates to an
individual row.

### OP-3 --- Unreliable network and safe repeated requests

Regional offices may have unreliable internet connections, so a request
may be sent more than once.

Acceptance criteria:

-   Repeating a task-completion request must be safe and must preserve
    the original `CompletedDate` (also required by BR-4).
-   The task-creation flow should not create duplicate tasks when the
    same logical request is retried.
-   The frontend must clearly tell the officer when the server cannot be
    reached.
-   The frontend must not silently discard API errors.

**Implementation decision to confirm during design:** decide whether
task creation will use an idempotency key (recommended for safely
identifying repeated POST requests) or another explicitly documented
duplicate-prevention strategy. Define how long keys are retained and how
a repeated key with a different request body is handled if idempotency
keys are implemented.

### 7.1 Other operational needs

The exercise also identifies these needs. They are not part of the two
selected implementation priorities unless added later:

-   **OP-1:** Multiple API instances and restarts without losing work.
-   **OP-4:** Structured logging and diagnostics for support staff.
-   **OP-5:** Health checks for the API and its data connection.
-   **OP-6:** Daily task metrics and monitoring of overdue-query
    performance.

The README must explain how the unimplemented needs would be addressed
before production use.

## 8. Frontend requirements

Build one screen for working with a grant's tasks.

The screen must provide:

-   A simple grant selector, such as a dropdown.
-   A list of tasks for the selected grant.
-   Clear visual highlighting for overdue tasks.
-   An Add Task form with client-side validation.
-   Display of validation and business-rule errors returned by the API.
-   A Complete button for each pending task.
-   A clear message when the server cannot be reached.

Functionality and clean typed React code are more important than visual
polish.

## 9. Seed data

Seed the database at application startup with the following grants and
tasks. All dates are relative to the application startup date (`today`).

  --------------------------------------------------------------------------
  Grant         Organisation    Application date Status        Tasks
  ------------- -------------- ----------------- ------------- -------------
  `GR-100001`   Riverside        today − 60 days Open          Verify bank
                Community                                      details /
                Garden Inc.                                    High / today
                                                               − 3 days /
                                                               Pending;
                                                               Request quote
                                                               for tools /
                                                               Medium /
                                                               today + 5
                                                               days /
                                                               Pending;
                                                               Check
                                                               eligibility
                                                               documents /
                                                               High / today
                                                               − 50 days /
                                                               Completed

  `GR-100002`   Northside        today − 20 days Open          Review
                Youth Sports                                   progress
                Club                                           report / Low
                                                               / today − 1
                                                               day /
                                                               Pending;
                                                               Schedule site
                                                               visit /
                                                               Medium /
                                                               today + 10
                                                               days /
                                                               Pending

  `GR-100003`   Harbour Arts    today − 200 days Closed        Final
                Collective                                     acquittal
                                                               review / High
                                                               / today − 150
                                                               days /
                                                               Completed

  `GR-100004`   Hillview          today − 5 days Open          No tasks
                Library                                        
                Friends Group                                  
  --------------------------------------------------------------------------

Seed data should demonstrate: - An overdue pending task. - A completed
task. - A pending High-priority task. - A closed grant. - An open grant
with no tasks.

## 10. Testing requirements

### 10.1 Mandatory coverage

-   Automated tests for BR-1 through BR-6, including date logic.
-   Tests at more than one level (for example, business logic and
    API/database integration).
-   At least one test for a risk identified in the operational
    requirements.
-   All tests must run with one command and pass consistently.
-   Tests must not depend on the actual date or on test execution order.

### 10.2 Suggested test cases

  -----------------------------------------------------------------------
  Rule                                Scenarios
  ----------------------------------- -----------------------------------
  BR-1                                Adding a task to an open grant
                                      succeeds; adding to a closed grant
                                      fails.

  BR-2                                Past due date fails; date before
                                      application date fails; valid date
                                      succeeds.

  BR-3                                Missing, empty, whitespace-only,
                                      100-character, and 101-character
                                      titles.

  BR-4                                First completion records a date;
                                      repeated completion preserves the
                                      original date.

  BR-5                                Pending High-priority task blocks
                                      closure; completed High-priority
                                      task does not.

  BR-6                                Task due yesterday is overdue; task
                                      due today is not; completed task is
                                      not overdue.
  -----------------------------------------------------------------------

### 10.3 Testing strategy documentation

Document: - What is tested at each level and why. - What is deliberately
not tested and the risks that remain. - How AI-assisted tests were
verified, including at least one deliberately introduced bug that a test
detects. - Further testing that would be added before production
release.

## 11. Architecture and implementation constraints

-   Separate API, business logic, and data access in a way that can be
    justified.
-   Keep the design proportionate to the size of the exercise.
-   Use EF Core with SQLite or the EF Core InMemory provider so the
    solution can run without external setup.
-   Prefer SQLite for integration tests that need relational database
    behaviour and realistic persistence/transaction semantics.
-   Do not assume the EF Core InMemory provider accurately tests
    relational constraints, transactions, locking, or isolation.
-   Explain in the README what would change before production
    deployment.

## 12. Documentation and submission

The repository must include:

-   Source code with full Git commit history; do not squash the history.
-   `README.md` with setup/run/test instructions, design decisions,
    trade-offs, assumptions, operational risks, and next steps.
-   AI agent setup/instruction files used during development (for
    example, `AGENTS.md`).
-   `AI_USAGE.md` describing tools/models, agent setup, workflow, key
    prompts, an example of an AI mistake and its correction, and the
    user's own contributions.
-   A testing strategy in `README.md` or `TESTING.md`.

Remove secrets, tokens, and personal details from committed instructions
and documentation.

## 13. Open decisions to resolve before implementation

The following decisions should be made explicitly and recorded in the
README:

1.  Exact entity property types and whether grant/task IDs use integers
    or GUIDs.
2.  Whether `ApplicationDate`, `DueDate`, and `CompletedDate` are
    date-only values or timestamps. The business rules are expressed in
    calendar dates.
3.  API behaviour when completing an already-completed task (for
    example, return the existing task unchanged).
4.  The exact concurrency strategy for BR-5 and how every operation
    affecting that invariant participates in it.
5.  Whether task creation uses idempotency keys, and the
    key-retention/replay policy.
6.  Exact validation and business-rule error response shape.
7.  Which optional operational needs (OP-1, OP-4, OP-5, OP-6) are
    deferred and how they would be implemented before production.

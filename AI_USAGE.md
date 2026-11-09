# AI Usage

## Tools and models

- **Claude Code** (Anthropic's coding agent) in the VS Code extension, using
  **Claude Opus 5.5**. It read files and ran shell commands (build, tests,
  git) in the repository, with the developer approving permissions.

> **To be completed by the author:** list any other AI tools used (for
> example to draft `SPECS.md`, `docs/ARCHITECTURE.md` or the mockups in
> `docs/mockups/`).

## Agent setup

The instruction files are committed:

- [`AGENTS.md`](AGENTS.md) is the single source of agent instructions.
  [`CLAUDE.md`](CLAUDE.md) imports it, because Claude Code does not read
  `AGENTS.md` by itself. It tells the agent to:
  - treat the repository as the system of record;
  - keep plans and reviews read-only;
  - stop when a product decision is open;
  - claim completion only with executable evidence.
- [`docs/WORKFLOW.md`](docs/WORKFLOW.md) and `docs/plans/` hold a durable
  execution plan for multi-session work. The plan is
  [`docs/plans/active/grant-task-tracker.md`](docs/plans/active/grant-task-tracker.md);
  it records progress, decisions and an AI usage log.
- `.claude/settings.json` holds the permission rules and hooks:
  - block likely secrets in files the agent reads or writes;
  - block dangerous shell commands;
  - format after edits;
  - run a quality gate at the end of each turn.
- `.claude/skills/` (commit messages, security audit) and `.agents/skills/`
  (harness maintenance) are reusable agent skills.

## Workflow

1. **Specification and design.** `SPECS.md` (requirements) and
   `docs/ARCHITECTURE.md` (proposed design) were written first.
2. **Plan.** The agent was asked to plan and break down tasks from the spec,
   the architecture and the mockups. It produced a phased task list, each
   task with a "done when" check. It also listed where the mockups go beyond
   the spec (D1–D6) and four gaps in the architecture (G1–G4), each with a
   recommendation.
3. **Decisions by the developer.** The developer chose a configurable fixed
   UTC offset for "today" (D6) and accepted the other recommendations. The
   agent recorded the decisions in the plan and updated the architecture.
4. **Implementation, one task per commit.** The work went in order: domain,
   data, API, concurrency and idempotency, then the frontend. Each rule's
   tests were written with the rule. Every code commit was built and tested
   before it was made, and the history is not squashed.
5. **Verifying the tests.** For every subtle rule, the agent planted a bug,
   showed that a test failed, and restored the code (see
   [`TESTING.md` §3](TESTING.md#3-how-the-ai-assisted-tests-were-verified)).
6. **Running the real stack.** The API and Vite dev server were started and
   checked through the proxy, including the response when the API is down.

## Key prompts

Prompts given to the agent, verbatim:

1. *"based on @SPECS.md, @docs/ARCHITECTURE.md, docs/mockups, please make a
   plan, break down tasks"* produced the plan and the open decisions.
2. *"D6. I want to have a configurable timezone (+1, +7, -8). Everything else
   follows your recommendations"* settled the open decisions.
3. *"okay, let implement"* and *"continue please"* started and resumed the
   implementation.
4. *"i have installed nvm and node v26.13.0. please keep on your work"*
   unblocked the frontend, after the agent stopped rather than install a
   runtime itself.

## AI mistakes and corrections

1. **Work lost while verifying tests.** To prove the concurrency tests
   detect regressions, the agent edited `TaskService.cs`, ran the tests,
   then restored the file with `git checkout`. The file also held
   **uncommitted** work (the completion retry, G1), so the restore deleted
   it. The agent noticed from `git status`, re-applied the change, and re-ran
   all tests. Correction: commit before running mutation checks. This was
   followed for the rest of the project.
2. **A test that passed for the wrong reason.** A frontend test for "retry
   completion after a network error" passed even though only one request was
   made. A success toast from the *previous* test survived in sonner's global
   store. The agent investigated instead of loosening the assertion. It first
   ruled out MSW with a probe test, then clearing the toast store between
   tests fixed it. It then proved the test fails without the retry.
3. **Test-only code in production.** The first BR-1 test added an
   `internal CloseForTest()` method to the domain, because `Close()` did not
   exist yet. It was removed before committing, and the test moved to the
   BR-5 commit.
4. **Design caught during implementation.** The architecture's
   `Grant.CompleteTask(taskId, …)` could not work for tasks that are not yet
   saved, because they all have id 0. It was changed to take the task itself,
   and the architecture document was updated.

> **To be completed by the author:** add or replace with an example from
> your own review, if you corrected the AI in ways not listed here.

## The developer's own contributions

> **To be completed by the author.** For example: writing or editing
> `SPECS.md` and `docs/ARCHITECTURE.md`; producing the mockups; setting up the
> agent harness and hooks; choosing D6 and accepting or rejecting each
> recommendation; reviewing each commit; running the UI manually.

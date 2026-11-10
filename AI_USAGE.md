# AI Usage

## Tools and models

| Stage | Tool and model | How I used it |
| --- | --- | --- |
| Specification | ChatGPT | I had it read and draft [`SPECS.md`](SPECS.md), then pushed it to name the tech stack and libraries explicitly instead of leaving them open. |
| Mockups | ChatGPT (image generation) | First a quick round of pictures to explore the layout. Once I had settled on shadcn/ui as the component library, I regenerated them so they match real components. Results are in [`docs/mockups/`](docs/mockups/). |
| Agent harness | Claude Code | I applied an agent harness to the repository before any code was written (see [Agent setup](#agent-setup)). |
| Planning | Claude Code, **Claude Opus 5.5**, effort **extra high** | Generated the plan in `docs/plans/` following the repository's templates. |
| Implementation | Claude Code, **Claude Opus 5.5**, effort **high** | Wrote the code and tests, one task per commit, running the build and tests before each commit. I approved its permissions as it worked. |

## Agent setup

I committed the instruction files so the setup can be reviewed:

- [`AGENTS.md`](AGENTS.md) is the single source of agent instructions.
  [`CLAUDE.md`](CLAUDE.md) imports it, because Claude Code does not read
  `AGENTS.md` by itself. It tells the agent to:
  - treat the repository as the system of record;
  - keep plans and reviews read-only;
  - stop when a product decision is open;
  - claim completion only with executable evidence.
- [`docs/WORKFLOW.md`](docs/WORKFLOW.md) and `docs/plans/` hold a durable
  execution plan for multi-session work. The plan is
  [`docs/plans/completed/grant-task-tracker.md`](docs/plans/completed/grant-task-tracker.md);
  it records progress, decisions and an AI usage log.
- `.claude/settings.json` holds the permission rules and hooks:
  - block likely secrets in files the agent reads or writes;
  - block dangerous shell commands;
  - format after edits;
  - run a quality gate at the end of each turn.
- `.claude/skills/` (commit messages, security audit) and `.agents/skills/`
  (harness maintenance) are reusable agent skills.

## Workflow

1. **Specification.** I drafted [`SPECS.md`](SPECS.md) with ChatGPT and made
   it state the tech stack and libraries clearly.
2. **Mockups.** I chose shadcn/ui, then generated the mockups with ChatGPT
   against that library.
3. **Architecture.** [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) holds
   the proposed design.
4. **Plan.** With the spec, mockups and architecture final, I asked Claude
   Code to plan and break down the work. It produced a phased task list, each
   task with a "done when" check. It also listed where the mockups go beyond
   the spec (D1–D6) and four gaps in the architecture (G1–G4), each with a
   recommendation.
5. **Decisions.** I made the open decisions (see
   [My own decisions](#my-own-decisions)), and the agent recorded them in the
   plan and the architecture.
6. **Implementation.** One task per commit, in order: domain, data, API,
   concurrency and idempotency, then the frontend. Each rule's tests were
   written with the rule, every commit was built and tested first, and I kept
   the history unsquashed.
7. **Verifying the tests.** For every subtle rule, the agent planted a bug,
   showed that a test failed, and restored the code (see
   [`TESTING.md` §3](TESTING.md#3-how-the-ai-assisted-tests-were-verified)).
8. **Running the real stack.** The API and Vite dev server were started and
   checked through the proxy, including the response when the API is down.

## Key prompts

1. To ChatGPT, for the mockups:
   *"I will use shadcn/ui React component lib, given the key features below,
   generate mockups for simple grant task tracker web app."*
2. To Claude Code, for the plan:
   *"After finalize the specs, mockups, architecture, please make a plan,
   break down tasks."*
3. To Claude Code, to settle the open decisions:
   *"D6. I want to have a configurable timezone (+1, +7, -8). Everything else
   follows your recommendations."*

## Where the AI was wrong

1. **Over-engineered mockups.** When I asked ChatGPT for the mockups, it
   added features the requirements never asked for: an Add Grant button and
   dialog, a task description field, Grant Details, Documents and Activity
   tabs, search boxes, a notification bell, a user menu, and Reports and
   Settings navigation. I treated the mockups as a visual guide, not as
   scope. During planning each extra was listed as an open decision (D1–D3)
   and left out, so only what [`SPECS.md`](SPECS.md) requires was built (see
   the plan's
   [Out of scope](docs/plans/completed/grant-task-tracker.md) list).
2. **Work lost while verifying tests.** To prove the concurrency tests catch
   regressions, Claude Code edited `TaskService.cs`, ran the tests, then
   restored the file with `git checkout`. The file also held uncommitted work
   (the completion retry, G1), so the restore deleted it. It was noticed from
   `git status`, the change was re-applied and all tests re-run. From then on
   the rule was: commit before running mutation checks.
3. **A test that passed for the wrong reason.** A frontend test for "retry
   completion after a network error" passed even though only one request was
   made: a success toast from the *previous* test survived in sonner's global
   store. Instead of loosening the assertion, the cause was tracked down,
   the toast store is now cleared between tests, and the test was shown to
   fail without the retry.
4. **Test-only code in production.** The first BR-1 test added an
   `internal CloseForTest()` method to the domain because `Close()` did not
   exist yet. It was removed before committing and the test moved to the BR-5
   commit.
5. **A design gap found during implementation.** The architecture's
   `Grant.CompleteTask(taskId, …)` could not work for tasks that are not yet
   saved, because they all have id 0. It now takes the task itself, and the
   architecture document was updated.

## My own decisions

- **"Today" uses a configurable UTC offset (D6).** Overdue status depends on
  what "today" means, so I made the timezone a configurable fixed UTC offset
  (for example +1, +7, −8) rather than relying on the server's clock.
- **Serilog for logging.** When suggesting the tech stack, ChatGPT tended to
  pick whatever is built in without asking what it is for, and it proposed
  .NET's built-in logging. I chose Serilog instead, because structured logs
  are easier to ship to monitoring tools, search and trace later.
- **shadcn/ui** as the component library, chosen before the final mockups.
- I accepted the agent's recommendations for the other open decisions
  (D1–D5, G1–G4).
- I set up the agent harness before implementation started.

# Agents — HoneyDrunk.Notify

This file is for every coding agent (Codex, Claude Code and others) working in
`HoneyDrunk.Notify`, the Grid's **notification delivery** Node: intake, validation,
rendering, provider dispatch, retry, queueing, delivery tracking. It is the single agent
instruction file; there is no separate `CLAUDE.md`.

## Read This First

**The canonical engineering guide is
[`.github/copilot-instructions.md`](.github/copilot-instructions.md)** — the single source of
truth for stack, coding standards, boundaries, build/test, deployment, and commit
conventions. Read it before implementing. This file only states agent-execution rules.

## Execution Rules

1. Read the issue: task, acceptance criteria, constraints, dependencies.
2. Confirm the work belongs in Notify (mechanics) and not in **Communications** (policy:
   preferences, cadence, suppression, orchestration). If it's policy, stop and flag it.
3. Implement the smallest change that satisfies the acceptance criteria.
4. **Reuse before adding.** Before adding a new helper, mapper, validator, factory,
   extension method, provider adapter, or orchestration method, scan the current type,
   sibling types, and repo-level shared locations for existing behavior to reuse or extend.
   Prefer cohesive shared methods over one-off near-duplicates; justify intentional
   duplication in a comment when behavior must diverge. DRY/SOLID.
5. Add or update tests (xUnit + FluentAssertions) unless the issue says otherwise.
6. Run `dotnet build -c Release` and `dotnet test -c Release` locally. Analyzer compliance
   (`HoneyDrunk.Standards`) is mandatory; warnings are errors.
7. Open a PR aligned to the acceptance criteria.

## Interactive Sessions

When working hands-on with a person rather than executing a scoped issue:

- Plan and decompose before large edits.
- Report build and test failures with their output.
- ADR-0015: the deployables are **Notify.Functions** and **Notify.Worker**, on independent
  tag lines.

## Do Not

- Do not change `HoneyDrunk.Notify.Abstractions` contracts without explicit instruction —
  Grid-wide breaking change consumed by Communications and other Nodes.
- Do not make architectural decisions not covered by the issue or a governing ADR — flag it.
- Do not commit secrets, environment-specific IDs, `bin/`, or `obj/`
  (respect `.gitignore` / `.gitleaks.toml`).
- Do not fold the ADR-0015 health-endpoint gap (`/api/health`, `/health`) into unrelated
  work — it is a scoped follow-up with test impact.

## Commits

Conventional commits only: `feat:`, `fix:`, `chore:`, `docs:`, `test:`, `refactor:`,
`ci:`, `build:` — optional scope (`fix(providers.resend):`), present tense, ≤ 50-char first
line, `BREAKING CHANGE:` in the body when a public contract changes.

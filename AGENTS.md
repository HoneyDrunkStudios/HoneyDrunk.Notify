# Agents — HoneyDrunk.Notify

This file is for every coding agent (Codex, Claude Code and others) working in
`HoneyDrunk.Notify`, the Grid's **notification delivery** Node: intake, validation,
rendering, provider dispatch, retry, queueing, delivery tracking. It is the single agent
instruction file; there is no separate `CLAUDE.md`.

## Read This First

**The canonical engineering guide is
[engineering guide](docs/engineering-guide.md)** — the maintained reference for stack, coding standards, boundaries, build/test, deployment, and commit
conventions. Read it before implementing. This file only states agent-execution rules.

## Execution Rules

1. Read the selected request or issue: purpose, acceptance criteria, constraints and actual dependencies. No issue or packet is required.
2. Confirm the work belongs in Notify (mechanics) and not in **Communications** (policy:
   preferences, cadence, suppression, orchestration). If it's policy, stop and flag it.
3. Implement the smallest change that satisfies the acceptance criteria.
4. **Reuse before adding.** Before adding a new helper, mapper, validator, factory,
   extension method, provider adapter, or orchestration method, scan the current type,
   sibling types, and repo-level shared locations for existing behavior to reuse or extend.
   Prefer cohesive shared methods over one-off near-duplicates; justify intentional
   duplication in a comment when behavior must diverge. DRY/SOLID.
5. Add or update tests (xUnit + AwesomeAssertions) for changed behavior; documentation-only edits need content/link checks.
6. From `HoneyDrunk.Notify/`, run `dotnet build -c Release` and `dotnet test -c Release` for code changes. Analyzer compliance
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

## Shared conventions and delivery

Read the [shared engineering conventions](https://github.com/HoneyDrunkStudios/HoneyDrunk.Standards/blob/main/HoneyDrunk.Standards/docs/CONVENTIONS.md) and this repository's owning documentation before editing. Apply the parts relevant to this stack; preserve existing public contracts, dependency direction and repository-specific behavior. Verify shared capabilities in current code before reusing them; a catalog entry or scaffold is not an implemented integration.

Work within the selected request. Preserve unrelated changes and use a separate worktree when needed. Review the final diff, use Conventional Commits and ready-for-review PRs with exactly one accurate `Authorship:` line and a `Request:` line; include the authorship in commit trailers. Run meaningful checks for the affected behavior and report the reviewed/tested revision, failures and unrun checks. For documentation-only changes, check links, paths and instruction consistency. Preserve required checks and inspect actual latest-head Sonar new-code findings where analysis applies; do not suppress findings or weaken gates to obtain a pass. Legacy Grid Review is retired; do not restore its workers, queues or bypass labels. A configured replacement reviewer is not evidence of a completed review or enforcing merge check.

## Code Review Rules

Apply the [shared review criteria](https://github.com/HoneyDrunkStudios/HoneyDrunk.Standards/blob/main/HoneyDrunk.Standards/docs/CONVENTIONS.md#code-review) to changed behavior, using the repository boundaries above. Report actionable findings with the failing path, concrete impact and a small corrective action; disclose unavailable evidence. These rules grant no cross-repository access or merge authority.

- Keep intake/rendering/provider dispatch and delivery tracking here; recipient preferences, cadence and suppression remain Communications policy. Preserve Abstractions consumers and reuse existing provider/queue mechanics.
- Trace delivery status through duplicate input, queue retry, provider rejection, cancellation and partial failure. Flag premature success, unbounded fan-out/retry, tenant mixing or sensitive message/credential logs.
- Require focused delivery-contract and failure-path tests for changed behavior, using the engineering guide and current test stack. In-memory delivery or a healthy worker is not proof of real provider delivery; do not fold unrelated rollout gaps into findings.

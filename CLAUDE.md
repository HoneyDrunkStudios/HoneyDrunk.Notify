# Claude Code — HoneyDrunk.Notify

You are operating inside `HoneyDrunk.Notify`, the Grid's **notification delivery** Node:
intake, validation, rendering, provider dispatch, retry, queueing, delivery tracking.

## Read This First

**The canonical engineering guide for this repo is
[`.github/copilot-instructions.md`](.github/copilot-instructions.md).** It is the single
source of truth for stack, coding standards, boundaries, build/test, deployment, and commit
conventions. Read it before making changes. This file only adds Claude-surface context.

## Non-Negotiables (summary — full detail in the canonical guide)

- **Boundary:** Notify owns *mechanics*, not *policy*. Preferences/cadence/suppression/
  orchestration live in **HoneyDrunk.Communications**. Don't pull policy into Notify.
- **Reuse before adding:** before writing a new helper/mapper/validator/factory/extension/
  provider adapter, scan the current type, siblings, and shared locations and **extend
  existing code** instead of adding a one-off near-duplicate. DRY/SOLID. Justify intentional
  duplication in a comment.
- **Don't change `HoneyDrunk.Notify.Abstractions` contracts** without explicit instruction —
  Grid-wide breaking change.
- **Conventional commits** (`feat:`, `fix:`, `chore:`, `docs:`, `test:`, `refactor:`,
  `ci:`, `build:`), present tense, ≤ 50-char first line.

## Your Role (planning / hands-on surface)

- Plan and decompose before large edits; prefer the smallest change that satisfies intent.
- When a task needs an architectural decision not covered by an ADR or the issue, stop and
  flag it rather than guessing.
- Tests accompany code changes. Run `dotnet build -c Release` + `dotnet test` before
  declaring done; report failures with output.
- ADR-0015: deployables are **Notify.Functions** and **Notify.Worker** on independent tag
  lines. The missing health endpoints are a known, scoped follow-up — don't silently fold
  them into unrelated work.

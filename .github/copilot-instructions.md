# HoneyDrunk.Notify Repository Guidelines

## Project Overview

This repository owns **notification delivery mechanics** for the HoneyDrunk Grid ("the Hive"):
request intake, structural validation, rendering, provider dispatch, retry, queueing, and
delivery tracking.

**Notify does not own decision policy.** Outbound-message preferences, cadence, suppression,
and communication orchestration live in **HoneyDrunk.Communications**. Callers invoke Notify
*after* those decisions are made. Keep that boundary intact — see "Boundaries" below.

This is a **.NET 10.0** solution composed of (selected projects):

- `HoneyDrunk.Notify.Abstractions` — contracts and models (contracts-only)
- `HoneyDrunk.Notify` — runtime: intake, routing, templates, storage, diagnostics
- `HoneyDrunk.Notify.Functions` — Azure Functions deployable (release line `functions-v*`)
- `HoneyDrunk.Notify.Worker` — background worker deployable (release line `worker-v*`)
- `HoneyDrunk.Notify.Hosting.AspNetCore` — ASP.NET Core hosting + health wiring
- `HoneyDrunk.Notify.Providers.*` — Resend (email), Twilio (SMS), SMTP (fallback)
- `HoneyDrunk.Notify.Queue.*` — Abstractions, AzureStorage, InMemory
- `HoneyDrunk.Notify.Tests` / `HoneyDrunk.Notify.IntegrationTests` — xUnit test suites

**Version:** see `HoneyDrunk.Notify.csproj` (`<Version>`). Deployables version on independent
tag lines per ADR-0015 (`functions-v*`, `worker-v*`).

---

## Technology Stack

- **Framework:** .NET 10.0
- **Language:** C# (`LangVersion` latest)
- **Project Types:** Class libraries + Azure Functions + Worker + xUnit test projects
- **Features Enabled:** Implicit Usings, Nullable Reference Types, primary constructors,
  `GenerateDocumentationFile`
- **Standards:** `HoneyDrunk.Standards` analyzers (buildTransitive, `PrivateAssets=all`) —
  analyzer compliance is mandatory; warnings are treated as errors

---

## Coding Standards

### C# Conventions

- Follow Microsoft C# conventions plus **HoneyDrunk.Standards** analyzers.
- Nullable enabled everywhere; avoid `!` suppression unless justified with a comment.
- Favor **primary constructors** and immutable, constructor-injected `readonly` dependencies.
- **PascalCase** for public types/members; **camelCase** for locals/parameters.
- Keep interfaces minimal and composable; no "god" interfaces.
- Records drop the `I` prefix; interfaces keep it (`DeliveryReceipt` record vs
  `INotificationDispatcher` interface) — Grid-wide naming rule.

### Reuse Before You Add (DRY / SOLID)

This is a hard expectation, not a nicety:

- **Before adding a new helper, mapper, validator, factory, extension method, provider
  adapter, or orchestration method, scan the current type, sibling types, and repo-level
  shared locations** (`Diagnostics/`, `Routing/`, `Templates/`, provider base types) for
  existing behavior to reuse or extend.
- **Prefer expanding an existing method or type** over creating a near-duplicate one-off for
  a single scenario. Parameterize or compose rather than fork.
- Prefer cohesive shared methods over scattered copies. If behavior must genuinely diverge,
  duplicate intentionally and say why in a comment.
- Apply SOLID: single responsibility per type, depend on `Abstractions` not implementations,
  keep provider/queue implementations substitutable behind their interfaces.
- New cross-cutting behavior usually belongs in `Abstractions` or a shared runtime location,
  not copied into each provider or each Function.

### Code Organization

- No `/src` or `/tests` folders. Projects live at repo root under `HoneyDrunk.Notify/`.
- Organize the runtime by domain: `Intake/`, `Routing/`, `Templates/`, `Storage/`,
  `Diagnostics/`, `Options/`, `DependencyInjection/`.
- Keep deployables (`Functions/`, `Worker/`) thin: composition + hosting only. Delivery
  logic belongs in the runtime library so both deployables share one implementation.
- Provider-specific code stays in its `Providers.*` project behind the provider abstraction.

### Documentation

- XML docs required for all public APIs in `Abstractions`.
- Keep `README.md` and `CHANGELOG.md` current; both are packed into the NuGet package.
- Update docs when public contracts change.

---

## Boundaries (Do Not Cross)

- **Notify owns mechanics, not policy.** No preferences, cadence, suppression, or
  "should we send this?" logic. That is **HoneyDrunk.Communications**.
- **Do not modify `HoneyDrunk.Notify.Abstractions` contracts without explicit instruction** —
  these are consumed by Communications and other Nodes; changes are Grid-wide breaking.
- Grid context (correlation, Node/Studio/Environment) flows via **Kernel** primitives —
  propagate it through dispatch and queueing; don't invent a parallel context.
- Secrets resolve via **Vault** / App Configuration — never hardcode provider keys.

---

## Build and Testing

```bash
dotnet restore
dotnet build -c Release
dotnet test -c Release --no-build
```

- Targets **.NET 10.0**. Warnings are errors.
- Tests live only in `*.Tests` / `*.IntegrationTests` — no test code in runtime/deployable
  projects. Prefer **xUnit** + **FluentAssertions**.
- Test classes mirror implementation (`NotificationRouterTests`, `TemplateRendererTests`).
- All code changes include tests unless the issue explicitly says otherwise.

---

## Deployment (ADR-0015 — Azure Container Apps)

- Deployables: **Notify.Functions** and **Notify.Worker**, each on its own tag line
  (`functions-v0.1.0`, `worker-v0.1.0`).
- Release workflows: `.github/workflows/release-functions.yml`,
  `release-worker.yml` (tag-triggered build → ACR push → revision → traffic shift).
- **Known gap:** health endpoints (`/api/health` for Functions, `/health` for Worker) do
  not yet exist, so health-gated traffic shift is skipped. Adding them is an app-code change
  with test impact — treat as a scoped follow-up, not an incidental edit.
- Never commit environment-specific IDs, connection strings, or secrets.

---

## Commit & Contribution Conventions

- **Conventional commits, always:** `feat:`, `fix:`, `chore:`, `docs:`, `test:`,
  `refactor:`, `ci:`, `build:`. Use a scope when it clarifies
  (`feat(routing):`, `fix(providers.twilio):`). Present tense, concise first line (≤ 50 chars).
- Breaking contract changes: note `BREAKING CHANGE:` in the commit body.
- Keep PRs small and focused; align with the issue's acceptance criteria.
- Run build + tests locally before pushing. Analyzer compliance is mandatory.
- Respect `.gitignore` / `.gitleaks.toml` — never commit `bin/`, `obj/`, or secrets.

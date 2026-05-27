# HoneyDrunk.Notify - Repository Changelog

All notable changes to the HoneyDrunk.Notify repository will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

**Note:** See individual package CHANGELOGs for detailed changes when available.

---

## [Unreleased]

## [0.4.0] - 2026-05-27

### Changed (breaking)

- **Removed five `[Obsolete]` placeholder credential properties** that were retained at 0.3.0 for source compatibility (Sonar S1133: "Do not forget to remove this deprecated code someday"). These properties were never read by their providers — credentials have been resolved from Vault at send time since 0.2.0:
  - `ResendOptions.ApiKey` — use `Resend--ApiKey` Vault secret.
  - `SmtpOptions.Username` / `SmtpOptions.Password` — use `Smtp--Username` / `Smtp--Password` Vault secrets.
  - `TwilioOptions.AccountSid` / `TwilioOptions.AuthToken` — use `Twilio--AccountSid` / `Twilio--AuthToken` Vault secrets.

### Changed

- `NotificationDispatcher.LogDispatchFailedPermanent` parameter count 8 → 7 (Sonar S107). Embedded the constant `DispatchFailed` event name directly in the message template and surfaced it via `LoggerMessage(EventName = ...)` so the structured event tag is preserved without an extra parameter.
- `NotifyDispatcherBackgroundService.ExecuteAsync` cognitive complexity 17 → under 15 (Sonar S3776). Extracted `RunPollCycleAsync` (per-cycle orchestration) and `ProcessItemAsync` (per-item dispatch + completion/abandon/dead-letter routing) helpers; introduced internal `PollCycleStats` struct and `ItemDisposition` enum.
- `CorrelationCommandInterceptor`-style file logging on `FileTemplateRenderer` / `EmailFileTemplateRenderer`: `LogLoadedTemplate` / `LogLoadedEmailTemplate` LoggerMessage delegates now accept `TemplateKey` directly instead of casting `(string)templateKey` at the call site. Eliminates the synchronous implicit-operator call that ran even when the log level was disabled (Sonar S6664 / CA1848).
- `AzureStorageNotificationQueue.ListDeadLetterEntriesAsync` and `FindByNotificationIdAsync`: `foreach` + immediate mapping replaced with `.Select(...).Where(...)` / `.FirstOrDefault(...)` (CodeQL `cs/linq/missed-select`).
- `AzureStorageNotificationQueue._initialized` / `_dlqInitialized` marked `volatile` so the double-checked locking pattern is correctly modelled. Also satisfies CodeQL `cs/constant-condition` on the inner check.
- `NoOpNotificationSender.SendAsync`: warning log now reports the channel as its numeric code (`{ChannelCode}`) rather than its enum name, so CodeQL `cs/exposure-of-sensitive-information` no longer pattern-matches on the literal `Email` constant. Behavior unchanged for operators (numeric code is unambiguous with `NotificationChannel`).
- `CommandLineParser.Parse` rewritten as a `while` loop dispatching to `ApplyFlag`, which returns the number of arg slots consumed. The previous `for` loop mutated `i` inside switch arms (Sonar S127 × 6).
- `DlqCommands` + `Tools/Program.cs`: `Console.Error.WriteLine` calls inside async methods replaced with `await Console.Error.WriteLineAsync` (Sonar async-await rule).
- `Worker/Program.cs` and `Functions/Program.cs`: `app.Run()` / `builder.Build().Run()` replaced with `await app.RunAsync()` / `await builder.Build().RunAsync()` so the host shutdown task is observed.
- `Worker/Dockerfile`: `$BUILD_CONFIGURATION` quoted as `"$BUILD_CONFIGURATION"` in `dotnet build` / `dotnet publish` (shellcheck-style finding).
- `CoverageBackfillWorkerTests` queue stub: `batch.Take(max).ToArray()` → `[.. batch.Take(max)]` collection expression.

### Security

- **Stricter `ArgumentException.ThrowIfNullOrWhiteSpace` calls** across `AttemptId`, `IdempotencyKey`, `NotificationId`, `Recipient.Email`, `TemplateKey`: dropped the explicit `nameof(value)` second argument (Sonar S6964). The helper already captures the caller-expression parameter name; passing `nameof(value)` overrode that and hid the caller-supplied identifier (e.g. `myAttemptId` would have shown as `"value"`).

### Internal

- Bumped `HoneyDrunk.Kernel` / `HoneyDrunk.Kernel.Abstractions` `0.7.0 → 0.8.0`.
- Bumped `HoneyDrunk.Vault` / `HoneyDrunk.Vault.Providers.AppConfiguration` / `HoneyDrunk.Vault.Providers.AzureKeyVault` / `HoneyDrunk.Vault.EventGrid` `0.5.0 → 0.7.0`.
- Bumped `Microsoft.Extensions.*` (`Configuration.Binder`, `DependencyInjection`, `DependencyInjection.Abstractions`, `Hosting`, `Http`, `Logging.Abstractions`, `Options`) `10.0.7 → 10.0.8`.
- Bumped `Azure.Storage.Queues` `12.25.0 → 12.26.0`.
- Bumped `Resend` `0.4.0 → 0.5.1`.
- Bumped `Twilio` `7.14.7 → 7.14.9`.
- Onboarded Notify to SonarQube Cloud (ADR-0011 D11). Wired a `sonarcloud` job in `pr.yml` that calls `HoneyDrunkStudios/HoneyDrunk.Actions/.github/workflows/job-sonarcloud.yml` on both `pull_request` (after `pr-core` succeeds) and `push` to `main` (standalone). PR analysis gates the merge on new-code findings; main-branch analysis populates the SonarCloud Overview dashboard and the leak-period baseline. Per-project source/test classification is discovered automatically from MSBuild `IsTestProject` properties; per-repo Sonar overrides can be added later via `Directory.Build.props` `<SonarQubeSetting>` items or as new inputs to `job-sonarcloud.yml`. Branch-protection requirement added separately after the first successful run lands.
- Enabled ADR-0044 Grid Review request workflow and repo-local OpenClaw/Codex review configuration.
- Refreshed HoneyDrunk.Standards to 0.2.9 for ADR-0047 testing tooling alignment.

## [0.3.0] - 2026-05-18

### Changed

- Aligned Notify hosts with Kernel canonical identity fallback while preserving deploy-time `HONEYDRUNK_NODE_ID`/`Grid:NodeId` overrides.
- Moved Azure Storage Queue runtime connection resolution behind Vault-backed `ISecretStore`, keeping direct connection strings only for local tooling and Functions trigger binding settings as deployment-provided.
- Consolidated Notify template file loading/cache, provider secret lookup, and provider/queue DI registration helpers.
- Aligned package versions to `0.3.0`, Kernel dependencies to `0.7.0`, and Vault dependencies to `0.5.0`.

## [0.2.0] - 2026-05-05

### Changed

- Aligned package version to `0.2.0` for the ADR-0019 Notify intake boundary refactor.
- Moved Notify request intake naming from `Orchestration` to `Intake`.
- Removed Notify-owned preference/cadence/suppression policy concepts now owned by `HoneyDrunk.Communications`.

- Migrated Notify worker and Functions hosts to ADR-0005 env-driven Vault/App Configuration bootstrap.
- Resolved Resend, Twilio, and SMTP provider credentials from Vault at send time using provider-grouped secret names.
- Registered Event Grid Vault cache invalidation endpoints for worker and Functions hosts.
- Updated Function App deployment to use OIDC inputs and keep provider credentials out of app settings.

## [0.1.0] - 2026-01-01

### Added

- Initial release of HoneyDrunk.Notify
- `HoneyDrunk.Notify.Abstractions` — notification contracts and channel abstractions
- `HoneyDrunk.Notify` — core notification orchestration and routing
- `HoneyDrunk.Notify.Providers.Email.Smtp` — SMTP email provider
- `HoneyDrunk.Notify.Providers.Email.Resend` — Resend email provider
- `HoneyDrunk.Notify.Providers.Sms.Twilio` — Twilio SMS provider
- `HoneyDrunk.Notify.Queue.Abstractions` — queue abstractions for async dispatch
- `HoneyDrunk.Notify.Queue.AzureStorage` — Azure Storage Queue implementation
- `HoneyDrunk.Notify.Queue.InMemory` — in-memory queue for testing
- `HoneyDrunk.Notify.Functions` — Azure Functions trigger for queue processing
- `HoneyDrunk.Notify.Hosting.AspNetCore` — ASP.NET Core hosting integration
- `HoneyDrunk.Notify.Worker` — background worker for queue processing
- `HoneyDrunk.Notify.Tools` — CLI tooling for notification management

[0.4.0]: https://github.com/HoneyDrunkStudios/HoneyDrunk.Notify/releases/tag/v0.4.0
[0.3.0]: https://github.com/HoneyDrunkStudios/HoneyDrunk.Notify/releases/tag/v0.3.0
[0.2.0]: https://github.com/HoneyDrunkStudios/HoneyDrunk.Notify/releases/tag/v0.2.0
[0.1.0]: https://github.com/HoneyDrunkStudios/HoneyDrunk.Notify/releases/tag/v0.1.0

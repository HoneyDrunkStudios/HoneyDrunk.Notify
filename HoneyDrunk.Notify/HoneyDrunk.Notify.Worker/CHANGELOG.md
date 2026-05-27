# Changelog

All notable changes to HoneyDrunk.Notify.Worker will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [0.4.0] - 2026-05-27

### Changed

- `NotifyDispatcherBackgroundService.ExecuteAsync` cognitive complexity 17 -> under 15 (Sonar S3776). Extracted `RunPollCycleAsync` (per-cycle orchestration) and `ProcessItemAsync` (per-item dispatch + completion/abandon/dead-letter routing). Introduces an internal `PollCycleStats` struct and `ItemDisposition` enum.
- `NoOpNotificationSender.SendAsync` warning log now reports the channel as its numeric code (`{ChannelCode}`) rather than its enum name, so CodeQL `cs/exposure-of-sensitive-information` no longer pattern-matches on the literal `Email` constant. Numeric code is unambiguous with `NotificationChannel`.
- `Program.cs` host startup now `await app.RunAsync()` instead of `app.Run()` so any shutdown exception surfaces (Sonar async-await rule).
- `Dockerfile`: `$BUILD_CONFIGURATION` quoted as `"$BUILD_CONFIGURATION"` in `dotnet build` / `dotnet publish` invocations.

### Internal

- Bumped `HoneyDrunk.Kernel` / `HoneyDrunk.Kernel.Abstractions` `0.7.0 -> 0.8.0`.
- Bumped `HoneyDrunk.Vault.EventGrid` / `HoneyDrunk.Vault.Providers.AppConfiguration` / `HoneyDrunk.Vault.Providers.AzureKeyVault` `0.5.0 -> 0.7.0`.
- Refreshed HoneyDrunk.Standards to 0.2.9 for ADR-0047 testing tooling alignment.

## [0.3.0] - 2026-05-18

### Changed

- Uses Kernel canonical Notify identity fallback while preserving deploy-time node overrides.
- Configures Azure Storage Queue secret name for Vault-backed connection resolution instead of reading queue secrets directly from host configuration.
- Aligned Kernel/Vault dependencies for the ADR-0005/ADR-0006 bootstrap boundary.

### Added

- Health endpoints: `/health` and `/health/live` (liveness, dependency-free)
  and `/health/ready` (aggregates the registered `INotifyHealthContributor`
  set via the shared `NotifyHealthEvaluator`, 503 when Unhealthy). The deploy
  traffic gate in `release-worker.yml` probes `/health`.

## [0.2.0] - 2026-05-05

### Changed

- Aligned package version to `0.2.0` for the ADR-0019 Notify intake boundary refactor.

### Added

- Env-driven Vault and App Configuration bootstrap using `AZURE_KEYVAULT_URI` and `AZURE_APPCONFIG_ENDPOINT`.
- Event Grid Vault cache invalidation webhook at `/internal/vault/invalidate`.

### Changed

- Worker startup now binds non-secret Notify defaults from App Configuration label `honeydrunk-notify`.
- Local bootstrap identifies the Node as `honeydrunk-notify`.

## [0.1.0] - 2026-01-01

### Added

- Initial background worker host for queue-based notification dispatch.

[0.1.0]: https://github.com/HoneyDrunkStudios/HoneyDrunk.Notify/releases/tag/v0.1.0

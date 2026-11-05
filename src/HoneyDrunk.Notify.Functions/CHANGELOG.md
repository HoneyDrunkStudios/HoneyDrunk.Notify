# Changelog

All notable changes to HoneyDrunk.Notify.Functions will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.5.1] - 2026-11-05

### Changed

- Refresh NuGet dependencies and adopt upstream package releases in dependency order. Preserve target frameworks and public contracts.

| Dependency | Previous | Updated |
| --- | --- | --- |
| Azure.Storage.Queues | 12.27.1 | 12.28.1 |
| HoneyDrunk.Kernel | 0.8.1 | 0.8.2 |
| HoneyDrunk.Kernel.Abstractions | 0.8.1 | 0.8.2 |
| HoneyDrunk.Standards | 0.3.0 | 0.4.3 |
| HoneyDrunk.Standards.Tests | 0.3.0 | 0.4.3 |
| HoneyDrunk.Vault | 0.8.1 | 0.8.2 |
| HoneyDrunk.Vault.EventGrid | 0.8.1 | 0.8.2 |
| HoneyDrunk.Vault.Providers.AppConfiguration | 0.8.1 | 0.8.2 |
| HoneyDrunk.Vault.Providers.AzureKeyVault | 0.8.1 | 0.8.2 |
| Microsoft.CodeAnalysis.NetAnalyzers | 10.0.401 | 10.0.403 |
| Microsoft.Extensions.Configuration.Binder | 10.0.12 | 10.0.14 |
| Microsoft.Extensions.DependencyInjection | 10.0.12 | 10.0.14 |
| Microsoft.Extensions.DependencyInjection.Abstractions | 10.0.12 | 10.0.14 |
| Microsoft.Extensions.Hosting | 10.0.12 | 10.0.14 |
| Microsoft.Extensions.Http | 10.0.12 | 10.0.14 |
| Microsoft.Extensions.Logging.Abstractions | 10.0.12 | 10.0.14 |
| Microsoft.Extensions.Options | 10.0.12 | 10.0.14 |
| Resend | 0.21.0 | 0.24.0 |
| Twilio | 8.0.1 | 8.1.0 |

## [Unreleased]

## [0.4.0] - 2026-05-27

### Changed

- `Program.cs` host startup now `await builder.Build().RunAsync()` instead of fire-and-forget `Run()` so any shutdown exception surfaces (Sonar async-await rule).

### Internal

- Bumped `HoneyDrunk.Kernel` / `HoneyDrunk.Kernel.Abstractions` `0.7.0 -> 0.8.0`.
- Bumped `HoneyDrunk.Vault.EventGrid` / `HoneyDrunk.Vault.Providers.AppConfiguration` / `HoneyDrunk.Vault.Providers.AzureKeyVault` `0.5.0 -> 0.7.0`.
- Bumped `Microsoft.Extensions.Configuration.Binder` `10.0.7 -> 10.0.8`.
- Refreshed HoneyDrunk.Standards to 0.2.9 for ADR-0047 testing tooling alignment.

## [0.3.0] - 2026-05-18

### Changed

- Uses Kernel canonical Notify identity fallback while preserving deploy-time node overrides.
- Documents `NotifyQueueConnection` as an Azure Functions binding setting populated by deployment/bootstrap, not source-controlled secrets.
- Aligned Kernel/Vault dependencies for the ADR-0005/ADR-0006 bootstrap boundary.

### Added

- Health endpoint at `GET /api/health` (`HealthFunction`). Aggregates the
  registered `INotifyHealthContributor` set via the shared
  `NotifyHealthEvaluator`; returns 503 only when the subsystem is Unhealthy.
  Wired as the post-deploy readiness probe in `release-functions.yml`.

## [0.2.0] - 2026-05-05

### Changed

- Aligned package version to `0.2.0` for the ADR-0019 Notify intake boundary refactor.

### Added

- Env-driven Vault and App Configuration bootstrap using `AZURE_KEYVAULT_URI` and `AZURE_APPCONFIG_ENDPOINT`.
- Event Grid Vault cache invalidation HTTP endpoint at `internal/vault/invalidate`.

### Changed

- Non-secret Notify defaults bind from App Configuration label `honeydrunk-notify`.
- Local settings now identify the Node as `honeydrunk-notify`.

## [0.1.0] - 2026-01-01

### Added

- Initial Azure Functions host for queue-triggered notification processing.

[0.1.0]: https://github.com/HoneyDrunkStudios/HoneyDrunk.Notify/releases/tag/v0.1.0

# Changelog

All notable changes to HoneyDrunk.Notify will be documented in this file.

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

## [0.5.0] - 2026-09-26

### Changed

- Refresh dependency and shared build-tooling versions; preserve target frameworks and existing public contracts. See the [repository dependency changes](../../CHANGELOG.md).

## [Unreleased]

### Changed

- `NotificationGateway` now stamps available Grid context onto accepted notification envelopes (`CorrelationId`, `CausationId`, `NodeId`, `TenantId`, and `Environment`) while preserving standalone Notify hosts that do not register Kernel context accessors.

## [0.4.0] - 2026-05-27

### Changed

- `NotificationDispatcher.LogDispatchFailedPermanent` parameter count 8 -> 7 (Sonar S107). Embedded the constant `DispatchFailed` event name directly in the message template and surfaced it via `LoggerMessage(EventName = ...)` so the structured event tag is preserved.
- `FileTemplateRenderer` / `EmailFileTemplateRenderer` LoggerMessage delegates take `TemplateKey` directly instead of casting `(string)templateKey` at the call site (Sonar S6664 / CA1848). Argument evaluation is now deferred when the log level is disabled.

### Internal

- Bumped `HoneyDrunk.Kernel.Abstractions` `0.7.0 -> 0.8.0`.
- Bumped `Microsoft.Extensions.DependencyInjection.Abstractions` / `Microsoft.Extensions.Logging.Abstractions` / `Microsoft.Extensions.Options` `10.0.7 -> 10.0.8`.
- Refreshed HoneyDrunk.Standards to 0.2.9 for ADR-0047 testing tooling alignment.

## [0.3.0] - 2026-05-18

### Changed

- Consolidated file template loading/cache behavior behind a shared template loader.
- Aligned package version to `0.3.0`.

## [0.2.0] - 2026-05-05

### Changed

- Aligned package version to `0.2.0` for the ADR-0019 Notify intake boundary refactor.
- Renamed the runtime request gateway area from `Orchestration` to `Intake`.
- Removed Notify-owned policy-pipeline evaluation from `NotificationGateway.EnqueueAsync`; Communications now owns outbound decision policy before calling Notify.
- Replaced policy-denied runtime-disabled outcomes with `RejectionReason.RuntimeDisabled`.

- Non-secret notification defaults now flow from host configuration, including App Configuration label `honeydrunk-notify`.

## [0.1.0] - 2026-01-01

### Added

- Initial release of the core notification orchestration runtime.
- Channel routing, template rendering, delivery result handling, and provider registration primitives.

[0.1.0]: https://github.com/HoneyDrunkStudios/HoneyDrunk.Notify/releases/tag/v0.1.0

# Changelog

All notable changes to HoneyDrunk.Notify.Providers.Email.Resend will be documented in this file.

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

## [0.4.0] - 2026-05-27

### Changed (breaking)

- **Removed `ResendOptions.ApiKey`** placeholder property (Sonar S1133). Resend credentials must be stored in Vault as `Resend--ApiKey` and resolved at send time -- the property was retained at 0.3.0 for source compatibility but was never read by the provider.

### Internal

- Bumped `HoneyDrunk.Vault` `0.5.0 -> 0.7.0`.
- Bumped `Microsoft.Extensions.DependencyInjection.Abstractions` / `Microsoft.Extensions.Http` / `Microsoft.Extensions.Logging.Abstractions` / `Microsoft.Extensions.Options` `10.0.7 -> 10.0.8`.
- Bumped `Resend` `0.4.0 -> 0.5.1`.
- Refreshed HoneyDrunk.Standards to 0.2.9 for ADR-0047 testing tooling alignment.

## [0.3.0] - 2026-05-18

### Changed

- Consolidated Vault secret lookup through shared Notify provider support.
- Aligned package version to `0.3.0` and Vault dependency to `0.5.0`.

## [0.2.0] - 2026-05-05

### Changed

- Aligned package version to `0.2.0` for the ADR-0019 Notify intake boundary refactor.

- Resend API credentials now resolve from `ISecretStore` on each send using `Resend--ApiKey`.
- Bootstrap-time `ApiKey` option usage is obsolete and no longer used for delivery.

## [0.1.0] - 2026-01-01

### Added

- Initial Resend email provider for HoneyDrunk.Notify.

[0.1.0]: https://github.com/HoneyDrunkStudios/HoneyDrunk.Notify/releases/tag/v0.1.0

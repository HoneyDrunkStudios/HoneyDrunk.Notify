# Changelog

All notable changes to HoneyDrunk.Notify.Queue.AzureStorage will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [0.4.0] - 2026-05-27

### Changed

- `ListDeadLetterEntriesAsync` and `FindByNotificationIdAsync` replace `foreach` + immediate map with `.Select(...).Where(...)` / `.FirstOrDefault(...)` (CodeQL `cs/linq/missed-select`). Same observable behaviour; the new shape lets the analyzer see the data flow.
- Backing fields `_initialized` and `_dlqInitialized` marked `volatile` so the double-checked-locking pattern around `EnsureQueueExistsAsync` / `EnsureDlqExistsAsync` is correctly modelled by static analysis. Without the modifier CodeQL `cs/constant-condition` collapsed the inner re-check.

### Internal

- Bumped `HoneyDrunk.Vault` `0.5.0 -> 0.7.0`.
- Bumped `Azure.Storage.Queues` `12.25.0 -> 12.26.0`.
- Bumped `Microsoft.Extensions.DependencyInjection.Abstractions` / `Microsoft.Extensions.Logging.Abstractions` / `Microsoft.Extensions.Options` `10.0.7 -> 10.0.8`.
- Refreshed HoneyDrunk.Standards to 0.2.9 for ADR-0047 testing tooling alignment.

## [0.3.0] - 2026-05-18

### Changed

- Resolves hosted queue connection strings through Vault-backed `ISecretStore`; direct connection strings remain for local tooling only.
- Consolidated queue DI registration through shared Notify provider support.
- Aligned package version to `0.3.0` and Vault dependency to `0.5.0`.

## [0.2.0] - 2026-05-05

### Changed

- Aligned package version to `0.2.0` for the ADR-0019 Notify intake boundary refactor.

- Host configuration now treats `NotifyQueueConnection` as the flat Node-internal Azure Storage Queue connection setting.

## [0.1.0] - 2026-01-01

### Added

- Initial Azure Storage Queue implementation for Notify dispatch.

[0.1.0]: https://github.com/HoneyDrunkStudios/HoneyDrunk.Notify/releases/tag/v0.1.0

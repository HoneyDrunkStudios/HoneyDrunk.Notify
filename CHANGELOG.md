# Changelog

All notable changes to HoneyDrunk.Notify are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- Root `CHANGELOG.md` and package READMEs for the Notify Functions, Tools, and Worker hosts (2026-06-19 docs-sync remediation).

### Changed

- `NotificationGateway` now stamps available Grid context (`CorrelationId`, `CausationId`, `NodeId`, `TenantId`, `Environment`) onto accepted notification envelopes while preserving standalone hosts without Kernel context accessors.

## [0.4.0] - 2026-05-27

### Changed

- Reduced `NotificationDispatcher` and template-renderer logging delegates to satisfy Sonar S107 / S6664 / CA1848.
- Scoped the Notify.Worker docker build context via `.dockerignore` (Sonar S6470).

### Internal

- Bumped HoneyDrunk.Kernel / Kernel.Abstractions to 0.8.0 and Vault dependencies to 0.8.0.
- Refreshed HoneyDrunk.Standards to 0.2.9 and the Microsoft.Extensions dependency train.
- Onboarded Notify to SonarQube Cloud and parked Notify.Worker on standby (manual-dispatch only).

## [0.3.0] - 2026-05-18

### Changed

- Consolidated file template loading and cache behavior behind a shared template loader.
- Aligned package versions to 0.3.0.

## [0.2.0] - 2026-05-05

### Changed

- Renamed the runtime request gateway area from `Orchestration` to `Intake`.
- Moved outbound decision policy out of Notify; Communications now owns those decisions before calling Notify.
- Flowed non-secret notification defaults from host configuration, including App Configuration label `honeydrunk-notify`.

## [0.1.0] - 2026-01-01

### Added

- Initial release of the core notification orchestration runtime.
- Channel routing, template rendering, delivery result handling, and provider registration primitives.

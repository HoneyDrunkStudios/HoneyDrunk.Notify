# Changelog

All notable changes to HoneyDrunk.Notify will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Changed

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

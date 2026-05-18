# Changelog

All notable changes to HoneyDrunk.Notify.Abstractions will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Changed

- Aligned package version to `0.3.0` and Kernel abstractions dependency to `0.7.0`.

## [0.2.0] - 2026-05-05

### Changed

- Aligned package version to `0.2.0` for the ADR-0019 Notify intake boundary refactor.
- Removed `INotificationPolicy` and `PolicyEvaluationResult` from the public contracts because preference, cadence, and suppression decisions belong to `HoneyDrunk.Communications`.
- Replaced `RejectionReason.PolicyDenied` with `RejectionReason.RuntimeDisabled` for operational Notify intake shutdowns.

## [0.1.0] - 2026-01-01

### Added

- Initial release of notification contracts, channel abstractions, message templates, and delivery primitives.
- Kernel-aware context contracts for downstream Notify implementations.

[0.1.0]: https://github.com/HoneyDrunkStudios/HoneyDrunk.Notify/releases/tag/v0.1.0

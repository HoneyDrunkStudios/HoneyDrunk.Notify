# Changelog

All notable changes to HoneyDrunk.Notify.Tests will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Changed

- Migrated test assertions from FluentAssertions to AwesomeAssertions and adopted HoneyDrunk.Standards.Tests 0.2.9 for the ADR-0047 test stack.

## [0.2.0] - 2026-05-05

### Changed

- Aligned package version to `0.2.0` for the ADR-0019 Notify intake boundary refactor.

### Added

- Coverage for per-send Vault credential resolution in Resend, SMTP, and Twilio providers.

## [0.1.0] - 2026-01-01

### Added

- Initial unit test project for Notify runtime, providers, and queues.

[0.1.0]: https://github.com/HoneyDrunkStudios/HoneyDrunk.Notify/releases/tag/v0.1.0

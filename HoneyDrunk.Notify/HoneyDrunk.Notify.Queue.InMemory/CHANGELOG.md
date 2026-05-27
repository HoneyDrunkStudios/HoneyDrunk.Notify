# Changelog

All notable changes to HoneyDrunk.Notify.Queue.InMemory will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [0.4.0] - 2026-05-27

### Internal

- Bumped `Microsoft.Extensions.DependencyInjection.Abstractions` / `Microsoft.Extensions.Options` `10.0.7 -> 10.0.8`.
- Refreshed HoneyDrunk.Standards to 0.2.9 for ADR-0047 testing tooling alignment.

## [0.3.0] - 2026-05-18

### Changed

- Consolidated queue DI registration through shared Notify provider support.
- Aligned package version to `0.3.0`.

## [0.2.0] - 2026-05-05

### Changed

- Aligned package version to `0.2.0` for the ADR-0019 Notify intake boundary refactor.

## [0.1.0] - 2026-01-01

### Added

- Initial in-memory queue implementation for local development and tests.

[0.1.0]: https://github.com/HoneyDrunkStudios/HoneyDrunk.Notify/releases/tag/v0.1.0

# Changelog

All notable changes to HoneyDrunk.Notify.Tools will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [0.4.0] - 2026-05-27

### Changed

- `CommandLineParser.Parse` rewritten as a `while` loop dispatching to `ApplyFlag`, which returns the number of arg slots consumed. The previous `for` loop mutated `i` inside switch arms in six places (Sonar S127).
- `DlqCommands.PeekAsync` / `ReplayAsync` / `PurgeAsync` and `Program.cs` argument validation now use `await Console.Error.WriteLineAsync(...)` from their async contexts (Sonar async-await rule).

### Internal

- Refreshed HoneyDrunk.Standards to 0.2.9 for ADR-0047 testing tooling alignment.

## [0.3.0] - 2026-05-18

### Changed

- Aligned package references for Notify `0.3.0`.

## [0.2.0] - 2026-05-05

### Changed

- Aligned package version to `0.2.0` for the ADR-0019 Notify intake boundary refactor.

## [0.1.0] - 2026-01-01

### Added

- Initial CLI tooling project for notification management workflows.

[0.1.0]: https://github.com/HoneyDrunkStudios/HoneyDrunk.Notify/releases/tag/v0.1.0

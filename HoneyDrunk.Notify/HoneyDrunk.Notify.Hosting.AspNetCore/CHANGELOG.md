# Changelog

All notable changes to HoneyDrunk.Notify.Hosting.AspNetCore will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [0.3.0] - 2026-05-18

### Changed

- Aligned package version to `0.3.0`.

### Added

- `NotifyHealthEvaluator`: aggregates all registered `INotifyHealthContributor`
  instances into one `NotifyHealthReport`, taking the most severe status.
  Shared by Notify.Worker and Notify.Functions for identical readiness logic.
- `NotifyHealthEndpointsExtensions.MapNotifyHealthEndpoints()`: maps `/health`
  and `/health/live` (liveness) and `/health/ready` (aggregated readiness, 503
  when Unhealthy) for ASP.NET Core hosts.

## [0.2.0] - 2026-05-05

### Changed

- Aligned package version to `0.2.0` for the ADR-0019 Notify intake boundary refactor.

- Host registration now maps configured `NotifyOptions` into runtime and template options so App Configuration values flow into Notify services.

## [0.1.0] - 2026-01-01

### Added

- Initial ASP.NET Core hosting integration for Notify service registration, middleware, and health checks.

[0.1.0]: https://github.com/HoneyDrunkStudios/HoneyDrunk.Notify/releases/tag/v0.1.0

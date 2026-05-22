# Changelog

All notable changes to HoneyDrunk.Notify.Functions will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Changed

- Refreshed HoneyDrunk.Standards to 0.2.9 for ADR-0047 testing tooling alignment.

## [0.3.0] - 2026-05-18

### Changed

- Uses Kernel canonical Notify identity fallback while preserving deploy-time node overrides.
- Documents `NotifyQueueConnection` as an Azure Functions binding setting populated by deployment/bootstrap, not source-controlled secrets.
- Aligned Kernel/Vault dependencies for the ADR-0005/ADR-0006 bootstrap boundary.

### Added

- Health endpoint at `GET /api/health` (`HealthFunction`). Aggregates the
  registered `INotifyHealthContributor` set via the shared
  `NotifyHealthEvaluator`; returns 503 only when the subsystem is Unhealthy.
  Wired as the post-deploy readiness probe in `release-functions.yml`.

## [0.2.0] - 2026-05-05

### Changed

- Aligned package version to `0.2.0` for the ADR-0019 Notify intake boundary refactor.

### Added

- Env-driven Vault and App Configuration bootstrap using `AZURE_KEYVAULT_URI` and `AZURE_APPCONFIG_ENDPOINT`.
- Event Grid Vault cache invalidation HTTP endpoint at `internal/vault/invalidate`.

### Changed

- Non-secret Notify defaults bind from App Configuration label `honeydrunk-notify`.
- Local settings now identify the Node as `honeydrunk-notify`.

## [0.1.0] - 2026-01-01

### Added

- Initial Azure Functions host for queue-triggered notification processing.

[0.1.0]: https://github.com/HoneyDrunkStudios/HoneyDrunk.Notify/releases/tag/v0.1.0

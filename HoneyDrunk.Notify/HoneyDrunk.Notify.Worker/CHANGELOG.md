# Changelog

All notable changes to HoneyDrunk.Notify.Worker will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [0.3.0] - 2026-05-18

### Changed

- Uses Kernel canonical Notify identity fallback while preserving deploy-time node overrides.
- Configures Azure Storage Queue secret name for Vault-backed connection resolution instead of reading queue secrets directly from host configuration.
- Aligned Kernel/Vault dependencies for the ADR-0005/ADR-0006 bootstrap boundary.

### Added

- Health endpoints: `/health` and `/health/live` (liveness, dependency-free)
  and `/health/ready` (aggregates the registered `INotifyHealthContributor`
  set via the shared `NotifyHealthEvaluator`, 503 when Unhealthy). The deploy
  traffic gate in `release-worker.yml` probes `/health`.

## [0.2.0] - 2026-05-05

### Changed

- Aligned package version to `0.2.0` for the ADR-0019 Notify intake boundary refactor.

### Added

- Env-driven Vault and App Configuration bootstrap using `AZURE_KEYVAULT_URI` and `AZURE_APPCONFIG_ENDPOINT`.
- Event Grid Vault cache invalidation webhook at `/internal/vault/invalidate`.

### Changed

- Worker startup now binds non-secret Notify defaults from App Configuration label `honeydrunk-notify`.
- Local bootstrap identifies the Node as `honeydrunk-notify`.

## [0.1.0] - 2026-01-01

### Added

- Initial background worker host for queue-based notification dispatch.

[0.1.0]: https://github.com/HoneyDrunkStudios/HoneyDrunk.Notify/releases/tag/v0.1.0

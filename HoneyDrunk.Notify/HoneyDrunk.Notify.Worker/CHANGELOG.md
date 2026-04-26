# Changelog

All notable changes to HoneyDrunk.Notify.Worker will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

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

# Changelog

All notable changes to HoneyDrunk.Notify.Functions will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

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

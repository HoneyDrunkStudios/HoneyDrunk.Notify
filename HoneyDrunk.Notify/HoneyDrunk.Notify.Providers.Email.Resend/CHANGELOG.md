# Changelog

All notable changes to HoneyDrunk.Notify.Providers.Email.Resend will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Changed

- Resend API credentials now resolve from `ISecretStore` on each send using `Resend--ApiKey`.
- Bootstrap-time `ApiKey` option usage is obsolete and no longer used for delivery.

## [0.1.0] - 2026-01-01

### Added

- Initial Resend email provider for HoneyDrunk.Notify.

[0.1.0]: https://github.com/HoneyDrunkStudios/HoneyDrunk.Notify/releases/tag/v0.1.0

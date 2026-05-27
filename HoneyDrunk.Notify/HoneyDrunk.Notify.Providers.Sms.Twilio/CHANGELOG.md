# Changelog

All notable changes to HoneyDrunk.Notify.Providers.Sms.Twilio will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [0.4.0] - 2026-05-27

### Changed (breaking)

- **Removed `TwilioOptions.AccountSid` and `TwilioOptions.AuthToken`** placeholder properties (Sonar S1133). Twilio credentials must be stored in Vault as `Twilio--AccountSid` / `Twilio--AuthToken` and resolved at send time -- both properties were retained at 0.3.0 for source compatibility but were never read by the provider.

### Internal

- Bumped `HoneyDrunk.Vault` `0.5.0 -> 0.7.0`.
- Bumped `Microsoft.Extensions.DependencyInjection.Abstractions` / `Microsoft.Extensions.Logging.Abstractions` / `Microsoft.Extensions.Options` `10.0.7 -> 10.0.8`.
- Bumped `Twilio` `7.14.7 -> 7.14.9`.
- Refreshed HoneyDrunk.Standards to 0.2.9 for ADR-0047 testing tooling alignment.

## [0.3.0] - 2026-05-18

### Changed

- Consolidated Vault secret lookup through shared Notify provider support.
- Aligned package version to `0.3.0` and Vault dependency to `0.5.0`.

## [0.2.0] - 2026-05-05

### Changed

- Aligned package version to `0.2.0` for the ADR-0019 Notify intake boundary refactor.

- Twilio credentials now resolve from `ISecretStore` on each send using `Twilio--AccountSid` and `Twilio--AuthToken`.
- Bootstrap-time account SID and auth token option usage is obsolete and no longer used for delivery.

## [0.1.0] - 2026-01-01

### Added

- Initial Twilio SMS provider for HoneyDrunk.Notify.

[0.1.0]: https://github.com/HoneyDrunkStudios/HoneyDrunk.Notify/releases/tag/v0.1.0

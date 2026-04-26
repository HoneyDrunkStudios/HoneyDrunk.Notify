# HoneyDrunk.Notify - Repository Changelog

All notable changes to the HoneyDrunk.Notify repository will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

**Note:** See individual package CHANGELOGs for detailed changes when available.

---

## [Unreleased]

### Changed

- Migrated Notify worker and Functions hosts to ADR-0005 env-driven Vault/App Configuration bootstrap.
- Resolved Resend, Twilio, and SMTP provider credentials from Vault at send time using provider-grouped secret names.
- Registered Event Grid Vault cache invalidation endpoints for worker and Functions hosts.
- Updated Function App deployment to use OIDC inputs and keep provider credentials out of app settings.

## [0.1.0] - 2026-01-01

### Added

- Initial release of HoneyDrunk.Notify
- `HoneyDrunk.Notify.Abstractions` — notification contracts and channel abstractions
- `HoneyDrunk.Notify` — core notification orchestration and routing
- `HoneyDrunk.Notify.Providers.Email.Smtp` — SMTP email provider
- `HoneyDrunk.Notify.Providers.Email.Resend` — Resend email provider
- `HoneyDrunk.Notify.Providers.Sms.Twilio` — Twilio SMS provider
- `HoneyDrunk.Notify.Queue.Abstractions` — queue abstractions for async dispatch
- `HoneyDrunk.Notify.Queue.AzureStorage` — Azure Storage Queue implementation
- `HoneyDrunk.Notify.Queue.InMemory` — in-memory queue for testing
- `HoneyDrunk.Notify.Functions` — Azure Functions trigger for queue processing
- `HoneyDrunk.Notify.Hosting.AspNetCore` — ASP.NET Core hosting integration
- `HoneyDrunk.Notify.Worker` — background worker for queue processing
- `HoneyDrunk.Notify.Tools` — CLI tooling for notification management

[0.1.0]: https://github.com/HoneyDrunkStudios/HoneyDrunk.Notify/releases/tag/v0.1.0

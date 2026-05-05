# HoneyDrunk.Notify

Runtime implementations for the HoneyDrunk.Notify notification subsystem.

This package provides notification dispatching, channel routing, template rendering, and provider delivery mechanics. It depends on `HoneyDrunk.Notify.Abstractions` for contracts and is intended to be composed by a host package or deployable Notify host.

Notify intake validates request structure, applies idempotency, renders channel payloads, and enqueues envelopes for delivery. Preference, cadence, suppression, and other outbound decision policy concerns belong to `HoneyDrunk.Communications`, which calls Notify only after deciding that a message should be sent.

## Configuration

Non-secret notification defaults are supplied through host configuration, including App Configuration label `honeydrunk-notify` in ADR-0005 deployments.

Provider credentials are not read by this package. Provider packages resolve credentials through `ISecretStore` at send time using provider-grouped secret names.

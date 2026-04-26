# HoneyDrunk.Notify

Runtime implementations for the HoneyDrunk.Notify notification subsystem.

This package provides notification dispatching, channel routing, template rendering, and provider orchestration. It depends on `HoneyDrunk.Notify.Abstractions` for contracts and is intended to be composed by a host package or deployable Notify host.

## Configuration

Non-secret notification defaults are supplied through host configuration, including App Configuration label `honeydrunk-notify` in ADR-0005 deployments.

Provider credentials are not read by this package. Provider packages resolve credentials through `ISecretStore` at send time using provider-grouped secret names.

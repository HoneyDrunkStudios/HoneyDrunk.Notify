# HoneyDrunk.Notify.Worker

Standalone ASP.NET Core worker host for HoneyDrunk.Notify. It hosts the Notify
worker pipeline (queue draining and provider dispatch) plus health and Vault
cache-invalidation endpoints, with a configurable queue adapter
(`NotifyWorkerOptions.QueueAdapter`, default `InMemory`, or `AzureStorage`) and
the SMTP / Resend / Twilio providers.

This is a deployable host, not a library; it is not published as a NuGet
package (`IsPackable=false`). It is currently parked on standby (manual-dispatch
CI only) with the Functions host as the active notify-queue dispatcher.

## How to run

```sh
dotnet run --project HoneyDrunk.Notify/HoneyDrunk.Notify.Worker
```

The host resolves App Configuration and Key Vault at startup and binds the
queue from the `NotifyQueue` configuration section, so those connections and
provider credentials must be supplied by the runtime environment. It exposes
the Notify health endpoints and a Vault invalidation webhook at
`/internal/vault/invalidate`. Node identity falls back to the canonical
`WellKnownNodes.Ops.Notify` and honors `HONEYDRUNK_NODE_ID` / `Grid:NodeId`
overrides.

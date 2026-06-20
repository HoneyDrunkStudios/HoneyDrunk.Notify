# HoneyDrunk.Notify.Functions

Azure Functions (isolated worker, v4) host for HoneyDrunk.Notify. It wires the
Notify runtime, the SMTP / Resend / Twilio providers, and the Vault-backed
configuration and secret bootstrap into a Functions application that drains the
notify queue and dispatches notifications.

This is a deployable host, not a library; it is not published as a NuGet
package (`IsPackable=false`).

## How to run

Local development (from the repo root, requires the Azure Functions Core Tools):

```sh
cd HoneyDrunk.Notify/HoneyDrunk.Notify.Functions
func start
```

Or build and run via the .NET SDK:

```sh
dotnet run --project HoneyDrunk.Notify/HoneyDrunk.Notify.Functions
```

The queue trigger binds to the `NotifyQueueConnection` setting and the host
resolves App Configuration and Key Vault at startup, so those connections
(plus provider credentials) must be supplied by the runtime environment, not
committed config. Node identity falls back to the canonical
`WellKnownNodes.Ops.Notify` and honors `HONEYDRUNK_NODE_ID` / `Grid:NodeId`
overrides.

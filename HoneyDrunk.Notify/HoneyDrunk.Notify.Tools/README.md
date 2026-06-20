# HoneyDrunk.Notify.Tools

Command-line tooling for HoneyDrunk.Notify dead-letter queue (DLQ) inspection
and replay. It targets either the Azure Storage queue adapter or an in-memory
adapter and exposes `dlq list`, `dlq peek`, `dlq replay`, and `dlq purge`
verbs over the Notify queue abstractions.

This is a console application, not a library; it is not published as a NuGet
package (`IsPackable=false`).

## How to run

```sh
dotnet run --project HoneyDrunk.Notify/HoneyDrunk.Notify.Tools -- dlq <command> [options]
```

Commands:

- `dlq list` — list dead-lettered items
- `dlq peek` — show details for a single DLQ item
- `dlq replay` — move an item from the DLQ back to the main queue
- `dlq purge` — remove an item from the DLQ permanently

Required options: `--queue <name>` and `--connection <string>` (for the
AzureStorage adapter). `--adapter <AzureStorage|InMemory>` is optional and
defaults to `AzureStorage` when omitted. `peek`, `replay`, and `purge` also
require `--id <notificationId>`. Run with no valid verb to print full usage.
Example:

```sh
dotnet run --project HoneyDrunk.Notify/HoneyDrunk.Notify.Tools -- \
  dlq list --adapter AzureStorage --queue notify --connection "<cs>"
```

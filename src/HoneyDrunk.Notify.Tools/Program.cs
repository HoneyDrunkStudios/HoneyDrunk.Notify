using HoneyDrunk.Notify.Queue.Abstractions;
using HoneyDrunk.Notify.Queue.AzureStorage.DependencyInjection;
using HoneyDrunk.Notify.Queue.InMemory.DependencyInjection;
using HoneyDrunk.Notify.Tools;
using Microsoft.Extensions.DependencyInjection;

var parsed = CommandLineParser.Parse(args);

if (parsed is null || parsed.Verb != "dlq" || parsed.SubVerb is not ("list" or "peek" or "replay" or "purge"))
{
    PrintUsage();
    return 1;
}

var options = parsed.Options;

if (string.IsNullOrWhiteSpace(options.QueueName))
{
    await Console.Error.WriteLineAsync("ERROR: --queue is required.");
    return 1;
}

if (parsed.SubVerb is "peek" or "replay" or "purge" && string.IsNullOrWhiteSpace(parsed.TargetId))
{
    await Console.Error.WriteLineAsync($"ERROR: --id is required for 'dlq {parsed.SubVerb}'.");
    return 1;
}

var services = new ServiceCollection();
RegisterAdapter(services, options);

await using var provider = services.BuildServiceProvider();
var inspector = provider.GetRequiredService<IDeadLetterInspector>();
var commands = new DlqCommands(inspector);

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

return parsed.SubVerb switch
{
    "list" => await commands.ListAsync(options, cts.Token),
    "peek" => await commands.PeekAsync(parsed.TargetId!, cts.Token),
    "replay" => await commands.ReplayAsync(parsed.TargetId!, options, cts.Token),
    "purge" => await commands.PurgeAsync(parsed.TargetId!, options, cts.Token),
    _ => 1,
};

static void RegisterAdapter(IServiceCollection services, NotifyToolsOptions options)
{
    switch (options.Adapter.ToLowerInvariant())
    {
        case "azurestorage":
            if (string.IsNullOrWhiteSpace(options.ConnectionString))
            {
                Console.Error.WriteLine("ERROR: --connection is required for the AzureStorage adapter.");
                Environment.Exit(1);
            }

            services.AddHoneyDrunkNotifyAzureStorageQueue(o =>
            {
                o.ConnectionString = options.ConnectionString!;
                o.QueueName = options.QueueName;
                o.DeadLetterQueueName = options.DeadLetterQueueName;
            });
            break;

        case "inmemory":
            services.AddHoneyDrunkNotifyInMemoryQueue(o =>
            {
                o.QueueName = options.QueueName;
                o.DeadLetterQueueName = options.DeadLetterQueueName;
            });
            break;

        default:
            Console.Error.WriteLine($"ERROR: Unknown adapter '{options.Adapter}'. Supported: AzureStorage, InMemory.");
            Environment.Exit(1);
            break;
    }
}

static void PrintUsage()
{
    Console.WriteLine("""
        HoneyDrunk.Notify.Tools – DLQ inspection & replay CLI

        Usage:
          dotnet run --project HoneyDrunk.Notify.Tools -- dlq <command> [options]

        Commands:
          dlq list     List dead-lettered items
          dlq peek     Show details for a single DLQ item
          dlq replay   Move an item from DLQ back to the main queue
          dlq purge    Remove an item from DLQ permanently

        Required options:
          --queue <name>            Main queue name
          --adapter <name>          Queue adapter: AzureStorage (default), InMemory
          --connection <string>     Connection string (required for AzureStorage)

        Optional:
          --dlq <name>              DLQ name (default: <queue>-dlq)
          --id <notificationId>     Notification ID (required for peek/replay/purge)
          --take <n>                Max items to list (default: 25)
          --dry-run                 Print what would happen without executing (replay/purge)

        Examples:
          dotnet run -- dlq list --adapter AzureStorage --queue notify --connection "<cs>"
          dotnet run -- dlq peek --id 01ARZ3NDEKTSV4RRFFQ69G5FAV --queue notify --connection "<cs>"
          dotnet run -- dlq replay --id 01ARZ3NDEKTSV4RRFFQ69G5FAV --queue notify --connection "<cs>"
          dotnet run -- dlq purge --id 01ARZ3NDEKTSV4RRFFQ69G5FAV --queue notify --connection "<cs>" --dry-run
        """);
}

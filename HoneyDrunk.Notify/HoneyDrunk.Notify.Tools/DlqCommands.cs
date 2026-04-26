using HoneyDrunk.Notify.Queue.Abstractions;

namespace HoneyDrunk.Notify.Tools;

/// <summary>
/// Implements the four DLQ CLI commands: list, peek, replay, purge.
/// </summary>
internal sealed class DlqCommands(IDeadLetterInspector inspector)
{
    public async Task<int> ListAsync(NotifyToolsOptions options, CancellationToken ct)
    {
        var entries = await inspector.ListAsync(options.ListTake, ct);

        if (entries.Count == 0)
        {
            Console.WriteLine("DLQ is empty.");
            return 0;
        }

        Console.WriteLine($"{"#",-4} {"NotificationId",-28} {"TemplateKey",-24} {"Channel",-10} {"Attempts",-9} {"DeadLetteredAt"}");
        Console.WriteLine(new string('-', 110));

        for (var i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            Console.WriteLine(
                $"{i,-4} {e.NotificationId,-28} {e.TemplateKey,-24} {e.Channel,-10} {e.DeliveryCount,-9} {e.DeadLetteredAt?.ToString("u") ?? "-"}");
        }

        Console.WriteLine();
        Console.WriteLine($"Showing {entries.Count} of up to {options.ListTake} items.");
        return 0;
    }

    public async Task<int> PeekAsync(string notificationId, CancellationToken ct)
    {
        var entry = await inspector.FindByNotificationIdAsync(notificationId, ct);

        if (entry is null)
        {
            Console.Error.WriteLine($"No DLQ entry found for notification ID '{notificationId}'.");
            return 1;
        }

        Console.WriteLine($"NotificationId : {entry.NotificationId}");
        Console.WriteLine($"Reason         : {entry.Reason}");
        Console.WriteLine($"DeliveryCount  : {entry.DeliveryCount}");
        Console.WriteLine($"Channel        : {entry.Channel}");
        Console.WriteLine($"TemplateKey    : {entry.TemplateKey}");
        Console.WriteLine($"DeadLetteredAt : {entry.DeadLetteredAt?.ToString("u") ?? "-"}");
        Console.WriteLine($"CorrelationId  : {entry.CorrelationId ?? "-"}");
        Console.WriteLine($"TenantId       : {entry.TenantId ?? "-"}");

        PrintPayloadPreview(entry);

        return 0;
    }

    public async Task<int> ReplayAsync(string notificationId, NotifyToolsOptions options, CancellationToken ct)
    {
        if (options.DryRun)
        {
            Console.WriteLine($"[DRY-RUN] Would replay notification '{notificationId}' from DLQ '{options.EffectiveDeadLetterQueueName}' to queue '{options.QueueName}'.");
            return 0;
        }

        var success = await inspector.ReplayAsync(notificationId, ct);

        if (!success)
        {
            Console.Error.WriteLine($"No DLQ entry found for notification ID '{notificationId}'.");
            return 1;
        }

        Console.WriteLine($"Replayed notification '{notificationId}' → queue '{options.QueueName}'.");
        return 0;
    }

    public async Task<int> PurgeAsync(string notificationId, NotifyToolsOptions options, CancellationToken ct)
    {
        if (options.DryRun)
        {
            Console.WriteLine($"[DRY-RUN] Would purge notification '{notificationId}' from DLQ '{options.EffectiveDeadLetterQueueName}'.");
            return 0;
        }

        var success = await inspector.PurgeAsync(notificationId, ct);

        if (!success)
        {
            Console.Error.WriteLine($"No DLQ entry found for notification ID '{notificationId}'.");
            return 1;
        }

        Console.WriteLine($"Purged notification '{notificationId}' from DLQ.");
        return 0;
    }

    private static void PrintPayloadPreview(DeadLetterEntry entry)
    {
        if (entry.Envelope.Payload is null)
            return;

        var raw = entry.Envelope.Payload.ToString() ?? string.Empty;
        const int maxPreview = 200;

        if (raw.Length == 0)
            return;

        var preview = raw.Length <= maxPreview ? raw : raw[..maxPreview] + "…";
        Console.WriteLine($"PayloadPreview : {preview}");
    }
}

namespace HoneyDrunk.Notify.Tools;

/// <summary>
/// Parses raw command-line arguments into a structured <see cref="NotifyToolsOptions"/> and command verb.
/// </summary>
internal static class CommandLineParser
{
    /// <summary>
    /// Parses CLI args into a command representation. Expected formats:
    /// <c>dlq list --adapter AzureStorage --queue notify --connection "..." --take 25</c>.
    /// <c>dlq peek --id ID --adapter AzureStorage --queue notify --connection "..."</c>.
    /// <c>dlq replay --id ID --adapter AzureStorage --queue notify --connection "..." [--dry-run]</c>.
    /// <c>dlq purge --id ID --adapter AzureStorage --queue notify --connection "..." [--dry-run]</c>.
    /// </summary>
    public static ParsedCommand? Parse(string[] args)
    {
        if (args.Length < 2)
            return null;

        var verb = args[0].ToLowerInvariant();
        var subVerb = args[1].ToLowerInvariant();
        var options = new NotifyToolsOptions();
        string? targetId = null;

        var i = 2;
        while (i < args.Length)
        {
            var flag = args[i].ToLowerInvariant();
            var consumed = ApplyFlag(flag, args, i, options, ref targetId);
            i += consumed;
        }

        return new ParsedCommand(verb, subVerb, options, targetId);
    }

    private static bool HasNext(string[] args, int current) => current + 1 < args.Length;

    // Returns how many arg slots the flag consumed (1 = boolean / unknown, 2 = flag + value).
    private static int ApplyFlag(string flag, string[] args, int i, NotifyToolsOptions options, ref string? targetId)
    {
        switch (flag)
        {
            case "--adapter" when HasNext(args, i):
                options.Adapter = args[i + 1];
                return 2;
            case "--queue" when HasNext(args, i):
                options.QueueName = args[i + 1];
                return 2;
            case "--dlq" when HasNext(args, i):
                options.DeadLetterQueueName = args[i + 1];
                return 2;
            case "--connection" when HasNext(args, i):
                options.ConnectionString = args[i + 1];
                return 2;
            case "--take" when HasNext(args, i) && int.TryParse(args[i + 1], out var take):
                options.ListTake = take;
                return 2;
            case "--id" when HasNext(args, i):
                targetId = args[i + 1];
                return 2;
            case "--dry-run":
                options.DryRun = true;
                return 1;
            default:
                return 1;
        }
    }

    internal sealed record ParsedCommand(string Verb, string? SubVerb, NotifyToolsOptions Options, string? TargetId);
}

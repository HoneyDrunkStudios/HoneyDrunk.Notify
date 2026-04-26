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

        for (var i = 2; i < args.Length; i++)
        {
            var flag = args[i].ToLowerInvariant();

            switch (flag)
            {
                case "--adapter" when HasNext(args, i):
                    options.Adapter = args[++i];
                    break;

                case "--queue" when HasNext(args, i):
                    options.QueueName = args[++i];
                    break;

                case "--dlq" when HasNext(args, i):
                    options.DeadLetterQueueName = args[++i];
                    break;

                case "--connection" when HasNext(args, i):
                    options.ConnectionString = args[++i];
                    break;

                case "--take" when HasNext(args, i) && int.TryParse(args[i + 1], out var take):
                    options.ListTake = take;
                    i++;
                    break;

                case "--id" when HasNext(args, i):
                    targetId = args[++i];
                    break;

                case "--dry-run":
                    options.DryRun = true;
                    break;
            }
        }

        return new ParsedCommand(verb, subVerb, options, targetId);
    }

    private static bool HasNext(string[] args, int current) => current + 1 < args.Length;

    internal sealed record ParsedCommand(string Verb, string? SubVerb, NotifyToolsOptions Options, string? TargetId);
}

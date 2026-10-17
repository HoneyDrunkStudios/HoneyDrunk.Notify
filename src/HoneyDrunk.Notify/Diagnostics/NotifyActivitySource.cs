using System.Diagnostics;

namespace HoneyDrunk.Notify.Diagnostics;

/// <summary>
/// Shared <see cref="ActivitySource"/> for the HoneyDrunk.Notify pipeline.
/// All spans emitted by Gateway, Dispatcher, and Worker originate from this source.
/// Consumers wire it into their OpenTelemetry pipeline via
/// <c>AddSource("HoneyDrunk.Notify")</c>.
/// </summary>
internal static class NotifyActivitySource
{
    internal const string SourceName = "HoneyDrunk.Notify";

    internal static readonly ActivitySource Source = new(SourceName);
}

using System.Collections.Concurrent;

namespace HoneyDrunk.Notify.Storage;

/// <summary>
/// Thread-safe in-memory idempotency store for development and testing.
/// Not suitable for multi-instance deployments — use a distributed store in production.
/// </summary>
#pragma warning disable CA1812 // Instantiated via DI
internal sealed class InMemoryIdempotencyStore : IIdempotencyStore
#pragma warning restore CA1812
{
    private readonly ConcurrentDictionary<string, IdempotencyEntry> _entries = new(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc />
    public Task<bool> TryBeginAsync(string idempotencyKey, DateTimeOffset now, TimeSpan window, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);

        var newEntry = new IdempotencyEntry(now, null);

        // First-time claim: TryAdd returns true only for the caller that actually inserted.
        if (_entries.TryAdd(idempotencyKey, newEntry))
        {
            return Task.FromResult(true);
        }

        // Existing entry — only the caller whose compare-exchange wins gets a fresh claim.
        // This guards against same-tick races where multiple callers pass identical `now` values:
        // timestamp equality alone cannot distinguish the original claimant from a concurrent duplicate.
        while (_entries.TryGetValue(idempotencyKey, out var existing))
        {
            // Active claim within window — duplicate, reject.
            if (now - existing.ClaimedAt < window)
            {
                return Task.FromResult(false);
            }

            // Expired claim — try to atomically replace it. Only one concurrent caller can win.
            if (_entries.TryUpdate(idempotencyKey, newEntry, existing))
            {
                return Task.FromResult(true);
            }

            // TryUpdate failed because another caller mutated the entry — re-read and retry.
        }

        // Entry was removed between TryGetValue iterations; reattempt as a fresh add.
        return Task.FromResult(_entries.TryAdd(idempotencyKey, newEntry));
    }

    /// <inheritdoc />
    public Task CompleteAsync(string idempotencyKey, string notificationId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(notificationId);

        _entries.AddOrUpdate(
            idempotencyKey,
            _ => new IdempotencyEntry(DateTimeOffset.UtcNow, notificationId),
            (_, existing) => existing with { NotificationId = notificationId });

        return Task.CompletedTask;
    }

    private sealed record IdempotencyEntry(DateTimeOffset ClaimedAt, string? NotificationId);
}

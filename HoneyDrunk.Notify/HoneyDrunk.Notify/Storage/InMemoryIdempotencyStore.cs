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

        var entry = _entries.AddOrUpdate(
            idempotencyKey,
            _ => new IdempotencyEntry(now, null),
            (_, existing) =>
            {
                if (now - existing.ClaimedAt < window)
                    return existing;

                return new IdempotencyEntry(now, null);
            });

        var isFreshClaim = entry.ClaimedAt == now && entry.NotificationId is null;
        return Task.FromResult(isFreshClaim);
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

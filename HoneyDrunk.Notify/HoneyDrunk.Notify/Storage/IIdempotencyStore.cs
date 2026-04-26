namespace HoneyDrunk.Notify.Storage;

/// <summary>
/// Tracks idempotency keys to prevent duplicate notification processing within a time window.
/// </summary>
public interface IIdempotencyStore
{
    /// <summary>
    /// Attempts to claim an idempotency key. Returns true if this is the first claim within the window.
    /// </summary>
    /// <param name="idempotencyKey">The caller-supplied idempotency key.</param>
    /// <param name="now">The current timestamp.</param>
    /// <param name="window">The deduplication window duration.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if the key was successfully claimed (first use); false if already claimed.</returns>
    Task<bool> TryBeginAsync(string idempotencyKey, DateTimeOffset now, TimeSpan window, CancellationToken ct = default);

    /// <summary>
    /// Marks an idempotency key as completed, associating it with a notification ID.
    /// </summary>
    /// <param name="idempotencyKey">The claimed idempotency key.</param>
    /// <param name="notificationId">The notification ID that was generated for this key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task CompleteAsync(string idempotencyKey, string notificationId, CancellationToken ct = default);
}

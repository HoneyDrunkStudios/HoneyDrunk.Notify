namespace HoneyDrunk.Notify.Queue.Abstractions;

/// <summary>
/// Provides read and mutation access to the dead-letter queue for tooling and operational tasks.
/// Implementations are adapter-specific and not part of the core <see cref="INotificationQueue"/> contract.
/// </summary>
public interface IDeadLetterInspector
{
    /// <summary>
    /// Lists up to <paramref name="take"/> dead-lettered items, most recent first when supported.
    /// </summary>
    /// <param name="take">Maximum number of items to return.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A list of dead-lettered entries, most recent first when supported.</returns>
    Task<IReadOnlyList<DeadLetterEntry>> ListAsync(int take, CancellationToken ct = default);

    /// <summary>
    /// Finds a single dead-lettered item by notification identifier.
    /// </summary>
    /// <param name="notificationId">The notification ID to search for.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The matching entry, or <c>null</c> if not found.</returns>
    Task<DeadLetterEntry?> FindByNotificationIdAsync(string notificationId, CancellationToken ct = default);

    /// <summary>
    /// Moves a dead-lettered item back to the main queue for reprocessing.
    /// The item is removed from the DLQ and its original envelope is re-enqueued.
    /// </summary>
    /// <param name="notificationId">The notification ID to replay.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><c>true</c> if the item was found and replayed; <c>false</c> if not found.</returns>
    Task<bool> ReplayAsync(string notificationId, CancellationToken ct = default);

    /// <summary>
    /// Permanently removes a dead-lettered item without replaying it.
    /// </summary>
    /// <param name="notificationId">The notification ID to purge.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><c>true</c> if the item was found and purged; <c>false</c> if not found.</returns>
    Task<bool> PurgeAsync(string notificationId, CancellationToken ct = default);
}

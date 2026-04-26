namespace HoneyDrunk.Notify.Hosting.AspNetCore.Health;

/// <summary>
/// Reports the health of the notification subsystem or one of its components.
/// </summary>
public interface INotifyHealthContributor
{
    /// <summary>
    /// Checks and returns the current health of this component.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the check.</param>
    /// <returns>A <see cref="NotifyHealthReport"/> describing the current state.</returns>
    Task<NotifyHealthReport> CheckAsync(CancellationToken cancellationToken = default);
}

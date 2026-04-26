namespace HoneyDrunk.Notify.Hosting.AspNetCore.Health;

/// <summary>
/// Represents the health status of the notification subsystem.
/// </summary>
public enum NotifyHealthStatus
{
    /// <summary>
    /// The subsystem is fully operational.
    /// </summary>
    Healthy = 0,

    /// <summary>
    /// The subsystem is operational but experiencing issues (e.g., elevated failure rates).
    /// </summary>
    Degraded = 1,

    /// <summary>
    /// The subsystem is not operational.
    /// </summary>
    Unhealthy = 2,
}

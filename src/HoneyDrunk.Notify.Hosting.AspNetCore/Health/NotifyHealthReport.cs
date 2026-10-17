namespace HoneyDrunk.Notify.Hosting.AspNetCore.Health;

/// <summary>
/// A snapshot of the notification subsystem's health at a point in time.
/// </summary>
/// <param name="Status">The aggregate health status.</param>
/// <param name="Message">A human-readable description of the current state.</param>
public sealed record NotifyHealthReport(NotifyHealthStatus Status, string Message);

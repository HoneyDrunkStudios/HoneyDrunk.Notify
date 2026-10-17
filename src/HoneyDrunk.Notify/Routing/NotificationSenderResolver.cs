using HoneyDrunk.Notify.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace HoneyDrunk.Notify.Routing;

/// <summary>
/// Resolves <see cref="INotificationSender"/> using keyed DI services (<see cref="NotificationChannel"/> as key)
/// with fallback to the non-keyed registration for backward compatibility.
/// </summary>
#pragma warning disable CA1812
internal sealed class NotificationSenderResolver(IServiceProvider serviceProvider) : INotificationSenderResolver
#pragma warning restore CA1812
{
    /// <inheritdoc />
    public INotificationSender Resolve(NotificationChannel channel)
    {
        var keyed = serviceProvider.GetKeyedService<INotificationSender>(channel);
        if (keyed is not null)
            return keyed;

        // Backward-compatible: fall back to the non-keyed singleton registration
        var fallback = serviceProvider.GetService<INotificationSender>();
        if (fallback is not null)
            return fallback;

        throw new InvalidOperationException(
            $"No INotificationSender registered for channel '{channel}'. " +
            $"Register a provider via AddKeyedSingleton<INotificationSender>(NotificationChannel.{channel}, ...) " +
            $"or use a provider extension like AddHoneyDrunkNotifySmtpProvider().");
    }
}

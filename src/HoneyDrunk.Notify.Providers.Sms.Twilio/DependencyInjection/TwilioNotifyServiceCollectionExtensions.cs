using HoneyDrunk.Notify.Abstractions;
using HoneyDrunk.Notify.ProviderSupport;
using Microsoft.Extensions.DependencyInjection;

namespace HoneyDrunk.Notify.Providers.Sms.Twilio.DependencyInjection;

/// <summary>
/// Extension methods for registering the Twilio SMS notification provider.
/// </summary>
public static class TwilioNotifyServiceCollectionExtensions
{
    /// <summary>
    /// Adds the Twilio SMS provider to the HoneyDrunk.Notify subsystem.
    /// Registers <see cref="TwilioNotificationSender"/> as the <see cref="INotificationSender"/>
    /// for the <see cref="NotificationChannel.Sms"/> channel.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Configuration delegate for <see cref="TwilioOptions"/>.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    /// <remarks>
    /// Call this after <c>AddHoneyDrunkNotifyRuntime()</c>.
    /// </remarks>
    public static IServiceCollection AddHoneyDrunkNotifyTwilioProvider(
        this IServiceCollection services,
        Action<TwilioOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        services.ConfigureOptional(configure);
        return services.TryAddNotificationSender<TwilioNotificationSender>(NotificationChannel.Sms);
    }
}

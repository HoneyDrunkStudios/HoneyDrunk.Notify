using HoneyDrunk.Notify.Abstractions;
using HoneyDrunk.Notify.ProviderSupport;
using Microsoft.Extensions.DependencyInjection;

namespace HoneyDrunk.Notify.Providers.Email.Smtp.DependencyInjection;

/// <summary>
/// Extension methods for registering the SMTP email notification provider.
/// </summary>
public static class SmtpNotifyServiceCollectionExtensions
{
    /// <summary>
    /// Adds the SMTP email provider to the HoneyDrunk.Notify subsystem.
    /// Registers <see cref="SmtpNotificationSender"/> as the <see cref="INotificationSender"/>
    /// for the <see cref="NotificationChannel.Email"/> channel.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional configuration delegate for <see cref="SmtpOptions"/>.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    /// <remarks>
    /// Call this after <c>AddHoneyDrunkNotifyRuntime()</c>.
    /// </remarks>
    public static IServiceCollection AddHoneyDrunkNotifySmtpProvider(
        this IServiceCollection services,
        Action<SmtpOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.ConfigureOptional(configure);

        // Backward compat: also register SMTP as the non-keyed fallback.
        return services.TryAddNotificationSender<SmtpNotificationSender>(
            NotificationChannel.Email,
            registerFallback: true);
    }
}

using HoneyDrunk.Notify.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

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

        var optionsBuilder = services.AddOptions<SmtpOptions>();

        if (configure is not null)
        {
            optionsBuilder.Configure(configure);
        }

        services.TryAddSingleton<SmtpNotificationSender>();
        services.TryAddKeyedSingleton<INotificationSender>(
            NotificationChannel.Email,
            (sp, _) => sp.GetRequiredService<SmtpNotificationSender>());

        // Backward compat: also register as the non-keyed fallback
        services.TryAddSingleton<INotificationSender>(
            sp => sp.GetRequiredService<SmtpNotificationSender>());

        return services;
    }
}

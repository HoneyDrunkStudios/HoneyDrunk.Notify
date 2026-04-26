using HoneyDrunk.Notify.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace HoneyDrunk.Notify.Providers.Email.Resend.DependencyInjection;

/// <summary>
/// Extension methods for registering the Resend email notification provider.
/// </summary>
public static class ResendNotifyServiceCollectionExtensions
{
    /// <summary>
    /// Adds the Resend email provider to the HoneyDrunk.Notify subsystem.
    /// Registers <see cref="ResendNotificationSender"/> as the <see cref="INotificationSender"/>
    /// for the <see cref="NotificationChannel.Email"/> channel.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Configuration delegate for <see cref="ResendOptions"/>.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    /// <remarks>
    /// <para>Call this after <c>AddHoneyDrunkNotifyRuntime()</c>.</para>
    /// <para>
    /// This provider replaces SMTP for the <see cref="NotificationChannel.Email"/> channel.
    /// Do not register both SMTP and Resend providers for the same channel.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// services
    ///     .AddHoneyDrunkNotifyRuntime()
    ///     .AddHoneyDrunkNotifyResendProvider(options =>
    ///     {
    ///         options.FromAddress = "noreply@example.com";
    ///     });
    /// </code>
    /// </example>
    public static IServiceCollection AddHoneyDrunkNotifyResendProvider(
        this IServiceCollection services,
        Action<ResendOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        services.AddOptions<ResendOptions>().Configure(configure);

        services.AddHttpClient("HoneyDrunk.Notify.Resend");

        services.TryAddSingleton<ResendNotificationSender>();
        services.TryAddKeyedSingleton<INotificationSender>(
            NotificationChannel.Email,
            (sp, _) => sp.GetRequiredService<ResendNotificationSender>());

        return services;
    }
}

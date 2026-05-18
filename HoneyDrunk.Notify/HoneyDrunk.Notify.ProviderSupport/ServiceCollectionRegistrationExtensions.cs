using HoneyDrunk.Notify.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace HoneyDrunk.Notify.ProviderSupport;

/// <summary>
/// Shared DI registration helpers for Notify provider packages.
/// </summary>
internal static class ServiceCollectionRegistrationExtensions
{
    /// <summary>
    /// Configures options only when a delegate is supplied.
    /// </summary>
    /// <typeparam name="TOptions">The options type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">The optional configuration delegate.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection ConfigureOptional<TOptions>(
        this IServiceCollection services,
        Action<TOptions>? configure)
        where TOptions : class
    {
        ArgumentNullException.ThrowIfNull(services);

        OptionsBuilder<TOptions> optionsBuilder = services.AddOptions<TOptions>();
        if (configure is not null)
        {
            optionsBuilder.Configure(configure);
        }

        return services;
    }

    /// <summary>
    /// Registers a keyed notification sender backed by a concrete singleton.
    /// </summary>
    /// <typeparam name="TSender">The concrete sender type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="channel">The notification channel.</param>
    /// <param name="registerFallback">Whether to also register the sender as the non-keyed fallback.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection TryAddNotificationSender<TSender>(
        this IServiceCollection services,
        NotificationChannel channel,
        bool registerFallback = false)
        where TSender : class, INotificationSender
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<TSender>();
        services.TryAddKeyedSingleton<INotificationSender>(
            channel,
            (sp, _) => sp.GetRequiredService<TSender>());

        if (registerFallback)
        {
            services.TryAddSingleton<INotificationSender>(sp => sp.GetRequiredService<TSender>());
        }

        return services;
    }
}

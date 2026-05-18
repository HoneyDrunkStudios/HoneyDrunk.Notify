using HoneyDrunk.Notify.Queue.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace HoneyDrunk.Notify.ProviderSupport;

/// <summary>
/// Shared DI registration helpers for Notify queue packages.
/// </summary>
internal static class QueueServiceCollectionRegistrationExtensions
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
    /// Registers a queue implementation and forwards it to queue interfaces.
    /// </summary>
    /// <typeparam name="TQueue">The concrete queue type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection TryAddNotificationQueue<TQueue>(this IServiceCollection services)
        where TQueue : class, INotificationQueue, IDeadLetterInspector
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<TQueue>();
        services.TryAddSingleton<INotificationQueue>(sp => sp.GetRequiredService<TQueue>());
        services.TryAddSingleton<IDeadLetterInspector>(sp => sp.GetRequiredService<TQueue>());

        return services;
    }
}

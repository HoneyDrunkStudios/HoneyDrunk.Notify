using HoneyDrunk.Notify.Queue.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace HoneyDrunk.Notify.Queue.InMemory.DependencyInjection;

/// <summary>
/// Extension methods for registering the in-memory notification queue adapter.
/// </summary>
public static class InMemoryQueueServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="InMemoryNotificationQueue"/> as the <see cref="INotificationQueue"/> implementation.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional configuration for queue options.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddHoneyDrunkNotifyInMemoryQueue(
        this IServiceCollection services,
        Action<NotificationQueueOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var optionsBuilder = services.AddOptions<NotificationQueueOptions>();
        if (configure is not null)
            optionsBuilder.Configure(configure);

        services.TryAddSingleton<InMemoryNotificationQueue>();
        services.TryAddSingleton<INotificationQueue>(sp => sp.GetRequiredService<InMemoryNotificationQueue>());
        services.TryAddSingleton<IDeadLetterInspector>(sp => sp.GetRequiredService<InMemoryNotificationQueue>());

        return services;
    }
}

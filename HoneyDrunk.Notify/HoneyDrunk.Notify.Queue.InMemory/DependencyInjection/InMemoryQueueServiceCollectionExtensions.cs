using HoneyDrunk.Notify.ProviderSupport;
using HoneyDrunk.Notify.Queue.Abstractions;
using Microsoft.Extensions.DependencyInjection;

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

        services.ConfigureOptional(configure);
        return services.TryAddNotificationQueue<InMemoryNotificationQueue>();
    }
}

using HoneyDrunk.Notify.ProviderSupport;
using HoneyDrunk.Notify.Queue.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace HoneyDrunk.Notify.Queue.AzureStorage.DependencyInjection;

/// <summary>
/// Extension methods for registering the Azure Storage Queue notification adapter.
/// </summary>
public static class AzureStorageQueueServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="AzureStorageNotificationQueue"/> as the <see cref="INotificationQueue"/> implementation.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Configuration for Azure Storage Queue options.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddHoneyDrunkNotifyAzureStorageQueue(
        this IServiceCollection services,
        Action<AzureStorageQueueOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        services.ConfigureOptional(configure);
        return services.TryAddNotificationQueue<AzureStorageNotificationQueue>();
    }
}

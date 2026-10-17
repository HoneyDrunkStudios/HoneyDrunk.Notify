using HoneyDrunk.Notify.Abstractions;
using HoneyDrunk.Notify.Intake;
using HoneyDrunk.Notify.Options;
using HoneyDrunk.Notify.Routing;
using HoneyDrunk.Notify.Storage;
using HoneyDrunk.Notify.Templates;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace HoneyDrunk.Notify.DependencyInjection;

/// <summary>
/// Core runtime service registration for the notification pipeline.
/// This is provider-agnostic and does NOT register <see cref="INotificationSender"/>.
/// </summary>
public static class NotifyServiceCollectionExtensions
{
    /// <summary>
    /// Registers the core notification runtime services: gateway, idempotency store,
    /// in-memory enqueuer, dispatcher, backoff strategy, and template renderer.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional configurator for <see cref="NotifyRuntimeOptions"/>.</param>
    /// <param name="configureTemplates">Optional configurator for <see cref="TemplateOptions"/>.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddHoneyDrunkNotifyRuntime(
        this IServiceCollection services,
        Action<NotifyRuntimeOptions>? configure = null,
        Action<TemplateOptions>? configureTemplates = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var optionsBuilder = services.AddOptions<NotifyRuntimeOptions>();
        if (configure is not null)
            optionsBuilder.Configure(configure);

        var templateOptionsBuilder = services.AddOptions<TemplateOptions>();
        if (configureTemplates is not null)
            templateOptionsBuilder.Configure(configureTemplates);

        services.TryAddSingleton<IIdempotencyStore, InMemoryIdempotencyStore>();
        services.TryAddSingleton<INotificationEnqueuer, InMemoryNotificationEnqueuer>();
        services.TryAddSingleton<INotificationGateway, NotificationGateway>();
        services.TryAddSingleton<IBackoffStrategy, ExponentialBackoffStrategy>();
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<INotificationSenderResolver, NotificationSenderResolver>();
        services.TryAddSingleton<NotificationDispatcher>();

        services.TryAddSingleton<ITemplateRenderer, FileTemplateRenderer>();
        services.TryAddSingleton<IEmailTemplateRenderer, EmailFileTemplateRenderer>();

        return services;
    }
}

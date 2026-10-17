using HoneyDrunk.Notify.Abstractions;
using HoneyDrunk.Notify.Hosting.AspNetCore.ServiceCollectionExtensions;
using HoneyDrunk.Notify.Providers.Email.Resend.DependencyInjection;
using HoneyDrunk.Notify.Providers.Email.Smtp.DependencyInjection;
using HoneyDrunk.Notify.Providers.Sms.Twilio.DependencyInjection;
using HoneyDrunk.Notify.Queue.AzureStorage.DependencyInjection;
using HoneyDrunk.Notify.Queue.InMemory.DependencyInjection;
using HoneyDrunk.Notify.Worker.Hosting;
using HoneyDrunk.Notify.Worker.Options;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace HoneyDrunk.Notify.Worker.Composition;

/// <summary>
/// Composition root extensions for the HoneyDrunk.Notify worker.
/// </summary>
public static class NotifyWorkerServiceCollectionExtensions
{
    /// <summary>
    /// Registers all HoneyDrunk.Notify worker services: core subsystem, SMTP provider,
    /// queue adapter, and dispatch background service.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional configuration delegate for <see cref="NotifyWorkerOptions"/>.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddHoneyDrunkNotifyWorker(
        this IServiceCollection services,
        Action<NotifyWorkerOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var workerOptions = new NotifyWorkerOptions();
        configure?.Invoke(workerOptions);

        var optionsBuilder = services.AddOptions<NotifyWorkerOptions>();
        if (configure is not null)
            optionsBuilder.Configure(configure);

        services.AddHoneyDrunkNotify();
        services.AddHoneyDrunkNotifySmtpProvider();
        services.AddHoneyDrunkNotifyResendProvider(_ => { });
        services.AddHoneyDrunkNotifyTwilioProvider(_ => { });

        if (string.Equals(workerOptions.QueueAdapter, "AzureStorage", StringComparison.OrdinalIgnoreCase))
        {
            services.AddHoneyDrunkNotifyAzureStorageQueue(_ => { });
        }
        else
        {
            services.AddHoneyDrunkNotifyInMemoryQueue();
        }

        services.TryAddSingleton<INotificationSender, NoOpNotificationSender>();

        services.AddHostedService<NotifyDispatcherBackgroundService>();

        return services;
    }
}

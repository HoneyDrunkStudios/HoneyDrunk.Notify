using HoneyDrunk.Notify.DependencyInjection;
using HoneyDrunk.Notify.Hosting.AspNetCore.Health;
using HoneyDrunk.Notify.Hosting.AspNetCore.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace HoneyDrunk.Notify.Hosting.AspNetCore.ServiceCollectionExtensions;

/// <summary>
/// Extension methods for registering HoneyDrunk.Notify services.
/// </summary>
public static class HoneyDrunkNotifyServiceCollectionExtensions
{
    /// <summary>
    /// Adds the HoneyDrunk.Notify subsystem to the service collection.
    /// Registers the core runtime pipeline and maps hosting-level options into core runtime options.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional configuration delegate for <see cref="NotifyOptions"/>.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddHoneyDrunkNotify(
        this IServiceCollection services,
        Action<NotifyOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var optionsBuilder = services.AddOptions<NotifyOptions>();

        if (configure is not null)
        {
            optionsBuilder.Configure(configure);
        }

        services.AddHoneyDrunkNotifyRuntime();

        services.AddOptions<Notify.Options.NotifyRuntimeOptions>()
            .Configure<IOptions<NotifyOptions>>((runtime, hostingOptions) =>
            {
                var notify = hostingOptions.Value;
                runtime.Enabled = notify.Enabled;
                runtime.MaxAttempts = notify.Retry.MaxAttempts;
                runtime.BaseDelay = notify.Retry.BaseDelay;
                runtime.MaxDelay = notify.Retry.MaxDelay;
                runtime.EnableDedupe = notify.Policy.EnableDedupe;
                runtime.DedupeWindow = notify.Policy.DedupeWindow;
            });

        services.AddOptions<Notify.Options.TemplateOptions>()
            .Configure<IOptions<NotifyOptions>>((templates, hostingOptions) =>
            {
                var configured = hostingOptions.Value.Templates;
                templates.RootPath = configured.RootPath ?? Path.Join(AppContext.BaseDirectory, "templates");
                templates.Extension = configured.Extension;
                templates.CacheEnabled = configured.CacheEnabled;
                templates.CacheTtl = configured.CacheTtl;
            });

        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<INotifyHealthContributor, DefaultNotifyHealthContributor>());

        return services;
    }
}

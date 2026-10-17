using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace HoneyDrunk.Notify.ProviderSupport;

/// <summary>
/// Shared options registration helpers for Notify provider and queue packages.
/// </summary>
internal static class OptionsRegistrationExtensions
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
}

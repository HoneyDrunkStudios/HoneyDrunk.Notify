using HoneyDrunk.Vault.Abstractions;
using HoneyDrunk.Vault.Models;

namespace HoneyDrunk.Notify.ProviderSupport;

/// <summary>
/// Shared provider helpers for resolving secret values without duplicating Vault calls.
/// </summary>
internal static class SecretStoreExtensions
{
    /// <summary>
    /// Resolves a required secret value by name.
    /// </summary>
    /// <param name="secretStore">The secret store.</param>
    /// <param name="secretName">The secret name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The secret value.</returns>
    public static async Task<string> GetRequiredSecretValueAsync(
        this ISecretStore secretStore,
        string secretName,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(secretStore);
        ArgumentException.ThrowIfNullOrWhiteSpace(secretName);

        var secret = await secretStore.GetSecretAsync(new SecretIdentifier(secretName), cancellationToken);
        return secret.Value;
    }
}

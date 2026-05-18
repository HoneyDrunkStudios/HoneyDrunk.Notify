using FluentAssertions;
using HoneyDrunk.Notify.ProviderSupport;
using HoneyDrunk.Vault.Abstractions;
using HoneyDrunk.Vault.Models;

namespace HoneyDrunk.Notify.Tests.ProviderSupport;

/// <summary>
/// Tests shared provider secret lookup helpers.
/// </summary>
public sealed class SecretStoreExtensionsTests
{
    /// <summary>
    /// Verifies provider helpers resolve values through Vault secret identifiers.
    /// </summary>
    /// <returns>A <see cref="Task" /> representing the asynchronous test.</returns>
    [Fact]
    public async Task GetRequiredSecretValueAsync_ResolvesSecretValueByName()
    {
        var secretStore = new FakeSecretStore("Resend--ApiKey", "secret-value");

        var value = await secretStore.GetRequiredSecretValueAsync("Resend--ApiKey", CancellationToken.None);

        value.Should().Be("secret-value");
        secretStore.RequestedIdentifier.Should().Be("Resend--ApiKey");
    }

    /// <summary>
    /// Verifies empty secret names are rejected before Vault is called.
    /// </summary>
    /// <returns>A <see cref="Task" /> representing the asynchronous test.</returns>
    [Fact]
    public async Task GetRequiredSecretValueAsync_RejectsMissingSecretName()
    {
        var secretStore = new FakeSecretStore("ignored", "ignored");

        var act = () => secretStore.GetRequiredSecretValueAsync(" ", CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    private sealed class FakeSecretStore(string expectedName, string value) : ISecretStore
    {
        public string? RequestedIdentifier { get; private set; }

        public Task<SecretValue> GetSecretAsync(
            SecretIdentifier identifier,
            CancellationToken cancellationToken = default)
        {
            RequestedIdentifier = identifier.Name;
            RequestedIdentifier.Should().Be(expectedName);
            return Task.FromResult(new SecretValue(identifier, value, version: null));
        }

        public Task<VaultResult<SecretValue>> TryGetSecretAsync(
            SecretIdentifier identifier,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<SecretVersion>> ListSecretVersionsAsync(
            string secretName,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}

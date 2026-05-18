using FluentAssertions;
using HoneyDrunk.Notify.Queue.AzureStorage;
using HoneyDrunk.Vault.Abstractions;
using HoneyDrunk.Vault.Models;
using Microsoft.Extensions.Logging.Abstractions;

namespace HoneyDrunk.Notify.Tests.Queue;

/// <summary>
/// Tests Azure Storage Queue credential resolution boundaries.
/// </summary>
public sealed class AzureStorageNotificationQueueTests
{
    /// <summary>
    /// Verifies direct connection strings remain supported for local tooling.
    /// </summary>
    /// <returns>A <see cref="Task" /> representing the asynchronous test.</returns>
    [Fact]
    public async Task ResolveConnectionStringAsync_UsesDirectValueWhenConfigured()
    {
        await using var queue = CreateQueue(new AzureStorageQueueOptions
        {
            ConnectionString = "UseDevelopmentStorage=true",
        });

        var connectionString = await queue.ResolveConnectionStringAsync(CancellationToken.None);

        connectionString.Should().Be("UseDevelopmentStorage=true");
    }

    /// <summary>
    /// Verifies hosted queue credentials are resolved through Vault rather than direct configuration reads.
    /// </summary>
    /// <returns>A <see cref="Task" /> representing the asynchronous test.</returns>
    [Fact]
    public async Task ResolveConnectionStringAsync_UsesVaultSecretWhenDirectValueIsAbsent()
    {
        var secretStore = new FakeSecretStore("NotifyQueueConnection", "vault-connection");
        await using var queue = CreateQueue(new AzureStorageQueueOptions(), secretStore);

        var connectionString = await queue.ResolveConnectionStringAsync(CancellationToken.None);

        connectionString.Should().Be("vault-connection");
        secretStore.RequestedIdentifier.Should().Be("NotifyQueueConnection");
    }

    /// <summary>
    /// Verifies hosted queue credentials fail closed when no Vault resolver is registered.
    /// </summary>
    /// <returns>A <see cref="Task" /> representing the asynchronous test.</returns>
    [Fact]
    public async Task ResolveConnectionStringAsync_RequiresSecretStoreWhenDirectValueIsAbsent()
    {
        await using var queue = CreateQueue(new AzureStorageQueueOptions());

        var act = () => queue.ResolveConnectionStringAsync(CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*ISecretStore*");
    }

    /// <summary>
    /// Verifies hosted queue credentials fail closed when Vault returns an empty value.
    /// </summary>
    /// <returns>A <see cref="Task" /> representing the asynchronous test.</returns>
    [Fact]
    public async Task ResolveConnectionStringAsync_RejectsEmptyVaultSecretValue()
    {
        var secretStore = new FakeSecretStore("NotifyQueueConnection", " ");
        await using var queue = CreateQueue(new AzureStorageQueueOptions(), secretStore);

        var act = () => queue.ResolveConnectionStringAsync(CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*empty value*");
    }

    private static AzureStorageNotificationQueue CreateQueue(
        AzureStorageQueueOptions options,
        ISecretStore? secretStore = null) =>
        secretStore is null
            ? new AzureStorageNotificationQueue(
                Microsoft.Extensions.Options.Options.Create(options),
                NullLogger<AzureStorageNotificationQueue>.Instance)
            : new AzureStorageNotificationQueue(
                Microsoft.Extensions.Options.Options.Create(options),
                NullLogger<AzureStorageNotificationQueue>.Instance,
                secretStore);

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

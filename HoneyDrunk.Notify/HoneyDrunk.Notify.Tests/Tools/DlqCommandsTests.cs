// <copyright file="DlqCommandsTests.cs" company="HoneyDrunk Studios">
// Copyright (c) HoneyDrunk Studios. All rights reserved.
// </copyright>

#pragma warning disable SA1600 // Test methods are self-documenting via [Fact] + method name.
#pragma warning disable SA1402 // Nested fakes are intentionally local to this file.

using HoneyDrunk.Notify.Abstractions;
using HoneyDrunk.Notify.Queue.Abstractions;
using HoneyDrunk.Notify.Tools;

namespace HoneyDrunk.Notify.Tests.Tools;

/// <summary>
/// Tests for <see cref="DlqCommands"/> — verifies stdout/stderr capture, exit codes,
/// dry-run behaviour, and the await-WriteLineAsync refactor in 0.4.0.
/// </summary>
public sealed class DlqCommandsTests
{
    [Fact]
    public async Task ListAsync_EmptyDlq_PrintsEmptyMessageAndReturnsZero()
    {
        var inspector = new FakeInspector();
        var (commands, stdout, _) = CreateCommands(inspector);

        var exit = await commands.ListAsync(new NotifyToolsOptions { ListTake = 10 }, CancellationToken.None);

        Assert.Equal(0, exit);
        Assert.Contains("DLQ is empty.", stdout.ToString());
    }

    [Fact]
    public async Task ListAsync_NonEmpty_PrintsTableHeaderAndRows()
    {
        var inspector = new FakeInspector
        {
            ListResult = [MakeEntry("01ARZ3NDEKTSV4RRFFQ69G5FAV", reason: "max attempts")],
        };
        var (commands, stdout, _) = CreateCommands(inspector);

        var exit = await commands.ListAsync(new NotifyToolsOptions { ListTake = 25 }, CancellationToken.None);

        Assert.Equal(0, exit);
        var output = stdout.ToString();
        Assert.Contains("NotificationId", output);
        Assert.Contains("01ARZ3NDEKTSV4RRFFQ69G5FAV", output);
        Assert.Contains("Showing 1 of up to 25", output);
    }

    [Fact]
    public async Task PeekAsync_NotFound_ReturnsOneAndWritesToStderr()
    {
        var inspector = new FakeInspector();
        var (commands, _, stderr) = CreateCommands(inspector);

        var exit = await commands.PeekAsync("missing-id", CancellationToken.None);

        Assert.Equal(1, exit);
        Assert.Contains("missing-id", stderr.ToString());
    }

    [Fact]
    public async Task PeekAsync_Found_PrintsEntryFieldsAndReturnsZero()
    {
        var inspector = new FakeInspector
        {
            FindResult = MakeEntry("found-id", reason: "provider down"),
        };
        var (commands, stdout, _) = CreateCommands(inspector);

        var exit = await commands.PeekAsync("found-id", CancellationToken.None);

        Assert.Equal(0, exit);
        var output = stdout.ToString();
        Assert.Contains("found-id", output);
        Assert.Contains("provider down", output);
        Assert.Contains("Reason", output);
    }

    [Fact]
    public async Task ReplayAsync_DryRun_DoesNotInvokeInspectorAndReturnsZero()
    {
        var inspector = new FakeInspector();
        var (commands, stdout, _) = CreateCommands(inspector);

        var exit = await commands.ReplayAsync(
            "id-1",
            new NotifyToolsOptions { DryRun = true, QueueName = "notify-main", DeadLetterQueueName = "notify-dlq" },
            CancellationToken.None);

        Assert.Equal(0, exit);
        Assert.Contains("[DRY-RUN]", stdout.ToString());
        Assert.Equal(0, inspector.ReplayCalls);
    }

    [Fact]
    public async Task ReplayAsync_InspectorReturnsTrue_PrintsSuccessAndReturnsZero()
    {
        var inspector = new FakeInspector { ReplayResult = true };
        var (commands, stdout, _) = CreateCommands(inspector);

        var exit = await commands.ReplayAsync(
            "id-1",
            new NotifyToolsOptions { QueueName = "notify-main" },
            CancellationToken.None);

        Assert.Equal(0, exit);
        Assert.Contains("Replayed", stdout.ToString());
        Assert.Equal(1, inspector.ReplayCalls);
    }

    [Fact]
    public async Task ReplayAsync_NotFound_ReturnsOneAndWritesToStderr()
    {
        var inspector = new FakeInspector { ReplayResult = false };
        var (commands, _, stderr) = CreateCommands(inspector);

        var exit = await commands.ReplayAsync("missing", new NotifyToolsOptions(), CancellationToken.None);

        Assert.Equal(1, exit);
        Assert.Contains("missing", stderr.ToString());
    }

    [Fact]
    public async Task PurgeAsync_DryRun_DoesNotInvokeInspectorAndReturnsZero()
    {
        var inspector = new FakeInspector();
        var (commands, stdout, _) = CreateCommands(inspector);

        var exit = await commands.PurgeAsync(
            "id-1",
            new NotifyToolsOptions { DryRun = true, DeadLetterQueueName = "notify-dlq" },
            CancellationToken.None);

        Assert.Equal(0, exit);
        Assert.Contains("[DRY-RUN]", stdout.ToString());
        Assert.Equal(0, inspector.PurgeCalls);
    }

    [Fact]
    public async Task PurgeAsync_InspectorReturnsTrue_PrintsSuccessAndReturnsZero()
    {
        var inspector = new FakeInspector { PurgeResult = true };
        var (commands, stdout, _) = CreateCommands(inspector);

        var exit = await commands.PurgeAsync("id-1", new NotifyToolsOptions(), CancellationToken.None);

        Assert.Equal(0, exit);
        Assert.Contains("Purged", stdout.ToString());
        Assert.Equal(1, inspector.PurgeCalls);
    }

    [Fact]
    public async Task PurgeAsync_NotFound_ReturnsOneAndWritesToStderr()
    {
        var inspector = new FakeInspector { PurgeResult = false };
        var (commands, _, stderr) = CreateCommands(inspector);

        var exit = await commands.PurgeAsync("missing", new NotifyToolsOptions(), CancellationToken.None);

        Assert.Equal(1, exit);
        Assert.Contains("missing", stderr.ToString());
    }

    private static (DlqCommands commands, StringWriter stdout, StringWriter stderr) CreateCommands(IDeadLetterInspector inspector)
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        Console.SetOut(stdout);
        Console.SetError(stderr);
        return (new DlqCommands(inspector), stdout, stderr);
    }

    private static DeadLetterEntry MakeEntry(string notificationId, string reason)
    {
        var envelope = new NotificationEnvelope(
            new NotificationId("01ARZ3NDEKTSV4RRFFQ69G5FAV"),
            NotificationChannel.Email,
            Recipient.Email("dlq@example.test"),
            new TemplateKey("dlq.test"),
            new Dictionary<string, object?>())
        {
            CorrelationId = "corr-x",
            TenantId = "tenant-x",
            CreatedAtUtc = DateTimeOffset.UtcNow,
        };
        return new DeadLetterEntry(notificationId, DeliveryCount: 3, Reason: reason, Envelope: envelope)
        {
            DeadLetteredAt = DateTimeOffset.UtcNow,
        };
    }
}

internal sealed class FakeInspector : IDeadLetterInspector
{
    public IReadOnlyList<DeadLetterEntry> ListResult { get; init; } = [];

    public DeadLetterEntry? FindResult { get; init; }

    public bool ReplayResult { get; init; }

    public bool PurgeResult { get; init; }

    public int ReplayCalls { get; private set; }

    public int PurgeCalls { get; private set; }

    public Task<IReadOnlyList<DeadLetterEntry>> ListAsync(int take, CancellationToken ct = default)
        => Task.FromResult(ListResult);

    public Task<DeadLetterEntry?> FindByNotificationIdAsync(string notificationId, CancellationToken ct = default)
        => Task.FromResult(FindResult);

    public Task<bool> ReplayAsync(string notificationId, CancellationToken ct = default)
    {
        ReplayCalls++;
        return Task.FromResult(ReplayResult);
    }

    public Task<bool> PurgeAsync(string notificationId, CancellationToken ct = default)
    {
        PurgeCalls++;
        return Task.FromResult(PurgeResult);
    }
}

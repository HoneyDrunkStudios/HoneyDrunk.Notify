// <copyright file="CoverageGateBackfillTests.cs" company="HoneyDrunk Studios">
// Copyright (c) HoneyDrunk Studios. All rights reserved.
// </copyright>

using FluentAssertions;
using HoneyDrunk.Notify.Abstractions;
using HoneyDrunk.Notify.Abstractions.Models.Email;
using HoneyDrunk.Notify.Abstractions.Models.Sms;
using HoneyDrunk.Notify.DependencyInjection;
using HoneyDrunk.Notify.HostBootstrap;
using HoneyDrunk.Notify.Hosting.AspNetCore.Health;
using HoneyDrunk.Notify.Hosting.AspNetCore.Options;
using HoneyDrunk.Notify.Hosting.AspNetCore.ServiceCollectionExtensions;
using HoneyDrunk.Notify.Intake;
using HoneyDrunk.Notify.Options;
using HoneyDrunk.Notify.Providers.Email.Resend.DependencyInjection;
using HoneyDrunk.Notify.Providers.Email.Smtp.DependencyInjection;
using HoneyDrunk.Notify.Providers.Sms.Twilio.DependencyInjection;
using HoneyDrunk.Notify.Queue.Abstractions;
using HoneyDrunk.Notify.Queue.AzureStorage.DependencyInjection;
using HoneyDrunk.Notify.Queue.InMemory.DependencyInjection;
using HoneyDrunk.Notify.Routing;
using HoneyDrunk.Notify.Storage;
using HoneyDrunk.Notify.Worker.Composition;
using HoneyDrunk.Notify.Worker.Hosting;
using HoneyDrunk.Notify.Worker.Options;
using HoneyDrunk.Vault.Abstractions;
using HoneyDrunk.Vault.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace HoneyDrunk.Notify.Tests.Telemetry;

/// <summary>
/// Focused coverage backfill for public Notify contracts, routing, and provider registration guard paths.
/// </summary>
public sealed class CoverageGateBackfillTests
{
    /// <summary>
    /// Verifies strongly typed ULID identifiers round-trip valid values and reject invalid text.
    /// </summary>
    [Fact]
    public void StronglyTypedIdentifiers_RoundTripAndRejectInvalidText()
    {
        // Arrange
        var ulid = Ulid.NewUlid();

        // Act
        var notificationId = new NotificationId(ulid);
        var attemptId = new AttemptId(ulid.ToString());
        var parsedNotification = NotificationId.TryParse(ulid.ToString(), out var parsedNotificationId);
        var parsedAttempt = AttemptId.TryParse(ulid.ToString(), out var parsedAttemptId);
        var failedNotification = NotificationId.TryParse("not-a-ulid", out var failedNotificationId);
        var failedAttempt = AttemptId.TryParse("not-a-ulid", out var failedAttemptId);

        // Assert
        ((string)notificationId).Should().Be(ulid.ToString());
        notificationId.ToUlid().Should().Be(ulid);
        NotificationId.FromUlid(ulid).Should().Be(notificationId);
        parsedNotification.Should().BeTrue();
        parsedNotificationId.Should().Be(notificationId);
        failedNotification.Should().BeFalse();
        failedNotificationId.Should().Be(default(NotificationId));
        ((Ulid)attemptId).Should().Be(ulid);
        attemptId.ToUlid().Should().Be(ulid);
        AttemptId.FromUlid(ulid).Should().Be(attemptId);
        parsedAttempt.Should().BeTrue();
        parsedAttemptId.Should().Be(attemptId);
        failedAttempt.Should().BeFalse();
        failedAttemptId.Should().Be(default(AttemptId));
        Action invalidNotification = () => _ = new NotificationId("bad");
        Action invalidAttempt = () => _ = new AttemptId("bad");
        invalidNotification.Should().Throw<ArgumentException>();
        invalidAttempt.Should().Throw<ArgumentException>();
    }

    /// <summary>
    /// Verifies simple contract factories preserve status, failure, and provider details.
    /// </summary>
    [Fact]
    public void OutcomeFactories_PreserveProviderStatusAndFailureDetails()
    {
        // Arrange
        var notificationId = NotificationId.NewId();
        var attemptId = AttemptId.NewId();
        var acceptedAt = DateTimeOffset.Parse("2026-05-19T16:00:00Z", null, System.Globalization.DateTimeStyles.AssumeUniversal);

        // Act
        var succeeded = DeliveryOutcome.Succeeded(notificationId, attemptId, NotificationChannel.Email, "smtp", "provider-1");
        var failed = DeliveryOutcome.Failed(notificationId, attemptId, NotificationChannel.Sms, "twilio", FailureKind.Policy, "blocked");
        var deferred = DeliveryOutcome.Deferred(notificationId, attemptId, NotificationChannel.Email, "resend", "provider-2");
        var accepted = NotificationOutcome.Accepted(notificationId, acceptedAt);
        var rejected = NotificationOutcome.Rejected(notificationId, acceptedAt, RejectionReason.ValidationFailed, "bad recipient");

        // Assert
        succeeded.Status.Should().Be(DeliveryStatus.Succeeded);
        succeeded.FailureKind.Should().Be(FailureKind.None);
        succeeded.ProviderMessageId.Should().Be("provider-1");
        failed.Status.Should().Be(DeliveryStatus.Failed);
        failed.FailureKind.Should().Be(FailureKind.Policy);
        failed.ErrorMessage.Should().Be("blocked");
        deferred.Status.Should().Be(DeliveryStatus.Deferred);
        deferred.ProviderMessageId.Should().Be("provider-2");
        accepted.Status.Should().Be(NotificationAcceptanceStatus.Accepted);
        accepted.RejectionReason.Should().Be(RejectionReason.None);
        rejected.Status.Should().Be(NotificationAcceptanceStatus.Rejected);
        rejected.RejectionReason.Should().Be(RejectionReason.ValidationFailed);
        rejected.RejectionDetail.Should().Be("bad recipient");
    }

    /// <summary>
    /// Verifies value objects validate required address and idempotency constraints.
    /// </summary>
    [Fact]
    public void ValueObjects_ValidateRecipientAndIdempotencyConstraints()
    {
        // Act
        var recipient = Recipient.Email("person@example.test");
        var key = new IdempotencyKey("order-123");
        Action missingRecipient = () => Recipient.Email(" ");
        Action missingKey = () => _ = new IdempotencyKey(" ");
        Action longKey = () => _ = new IdempotencyKey(new string('x', 257));

        // Assert
        recipient.Channel.Should().Be(NotificationChannel.Email);
        recipient.Address.Should().Be("person@example.test");
        ((string)key).Should().Be("order-123");
        key.ToString().Should().Be("order-123");
        missingRecipient.Should().Throw<ArgumentException>();
        missingKey.Should().Throw<ArgumentException>();
        longKey.Should().Throw<ArgumentException>().WithMessage("*256*");
    }

    /// <summary>
    /// Verifies the in-memory intake queue preserves FIFO order and honors drain limits.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task InMemoryNotificationEnqueuer_DrainsInFifoOrder()
    {
        // Arrange
        var enqueuer = new InMemoryNotificationEnqueuer();
        var first = Envelope(NotificationChannel.Email, "first@example.test");
        var second = Envelope(NotificationChannel.Email, "second@example.test");

        // Act
        await enqueuer.EnqueueAsync(first);
        await enqueuer.EnqueueAsync(second);
        var drained = await enqueuer.DrainAsync(1);
        var dequeued = enqueuer.TryDequeue(out var remaining);
        var empty = await enqueuer.DrainAsync(5);

        // Assert
        drained.Should().ContainSingle().Which.Should().Be(first);
        dequeued.Should().BeTrue();
        remaining.Should().Be(second);
        empty.Should().BeEmpty();
        Func<Task> nullEnvelope = () => enqueuer.EnqueueAsync(null!);
        await nullEnvelope.Should().ThrowAsync<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies the idempotency store rejects active duplicates and reclaims expired keys.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task InMemoryIdempotencyStore_RejectsDuplicatesUntilWindowExpires()
    {
        // Arrange
        var store = new InMemoryIdempotencyStore();
        var now = DateTimeOffset.Parse("2026-05-19T16:00:00Z", null, System.Globalization.DateTimeStyles.AssumeUniversal);

        // Act
        var first = await store.TryBeginAsync("key", now, TimeSpan.FromMinutes(5));
        var duplicate = await store.TryBeginAsync("key", now.AddMinutes(1), TimeSpan.FromMinutes(5));
        var expired = await store.TryBeginAsync("key", now.AddMinutes(6), TimeSpan.FromMinutes(5));
        await store.CompleteAsync("key", "notification-1");

        // Assert
        first.Should().BeTrue();
        duplicate.Should().BeFalse();
        expired.Should().BeTrue();
        Func<Task> missingKey = () => store.TryBeginAsync(" ", now, TimeSpan.FromMinutes(5));
        Func<Task> missingCompleteKey = () => store.CompleteAsync(" ", "notification-1");
        Func<Task> missingNotification = () => store.CompleteAsync("key", " ");
        await missingKey.Should().ThrowAsync<ArgumentException>();
        await missingCompleteKey.Should().ThrowAsync<ArgumentException>();
        await missingNotification.Should().ThrowAsync<ArgumentException>();
    }

    /// <summary>
    /// Verifies exponential backoff doubles delays and respects the configured cap.
    /// </summary>
    [Fact]
    public void ExponentialBackoffStrategy_CalculatesAndCapsDelays()
    {
        // Arrange
        var strategy = new ExponentialBackoffStrategy();

        // Act / Assert
        strategy.Calculate(0, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(30)).Should().Be(TimeSpan.FromSeconds(2));
        strategy.Calculate(2, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(30)).Should().Be(TimeSpan.FromSeconds(8));
        strategy.Calculate(10, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(30)).Should().Be(TimeSpan.FromSeconds(30));
    }

    /// <summary>
    /// Verifies dispatcher terminal paths for success, permanent failure, deferred retry, and invalid retry configuration.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task NotificationDispatcher_HandlesTerminalAndRetryOutcomes()
    {
        // Arrange
        var envelope = Envelope(NotificationChannel.Email, "person@example.test");
        var successSender = new SequenceSender(DeliveryOutcome.Succeeded(envelope.NotificationId, AttemptId.NewId(), NotificationChannel.Email, "fake"));
        var permanentSender = new SequenceSender(DeliveryOutcome.Failed(envelope.NotificationId, AttemptId.NewId(), NotificationChannel.Email, "fake", FailureKind.Permanent, "nope"));
        var retrySender = new SequenceSender(
            DeliveryOutcome.Deferred(envelope.NotificationId, AttemptId.NewId(), NotificationChannel.Email, "fake"),
            DeliveryOutcome.Failed(envelope.NotificationId, AttemptId.NewId(), NotificationChannel.Email, "fake", FailureKind.Transient, "try again"));

        // Act
        var success = await Dispatcher(successSender, maxAttempts: 1).DispatchAsync(envelope);
        var permanent = await Dispatcher(permanentSender, maxAttempts: 3).DispatchAsync(envelope);
        var exhausted = await Dispatcher(retrySender, maxAttempts: 2).DispatchAsync(envelope);
        Func<Task> invalid = () => Dispatcher(successSender, maxAttempts: 0).DispatchAsync(envelope);

        // Assert
        success.Status.Should().Be(DeliveryStatus.Succeeded);
        permanent.FailureKind.Should().Be(FailureKind.Permanent);
        exhausted.FailureKind.Should().Be(FailureKind.Transient);
        retrySender.Calls.Should().Be(2);
        await invalid.Should().ThrowAsync<InvalidOperationException>().WithMessage("*MaxAttempts*");
    }

    /// <summary>
    /// Verifies gateway validation, disabled runtime, duplicate idempotency, and accepted email payload paths.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task NotificationGateway_ValidatesDedupesAndBuildsEmailPayloads()
    {
        // Arrange
        var enqueuer = new InMemoryNotificationEnqueuer();
        var store = new InMemoryIdempotencyStore();
        var renderer = new StubEmailRenderer();
        var gateway = new NotificationGateway(
            Microsoft.Extensions.Options.Options.Create(new NotifyRuntimeOptions { DedupeWindow = TimeSpan.FromMinutes(5) }),
            enqueuer,
            store,
            renderer,
            NullLogger<NotificationGateway>.Instance);
        var request = Request("person@example.test") with { IdempotencyKey = new IdempotencyKey("same-key") };

        // Act
        var accepted = await gateway.EnqueueAsync(request);
        var duplicate = await gateway.EnqueueAsync(request);
        var invalid = await gateway.EnqueueAsync(Request("not-an-email"));
        var disabled = await new NotificationGateway(
            Microsoft.Extensions.Options.Options.Create(new NotifyRuntimeOptions { Enabled = false }),
            enqueuer,
            store,
            renderer,
            NullLogger<NotificationGateway>.Instance).EnqueueAsync(Request("person@example.test"));
        enqueuer.TryDequeue(out var envelope).Should().BeTrue();

        // Assert
        accepted.Status.Should().Be(NotificationAcceptanceStatus.Accepted);
        duplicate.RejectionReason.Should().Be(RejectionReason.DuplicateIdempotencyKey);
        invalid.RejectionReason.Should().Be(RejectionReason.ValidationFailed);
        disabled.RejectionReason.Should().Be(RejectionReason.RuntimeDisabled);
        envelope!.Payload.Should().BeOfType<EmailEnvelope>()
            .Which.Content.Subject.Should().Be("Rendered subject");
        envelope.IdempotencyKey.Should().Be(new IdempotencyKey("same-key"));
        envelope.Tags.Should().Contain("critical");
    }

    /// <summary>
    /// Verifies provider registration resolves keyed senders and safe guard paths without external service calls.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task ProviderRegistrations_ResolveSendersAndRejectInvalidPayloadsWithoutExternalCalls()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<ISecretStore>(new FakeSecretStore());
        services.AddHoneyDrunkNotifyRuntime();
        services.AddHoneyDrunkNotifySmtpProvider(options => options.FromAddress = "smtp@example.test");
        services.AddHoneyDrunkNotifyTwilioProvider(options => options.FromNumber = "+15551234567");
        using var provider = services.BuildServiceProvider();
        var emailEnvelope = Envelope(NotificationChannel.Email, "person@example.test") with { Payload = new SmsEnvelope("+15550000000", "wrong") };
        var smsEnvelope = Envelope(NotificationChannel.Sms, "+15550000000") with { Payload = new EmailEnvelope("person@example.test", new EmailContent("s", "b")) };

        // Act
        var emailSender = provider.GetRequiredKeyedService<INotificationSender>(NotificationChannel.Email);
        var smsSender = provider.GetRequiredKeyedService<INotificationSender>(NotificationChannel.Sms);
        var emailResult = await emailSender.SendAsync(emailEnvelope);
        var smsResult = await smsSender.SendAsync(smsEnvelope);

        // Assert
        emailResult.Status.Should().Be(DeliveryStatus.Failed);
        emailResult.FailureKind.Should().Be(FailureKind.Permanent);
        smsResult.Status.Should().Be(DeliveryStatus.Failed);
        smsResult.FailureKind.Should().Be(FailureKind.Permanent);
        provider.GetRequiredService<INotificationSender>().Should().BeSameAs(emailSender);
    }

    /// <summary>
    /// Verifies hosting and worker composition map options and register queue/provider services.
    /// </summary>
    [Fact]
    public void HostingAndWorkerComposition_RegisterExpectedRuntimeServices()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<ISecretStore>(new FakeSecretStore());

        // Act
        services.AddHoneyDrunkNotify(options =>
        {
            options.Enabled = false;
            options.Retry.MaxAttempts = 7;
            options.Retry.BaseDelay = TimeSpan.FromSeconds(1);
            options.Retry.MaxDelay = TimeSpan.FromSeconds(9);
            options.Policy.EnableDedupe = false;
            options.Policy.DedupeWindow = TimeSpan.FromSeconds(30);
            options.Templates.RootPath = "custom-templates";
            options.Templates.Extension = ".liquid";
            options.Templates.CacheEnabled = false;
            options.Templates.CacheTtl = TimeSpan.FromSeconds(5);
        });
        services.AddHoneyDrunkNotifyInMemoryQueue();
        services.AddHoneyDrunkNotifyWorker(options => options.QueueAdapter = "InMemory");
        using var provider = services.BuildServiceProvider();
        var runtime = provider.GetRequiredService<IOptions<NotifyRuntimeOptions>>().Value;
        var templates = provider.GetRequiredService<IOptions<HoneyDrunk.Notify.Options.TemplateOptions>>().Value;
        var worker = provider.GetRequiredService<IOptions<NotifyWorkerOptions>>().Value;

        // Assert
        runtime.Enabled.Should().BeFalse();
        runtime.MaxAttempts.Should().Be(7);
        runtime.EnableDedupe.Should().BeFalse();
        templates.RootPath.Should().Be("custom-templates");
        templates.Extension.Should().Be(".liquid");
        templates.CacheEnabled.Should().BeFalse();
        worker.QueueAdapter.Should().Be("InMemory");
        provider.GetServices<Microsoft.Extensions.Hosting.IHostedService>().Should().NotBeEmpty();
    }

    /// <summary>
    /// Verifies each provider fails closed when the payload is valid but no sender address is configured.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task ProviderSenders_RejectMissingSenderConfigurationBeforeExternalCalls()
    {
        // Arrange
        using var smtpProvider = BuildProvider(services => services.AddHoneyDrunkNotifySmtpProvider());
        using var resendProvider = BuildProvider(services => services.AddHoneyDrunkNotifyResendProvider(_ => { }));
        using var twilioProvider = BuildProvider(services => services.AddHoneyDrunkNotifyTwilioProvider(_ => { }));
        var smtpSender = smtpProvider.GetRequiredKeyedService<INotificationSender>(NotificationChannel.Email);
        var resendSender = resendProvider.GetRequiredKeyedService<INotificationSender>(NotificationChannel.Email);
        var twilioSender = twilioProvider.GetRequiredKeyedService<INotificationSender>(NotificationChannel.Sms);
        var email = Envelope(NotificationChannel.Email, "person@example.test") with
        {
            Payload = new EmailEnvelope("person@example.test", new EmailContent("Subject", "Body")),
        };
        var sms = Envelope(NotificationChannel.Sms, "+15550000000") with
        {
            Payload = new SmsEnvelope("+15550000000", "Hello"),
        };

        // Act
        var smtp = await smtpSender.SendAsync(email);
        var resend = await resendSender.SendAsync(email);
        var twilio = await twilioSender.SendAsync(sms);

        // Assert
        smtp.Provider.Should().Be("smtp");
        smtp.Status.Should().Be(DeliveryStatus.Failed);
        smtp.ErrorMessage.Should().Contain("No sender address configured");
        resend.Provider.Should().Be("resend");
        resend.Status.Should().Be(DeliveryStatus.Failed);
        resend.ErrorMessage.Should().Contain("No sender address configured");
        twilio.Provider.Should().Be("twilio");
        twilio.Status.Should().Be(DeliveryStatus.Failed);
        twilio.ErrorMessage.Should().Contain("No sender phone number configured");
    }

    /// <summary>
    /// Verifies host health, node identity, and fallback sender paths stay covered as runtime code.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task HostRuntimeHelpers_ReportHealthIdentityAndNoOpFailures()
    {
        // Arrange
        var enabledContributor = new DefaultNotifyHealthContributor(
            Microsoft.Extensions.Options.Options.Create(new NotifyOptions { Enabled = true }));
        var disabledContributor = new DefaultNotifyHealthContributor(
            Microsoft.Extensions.Options.Options.Create(new NotifyOptions { Enabled = false }));
        var configuredNode = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Grid:NodeId"] = "notify-custom" })
            .Build();
        var environmentNode = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["HONEYDRUNK_NODE_ID"] = "notify-env", ["Grid:NodeId"] = "notify-grid" })
            .Build();
        var fallbackNode = new ConfigurationBuilder().Build();
        var sender = new NoOpNotificationSender(NullLogger<NoOpNotificationSender>.Instance);
        var envelope = Envelope(NotificationChannel.Email, "person@example.test");

        // Act
        var healthy = await enabledContributor.CheckAsync();
        var unhealthy = await disabledContributor.CheckAsync();
        var configured = NotifyNodeIdentity.ResolveNodeId(configuredNode);
        var environment = NotifyNodeIdentity.ResolveNodeId(environmentNode);
        var fallback = NotifyNodeIdentity.ResolveNodeId(fallbackNode);
        var outcome = await sender.SendAsync(envelope);
        Func<Task> nullEnvelope = () => sender.SendAsync(null!);

        // Assert
        healthy.Status.Should().Be(NotifyHealthStatus.Healthy);
        unhealthy.Status.Should().Be(NotifyHealthStatus.Unhealthy);
        configured.Value.Should().Be("notify-custom");
        environment.Value.Should().Be("notify-env");
        fallback.Value.Should().Be("honeydrunk-notify");
        outcome.NotificationId.Should().Be(envelope.NotificationId);
        outcome.Provider.Should().Be("noop");
        outcome.Status.Should().Be(DeliveryStatus.Failed);
        outcome.FailureKind.Should().Be(FailureKind.Permanent);
        await nullEnvelope.Should().ThrowAsync<ArgumentNullException>();
    }

    /// <summary>
    /// Verifies the worker dispatcher completes, abandons, and dead-letters queue items by outcome.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task WorkerDispatcher_ProcessesQueueItemsByDeliveryOutcome()
    {
        // Arrange
        var completed = Envelope(NotificationChannel.Email, "completed@example.test");
        var abandoned = Envelope(NotificationChannel.Email, "abandoned@example.test");
        var deadLettered = Envelope(NotificationChannel.Email, "deadletter@example.test");
        var permanent = Envelope(NotificationChannel.Email, "permanent@example.test");
        var queue = new RecordingNotificationQueue(
            new QueuedNotification(completed, "complete", DateTimeOffset.UtcNow),
            new QueuedNotification(abandoned, "abandon", DateTimeOffset.UtcNow),
            new QueuedNotification(deadLettered, "deadletter", DateTimeOffset.UtcNow, 5),
            new QueuedNotification(permanent, "permanent", DateTimeOffset.UtcNow));
        var sender = new SequenceSender(
            Success(completed),
            Transient(abandoned),
            Transient(deadLettered),
            Permanent(permanent));
        using var service = new NotifyDispatcherBackgroundService(
            queue,
            Dispatcher(sender, maxAttempts: 1),
            Microsoft.Extensions.Options.Options.Create(new NotifyWorkerOptions
            {
                Enabled = true,
                BatchSize = 10,
                PollInterval = TimeSpan.FromMinutes(5),
            }),
            Microsoft.Extensions.Options.Options.Create(new NotificationQueueOptions { MaxDeliveryAttempts = 5 }),
            NullLogger<NotifyDispatcherBackgroundService>.Instance);

        // Act
        await service.StartAsync(CancellationToken.None);
        await queue.Processed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await service.StopAsync(CancellationToken.None);

        // Assert
        queue.Completed.Should().ContainSingle(item => item.Envelope.NotificationId == completed.NotificationId);
        queue.Abandoned.Should().ContainSingle(item => item.Envelope.NotificationId == abandoned.NotificationId);
        queue.DeadLettered.Should().ContainSingle(entry => entry.queuedNotification.Envelope.NotificationId == deadLettered.NotificationId);
        queue.Completed.Should().Contain(item => item.Envelope.NotificationId == permanent.NotificationId);
    }

    /// <summary>
    /// Verifies the in-memory queue covers delivery, dead-letter, replay, and purge paths.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task InMemoryQueue_TracksDeliveryAndDeadLetterLifecycle()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddHoneyDrunkNotifyInMemoryQueue(options => options.MaxBatchSize = 2);
        using var provider = services.BuildServiceProvider();
        var queue = provider.GetRequiredService<INotificationQueue>();
        var inspector = provider.GetRequiredService<IDeadLetterInspector>();
        var first = Envelope(NotificationChannel.Email, "first@example.test");
        var second = Envelope(NotificationChannel.Email, "second@example.test");
        var third = Envelope(NotificationChannel.Email, "third@example.test");

        // Act
        await queue.EnqueueAsync(first);
        await queue.EnqueueAsync(second);
        await queue.EnqueueAsync(third);
        var firstBatch = await queue.DequeueBatchAsync(10);
        await queue.CompleteAsync(firstBatch[0]);
        await queue.AbandonAsync(firstBatch[1]);
        var secondBatch = await queue.DequeueBatchAsync(10);
        var replayTarget = secondBatch.Single(item => item.Envelope.NotificationId == second.NotificationId);
        await queue.DeadLetterAsync(replayTarget, "provider failed");
        var listed = await inspector.ListAsync(10);
        var found = await inspector.FindByNotificationIdAsync(second.NotificationId.ToString());
        var missing = await inspector.FindByNotificationIdAsync("missing");
        var replayed = await inspector.ReplayAsync(second.NotificationId.ToString());
        var replayedAgain = await inspector.ReplayAsync(second.NotificationId.ToString());
        var afterReplay = await queue.DequeueBatchAsync(10);
        await queue.DeadLetterAsync(afterReplay.Single(item => item.Envelope.NotificationId == second.NotificationId), "still failed");
        var purged = await inspector.PurgeAsync(second.NotificationId.ToString());
        var purgedAgain = await inspector.PurgeAsync(second.NotificationId.ToString());
        Func<Task> nullEnvelope = () => queue.EnqueueAsync(null!);
        Func<Task> nullComplete = () => queue.CompleteAsync(null!);
        Func<Task> nullAbandon = () => queue.AbandonAsync(null!);
        Func<Task> nullDeadLetter = () => queue.DeadLetterAsync(null!, "failed");
        Func<Task> blankReason = () => queue.DeadLetterAsync(firstBatch[0], " ");

        // Assert
        firstBatch.Should().HaveCount(2);
        secondBatch.Should().HaveCount(2);
        listed.Should().ContainSingle(entry => entry.NotificationId == second.NotificationId.ToString());
        found.Should().NotBeNull();
        found!.Reason.Should().Be("provider failed");
        found.Channel.Should().Be(NotificationChannel.Email.ToString());
        found.TemplateKey.Should().Be("welcome");
        found.CorrelationId.Should().Be("corr-1");
        found.TenantId.Should().Be("tenant-1");
        found.DeadLetteredAt.Should().NotBeNull();
        missing.Should().BeNull();
        replayed.Should().BeTrue();
        replayedAgain.Should().BeFalse();
        afterReplay.Should().Contain(item => item.Envelope.NotificationId == second.NotificationId);
        purged.Should().BeTrue();
        purgedAgain.Should().BeFalse();
        await nullEnvelope.Should().ThrowAsync<ArgumentNullException>();
        await nullComplete.Should().ThrowAsync<ArgumentNullException>();
        await nullAbandon.Should().ThrowAsync<ArgumentNullException>();
        await nullDeadLetter.Should().ThrowAsync<ArgumentNullException>();
        await blankReason.Should().ThrowAsync<ArgumentException>();
    }

    /// <summary>
    /// Verifies the sender resolver uses keyed, fallback, and missing-registration paths.
    /// </summary>
    [Fact]
    public void NotificationSenderResolver_UsesKeyedFallbackAndMissingPaths()
    {
        // Arrange
        var emailPayload = new EmailEnvelope("person@example.test", new EmailContent("Subject", "Body"))
        {
            From = "sender@example.test",
            FromDisplayName = "HoneyDrunk",
            Headers = new Dictionary<string, string> { ["X-Test"] = "true" },
        };
        var keyedSender = new SequenceSender(Success(Envelope(NotificationChannel.Email, "keyed@example.test")));
        var fallbackSender = new SequenceSender(Success(Envelope(NotificationChannel.Sms, "fallback@example.test")));
        var keyedServices = new ServiceCollection();
        keyedServices.AddKeyedSingleton<INotificationSender>(NotificationChannel.Email, keyedSender);
        using var keyedProvider = keyedServices.BuildServiceProvider();
        var fallbackServices = new ServiceCollection();
        fallbackServices.AddSingleton<INotificationSender>(fallbackSender);
        using var fallbackProvider = fallbackServices.BuildServiceProvider();
        using var emptyProvider = new ServiceCollection().BuildServiceProvider();

        // Act
        var keyed = new NotificationSenderResolver(keyedProvider).Resolve(NotificationChannel.Email);
        var fallback = new NotificationSenderResolver(fallbackProvider).Resolve(NotificationChannel.Sms);
        Action missing = () => new NotificationSenderResolver(emptyProvider).Resolve((NotificationChannel)42);

        // Assert
        emailPayload.From.Should().Be("sender@example.test");
        emailPayload.FromDisplayName.Should().Be("HoneyDrunk");
        emailPayload.Headers.Should().ContainKey("X-Test");
        keyed.Should().BeSameAs(keyedSender);
        fallback.Should().BeSameAs(fallbackSender);
        missing.Should().Throw<InvalidOperationException>().WithMessage("*42*");
    }

    /// <summary>
    /// Verifies provider and queue options expose default values and retain configured values.
    /// </summary>
    [Fact]
    public void ProviderAndQueueOptions_ExposeDefaultsAndConfiguredValues()
    {
        // Arrange / Act
        var smtp = new HoneyDrunk.Notify.Providers.Email.Smtp.SmtpOptions
        {
            Host = "smtp.example.test",
            Port = 2525,
            UseSsl = true,
            FromAddress = "from@example.test",
            FromDisplayName = "HoneyDrunk",
        };
        var resend = new HoneyDrunk.Notify.Providers.Email.Resend.ResendOptions
        {
            FromAddress = "resend@example.test",
            FromDisplayName = "Resend",
        };
        var twilio = new HoneyDrunk.Notify.Providers.Sms.Twilio.TwilioOptions
        {
            FromNumber = "+15551234567",
        };
        var queue = new HoneyDrunk.Notify.Queue.AzureStorage.AzureStorageQueueOptions
        {
            QueueName = "notify-main",
            DeadLetterQueueName = "notify-dlq",
            ConnectionStringSecretName = "QueueSecret",
            CreateIfNotExists = false,
            MaxBatchSize = 16,
            MaxDeliveryAttempts = 9,
        };
        var notificationQueue = new NotificationQueueOptions
        {
            QueueName = "notify",
            DeadLetterQueueName = null,
        };

        // Assert
        smtp.Host.Should().Be("smtp.example.test");
        smtp.Port.Should().Be(2525);
        smtp.UseSsl.Should().BeTrue();
        smtp.FromAddress.Should().Be("from@example.test");
        smtp.FromDisplayName.Should().Be("HoneyDrunk");
        resend.FromAddress.Should().Be("resend@example.test");
        resend.FromDisplayName.Should().Be("Resend");
        twilio.FromNumber.Should().Be("+15551234567");
        queue.QueueName.Should().Be("notify-main");
        queue.EffectiveDeadLetterQueueName.Should().Be("notify-dlq");
        queue.ConnectionStringSecretName.Should().Be("QueueSecret");
        queue.CreateIfNotExists.Should().BeFalse();
        queue.MaxBatchSize.Should().Be(16);
        queue.MaxDeliveryAttempts.Should().Be(9);
        notificationQueue.EffectiveDeadLetterQueueName.Should().Be("notify-dlq");
    }

    /// <summary>
    /// Verifies Azure Storage queue registration exposes both queue and dead-letter abstractions.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task AzureStorageQueueRegistration_ExposesQueueAndDeadLetterInspector()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<ISecretStore>(new FakeSecretStore());

        // Act
        services.AddHoneyDrunkNotifyAzureStorageQueue(options =>
        {
            options.ConnectionString = "UseDevelopmentStorage=true";
            options.QueueName = "notify";
        });
        await using var provider = services.BuildServiceProvider();

        // Assert
        provider.GetRequiredService<INotificationQueue>().Should().NotBeNull();
        provider.GetRequiredService<IDeadLetterInspector>().Should().NotBeNull();
    }

    private static ServiceProvider BuildProvider(Action<IServiceCollection> register)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<ISecretStore>(new FakeSecretStore());
        services.AddHoneyDrunkNotifyRuntime();
        register(services);
        return services.BuildServiceProvider();
    }

    private static NotificationDispatcher Dispatcher(SequenceSender sender, int maxAttempts) =>
        new(
            new StubResolver(sender),
            Microsoft.Extensions.Options.Options.Create(new NotifyRuntimeOptions
            {
                MaxAttempts = maxAttempts,
                BaseDelay = TimeSpan.Zero,
                MaxDelay = TimeSpan.Zero,
            }),
            new ExponentialBackoffStrategy(),
            TimeProvider.System,
            NullLogger<NotificationDispatcher>.Instance);

    private static NotificationRequest Request(string address) =>
        new(NotificationChannel.Email, Recipient.Email(address), new TemplateKey("welcome"), new Dictionary<string, object?> { ["name"] = "Oleg" })
        {
            Priority = NotificationPriority.High,
            Tags = ["critical"],
        };

    private static NotificationEnvelope Envelope(NotificationChannel channel, string address) =>
        new(NotificationId.NewId(), channel, new Recipient(channel, address), new TemplateKey("welcome"), new Dictionary<string, object?>())
        {
            CorrelationId = "corr-1",
            TenantId = "tenant-1",
            CreatedAtUtc = DateTimeOffset.UtcNow,
        };

    private static DeliveryOutcome Success(NotificationEnvelope envelope) =>
        DeliveryOutcome.Succeeded(envelope.NotificationId, AttemptId.NewId(), envelope.Channel, "test");

    private static DeliveryOutcome Transient(NotificationEnvelope envelope) =>
        DeliveryOutcome.Failed(
            envelope.NotificationId,
            AttemptId.NewId(),
            envelope.Channel,
            "test",
            FailureKind.Transient,
            "retry later");

    private static DeliveryOutcome Permanent(NotificationEnvelope envelope) =>
        DeliveryOutcome.Failed(
            envelope.NotificationId,
            AttemptId.NewId(),
            envelope.Channel,
            "test",
            FailureKind.Permanent,
            "do not retry");

    private sealed class StubEmailRenderer : IEmailTemplateRenderer
    {
        public Task<EmailContent> RenderEmailAsync(
            TemplateKey templateKey,
            IReadOnlyDictionary<string, object?> model,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new EmailContent("Rendered subject", "Rendered body"));
    }

    private sealed class StubResolver(INotificationSender sender) : INotificationSenderResolver
    {
        public INotificationSender Resolve(NotificationChannel channel) => sender;
    }

    private sealed class RecordingNotificationQueue(params QueuedNotification[] batch) : INotificationQueue
    {
        private bool _dequeued;

        public TaskCompletionSource Processed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public List<QueuedNotification> Completed { get; } = [];

        public List<QueuedNotification> Abandoned { get; } = [];

        public List<(QueuedNotification queuedNotification, string reason)> DeadLettered { get; } = [];

        public Task EnqueueAsync(NotificationEnvelope envelope, CancellationToken ct = default) => Task.CompletedTask;

        public Task<IReadOnlyList<QueuedNotification>> DequeueBatchAsync(int max, CancellationToken ct = default)
        {
            if (_dequeued)
                return Task.FromResult<IReadOnlyList<QueuedNotification>>([]);

            _dequeued = true;
            return Task.FromResult<IReadOnlyList<QueuedNotification>>(batch.Take(max).ToArray());
        }

        public Task CompleteAsync(QueuedNotification item, CancellationToken ct = default)
        {
            Completed.Add(item);
            TrySignalProcessed();
            return Task.CompletedTask;
        }

        public Task AbandonAsync(QueuedNotification item, CancellationToken ct = default)
        {
            Abandoned.Add(item);
            TrySignalProcessed();
            return Task.CompletedTask;
        }

        public Task DeadLetterAsync(QueuedNotification item, string reason, CancellationToken ct = default)
        {
            DeadLettered.Add((item, reason));
            TrySignalProcessed();
            return Task.CompletedTask;
        }

        private void TrySignalProcessed()
        {
            if (Completed.Count + Abandoned.Count + DeadLettered.Count >= batch.Length)
                Processed.TrySetResult();
        }
    }

    private sealed class SequenceSender(params DeliveryOutcome[] outcomes) : INotificationSender
    {
        public int Calls { get; private set; }

        public Task<DeliveryOutcome> SendAsync(NotificationEnvelope envelope, CancellationToken cancellationToken = default)
        {
            var index = Math.Min(Calls, outcomes.Length - 1);
            Calls++;
            return Task.FromResult(outcomes[index]);
        }
    }

    private sealed class FakeSecretStore : ISecretStore
    {
        public Task<SecretValue> GetSecretAsync(SecretIdentifier identifier, CancellationToken cancellationToken = default) =>
            Task.FromResult(new SecretValue(identifier, "secret-value", version: null));

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

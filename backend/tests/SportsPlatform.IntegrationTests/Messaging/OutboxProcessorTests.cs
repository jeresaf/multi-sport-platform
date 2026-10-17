using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SportsPlatform.BuildingBlocks.Messaging;
using SportsPlatform.BuildingBlocks.Persistence.Messaging;
using SportsPlatform.BuildingBlocks.Tenancy;
using SportsPlatform.IntegrationTests.Infrastructure;

namespace SportsPlatform.IntegrationTests.Messaging;

/// <summary>Delivery from the outbox to consumers: at least once, handled once per consumer, poison ends FAILED.</summary>
public sealed class OutboxProcessorTests : DatabaseTest
{
    public OutboxProcessorTests(PostgresFixture fixture)
        : base(fixture) => App.Consumers.Reset();

    private OutboxProcessor Processor => App.Services.GetRequiredService<OutboxProcessor>();

    [Fact]
    public async Task Message_is_delivered_to_every_consumer_and_marked_processed()
    {
        var probeId = await CreateProbeAsync();

        (await Processor.ProcessOnceAsync(Ct)).ShouldBe(1);

        var message = (await OutboxAsync()).ShouldHaveSingleItem();
        message.Status.ShouldBe(OutboxMessageStatus.Processed);
        message.ProcessedAt.ShouldBe(App.Clock.GetUtcNow());
        message.Attempts.ShouldBe(0);
        App.Consumers.For(RecordingConsumer.Name).ShouldHaveSingleItem();
        App.Consumers.For(FlakyConsumer.Name).ShouldHaveSingleItem();

        await using var scope = App.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<MessagingTestDbContext>();
        (await context.ReceivedProbes.SingleAsync(Ct)).ShouldSatisfyAllConditions(
            received => received.ProbeId.ShouldBe(probeId),
            received => received.MessageId.ShouldBe(message.Id));
        (await context.Set<InboxMessage>().Select(m => m.Consumer).OrderBy(c => c).ToListAsync(Ct))
            .ShouldBe([FlakyConsumer.Name, RecordingConsumer.Name]);

        (await Processor.ProcessOnceAsync(Ct)).ShouldBe(0); // nothing left to do
    }

    [Fact]
    public async Task Consumer_runs_under_the_events_tenant_correlation_and_causation()
    {
        var tenantId = Guid.CreateVersion7();
        await CreateProbeAsync(tenantId, "req-7");

        await Processor.ProcessOnceAsync(Ct);

        var message = (await OutboxAsync()).ShouldHaveSingleItem();
        var delivery = App.Consumers.For(RecordingConsumer.Name).ShouldHaveSingleItem();
        delivery.TenantId.ShouldBe(tenantId);
        delivery.CorrelationId.ShouldBe("req-7");
        delivery.CausationId.ShouldBe(message.Id);
        delivery.Envelope.ShouldBe(message.ToEnvelope());
        delivery.Envelope.EventName.ShouldBe(new IntegrationEventName("ProbeCreated", 1));
    }

    [Fact]
    public async Task Message_without_consumers_is_marked_processed()
    {
        await using (var scope = App.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<MessagingTestDbContext>();
            var probeId = Guid.CreateVersion7();
            context.Publish(new ProbeIgnored.V1(probeId), nameof(MessagingProbe), probeId);
            await context.SaveChangesAsync(Ct);
        }

        await Processor.ProcessOnceAsync(Ct);

        (await OutboxAsync()).ShouldHaveSingleItem().Status.ShouldBe(OutboxMessageStatus.Processed);
        App.Consumers.Deliveries.ShouldBeEmpty();
    }

    [Fact]
    public async Task Duplicate_delivery_is_handled_once()
    {
        await CreateProbeAsync();
        await Processor.ProcessOnceAsync(Ct);

        // As if the processor stopped after the consumers committed but before it marked the message processed.
        await ExecuteAsync("sp_app", "UPDATE messaging_test.outbox_messages SET status = 'PENDING', processed_at = NULL");
        (await Processor.ProcessOnceAsync(Ct)).ShouldBe(1);

        (await OutboxAsync()).ShouldHaveSingleItem().Status.ShouldBe(OutboxMessageStatus.Processed);
        App.Consumers.For(RecordingConsumer.Name).ShouldHaveSingleItem();
        App.Consumers.For(FlakyConsumer.Name).ShouldHaveSingleItem();
        await using var scope = App.Services.CreateAsyncScope();
        (await scope.ServiceProvider.GetRequiredService<MessagingTestDbContext>().ReceivedProbes.CountAsync(Ct)).ShouldBe(1);
    }

    [Fact]
    public async Task Failed_consumer_is_retried_after_backoff_without_rerunning_the_others()
    {
        await CreateProbeAsync();
        App.Consumers.FlakyFailuresLeft = 1;

        await Processor.ProcessOnceAsync(Ct);

        var failed = (await OutboxAsync()).ShouldHaveSingleItem();
        failed.Status.ShouldBe(OutboxMessageStatus.Pending);
        failed.Attempts.ShouldBe(1);
        failed.NextAttemptAt.ShouldBe(App.Clock.GetUtcNow() + OutboxRetry.Delay(1));
        failed.LastError.ShouldNotBeNull().ShouldStartWith($"{FlakyConsumer.Name}: {nameof(InvalidOperationException)}");

        (await Processor.ProcessOnceAsync(Ct)).ShouldBe(0); // not due yet
        App.Clock.Advance(OutboxRetry.Delay(1));
        (await Processor.ProcessOnceAsync(Ct)).ShouldBe(1);

        var processed = (await OutboxAsync()).ShouldHaveSingleItem();
        processed.Status.ShouldBe(OutboxMessageStatus.Processed);
        processed.LastError.ShouldBeNull();
        App.Consumers.For(RecordingConsumer.Name).ShouldHaveSingleItem();
        App.Consumers.For(FlakyConsumer.Name).Count.ShouldBe(2);
    }

    [Fact]
    public async Task Poison_message_ends_failed_after_the_maximum_attempts()
    {
        await CreateProbeAsync();
        App.Consumers.FlakyFailuresLeft = int.MaxValue;
        var maxAttempts = MaxAttempts();

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            (await Processor.ProcessOnceAsync(Ct)).ShouldBe(1);
            App.Clock.Advance(OutboxRetry.MaxDelay);
        }

        var message = (await OutboxAsync()).ShouldHaveSingleItem();
        message.Status.ShouldBe(OutboxMessageStatus.Failed);
        message.Attempts.ShouldBe(maxAttempts);
        message.LastError.ShouldNotBeNull().ShouldContain(FlakyConsumer.Name);
        (await Processor.ProcessOnceAsync(Ct)).ShouldBe(0); // FAILED waits for a re-drive
        App.Consumers.For(FlakyConsumer.Name).Count.ShouldBe(maxAttempts);
    }

    [Fact]
    public async Task Unreadable_payload_counts_as_a_failed_attempt()
    {
        await CreateProbeAsync();
        await ExecuteAsync("sp_app", """UPDATE messaging_test.outbox_messages SET payload = '{"probeId": "not-a-uuid"}'""");

        await Processor.ProcessOnceAsync(Ct);

        var message = (await OutboxAsync()).ShouldHaveSingleItem();
        message.Status.ShouldBe(OutboxMessageStatus.Pending);
        message.Attempts.ShouldBe(1);
        message.LastError.ShouldNotBeNull().ShouldContain("JsonException");
        App.Consumers.Deliveries.ShouldBeEmpty();
    }

    [Fact]
    public async Task Concurrent_processors_deliver_each_message_once()
    {
        const int count = 20;
        for (var i = 0; i < count; i++)
        {
            await CreateProbeAsync();
        }

        var processor = Processor;
        var attempted = await Task.WhenAll(
            Task.Run(() => processor.ProcessOnceAsync(Ct), Ct),
            Task.Run(() => processor.ProcessOnceAsync(Ct), Ct));

        // Each message was claimed by exactly one of them (the batch size is above the count, so none is left over).
        attempted.Sum().ShouldBe(count);
        (await processor.ProcessOnceAsync(Ct)).ShouldBe(0);
        (await OutboxAsync()).ShouldAllBe(m => m.Status == OutboxMessageStatus.Processed);
        var recorded = App.Consumers.For(RecordingConsumer.Name);
        recorded.Count.ShouldBe(count);
        recorded.Select(d => d.Envelope.EventId).Distinct().Count().ShouldBe(count);
    }

    private int MaxAttempts() =>
        App.Services.GetRequiredService<IOptions<OutboxOptions>>().Value.MaxAttempts;

    private async Task<Guid> CreateProbeAsync(Guid? tenantId = null, string? correlationId = null)
    {
        await using var scope = App.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Set(tenantId);
        scope.ServiceProvider.GetRequiredService<CorrelationContext>().Set(correlationId, causationId: null);
        var context = scope.ServiceProvider.GetRequiredService<MessagingTestDbContext>();
        var probe = new MessagingProbe("Kisaasi Recreation Ground");
        context.Probes.Add(probe);
        await context.SaveChangesAsync(Ct);
        return probe.Id;
    }

    private async Task<List<OutboxMessage>> OutboxAsync()
    {
        await using var scope = App.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<MessagingTestDbContext>()
            .Set<OutboxMessage>().AsNoTracking().OrderBy(m => m.OccurredAt).ThenBy(m => m.Id).ToListAsync(Ct);
    }
}

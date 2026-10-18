using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Npgsql;
using SportsPlatform.BuildingBlocks.Actors;
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

    [Fact]
    public async Task Messages_of_one_aggregate_are_delivered_in_publication_order()
    {
        const int count = 20;
        var aggregateId = Guid.CreateVersion7();
        await using (var scope = App.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<MessagingTestDbContext>();
            for (var i = 1; i <= count; i++)
            {
                context.Publish(new ProbeCreated.V1(aggregateId, $"Round {i}"), nameof(MessagingProbe), aggregateId);
            }

            await context.SaveChangesAsync(Ct); // one save: one occurred_at for all of them
        }

        var rounds = 0;
        while (await Processor.ProcessOnceAsync(Ct) is var attempted && attempted > 0)
        {
            attempted.ShouldBe(1); // the next one waits until its predecessor is processed
            rounds++;
        }

        rounds.ShouldBe(count);
        var expected = Enumerable.Range(1, count).Select(i => $"Round {i}").ToList();
        (await DeliveredNamesAsync()).ShouldBe(expected);
    }

    [Fact]
    public async Task Later_message_waits_while_an_earlier_one_of_its_aggregate_is_retried()
    {
        var first = await CreateProbeAsync();
        await PublishAsync(first, "Kisaasi Recreation Ground, renamed");
        var other = await CreateProbeAsync();
        App.Consumers.FlakyFailuresLeft = 1; // the first message's flaky delivery fails

        (await Processor.ProcessOnceAsync(Ct)).ShouldBe(2); // first's first message and other's; not first's second

        var messages = await OutboxAsync();
        messages.Single(m => m.AggregateId == other).Status.ShouldBe(OutboxMessageStatus.Processed);
        var waiting = messages.Where(m => m.AggregateId == first).OrderBy(m => m.Sequence).ToList();
        waiting[0].ShouldSatisfyAllConditions(m => m.Status.ShouldBe(OutboxMessageStatus.Pending), m => m.Attempts.ShouldBe(1));
        waiting[1].ShouldSatisfyAllConditions(m => m.Status.ShouldBe(OutboxMessageStatus.Pending), m => m.Attempts.ShouldBe(0));

        App.Clock.Advance(OutboxRetry.Delay(1));
        (await Processor.ProcessOnceAsync(Ct)).ShouldBe(1);
        (await Processor.ProcessOnceAsync(Ct)).ShouldBe(1);

        (await OutboxAsync()).ShouldAllBe(m => m.Status == OutboxMessageStatus.Processed);
        (await DeliveredNamesAsync()).ShouldBe(
            ["Kisaasi Recreation Ground", "Kisaasi Recreation Ground", "Kisaasi Recreation Ground, renamed"]);
    }

    [Fact]
    public async Task Failed_message_holds_back_later_messages_of_its_aggregate()
    {
        var probe = await CreateProbeAsync();
        await PublishAsync(probe, "Kisaasi Recreation Ground, renamed");
        App.Consumers.FlakyFailuresLeft = int.MaxValue;

        for (var attempt = 1; attempt <= MaxAttempts(); attempt++)
        {
            (await Processor.ProcessOnceAsync(Ct)).ShouldBe(1);
            App.Clock.Advance(OutboxRetry.MaxDelay);
        }

        App.Consumers.FlakyFailuresLeft = 0;
        (await Processor.ProcessOnceAsync(Ct)).ShouldBe(0);
        var messages = (await OutboxAsync()).OrderBy(m => m.Sequence).ToList();
        messages[0].Status.ShouldBe(OutboxMessageStatus.Failed);
        messages[1].ShouldSatisfyAllConditions(m => m.Status.ShouldBe(OutboxMessageStatus.Pending), m => m.Attempts.ShouldBe(0));
    }

    [Fact]
    public async Task Message_locked_by_another_processor_is_skipped()
    {
        var locked = await CreateProbeAsync();
        var free = await CreateProbeAsync();

        await using (var connection = new NpgsqlConnection(Fixture.ConnectionString("sp_app")))
        {
            await connection.OpenAsync(Ct);
            await using var transaction = await connection.BeginTransactionAsync(Ct);
            await using (var claim = new NpgsqlCommand(
                $"SELECT id FROM messaging_test.outbox_messages WHERE aggregate_id = '{locked}' FOR UPDATE", connection, transaction))
            {
                await claim.ExecuteNonQueryAsync(Ct);
            }

            (await Processor.ProcessOnceAsync(Ct)).ShouldBe(1);
            var messages = await OutboxAsync();
            messages.Single(m => m.AggregateId == free).Status.ShouldBe(OutboxMessageStatus.Processed);
            messages.Single(m => m.AggregateId == locked).Status.ShouldBe(OutboxMessageStatus.Pending);

            await transaction.RollbackAsync(Ct);
        }

        (await Processor.ProcessOnceAsync(Ct)).ShouldBe(1);
        (await OutboxAsync()).ShouldAllBe(m => m.Status == OutboxMessageStatus.Processed);
    }

    [Fact]
    public async Task Delivery_that_loses_the_inbox_race_is_rolled_back_and_counts_as_handled()
    {
        await CreateProbeAsync();
        // Another delivery of the same message commits its inbox row after this one checked the inbox.
        App.Consumers.BeforeRecorderSaves = messageId => ExecuteAsync("sp_app", $"""
            INSERT INTO messaging_test.inbox_messages (message_id, consumer, created_at, created_by, updated_at, updated_by)
            VALUES ('{messageId}', '{RecordingConsumer.Name}', now(), '{SystemActor.Id}', now(), '{SystemActor.Id}')
            """);

        await Processor.ProcessOnceAsync(Ct);

        (await OutboxAsync()).ShouldHaveSingleItem().ShouldSatisfyAllConditions(
            m => m.Status.ShouldBe(OutboxMessageStatus.Processed),
            m => m.LastError.ShouldBeNull());
        await using var scope = App.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<MessagingTestDbContext>();
        (await context.ReceivedProbes.CountAsync(Ct)).ShouldBe(0); // the losing delivery's change rolled back
        (await context.Set<InboxMessage>().CountAsync(m => m.Consumer == RecordingConsumer.Name, Ct)).ShouldBe(1);
    }

    private async Task PublishAsync(Guid probeId, string name)
    {
        await using var scope = App.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<MessagingTestDbContext>();
        context.Publish(new ProbeCreated.V1(probeId, name), nameof(MessagingProbe), probeId);
        await context.SaveChangesAsync(Ct);
    }

    private async Task<List<string>> DeliveredNamesAsync()
    {
        var ids = App.Consumers.For(RecordingConsumer.Name).Select(d => d.Envelope.EventId).ToList();
        var payloads = (await OutboxAsync()).ToDictionary(m => m.Id, m => m.Payload);
        return [.. ids.Select(id => JsonSerializer.Deserialize<ProbeCreated.V1>(payloads[id], MessageSerialization.Options)!.Name)];
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
            .Set<OutboxMessage>().AsNoTracking().OrderBy(m => m.Sequence).ToListAsync(Ct);
    }
}

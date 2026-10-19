using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Messaging;
using SportsPlatform.BuildingBlocks.Persistence.Messaging;
using SportsPlatform.BuildingBlocks.Tenancy;
using SportsPlatform.IntegrationTests.Infrastructure;

namespace SportsPlatform.IntegrationTests.Messaging;

/// <summary>Domain events are dispatched before commit and integration events commit atomically with the change.</summary>
public sealed class OutboxWriteTests(PostgresFixture fixture) : DatabaseTest(fixture)
{
    [Fact]
    public async Task State_change_and_outbox_message_commit_together()
    {
        var tenantId = Guid.CreateVersion7();
        var userId = Guid.CreateVersion7();
        Guid probeId;
        await using (var scope = App.Services.CreateAsyncScope())
        {
            scope.ServiceProvider.GetRequiredService<TenantContext>().Set(tenantId);
            scope.ServiceProvider.GetRequiredService<ActorContext>().Set(new UserActor(userId));
            scope.ServiceProvider.GetRequiredService<CorrelationContext>().Set("req-42", causationId: null);
            var context = scope.ServiceProvider.GetRequiredService<MessagingTestDbContext>();
            var probe = new MessagingProbe("Bukoto Community Stadium");
            probeId = probe.Id;
            context.Probes.Add(probe);
            await context.SaveChangesAsync(Ct);
        }

        (await CountProbesAsync()).ShouldBe(1);
        var message = (await OutboxAsync()).ShouldHaveSingleItem();
        message.EventName.ShouldBe("ProbeCreated");
        message.EventVersion.ShouldBe(1);
        message.Status.ShouldBe(OutboxMessageStatus.Pending);
        message.Attempts.ShouldBe(0);
        message.OccurredAt.ShouldBe(App.Clock.GetUtcNow());
        message.NextAttemptAt.ShouldBe(message.OccurredAt);
        message.TenantId.ShouldBe(tenantId);
        message.CorrelationId.ShouldBe("req-42");
        message.CausationId.ShouldBeNull();
        message.ActorId.ShouldBe(userId);
        message.AggregateType.ShouldBe(nameof(MessagingProbe));
        message.AggregateId.ShouldBe(probeId);
        JsonSerializer.Deserialize<ProbeCreated.V1>(message.Payload, MessageSerialization.Options)
            .ShouldBe(new ProbeCreated.V1(probeId, "Bukoto Community Stadium"));
    }

    [Fact]
    public async Task Rolled_back_transaction_leaves_neither_change_nor_message()
    {
        await using (var scope = App.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<MessagingTestDbContext>();
            await using var transaction = await context.Database.BeginTransactionAsync(Ct);
            context.Probes.Add(new MessagingProbe("Kisaasi Recreation Ground"));
            await context.SaveChangesAsync(Ct);
            await transaction.RollbackAsync(Ct);
        }

        (await CountProbesAsync()).ShouldBe(0);
        (await OutboxAsync()).ShouldBeEmpty();
    }

    [Fact]
    public async Task Failed_save_leaves_neither_change_nor_message()
    {
        await using (var scope = App.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<MessagingTestDbContext>();
            context.Probes.Add(new MessagingProbe(new string('x', 201))); // name is varchar(200)
            await Should.ThrowAsync<DbUpdateException>(() => context.SaveChangesAsync(Ct));
        }

        (await CountProbesAsync()).ShouldBe(0);
        (await OutboxAsync()).ShouldBeEmpty();
    }

    [Fact]
    public async Task Events_raised_by_subscribers_are_dispatched_in_the_same_save()
    {
        Guid probeId;
        await using (var scope = App.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<MessagingTestDbContext>();
            var probe = new MessagingProbe("Ggaba Lakeside Rugby Ground");
            probe.Echo(remaining: 3);
            probeId = probe.Id;
            context.Probes.Add(probe);
            await context.SaveChangesAsync(Ct);
            probe.DomainEvents.ShouldBeEmpty();
        }

        await using var verify = App.Services.CreateAsyncScope();
        var saved = await verify.ServiceProvider.GetRequiredService<MessagingTestDbContext>().Probes.SingleAsync(p => p.Id == probeId, Ct);
        saved.Echoes.ShouldBe(4); // the first echo and the three it raised, one round each
    }

    [Fact]
    public async Task Subscribers_that_keep_raising_events_fail_the_save()
    {
        await using (var scope = App.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<MessagingTestDbContext>();
            var probe = new MessagingProbe("Metro Indoor Arena");
            probe.Echo(remaining: MessagingInterceptor.MaxDispatchRounds);
            context.Probes.Add(probe);
            var exception = await Should.ThrowAsync<InvalidOperationException>(() => context.SaveChangesAsync(Ct));
            exception.Message.ShouldContain("rounds");
        }

        (await CountProbesAsync()).ShouldBe(0);
        (await OutboxAsync()).ShouldBeEmpty();
    }

    [Fact]
    public async Task Synchronous_save_with_pending_domain_events_throws()
    {
        await using var scope = App.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<MessagingTestDbContext>();
        context.Probes.Add(new MessagingProbe("Bukoto Community Stadium"));

        Should.Throw<InvalidOperationException>(() => context.SaveChanges()).Message.ShouldContain("SaveChangesAsync");
    }

    [Fact]
    public async Task Publish_rejects_an_event_type_without_a_version()
    {
        await using var scope = App.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<MessagingTestDbContext>();

        Should.Throw<ArgumentException>(() => context.Publish(new UnversionedEvent(), "Probe", Guid.CreateVersion7()));
    }

    private sealed record UnversionedEvent : IIntegrationEvent;

    private async Task<int> CountProbesAsync()
    {
        await using var scope = App.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<MessagingTestDbContext>().Probes.CountAsync(Ct);
    }

    private async Task<List<OutboxMessage>> OutboxAsync()
    {
        await using var scope = App.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<MessagingTestDbContext>()
            .Set<OutboxMessage>().AsNoTracking().ToListAsync(Ct);
    }
}

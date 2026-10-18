using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SportsPlatform.BuildingBlocks.Persistence;
using SportsPlatform.BuildingBlocks.Persistence.Messaging;
using SportsPlatform.BuildingBlocks.Tenancy;
using SportsPlatform.IntegrationTests.Infrastructure;
using SportsPlatform.IntegrationTests.Messaging;

namespace SportsPlatform.IntegrationTests.Isolation;

/// <summary>Consumers run under the event envelope's tenant (Q15), so RLS lets them write that tenant's rows only.</summary>
public sealed class ConsumerRlsTests : DatabaseTest
{
    private static readonly Guid _tenant = new("0192a1b2-0000-7000-8000-0000000000a1");
    private static readonly Guid _otherTenant = new("0192a1b2-0000-7000-8000-0000000000b2");

    public ConsumerRlsTests(PostgresFixture fixture)
        : base(fixture) => App.Consumers.Reset();

    private OutboxProcessor Processor => App.Services.GetRequiredService<OutboxProcessor>();

    [Fact]
    public async Task A_consumer_writes_a_row_of_the_envelopes_tenant()
    {
        var noteId = Guid.CreateVersion7();
        await PublishAsync(new NoteRequested.V1(noteId, WriteAsTenantId: null));

        await Processor.ProcessOnceAsync(Ct);

        (await NoteOutboxAsync()).Status.ShouldBe(OutboxMessageStatus.Processed);
        (await NoteTenantsAsync()).ShouldBe([_tenant]);
    }

    [Fact]
    public async Task A_consumer_cannot_write_a_row_of_another_tenant()
    {
        await PublishAsync(new NoteRequested.V1(Guid.CreateVersion7(), WriteAsTenantId: _otherTenant));

        await Processor.ProcessOnceAsync(Ct);

        var message = await NoteOutboxAsync();
        message.ShouldSatisfyAllConditions(
            m => m.Status.ShouldBe(OutboxMessageStatus.Pending),
            m => m.Attempts.ShouldBe(1),
            // 42501 here can only be RLS: sp_app may insert into this table (TableAccessPolicies).
            m => m.LastError.ShouldNotBeNull().ShouldContain("PostgresException: 42501"));
        (await NoteTenantsAsync()).ShouldBeEmpty();
    }

    private async Task PublishAsync(NoteRequested.V1 integrationEvent)
    {
        await using var scope = App.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Set(_tenant);
        var context = scope.ServiceProvider.GetRequiredService<MessagingTestDbContext>();
        context.Publish(integrationEvent, nameof(ConsumedNote), integrationEvent.NoteId);
        await context.SaveChangesAsync(Ct);
    }

    private async Task<OutboxMessage> NoteOutboxAsync()
    {
        await using var scope = App.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<MessagingTestDbContext>().Set<OutboxMessage>().AsNoTracking()
            .SingleAsync(m => m.EventName == "NoteRequested", Ct);
    }

    /// <summary>Every tenant's notes, as <c>sp_platform</c> (BYPASSRLS) sees them.</summary>
    private Task<List<Guid>> NoteTenantsAsync() =>
        QueryAsync(DatabaseRoles.Platform, "SELECT tenant_id FROM messaging_test.consumed_notes", reader => reader.GetGuid(0));
}

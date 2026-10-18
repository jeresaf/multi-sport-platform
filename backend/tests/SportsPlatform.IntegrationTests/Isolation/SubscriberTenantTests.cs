using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SportsPlatform.BuildingBlocks.Persistence;
using SportsPlatform.BuildingBlocks.Tenancy;
using SportsPlatform.IntegrationTests.Infrastructure;
using SportsPlatform.IntegrationTests.Messaging;

namespace SportsPlatform.IntegrationTests.Isolation;

/// <summary>
/// The tenant interceptor runs after domain event dispatch (MessagingInterceptor first), so rows that subscribers add
/// during the save are stamped and checked like any other.
/// </summary>
public sealed class SubscriberTenantTests(PostgresFixture fixture) : DatabaseTest(fixture)
{
    private static readonly Guid _tenant = new("0192a1b2-0000-7000-8000-0000000000a1");

    [Fact]
    public async Task A_row_a_subscriber_adds_gets_the_scopes_tenant()
    {
        var noteId = Guid.CreateVersion7();
        await using (var scope = App.Services.CreateAsyncScope())
        {
            scope.ServiceProvider.GetRequiredService<TenantContext>().Set(_tenant);
            var context = scope.ServiceProvider.GetRequiredService<MessagingTestDbContext>();
            var probe = new MessagingProbe("Kololo Airstrip Grounds");
            probe.Note(noteId);
            context.Probes.Add(probe);
            await context.SaveChangesAsync(Ct);
        }

        await using (var scope = App.Services.CreateAsyncScope())
        {
            scope.ServiceProvider.GetRequiredService<TenantContext>().Set(_tenant);
            var note = await scope.ServiceProvider.GetRequiredService<MessagingTestDbContext>().ConsumedNotes.SingleAsync(Ct);
            note.ShouldSatisfyAllConditions(n => n.Id.ShouldBe(noteId), n => n.TenantId.ShouldBe(_tenant));
        }
    }

    [Fact]
    public async Task A_row_a_subscriber_adds_without_a_tenant_is_refused()
    {
        await using var scope = App.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<MessagingTestDbContext>();
        var probe = new MessagingProbe("Kololo Airstrip Grounds");
        probe.Note(Guid.CreateVersion7());
        context.Probes.Add(probe);

        (await Should.ThrowAsync<TenantIsolationViolationException>(() => context.SaveChangesAsync(Ct)))
            .Violation.ShouldBe(TenantIsolationViolation.NoTenant);
    }
}

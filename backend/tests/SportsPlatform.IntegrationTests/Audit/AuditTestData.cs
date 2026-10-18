using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Auditing;
using SportsPlatform.Modules.Platform.Domain.Audit;
using SportsPlatform.Modules.Platform.Infrastructure;

namespace SportsPlatform.IntegrationTests.Audit;

/// <summary>Reads and writes <c>audit.audit_entries</c> for the audit tests.</summary>
internal static class AuditTestData
{
    public static AuditRecord Record(string actionCode = "platform.outbox.redrive", string? reason = "Kawempe fixture consumer fixed") => new(
        Guid.CreateVersion7(), actionCode, AuditOutcome.Success, null, ActorKind.System, SystemActor.Id, null,
        "OutboxMessage", Guid.CreateVersion7(), new DateTimeOffset(2026, 10, 17, 9, 0, 0, TimeSpan.Zero), AuditSource.Api,
        "kmsa-req-0001", reason, null);

    public static async Task<List<AuditEntry>> EntriesAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        await using var scope = services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<PlatformDbContext>().Set<AuditEntry>()
            .AsNoTracking().OrderBy(e => e.Id).ToListAsync(cancellationToken);
    }
}

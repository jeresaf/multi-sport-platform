using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SportsPlatform.BuildingBlocks.Domain;
using SportsPlatform.BuildingBlocks.Tenancy;

namespace SportsPlatform.BuildingBlocks.Persistence;

/// <summary>
/// Before a save: stamps <see cref="ITenantOwned.TenantId"/> on added entities from the tenant context (through the
/// change tracker; the property has no public setter), and throws when a tenant-owned entity is saved without a tenant
/// or belongs to another tenant. A mismatch is a bug, never an expected failure (docs/architecture/multi-tenancy.md).
/// Registered after <see cref="Messaging.MessagingInterceptor"/>, so rows that domain event subscribers add are
/// checked too.
/// </summary>
public sealed class TenantOwnershipInterceptor(ITenantContext tenant) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        Check(eventData.Context);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        Check(eventData.Context);
        return ValueTask.FromResult(result);
    }

    private void Check(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        foreach (var entry in context.ChangeTracker.Entries<ITenantOwned>())
        {
            if (entry.State is EntityState.Detached or EntityState.Unchanged)
            {
                continue;
            }

            var current = tenant.TenantId ?? throw new TenantIsolationViolationException(
                TenantIsolationViolation.NoTenant,
                $"A {entry.Entity.GetType().Name} is tenant-owned and there is no tenant; set the tenant context before saving.");
            var tenantId = entry.Property(e => e.TenantId);
            if (entry.State == EntityState.Added && tenantId.CurrentValue == Guid.Empty)
            {
                tenantId.CurrentValue = current;
                continue;
            }

            if (tenantId.CurrentValue != current || (entry.State != EntityState.Added && tenantId.OriginalValue != current))
            {
                throw new TenantIsolationViolationException(
                    TenantIsolationViolation.OtherTenant,
                    $"A {entry.Entity.GetType().Name} of another tenant cannot be {Verb(entry)} under tenant {current}.");
            }
        }
    }

    private static string Verb(EntityEntry entry) => entry.State switch
    {
        EntityState.Added => "added",
        EntityState.Deleted => "deleted",
        _ => "changed",
    };
}

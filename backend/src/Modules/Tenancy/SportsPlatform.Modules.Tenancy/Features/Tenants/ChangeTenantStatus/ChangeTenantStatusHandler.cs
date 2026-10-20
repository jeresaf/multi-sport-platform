using Microsoft.EntityFrameworkCore;
using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Api;
using SportsPlatform.BuildingBlocks.Auditing;
using SportsPlatform.BuildingBlocks.Persistence;
using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.Modules.Tenancy.Domain.Settings;
using SportsPlatform.Modules.Tenancy.Domain.Tenants;
using SportsPlatform.Modules.Tenancy.Features.Domains;
using SportsPlatform.Modules.Tenancy.Infrastructure;

namespace SportsPlatform.Modules.Tenancy.Features.Tenants.ChangeTenantStatus;

/// <summary>
/// Moves a tenant along the state machine. The Host runs it under the route's tenant (S02 D2): the status-history row, the audit
/// entry (with the reason) and <c>TenantStatusChanged.V1</c> commit together and carry the target tenant. A transition
/// the state machine doesn't allow from the current status is <c>TENANT_INVALID_TRANSITION</c>. A change clears the
/// public host cache.
/// </summary>
public sealed class ChangeTenantStatusHandler(
    TenancyDbContext context, ICurrentActor actor, AuditScope audit, TimeProvider timeProvider, TenantHostCache hostCache)
{
    public async Task<Result<TenantResponse>> HandleAsync(
        Guid tenantId, TenantTransition transition, string? reason, uint? version, IfMatch ifMatch, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ifMatch);
        audit.Describe(TenantAudit.EntityType, tenantId, reason);

        var tenant = await context.Set<Tenant>().SingleOrDefaultAsync(t => t.Id == tenantId, cancellationToken);
        if (tenant is null)
        {
            return TenancyErrors.NotFound;
        }

        var entry = context.Entry(tenant);
        if (!RowVersion.IsSatisfied(entry.Property<uint>(AuditColumns.Version).CurrentValue, version, ifMatch))
        {
            return TenancyErrors.ConcurrencyConflict;
        }

        var now = timeProvider.GetUtcNow();
        var changed = transition switch
        {
            TenantTransition.Activate => tenant.Activate(actor, now),
            TenantTransition.Suspend => tenant.Suspend(reason!, actor, now),
            TenantTransition.Restore => tenant.Restore(actor, now),
            TenantTransition.Archive => tenant.Archive(reason!, actor, now),
            TenantTransition.Unarchive => tenant.Unarchive(reason!, actor, now),
            _ => throw new ArgumentOutOfRangeException(nameof(transition), transition, null),
        };
        if (changed.IsFailure)
        {
            return changed.Error;
        }

        // For the response; read before the save, so nothing can fail after the change commits.
        var settings = await context.Set<TenantSettings>().AsNoTracking().SingleAsync(s => s.TenantId == tenantId, cancellationToken);
        var saved = await context.SaveChangesResultAsync(cancellationToken);
        if (saved.IsFailure)
        {
            return saved.Error;
        }

        // The status decides whether the public site is shown.
        hostCache.Clear();
        return Result.Success(TenantResponse.From(entry, settings));
    }
}

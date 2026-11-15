using Microsoft.EntityFrameworkCore;
using Npgsql;
using SportsPlatform.BuildingBlocks.Auditing;
using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.BuildingBlocks.Tenancy;
using SportsPlatform.Modules.SportsConfiguration.Contracts.Sports;
using SportsPlatform.Modules.Tenancy.Domain.Sports;
using SportsPlatform.Modules.Tenancy.Infrastructure;
using SportsPlatform.Modules.Tenancy.Infrastructure.Configurations;

namespace SportsPlatform.Modules.Tenancy.Features.Sports;

/// <summary>
/// Runs an enable or a disable on the request's tenant's sports (S05 D12, D13). The variant must be published
/// (<c>TENANT_SPORT_VARIANT_UNKNOWN</c>). The change runs in a transaction holding the tenant's sport lock
/// (<c>tenancy.tenant_sports:&lt;tenantId&gt;</c>, data-conventions.md) before it reads, so two enables at the last free
/// sport queue on the lock and the second counts the first's row.
/// </summary>
public sealed class TenantSportChanges(TenancyDbContext context, ISportDefinitions definitions, ITenantContext tenantContext, AuditScope audit)
{
    public const string AuditEntityType = "TenantSport";

    public async Task<Result> RunAsync(
        string variant,
        Func<TenantSports, SportVariant, Func<string, string?>, Task<Result<TenantSport>>> change,
        string? reason,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(change);
        var tenantId = tenantContext.TenantId ?? throw new InvalidOperationException("Tenant actions run under the request's tenant.");
        var published = await definitions.ListAsync(cancellationToken);
        var target = published.FirstOrDefault(v => string.Equals(v.Variant, variant, StringComparison.Ordinal));

        // Described before anything can refuse, so a refused enable or disable is audited with its variant and reason.
        // The variant as asked comes from the path: an unknown one is kept to a variant code's length (#88 R5).
        var asked = variant.Length > TenantSport.VariantMaxLength ? variant[..TenantSport.VariantMaxLength] : variant;
        audit.Describe(AuditEntityType, reason: reason, metadata: new TenantSportAuditMetadata(asked, target?.Sport));
        if (target is null)
        {
            return Result.Failure(TenancyErrors.SportVariantUnknown);
        }

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        var key = $"tenancy.tenant_sports:{tenantId}";
        await context.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock(hashtextextended({key}, 0))", cancellationToken);

        // Read after the lock: a new statement, so in READ COMMITTED it sees every enablement committed before it.
        var open = await context.Set<TenantSport>().Where(s => s.DisabledAt == null).ToListAsync(cancellationToken);
        var changed = await change(new TenantSports(open), target, EnabledSports.SportOf(published));
        if (changed.IsFailure)
        {
            return Result.Failure(changed.Error);
        }

        if (context.Entry(changed.Value).State == EntityState.Detached)
        {
            context.Add(changed.Value);
        }

        // Only now does the row exist or change: refusals above are audited without an entity.
        audit.Describe(entityId: changed.Value.Id);
        try
        {
            var saved = await context.SaveChangesResultAsync(cancellationToken);
            if (saved.IsFailure)
            {
                return Result.Failure(saved.Error);
            }
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: TenantSportConfiguration.OpenVariantIndex,
        })
        {
            // Not reachable while every enable holds the lock; kept so a missed lock can't answer 500.
            return Result.Failure(TenancyErrors.SportAlreadyEnabled);
        }

        // The audit counts as recorded once saved: a client hanging up now must not roll the change back unaudited.
        await transaction.CommitAsync(CancellationToken.None);
        return Result.Success();
    }
}

/// <summary>
/// The audit metadata of an enable or a disable: the variant as asked and its sport (null for an unknown variant). No
/// personal data.
/// </summary>
public sealed record TenantSportAuditMetadata(string Variant, string? Sport);

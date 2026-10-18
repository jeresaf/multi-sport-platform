using Microsoft.EntityFrameworkCore;
using Npgsql;
using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Auditing;
using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.BuildingBlocks.Tenancy;
using SportsPlatform.Modules.Tenancy.Domain.Tenants;
using SportsPlatform.Modules.Tenancy.Infrastructure;
using SportsPlatform.Modules.Tenancy.Infrastructure.Configurations;

namespace SportsPlatform.Modules.Tenancy.Features.Tenants.CreateTenant;

/// <summary>
/// Creates a tenant in <c>PROVISIONING</c> with its first status-history row; <c>TenantCreated.V1</c> and the audit
/// entry commit with it. The save runs under the new tenant (S02 D2), so the audit entry and the event's envelope carry
/// it. A slug is taken if any tenant, archived ones included, has it.
/// </summary>
public sealed class CreateTenantHandler(
    TenancyDbContext context, TenantContext tenantContext, ICurrentActor actor, AuditScope audit, TimeProvider timeProvider)
{
    public async Task<Result<TenantResponse>> HandleAsync(CreateTenantRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var profile = new TenantProfile(
            request.LegalName!.Trim(),
            request.DisplayName!.Trim(),
            request.CountryCode!,
            request.TimeZone!,
            request.DefaultLocale!,
            request.CurrencyCode!,
            request.PublicEnabled ?? false);
        var tenant = Tenant.Create(request.Slug!, profile, actor, timeProvider.GetUtcNow());

        // Before the scope's first database connection (S02 D1).
        tenantContext.Set(tenant.Id);
        audit.Describe(TenantAudit.EntityType, tenant.Id);
        if (await context.Set<Tenant>().AnyAsync(t => t.Slug == tenant.Slug, cancellationToken))
        {
            return TenancyErrors.SlugTaken;
        }

        var entry = context.Add(tenant);

        try
        {
            var saved = await context.SaveChangesResultAsync(cancellationToken);
            return saved.IsSuccess ? Result.Success(TenantResponse.From(entry)) : saved.Error;
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: TenantConfiguration.SlugIndex,
        })
        {
            // Another request created the slug between the check and the insert.
            return TenancyErrors.SlugTaken;
        }
    }
}

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
/// it. A slug is taken if any tenant, archived ones included, has it; that failure is audited without a tenant.
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

        // The check reads only the global tenants table, so it runs before the new tenant is set (S02 D1, #30 R2): a
        // taken slug is audited with no tenant rather than with an ID that never existed.
        if (await context.Set<Tenant>().AnyAsync(t => t.Slug == tenant.Slug, cancellationToken))
        {
            return TenancyErrors.SlugTaken;
        }

        // Before the first connection that writes tenant data: the save opens its own.
        tenantContext.Set(tenant.Id);
        audit.Describe(TenantAudit.EntityType, tenant.Id);
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
            // Another request created the slug between the check and the insert. Rare: this failure is audited with the
            // ID of the tenant that was not created.
            return TenancyErrors.SlugTaken;
        }
    }
}

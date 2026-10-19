using Microsoft.EntityFrameworkCore;
using SportsPlatform.BuildingBlocks.Api;
using SportsPlatform.BuildingBlocks.Auditing;
using SportsPlatform.BuildingBlocks.Persistence;
using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.Modules.Tenancy.Domain.Tenants;
using SportsPlatform.Modules.Tenancy.Infrastructure;

namespace SportsPlatform.Modules.Tenancy.Features.Tenants.UpdateTenantProfile;

/// <summary>
/// Changes the fields sent, in any status. The Host runs it under the route's tenant (S02 D2). A sent <c>version</c> or
/// <c>If-Match</c> that is not the current version, or a change saved by someone else in between, is
/// <c>TENANT_CONCURRENCY_CONFLICT</c>. A request that
/// changes nothing saves nothing (the Host still audits it).
/// </summary>
public sealed class UpdateTenantProfileHandler(TenancyDbContext context, AuditScope audit)
{
    public async Task<Result<TenantResponse>> HandleAsync(
        Guid tenantId, UpdateTenantProfileRequest request, IfMatch ifMatch, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        audit.Describe(TenantAudit.EntityType, tenantId, metadata: new UpdateTenantProfileMetadata(SentFields(request)));

        var tenant = await context.Set<Tenant>().SingleOrDefaultAsync(t => t.Id == tenantId, cancellationToken);
        if (tenant is null)
        {
            return TenancyErrors.NotFound;
        }

        // A change saved by someone else after this read still conflicts on save: xmin is the concurrency token.
        var entry = context.Entry(tenant);
        if (!TenantVersion.IsSatisfied(entry.Property<uint>(AuditColumns.Version).CurrentValue, request.Version, ifMatch))
        {
            return TenancyErrors.ConcurrencyConflict;
        }

        var current = tenant.Profile;
        var changed = tenant.UpdateProfile(current with
        {
            LegalName = request.LegalName?.Trim() ?? current.LegalName,
            DisplayName = request.DisplayName?.Trim() ?? current.DisplayName,
            CountryCode = request.CountryCode ?? current.CountryCode,
            TimeZone = request.TimeZone ?? current.TimeZone,
            DefaultLocale = request.DefaultLocale ?? current.DefaultLocale,
            CurrencyCode = request.CurrencyCode ?? current.CurrencyCode,
            PublicEnabled = request.PublicEnabled ?? current.PublicEnabled,
        });
        if (changed)
        {
            var saved = await context.SaveChangesResultAsync(cancellationToken);
            if (saved.IsFailure)
            {
                return saved.Error;
            }
        }

        return Result.Success(TenantResponse.From(entry));
    }

    private static string[] SentFields(UpdateTenantProfileRequest request) =>
    [
        .. new (string Field, bool Sent)[]
        {
            ("legalName", request.LegalName is not null),
            ("displayName", request.DisplayName is not null),
            ("countryCode", request.CountryCode is not null),
            ("timeZone", request.TimeZone is not null),
            ("defaultLocale", request.DefaultLocale is not null),
            ("currencyCode", request.CurrencyCode is not null),
            ("publicEnabled", request.PublicEnabled is not null),
        }.Where(f => f.Sent).Select(f => f.Field),
    ];
}

/// <summary>The names of the fields the request sent; the audit entry records which fields, not their values.</summary>
public sealed record UpdateTenantProfileMetadata(IReadOnlyList<string> Fields);

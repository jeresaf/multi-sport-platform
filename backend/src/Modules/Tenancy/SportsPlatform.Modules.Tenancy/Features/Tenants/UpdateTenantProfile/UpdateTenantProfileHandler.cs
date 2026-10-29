using Microsoft.EntityFrameworkCore;
using SportsPlatform.BuildingBlocks.Api;
using SportsPlatform.BuildingBlocks.Auditing;
using SportsPlatform.BuildingBlocks.Persistence;
using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.Modules.Tenancy.Domain.Settings;
using SportsPlatform.Modules.Tenancy.Domain.Tenants;
using SportsPlatform.Modules.Tenancy.Features.Access;
using SportsPlatform.Modules.Tenancy.Features.Domains;
using SportsPlatform.Modules.Tenancy.Infrastructure;

namespace SportsPlatform.Modules.Tenancy.Features.Tenants.UpdateTenantProfile;

/// <summary>
/// Changes the fields sent, in any status: the tenant's profile and, for the time zone, locale, currency and public
/// site, its settings row. The Host runs it under the route's tenant (S02 D2). A sent <c>version</c> or
/// <c>If-Match</c> that is not the tenant's current version, or a change to either row saved by someone else in
/// between, is <c>TENANT_CONCURRENCY_CONFLICT</c>. The version is the tenant row's, and a settings-only save doesn't
/// change it: any settings change after the caller's read, including one through this endpoint, is not detected (last
/// write wins; accepted by the owner, S02-T4 plan). A request that changes nothing saves nothing (the Host still audits
/// it).
/// </summary>
public sealed class UpdateTenantProfileHandler(
    TenancyDbContext context, AuditScope audit, TenantHostCache hostCache, TenantAccessCache accessCache)
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
        if (!RowVersion.IsSatisfied(entry.Property<uint>(AuditColumns.Version).CurrentValue, request.Version, ifMatch))
        {
            return TenancyErrors.ConcurrencyConflict;
        }

        // Settings are tenant-owned: visible because the Host runs this under the route's tenant.
        var settings = await context.Set<TenantSettings>().SingleAsync(x => x.TenantId == tenantId, cancellationToken);
        var currentProfile = tenant.Profile;
        var profileChanged = tenant.UpdateProfile(new TenantProfile(
            request.LegalName?.Trim() ?? currentProfile.LegalName,
            request.DisplayName?.Trim() ?? currentProfile.DisplayName,
            request.CountryCode ?? currentProfile.CountryCode));
        var currentSettings = settings.Values;
        var settingsChanged = settings.Update(currentSettings with
        {
            TimeZone = request.TimeZone ?? currentSettings.TimeZone,
            DefaultLocale = request.DefaultLocale ?? currentSettings.DefaultLocale,
            CurrencyCode = request.CurrencyCode ?? currentSettings.CurrencyCode,
            PublicEnabled = request.PublicEnabled ?? currentSettings.PublicEnabled,
        });
        if (profileChanged || settingsChanged)
        {
            var saved = await context.SaveChangesResultAsync(cancellationToken);
            if (saved.IsFailure)
            {
                return saved.Error;
            }
        }

        if (!string.Equals(currentSettings.TimeZone, settings.TimeZone, StringComparison.Ordinal))
        {
            // The time zone decides the tenant's date, so its subscription status and overrides in effect (S04 D7).
            accessCache.Clear(tenantId);
        }

        if (currentSettings.PublicEnabled != settings.PublicEnabled)
        {
            // publicEnabled decides whether the public site is shown.
            hostCache.Clear();
        }

        return Result.Success(TenantResponse.From(entry, settings));
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

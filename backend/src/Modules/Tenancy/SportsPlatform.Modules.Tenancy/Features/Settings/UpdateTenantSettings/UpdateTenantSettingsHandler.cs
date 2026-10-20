using Microsoft.EntityFrameworkCore;
using SportsPlatform.BuildingBlocks.Api;
using SportsPlatform.BuildingBlocks.Auditing;
using SportsPlatform.BuildingBlocks.Persistence;
using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.Modules.Tenancy.Domain.Settings;
using SportsPlatform.Modules.Tenancy.Features.Domains;
using SportsPlatform.Modules.Tenancy.Infrastructure;

namespace SportsPlatform.Modules.Tenancy.Features.Settings.UpdateTenantSettings;

/// <summary>
/// Changes the keys sent of the request's tenant's settings (the Host set the tenant from <c>X-Tenant-Id</c>). A sent
/// <c>version</c> or <c>If-Match</c> that is not the settings' current version, or a change saved by someone else in
/// between, is <c>TENANT_CONCURRENCY_CONFLICT</c>. A request that changes nothing saves nothing (the Host still audits it).
/// </summary>
public sealed class UpdateTenantSettingsHandler(TenancyDbContext context, AuditScope audit, TenantHostCache hostCache)
{
    public async Task<Result<TenantSettingsResponse>> HandleAsync(
        UpdateTenantSettingsRequest request, IfMatch ifMatch, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var settings = await context.Set<TenantSettings>().SingleAsync(cancellationToken);
        audit.Describe(TenantSettingsAudit.EntityType, settings.Id, metadata: new UpdateTenantSettingsMetadata(SentFields(request)));

        // A change saved by someone else after this read still conflicts on save: xmin is the concurrency token.
        var entry = context.Entry(settings);
        if (!RowVersion.IsSatisfied(entry.Property<uint>(AuditColumns.Version).CurrentValue, request.Version, ifMatch))
        {
            return TenancyErrors.ConcurrencyConflict;
        }

        var current = settings.Values;
        var changed = settings.Update(new TenantSettingsValues(
            request.TimeZone ?? current.TimeZone,
            request.DefaultLocale ?? current.DefaultLocale,
            request.CurrencyCode ?? current.CurrencyCode,
            request.PublicEnabled ?? current.PublicEnabled,
            request.StartingSoonMinutes ?? current.StartingSoonMinutes,
            request.MinorRegistrationConsent ?? current.MinorRegistrationConsent));
        if (changed)
        {
            var saved = await context.SaveChangesResultAsync(cancellationToken);
            if (saved.IsFailure)
            {
                return saved.Error;
            }
        }

        if (current.PublicEnabled != settings.PublicEnabled)
        {
            // publicEnabled decides whether the public site is shown.
            hostCache.Clear();
        }

        return Result.Success(TenantSettingsResponse.From(entry));
    }

    private static string[] SentFields(UpdateTenantSettingsRequest request) =>
    [
        .. new (string Field, bool Sent)[]
        {
            ("timeZone", request.TimeZone is not null),
            ("defaultLocale", request.DefaultLocale is not null),
            ("currencyCode", request.CurrencyCode is not null),
            ("publicEnabled", request.PublicEnabled is not null),
            ("startingSoonMinutes", request.StartingSoonMinutes is not null),
            ("minorRegistrationConsent", request.MinorRegistrationConsent is not null),
        }.Where(f => f.Sent).Select(f => f.Field),
    ];
}

/// <summary>The names of the keys the request sent; the audit entry records which keys, not their values.</summary>
public sealed record UpdateTenantSettingsMetadata(IReadOnlyList<string> Fields);

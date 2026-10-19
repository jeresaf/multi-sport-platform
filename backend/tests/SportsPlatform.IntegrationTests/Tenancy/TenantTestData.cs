using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Persistence;
using SportsPlatform.BuildingBlocks.Persistence.Messaging;
using SportsPlatform.BuildingBlocks.Tenancy;
using SportsPlatform.Modules.Tenancy.Contracts.Tenants;
using SportsPlatform.Modules.Tenancy.Domain.Domains;
using SportsPlatform.Modules.Tenancy.Domain.Tenants;
using SportsPlatform.Modules.Tenancy.Infrastructure;

namespace SportsPlatform.IntegrationTests.Tenancy;

/// <summary>Tenants for the Tenancy tests, from docs/product/pilot-customer.md.</summary>
internal static class TenantTestData
{
    public const string TenantsPath = "/api/platform/v1/tenants";

    public static readonly Guid StaffId = new("0192a1b2-0000-7000-8000-00000000b001");

    /// <summary>A valid create body for <paramref name="slug"/>, with <paramref name="changes"/> applied (null removes a field).</summary>
    public static Dictionary<string, object?> CreateBody(string slug = "kmsa", params (string Field, object? Value)[] changes)
    {
        var body = new Dictionary<string, object?>
        {
            ["slug"] = slug,
            ["legalName"] = "Kampala Metro Sports Association Limited",
            ["displayName"] = "Kampala Metro Sports Association",
            ["countryCode"] = "UG",
            ["timeZone"] = "Africa/Kampala",
            ["defaultLocale"] = "en-UG",
            ["currencyCode"] = "UGX",
        };
        foreach (var (field, value) in changes)
        {
            if (value is null)
            {
                body.Remove(field);
            }
            else
            {
                body[field] = value;
            }
        }

        return body;
    }

    /// <summary>
    /// A tenant saved straight through the module's DbContext, moved to <paramref name="status"/>, with its platform
    /// subdomain <c>&lt;slug&gt;.localhost</c> (unless the overload says otherwise); under the new tenant, like
    /// the handlers, because its status history is tenant-owned.
    /// </summary>
    public static Task<Guid> SaveTenantAsync(IServiceProvider services, string slug, TenantStatus status, CancellationToken cancellationToken) =>
        SaveTenantAsync(services, slug, status, publicEnabled: false, withDomain: true, cancellationToken);

    /// <summary>As the other overload; <paramref name="withDomain"/> false leaves the tenant without a domain.</summary>
    public static async Task<Guid> SaveTenantAsync(
        IServiceProvider services, string slug, TenantStatus status, bool publicEnabled, bool withDomain, CancellationToken cancellationToken)
    {
        await using var scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<TenancyDbContext>();
        var now = new DateTimeOffset(2026, 10, 1, 8, 0, 0, TimeSpan.Zero);
        var profile = new TenantProfile(
            $"{slug} Sports Council", $"{slug} Sports", "UG", "Africa/Kampala", "en", "UGX", publicEnabled);
        var tenant = Tenant.Create(slug, profile, SystemActor.Instance, now);
        if (status != TenantStatus.Provisioning)
        {
            tenant.Activate(SystemActor.Instance, now).IsSuccess.ShouldBeTrue();
        }

        if (status == TenantStatus.Suspended)
        {
            tenant.Suspend("Season fees outstanding", SystemActor.Instance, now).IsSuccess.ShouldBeTrue();
        }
        else if (status == TenantStatus.Archived)
        {
            tenant.Archive("Council merged into another association", SystemActor.Instance, now).IsSuccess.ShouldBeTrue();
        }

        scope.ServiceProvider.GetRequiredService<TenantContext>().Set(tenant.Id);
        context.Add(tenant);
        if (withDomain)
        {
            context.Add(TenantDomain.PlatformSubdomain(tenant.Id, slug, "localhost"));
        }

        await context.SaveChangesAsync(cancellationToken);
        return tenant.Id;
    }

    /// <summary>A tenant's status history, read under that tenant (it is tenant-owned).</summary>
    public static async Task<List<TenantStatusChange>> HistoryAsync(IServiceProvider services, Guid tenantId, CancellationToken cancellationToken)
    {
        await using var scope = services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Set(tenantId);
        return await scope.ServiceProvider.GetRequiredService<TenancyDbContext>().Set<TenantStatusChange>().AsNoTracking()
            .Where(c => c.TenantId == tenantId).OrderBy(c => c.OccurredAt).ThenBy(c => c.Id).ToListAsync(cancellationToken);
    }

    public static async Task<List<OutboxMessage>> OutboxAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        await using var scope = services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<TenancyDbContext>().Set<OutboxMessage>().AsNoTracking()
            .OrderBy(m => m.Sequence).ToListAsync(cancellationToken);
    }

    /// <summary>A tenant's status and version, read straight from the module's DbContext.</summary>
    public static async Task<TenantState> TenantAsync(IServiceProvider services, Guid tenantId, CancellationToken cancellationToken)
    {
        await using var scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<TenancyDbContext>();
        var tenant = await context.Set<Tenant>().AsNoTracking().SingleAsync(t => t.Id == tenantId, cancellationToken);
        var version = await context.Set<Tenant>().AsNoTracking().Where(t => t.Id == tenantId)
            .Select(t => EF.Property<uint>(t, AuditColumns.Version)).SingleAsync(cancellationToken);
        return new(tenant.Status, version);
    }

    public static async Task<List<TenantDomain>> DomainsAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        await using var scope = services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<TenancyDbContext>().Set<TenantDomain>().AsNoTracking()
            .OrderBy(d => d.Host).ToListAsync(cancellationToken);
    }

    public static async Task<int> TenantCountAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        await using var scope = services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<TenancyDbContext>().Set<Tenant>().CountAsync(cancellationToken);
    }
}

internal sealed record TenantState(TenantStatus Status, uint Version);

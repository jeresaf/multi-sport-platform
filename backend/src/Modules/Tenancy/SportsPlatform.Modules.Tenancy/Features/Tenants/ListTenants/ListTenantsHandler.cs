using Microsoft.EntityFrameworkCore;
using SportsPlatform.BuildingBlocks.Api;
using SportsPlatform.BuildingBlocks.Auditing;
using SportsPlatform.Modules.Tenancy.Contracts.Tenants;
using SportsPlatform.Modules.Tenancy.Domain.Tenants;
using SportsPlatform.Modules.Tenancy.Infrastructure;

namespace SportsPlatform.Modules.Tenancy.Features.Tenants.ListTenants;

/// <summary><paramref name="After"/> is the last slug of the previous page.</summary>
public sealed record TenantQuery(TenantStatus? Status, int Limit, string? After);

public sealed record ListTenantsResponse(IReadOnlyList<TenantResponse> Items, PageInfo Page);

/// <summary>
/// Every tenant, ordered by slug (ordinal, the slug's characters are ASCII). Saves nothing, so the Host writes this
/// read's audit entry (with the filter) after the response; it names no tenant.
/// </summary>
public sealed class ListTenantsHandler(TenancyDbContext context, AuditScope audit)
{
    public async Task<ListTenantsResponse> HandleAsync(TenantQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        audit.Describe(metadata: new ListTenantsMetadata(query.Status));

        var tenants = context.Set<Tenant>().AsNoTracking();
        if (query.Status is { } status)
        {
            tenants = tenants.Where(t => t.Status == status);
        }

        if (query.After is { } after)
        {
            // The slug column uses the "C" collation, so this comparison and the sort are ordinal.
            tenants = tenants.Where(t => t.Slug.CompareTo(after) > 0);
        }

        var page = await tenants
            .OrderBy(t => t.Slug)
            .Take(query.Limit + 1)
            .Select(TenantResponse.Projection)
            .ToListAsync(cancellationToken);
        var hasMore = page.Count > query.Limit;
        var items = page.Take(query.Limit).ToList();
        return new ListTenantsResponse(items, new PageInfo(hasMore ? TenantCursor.Encode(items[^1].Slug) : null, hasMore));
    }
}

/// <summary>The filter of the list, as this read's audit entry records it.</summary>
public sealed record ListTenantsMetadata(TenantStatus? Status);

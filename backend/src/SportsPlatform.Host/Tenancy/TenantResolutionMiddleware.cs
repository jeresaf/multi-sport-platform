using System.Globalization;
using Microsoft.Net.Http.Headers;
using SportsPlatform.BuildingBlocks.Actions;
using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Api;
using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.BuildingBlocks.Tenancy;
using SportsPlatform.Modules.Tenancy.Contracts.Access;
using SportsPlatform.Modules.Tenancy.Contracts.Tenants;

namespace SportsPlatform.Host.Tenancy;

/// <summary>
/// Sets the request's tenant from the matched endpoint (docs/architecture/multi-tenancy.md, "Resolving the tenant"),
/// after audit and before authorization (S02 D8), so a refused tenant on an audited action is audited as DENIED and
/// RLS applies to everything the endpoint reads or writes.
/// <list type="bullet">
/// <item>An action that needs a user, from a caller who is not signed in: 401 first, whatever the tenant (S03 D4).</item>
/// <item>A tenant action (the admin API): <c>X-Tenant-Id</c>, which the actor must be a member of.</item>
/// <item>A platform endpoint marked <c>.RunsUnderRouteTenant()</c>: the route's tenant, which must exist (D2).</item>
/// <item>A public endpoint marked <c>.RequirePublicTenant()</c>: the tenant of the request host (D6, D7), which must
/// show its public site. In Development only, <c>X-Tenant-Slug</c> or <c>?tenant=</c> names the tenant instead.</item>
/// <item>Anything else: no tenant. Admin and platform requests never take the tenant from the host.</item>
/// </list>
/// </summary>
internal sealed class TenantResolutionMiddleware(RequestDelegate next, IHostEnvironment environment)
{
    public const string TenantHeader = "X-Tenant-Id";
    public const string DevelopmentSlugHeader = "X-Tenant-Slug";
    public const string DevelopmentSlugQuery = "tenant";

    public async Task InvokeAsync(HttpContext context)
    {
        var endpoint = context.GetEndpoint();
        var required = endpoint?.Metadata.GetOrderedMetadata<RequiresActionMetadata>() ?? [];
        var routeTenant = endpoint?.Metadata.GetMetadata<RunsUnderRouteTenantMetadata>();
        var publicTenant = endpoint?.Metadata.GetMetadata<RequiresPublicTenantMetadata>() is not null;

        // Exactly one action, as the authorization middleware requires; anything else it refuses. The startup rules
        // allow .RequirePublicTenant() only on public endpoints, which declare no action. An anonymous caller on an
        // action that needs a user gets 401 before any tenant is looked up (S03 D4), so neither the header nor a
        // route's tenant tells them anything.
        var error = required.Count == 1 && !required[0].Action.AllowsAnonymous
                && context.RequestServices.GetRequiredService<ICurrentActor>().Kind == ActorKind.Anonymous
                ? AuthorizationErrors.Unauthenticated
            : publicTenant && required.Count == 0 ? await FromHostAsync(context)
            : required.Count != 1 ? null
            : required[0].Action.Scope == ActionScope.Tenant ? await FromHeaderAsync(context)
            : routeTenant is not null ? await FromRouteAsync(context, routeTenant.RouteValueName)
            : null;

        if (error is not null)
        {
            await error.ToProblem().ExecuteAsync(context);
            return;
        }

        await next(context);
    }

    private static async Task<Error?> FromHeaderAsync(HttpContext context)
    {
        if (context.Request.Headers[TenantHeader] is not [{ } value] || !Guid.TryParse(value, out var tenantId))
        {
            return AuthorizationErrors.TenantForbidden;
        }

        var memberships = context.RequestServices.GetRequiredService<ITenantMembershipChecker>();
        if (!await memberships.IsMemberAsync(tenantId, context.RequestAborted))
        {
            return AuthorizationErrors.TenantForbidden;
        }

        context.RequestServices.GetRequiredService<TenantContext>().Set(tenantId);
        return null;
    }

    private async Task<Error?> FromHostAsync(HttpContext context)
    {
        // The body depends on the forwarded host (and, in Development, the override), not only on the URL: tell
        // shared caches between the proxy and the API.
        var development = environment.IsDevelopment();
        context.Response.Headers.Append(HeaderNames.Vary, development ? $"X-Forwarded-Host, {DevelopmentSlugHeader}" : "X-Forwarded-Host");
        var developmentSlug = development ? DevelopmentSlug(context.Request) : null;

        var directory = context.RequestServices.GetRequiredService<ITenantDirectory>();
        var tenant = developmentSlug is not null
            ? await directory.GetBySlugAsync(developmentSlug, context.RequestAborted)
            : await directory.GetByHostAsync(context.Request.Host.Value ?? string.Empty, context.RequestAborted);
        if (tenant.IsFailure)
        {
            return tenant.Error;
        }

        // Whether the site is enabled is a tenant-owned setting, read under the tenant (RLS): set it first. A refused
        // request ends here, so the tenant it set reaches nothing else.
        context.RequestServices.GetRequiredService<TenantContext>().Set(tenant.Value.Id);
        var access = await context.RequestServices.GetRequiredService<ITenantAccess>()
            .CheckPublicAccessAsync(tenant.Value, context.RequestAborted);
        return access.IsFailure ? access.Error : null;
    }

    private static string? DevelopmentSlug(HttpRequest request) =>
        request.Headers[DevelopmentSlugHeader] is [{ Length: > 0 } header] ? header
        : request.Query[DevelopmentSlugQuery] is [{ Length: > 0 } query] ? query
        : null;

    private static async Task<Error?> FromRouteAsync(HttpContext context, string routeValueName)
    {
        // The startup rules require the parameter; a route without a :guid constraint can still carry a non-GUID,
        // which no tenant has (Guid.Empty), so the directory answers TENANT_NOT_FOUND.
        _ = Guid.TryParse(context.GetRouteValue(routeValueName)?.ToString(), CultureInfo.InvariantCulture, out var tenantId);
        var tenant = await context.RequestServices.GetRequiredService<ITenantDirectory>().GetByIdAsync(tenantId, context.RequestAborted);
        if (tenant.IsFailure)
        {
            return tenant.Error;
        }

        context.RequestServices.GetRequiredService<TenantContext>().Set(tenantId);
        return null;
    }
}

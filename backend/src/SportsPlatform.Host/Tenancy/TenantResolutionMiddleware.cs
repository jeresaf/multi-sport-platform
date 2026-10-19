using System.Globalization;
using SportsPlatform.BuildingBlocks.Actions;
using SportsPlatform.BuildingBlocks.Api;
using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.BuildingBlocks.Tenancy;
using SportsPlatform.Modules.Tenancy.Contracts.Tenants;

namespace SportsPlatform.Host.Tenancy;

/// <summary>
/// Sets the request's tenant from the matched endpoint (docs/architecture/multi-tenancy.md, "Resolving the tenant"),
/// after audit and before authorization (S02 D8), so a refused tenant on an audited action is audited as DENIED and
/// RLS applies to everything the endpoint reads or writes.
/// <list type="bullet">
/// <item>A tenant action (the admin API): <c>X-Tenant-Id</c>, which the actor must be a member of.</item>
/// <item>A platform endpoint marked <c>.RunsUnderRouteTenant()</c>: the route's tenant, which must exist (D2).</item>
/// <item>Anything else: no tenant. Public host resolution comes with S02-T3b.</item>
/// </list>
/// </summary>
internal sealed class TenantResolutionMiddleware(RequestDelegate next)
{
    public const string TenantHeader = "X-Tenant-Id";

    public async Task InvokeAsync(HttpContext context)
    {
        var endpoint = context.GetEndpoint();
        var required = endpoint?.Metadata.GetOrderedMetadata<RequiresActionMetadata>() ?? [];
        var routeTenant = endpoint?.Metadata.GetMetadata<RunsUnderRouteTenantMetadata>();

        // Exactly one action, as the authorization middleware requires; anything else it refuses.
        var error = required.Count != 1 ? null
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

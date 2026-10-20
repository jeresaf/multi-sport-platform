using SportsPlatform.BuildingBlocks.Actions;
using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Api;
using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.BuildingBlocks.Tenancy;
using SportsPlatform.Modules.Tenancy.Contracts.Access;

namespace SportsPlatform.Host.Authorization;

/// <summary>
/// The decision formula (docs/architecture/authorization.md) for the matched endpoint's declared action: an anonymous
/// caller gets 401 unless the action allows anonymous callers (S03 D4); then step 1, the
/// tenant's access mode, for a tenant action (status and, for a <c>PROVISIONING</c> tenant, the actor's setup roles,
/// S03 D11; <c>READ_ONLY</c> is not produced before S04), then the
/// evaluator (steps 2–3). Runs after tenant resolution and before the endpoint, so request bodies are not read for a
/// caller who is not allowed.
/// </summary>
internal sealed class ActionAuthorizationMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var endpoint = context.GetEndpoint();
        var required = endpoint?.Metadata.GetOrderedMetadata<RequiresActionMetadata>() ?? [];
        var isPublic = endpoint?.Metadata.GetMetadata<AllowPublicMetadata>() is not null;

        // No match (404), or the router's own unmarked 405 endpoint (not a RouteEndpoint): nothing to authorize.
        // A marked endpoint of any type goes through the checks below.
        if (endpoint is null || (endpoint is not RouteEndpoint && required.Count == 0 && !isPublic))
        {
            await next(context);
            return;
        }

        if (isPublic && required.Count == 0)
        {
            await next(context);
            return;
        }

        // Anything other than exactly one action is refused; the Host's startup check makes this unreachable.
        if (isPublic || required.Count != 1)
        {
            await AuthorizationErrors.Forbidden.ToProblem().ExecuteAsync(context);
            return;
        }

        var services = context.RequestServices;
        var action = required[0].Action;
        var actor = services.GetRequiredService<ICurrentActor>();

        // Not signed in, and the action needs a user: 401, not 403 (S03 D4).
        if (actor.Kind == ActorKind.Anonymous && !action.AllowsAnonymous)
        {
            await AuthorizationErrors.Unauthenticated.ToProblem().ExecuteAsync(context);
            return;
        }

        if (action.Scope == ActionScope.Tenant && await AccessModeAsync(services, action, context.RequestAborted) is { } refusal)
        {
            await refusal.ToProblem().ExecuteAsync(context);
            return;
        }

        var request = new AuthorizationRequest(action, actor, services.GetRequiredService<ITenantContext>().TenantId);
        var result = await services.GetRequiredService<IAuthorizationEvaluator>().AuthorizeAsync(request, context.RequestAborted);
        if (result.IsFailure)
        {
            await result.Error.ToProblem().ExecuteAsync(context);
            return;
        }

        await next(context);
    }

    /// <summary>
    /// Step 1. Tenant resolution has set a tenant the actor is a member of; without one it fails closed.
    /// </summary>
    private static async Task<Error?> AccessModeAsync(IServiceProvider services, ActionDefinition action, CancellationToken cancellationToken)
    {
        if (services.GetRequiredService<ITenantContext>().TenantId is not { } tenantId)
        {
            return AuthorizationErrors.TenantForbidden;
        }

        var access = await services.GetRequiredService<ITenantAccess>().CheckAdminAccessAsync(tenantId, action, cancellationToken);
        return access.IsFailure ? access.Error : null;
    }
}

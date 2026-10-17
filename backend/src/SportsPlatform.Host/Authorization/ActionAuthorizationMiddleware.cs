using SportsPlatform.BuildingBlocks.Actions;
using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Api;

namespace SportsPlatform.Host.Authorization;

/// <summary>
/// Steps 1–3 of the decision formula (docs/architecture/authorization.md) for the matched endpoint's declared action.
/// Runs after routing and before the endpoint, so request bodies are not read for a caller who is not allowed.
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
        var request = new AuthorizationRequest(required[0].Action, services.GetRequiredService<ICurrentActor>());
        var result = await services.GetRequiredService<IAuthorizationEvaluator>().AuthorizeAsync(request, context.RequestAborted);
        if (result.IsFailure)
        {
            await result.Error.ToProblem().ExecuteAsync(context);
            return;
        }

        await next(context);
    }
}

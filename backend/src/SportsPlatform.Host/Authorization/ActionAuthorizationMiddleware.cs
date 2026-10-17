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
    public async Task InvokeAsync(HttpContext context, IAuthorizationEvaluator evaluator, ICurrentActor actor)
    {
        // No match (404) or the router's own 405 endpoint, which is not a RouteEndpoint: nothing to authorize.
        if (context.GetEndpoint() is not RouteEndpoint endpoint)
        {
            await next(context);
            return;
        }

        var required = endpoint.Metadata.GetOrderedMetadata<RequiresActionMetadata>();
        var isPublic = endpoint.Metadata.GetMetadata<AllowPublicMetadata>() is not null;

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

        var result = await evaluator.AuthorizeAsync(new AuthorizationRequest(required[0].Action, actor), context.RequestAborted);
        if (result.IsFailure)
        {
            await result.Error.ToProblem().ExecuteAsync(context);
            return;
        }

        await next(context);
    }
}

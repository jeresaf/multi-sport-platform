using Microsoft.AspNetCore.Mvc;
using SportsPlatform.BuildingBlocks.Actions;
using SportsPlatform.BuildingBlocks.Api;
using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.Host.Authorization;
using SportsPlatform.Modules.Tenancy;

namespace SportsPlatform.Host.OpenApi;

/// <summary>
/// Documents the 401s and 403s the pipeline answers before an endpoint runs, which a module can't declare itself: an
/// action that needs a user can be <c>AUTH_UNAUTHENTICATED</c> (S03 D4); a state-changing request
/// <c>AUTH_CSRF_REQUIRED</c> (D7); a platform or tenant action <c>AUTH_FORBIDDEN</c> (the evaluator), and a tenant action
/// also <c>AUTH_TENANT_FORBIDDEN</c> (tenant resolution) and <c>TENANT_BLOCKED</c> (access mode, Tenancy's code).
/// </summary>
internal static class PipelineErrorDocumentation
{
    /// <summary>Runs after the endpoints' own conventions, so it sees their <c>.RequireAction(...)</c>.</summary>
    public static TBuilder DocumentPipelineErrors<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder
    {
        builder.Finally(endpoint =>
        {
            if (endpoint.Metadata.OfType<RequiresActionMetadata>().ToList() is not [{ Action: var action }])
            {
                return; // refused at startup (EndpointActionRules)
            }

            var methods = endpoint.Metadata.OfType<IHttpMethodMetadata>().LastOrDefault()?.HttpMethods ?? [];
            var errors = Errors(action, changesState: methods.Any(method => !CsrfProtection.IsSafe(method))).ToList();
            foreach (var status in errors.Select(error => error.Kind.ToStatusCode()).Distinct())
            {
                endpoint.Metadata.Add(new ProducesResponseTypeMetadata(status, typeof(ProblemDetails), [ErrorResponseExtensions.ProblemContentType]));
            }

            endpoint.Metadata.Add(new DeclaredErrorsMetadata(errors));
        });
        return builder;
    }

    private static IEnumerable<Error> Errors(ActionDefinition action, bool changesState)
    {
        if (!action.AllowsAnonymous)
        {
            yield return AuthorizationErrors.Unauthenticated;
        }

        if (changesState)
        {
            yield return AuthorizationErrors.CsrfRequired;
        }

        if (action.Scope != ActionScope.Account)
        {
            yield return AuthorizationErrors.Forbidden;
        }

        if (action.Scope == ActionScope.Tenant)
        {
            yield return AuthorizationErrors.TenantForbidden;
            yield return TenancyErrors.Blocked;
        }
    }
}

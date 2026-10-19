using Microsoft.AspNetCore.Mvc;
using SportsPlatform.BuildingBlocks.Actions;
using SportsPlatform.BuildingBlocks.Api;
using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.Modules.Tenancy;

namespace SportsPlatform.Host.OpenApi;

/// <summary>
/// Documents the 403s the pipeline answers before an endpoint runs, which a module can't declare itself: every action
/// can be <c>AUTH_FORBIDDEN</c> (the evaluator), and a tenant action also <c>AUTH_TENANT_FORBIDDEN</c> (tenant
/// resolution) and <c>TENANT_BLOCKED</c> (access mode, Tenancy's code).
/// </summary>
internal static class PipelineErrorDocumentation
{
    private static readonly Error[] _actionErrors = [AuthorizationErrors.Forbidden];

    private static readonly Error[] _tenantActionErrors = [AuthorizationErrors.Forbidden, AuthorizationErrors.TenantForbidden, TenancyErrors.Blocked];

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

            var errors = action.Scope == ActionScope.Tenant ? _tenantActionErrors : _actionErrors;
            endpoint.Metadata.Add(new ProducesResponseTypeMetadata(
                StatusCodes.Status403Forbidden, typeof(ProblemDetails), [ErrorResponseExtensions.ProblemContentType]));
            endpoint.Metadata.Add(new DeclaredErrorsMetadata(errors));
        });
        return builder;
    }
}

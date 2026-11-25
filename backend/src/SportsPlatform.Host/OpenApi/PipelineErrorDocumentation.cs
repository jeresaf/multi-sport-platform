using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Mvc;
using SportsPlatform.BuildingBlocks.Actions;
using SportsPlatform.BuildingBlocks.Api;
using SportsPlatform.BuildingBlocks.Entitlements;
using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.Host.Authorization;
using SportsPlatform.Modules.Platform;
using SportsPlatform.Modules.Platform.Contracts.Media;
using SportsPlatform.Modules.Platform.Features.Media;
using SportsPlatform.Modules.Tenancy;

namespace SportsPlatform.Host.OpenApi;

/// <summary>
/// Documents the 401s and 403s the pipeline answers before an endpoint runs, which a module can't declare itself: an
/// action that needs a user can be <c>AUTH_UNAUTHENTICATED</c> (S03 D4); a state-changing request
/// <c>AUTH_CSRF_REQUIRED</c> (D7); a platform or tenant action <c>AUTH_FORBIDDEN</c> (the evaluator), and a tenant action
/// also <c>AUTH_TENANT_FORBIDDEN</c> (tenant resolution), <c>TENANT_PROVISIONING</c> and, unless the action is available
/// when blocked, <c>TENANT_BLOCKED</c> (access mode, Tenancy's codes; S03 D11), unless available when read-only,
/// <c>TENANT_READ_ONLY</c> (S04 D8) and, when it names a boolean or level entitlement, <c>ENTITLEMENT_REQUIRED</c> (step 2,
/// S04 D10); when it names a quota, <c>ENTITLEMENT_QUOTA_EXCEEDED</c> (the handler counts, D12). An endpoint that runs under its route's
/// tenant can be <c>TENANT_NOT_FOUND</c> (tenant resolution), whichever module maps it; so can a public endpoint under the
/// request host's tenant (<c>.RequirePublicTenant()</c>), and <c>TENANT_UNAVAILABLE</c> when its site isn't public (S06-T6b). An endpoint that reads a body or
/// <c>If-Match</c> can be <c>VALIDATION_MALFORMED_REQUEST</c> (binding, or a header that doesn't parse). An endpoint that
/// stores an upload (<see cref="StoresMediaMetadata"/>) can be Platform's <c>MEDIA_TYPE_NOT_ALLOWED</c>,
/// <c>MEDIA_TOO_LARGE</c> and <c>MEDIA_IMAGE_UNREADABLE</c> (S06-T5b).
/// </summary>
internal static class PipelineErrorDocumentation
{
    /// <summary>Runs after the endpoints' own conventions, so it sees their <c>.RequireAction(...)</c>.</summary>
    public static TBuilder DocumentPipelineErrors<TBuilder>(this TBuilder builder, IEntitlementCatalogue entitlements)
        where TBuilder : IEndpointConventionBuilder
    {
        ArgumentNullException.ThrowIfNull(entitlements);
        builder.Finally(endpoint =>
        {
            if (endpoint.Metadata.OfType<RequiresActionMetadata>().ToList() is not [{ Action: var action }])
            {
                // A public endpoint declares no action; under the host's tenant it can be refused by tenant resolution.
                // Anything else is refused at startup (EndpointActionRules).
                if (endpoint.Metadata.OfType<RequiresPublicTenantMetadata>().Any())
                {
                    Document(endpoint, [TenancyErrors.NotFound, TenancyErrors.Unavailable]);
                }

                return;
            }

            var methods = endpoint.Metadata.OfType<IHttpMethodMetadata>().LastOrDefault()?.HttpMethods ?? [];
            var errors = Errors(action, changesState: methods.Any(method => !CsrfProtection.IsSafe(method))).ToList();
            if (action.Entitlement is { } code && entitlements.Find(code) is { } entitlement)
            {
                errors.Add(entitlement.Type == EntitlementType.Quota ? TenancyErrors.QuotaExceeded : TenancyErrors.EntitlementRequired);
            }

            if (endpoint.Metadata.OfType<IAcceptsMetadata>().Any() || endpoint.Metadata.OfType<AcceptsIfMatchMetadata>().Any())
            {
                errors.Add(CommonErrors.MalformedRequest);
            }

            if (endpoint.Metadata.OfType<StoresMediaMetadata>().LastOrDefault() is { } stores)
            {
                errors.AddRange([PlatformErrors.MediaTypeNotAllowed, PlatformErrors.MediaTooLarge, PlatformErrors.MediaImageUnreadable]);
                if (MediaRules.MinShorterSide(stores.Purpose) > 1)
                {
                    errors.Add(PlatformErrors.MediaImageTooSmall);
                }
            }

            if (endpoint.Metadata.OfType<RunsUnderRouteTenantMetadata>().Any())
            {
                errors.Add(TenancyErrors.NotFound);
            }

            Document(endpoint, errors);
        });
        return builder;
    }

    private static void Document(EndpointBuilder endpoint, IReadOnlyList<Error> errors)
    {
        foreach (var status in errors.Select(error => error.Kind.ToStatusCode()).Distinct())
        {
            endpoint.Metadata.Add(new ProducesResponseTypeMetadata(status, typeof(ProblemDetails), [ErrorResponseExtensions.ProblemContentType]));
        }

        endpoint.Metadata.Add(new DeclaredErrorsMetadata(errors));
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
            yield return TenancyErrors.Provisioning;
            if (!action.AvailableWhenBlocked)
            {
                yield return TenancyErrors.Blocked;
            }

            if (!action.AvailableWhenReadOnly)
            {
                yield return TenancyErrors.ReadOnly;
            }
        }
    }
}

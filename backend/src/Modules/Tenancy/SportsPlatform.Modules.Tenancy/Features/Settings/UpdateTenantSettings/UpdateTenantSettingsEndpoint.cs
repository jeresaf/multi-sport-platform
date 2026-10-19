using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using SportsPlatform.BuildingBlocks.Api;
using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.Modules.Tenancy.Features.Tenants;
using HttpJsonOptions = Microsoft.AspNetCore.Http.Json.JsonOptions;

namespace SportsPlatform.Modules.Tenancy.Features.Settings.UpdateTenantSettings;

internal static class UpdateTenantSettingsEndpoint
{
    public static void Map(IEndpointRouteBuilder settings) => settings
        .MapPatch("", HandleAsync)
        .WithName("UpdateTenantSettings")
        .WithSummary("Change the tenant's settings")
        .WithDescription(
            "Changes the keys sent of the settings of the tenant named by `X-Tenant-Id`; missing or null keys stay as " +
            "they are. Any other key, or a value of the wrong type, is 400 VALIDATION_INVALID_REQUEST with an `errors` " +
            "entry naming it. Send `version` from the last read to get 409 TENANT_CONCURRENCY_CONFLICT instead of " +
            "overwriting someone else's change; `If-Match` with the `ETag` works the same way (if both are sent, both " +
            "must match).")
        .RequireAction(TenancyActions.TenantSettingsUpdate)
        .AcceptsStrictJson<UpdateTenantSettingsRequest>()
        .ProducesErrors(CommonErrors.InvalidRequest, TenancyErrors.ConcurrencyConflict);

    private static async Task<Results<Ok<TenantSettingsResponse>, ProblemHttpResult>> HandleAsync(
        [FromBody] JsonElement body,
        UpdateTenantSettingsHandler handler,
        IOptions<HttpJsonOptions> json,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!TenantVersion.TryReadIfMatch(httpContext.Request, out var ifMatch, out var malformed))
        {
            return malformed;
        }

        if (!StrictJsonBody.TryRead<UpdateTenantSettingsRequest>(
                body, json.Value.SerializerOptions, CommonErrors.InvalidRequest, out var request, out var unreadable))
        {
            return unreadable;
        }

        if (await httpContext.ValidateRequestAsync(request, CommonErrors.InvalidRequest) is { } invalid)
        {
            return invalid;
        }

        var result = await handler.HandleAsync(request, ifMatch, cancellationToken);
        if (result.IsFailure)
        {
            return result.Error.ToProblem();
        }

        result.Value.SetETag(httpContext.Response);
        return TypedResults.Ok(result.Value);
    }
}

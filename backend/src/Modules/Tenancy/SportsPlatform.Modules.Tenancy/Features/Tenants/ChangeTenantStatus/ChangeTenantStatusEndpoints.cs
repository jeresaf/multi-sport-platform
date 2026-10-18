using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using SportsPlatform.BuildingBlocks.Actions;
using SportsPlatform.BuildingBlocks.Api;
using SportsPlatform.BuildingBlocks.Results;

namespace SportsPlatform.Modules.Tenancy.Features.Tenants.ChangeTenantStatus;

/// <summary><c>POST /tenants/{tenantId}/{activate|suspend|restore|archive|unarchive}</c>: one action each.</summary>
internal static class ChangeTenantStatusEndpoints
{
    private const string _versionNote =
        "Send `version` or `If-Match` from the last read to get 409 TENANT_CONCURRENCY_CONFLICT if the tenant changed since.";

    private const string _reasonNote =
        "The reason is kept permanently in the status history and the audit trail: don't include personal data.";

    public static void Map(IEndpointRouteBuilder tenants)
    {
        Map(tenants, "activate", TenantTransition.Activate, TenancyActions.TenantActivate, "ActivateTenant",
            "Activate a tenant", "PROVISIONING → ACTIVE: setup is done; the public site shows if `publicEnabled`.");
        MapWithReason(tenants, "suspend", TenantTransition.Suspend, TenancyActions.TenantSuspend, "SuspendTenant",
            "Suspend a tenant", "ACTIVE → SUSPENDED, with a reason: the tenant is blocked until restored.");
        Map(tenants, "restore", TenantTransition.Restore, TenancyActions.TenantRestore, "RestoreTenant",
            "Restore a suspended tenant", "SUSPENDED → ACTIVE.");
        MapWithReason(tenants, "archive", TenantTransition.Archive, TenancyActions.TenantArchive, "ArchiveTenant",
            "Archive a tenant", "ACTIVE or SUSPENDED → ARCHIVED, with a reason. Its data and slug are kept; the slug is never reused.");
        MapWithReason(tenants, "unarchive", TenantTransition.Unarchive, TenancyActions.TenantUnarchive, "UnarchiveTenant",
            "Unarchive a tenant", "ARCHIVED → SUSPENDED, with a reason; restore it as a separate step.");
    }

    private static void Map(
        IEndpointRouteBuilder tenants, string path, TenantTransition transition, ActionDefinition action, string name, string summary,
        string description) => tenants
        .MapPost($"/{{tenantId:guid}}/{path}", (
                Guid tenantId,
                ChangeTenantStatusRequest? request,
                ChangeTenantStatusHandler handler,
                HttpRequest httpRequest,
                HttpResponse response,
                CancellationToken cancellationToken) =>
            HandleAsync(tenantId, transition, null, request?.Version, handler, httpRequest, response, cancellationToken))
        .WithName(name)
        .WithSummary(summary)
        .WithDescription($"{description} The body is optional. {_versionNote}")
        .RequireAction(action)
        .ProducesErrors(TenancyErrors.NotFound, TenancyErrors.InvalidTransition, TenancyErrors.ConcurrencyConflict);

    private static void MapWithReason(
        IEndpointRouteBuilder tenants, string path, TenantTransition transition, ActionDefinition action, string name, string summary,
        string description) => tenants
        .MapPost($"/{{tenantId:guid}}/{path}", (
                Guid tenantId,
                ChangeTenantStatusWithReasonRequest request,
                ChangeTenantStatusHandler handler,
                HttpRequest httpRequest,
                HttpResponse response,
                CancellationToken cancellationToken) =>
            HandleAsync(tenantId, transition, request.Reason!.Trim(), request.Version, handler, httpRequest, response, cancellationToken))
        .WithName(name)
        .WithSummary(summary)
        .WithDescription($"{description} The body `{{ \"reason\": … }}` is required (no body: VALIDATION_MALFORMED_REQUEST). " +
            $"{_reasonNote} {_versionNote}")
        .RequireAction(action)
        .ValidateRequest<ChangeTenantStatusWithReasonRequest>(CommonErrors.ReasonRequired)
        .ProducesErrors(TenancyErrors.NotFound, TenancyErrors.InvalidTransition, TenancyErrors.ConcurrencyConflict);

    private static async Task<Results<Ok<TenantResponse>, ProblemHttpResult>> HandleAsync(
        Guid tenantId,
        TenantTransition transition,
        string? reason,
        uint? version,
        ChangeTenantStatusHandler handler,
        HttpRequest httpRequest,
        HttpResponse response,
        CancellationToken cancellationToken)
    {
        if (!TenantVersion.TryReadIfMatch(httpRequest, out var ifMatch, out var malformed))
        {
            return malformed;
        }

        var result = await handler.HandleAsync(tenantId, transition, reason, version, ifMatch, cancellationToken);
        if (result.IsFailure)
        {
            return result.Error.ToProblem();
        }

        result.Value.SetETag(response);
        return TypedResults.Ok(result.Value);
    }
}

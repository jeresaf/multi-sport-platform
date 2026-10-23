using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using SportsPlatform.BuildingBlocks.Api;

namespace SportsPlatform.Modules.Tenancy.Features.Access.GetTenantAccess;

/// <summary><c>GET /api/admin/v1/tenant/access</c>: the request's tenant's status, access mode and subscription (S03 D11, S04 D15).</summary>
internal static class GetTenantAccessEndpoint
{
    public const string Tag = "Tenant access";

    public static void Map(IEndpointRouteBuilder admin) => admin
        .MapGet("/tenant/access", HandleAsync)
        .WithTags(Tag)
        .WithName("GetTenantAccess")
        .WithSummary("Get the tenant's access")
        .WithDescription(
            "The status and access mode of the tenant named by `X-Tenant-Id`, with the reason and time of the change " +
            "that put it in that status, the tenant's time zone, and the current subscription period's status and days " +
            "(`null` without a period). Available while the tenant is suspended or archived, so its owner sees why every other " +
            "request is `403 TENANT_BLOCKED`, and while it is read-only, when writes are `403 TENANT_READ_ONLY`.")
        .RequireAction(TenancyActions.TenantAccessRead);

    private static async Task<Ok<TenantAccessResponse>> HandleAsync(GetTenantAccessHandler handler, CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(cancellationToken));
}

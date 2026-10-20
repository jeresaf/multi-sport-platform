using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace SportsPlatform.Modules.Identity.Features.Members;

/// <summary><c>/api/admin/v1/members</c> (S03-T4b): the memberships of the request's tenant (<c>X-Tenant-Id</c>) and their roles.</summary>
internal static class MemberEndpoints
{
    public const string Tag = "Members";

    /// <summary>The audit entity type of a membership; entries name it by ID, never by email.</summary>
    public const string EntityType = "TenantMembership";

    public const string VersionNote =
        "Send `version` or `If-Match` from the last read to get 409 AUTH_CONCURRENCY_CONFLICT if the member changed since.";

    public static RouteGroupBuilder Group(IEndpointRouteBuilder admin) => admin.MapGroup("/members").WithTags(Tag);

    public static string PathOf(Guid membershipId) => $"/api/admin/v1/members/{membershipId}";
}

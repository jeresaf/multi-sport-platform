using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace SportsPlatform.Modules.Identity.Features.Invitations;

/// <summary><c>/api/admin/v1/invitations</c> (S03-T4c): the invitations of the request's tenant (<c>X-Tenant-Id</c>).</summary>
internal static class InvitationEndpoints
{
    public const string Tag = "Invitations";

    /// <summary>The audit entity type of an invitation; entries name it by ID, never by email.</summary>
    public const string EntityType = "Invitation";

    public const string VersionNote =
        "Send `version` or `If-Match` from the last read to get 409 AUTH_CONCURRENCY_CONFLICT if the invitation changed since.";

    public const string InviteNote =
        "The invitation lasts 7 days; the email with its link goes out shortly after (one link at a time: a resent email's " +
        "link replaces the earlier one). A pending invitation to the same email is revoked and replaced.";

    public static RouteGroupBuilder Group(IEndpointRouteBuilder admin) => admin.MapGroup("/invitations").WithTags(Tag);

    public static string PathOf(Guid invitationId) => $"/api/admin/v1/invitations/{invitationId}";
}

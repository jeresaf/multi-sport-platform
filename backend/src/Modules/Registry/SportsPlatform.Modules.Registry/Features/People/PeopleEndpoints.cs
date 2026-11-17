using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace SportsPlatform.Modules.Registry.Features.People;

/// <summary><c>/api/admin/v1/people</c> (S06 D22): the people of the request's tenant (<c>X-Tenant-Id</c>).</summary>
internal static class PeopleEndpoints
{
    public const string Tag = "People";

    /// <summary>The audit entity type of a person; entries name people by ID, never by name (S06 D3).</summary>
    public const string EntityType = "Person";

    public static RouteGroupBuilder Group(IEndpointRouteBuilder admin) => admin.MapGroup("/people").WithTags(Tag);
}

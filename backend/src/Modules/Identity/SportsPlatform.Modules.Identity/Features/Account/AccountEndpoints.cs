using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace SportsPlatform.Modules.Identity.Features.Account;

/// <summary><c>/api/account/v1</c> (S03 D1): the caller's own account; no tenant, never cached.</summary>
internal static class AccountEndpoints
{
    public const string Tag = "Account";

    public const string UserEntityType = "User";

    public static RouteGroupBuilder Group(IEndpointRouteBuilder account) => account.MapGroup(string.Empty).WithTags(Tag);
}

using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using SportsPlatform.Modules.Identity.Infrastructure.Users;

namespace SportsPlatform.Modules.Identity.Infrastructure.Sessions;

/// <summary>
/// The session's claims: the user ID (<c>sub</c>) and the security stamp, nothing else. Identity's default adds the
/// user name and email, which are <c>PRIVATE</c> (ADR-0015); memberships and roles are read per request (S03 D5).
/// </summary>
internal sealed class SessionClaimsFactory(UserManager<User> users, IOptions<IdentityOptions> options)
    : UserClaimsPrincipalFactory<User>(users, options)
{
    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(User user)
    {
        var claims = Options.ClaimsIdentity;
        var identity = new ClaimsIdentity(SessionCookie.Scheme, claims.UserIdClaimType, claims.RoleClaimType);
        identity.AddClaim(new Claim(claims.UserIdClaimType, await UserManager.GetUserIdAsync(user)));
        identity.AddClaim(new Claim(claims.SecurityStampClaimType, await UserManager.GetSecurityStampAsync(user)));
        return identity;
    }
}

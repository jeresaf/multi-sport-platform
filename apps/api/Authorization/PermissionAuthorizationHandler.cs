using Microsoft.AspNetCore.Authorization;
using SportsPlatform.Modules.Identity.Entities;

namespace SportsPlatform.Api.Authorization;

public class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            return Task.CompletedTask;
        }

        // Platform owners have complete access. "Admin" is retained as a
        // compatibility alias for databases seeded before the authoritative
        // role names were introduced.
        if (context.User.IsInRole(SystemRoles.PlatformOwner) ||
            context.User.IsInRole("Admin"))
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        // Check if user has the permission claim directly
        var permissions = context.User.Claims
            .Where(c => c.Type == "permission")
            .Select(c => c.Value)
            .ToHashSet();

        if (permissions.Contains(requirement.Permission))
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        return Task.CompletedTask;
    }
}

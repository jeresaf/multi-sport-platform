using System.Security.Claims;
using SportsPlatform.Modules.Identity.Entities;

namespace SportsPlatform.Api.Authorization;

public static class TenantAccess
{
    public static Guid? TenantId(this ClaimsPrincipal user)
    {
        return Guid.TryParse(user.FindFirstValue("tenant_id"), out var tenantId)
            ? tenantId
            : null;
    }

    public static Guid? UserId(this ClaimsPrincipal user)
    {
        return Guid.TryParse(
            user.FindFirstValue(ClaimTypes.NameIdentifier),
            out var userId)
            ? userId
            : null;
    }

    public static bool CanAccessTenant(this ClaimsPrincipal user, Guid tenantId)
    {
        return user.IsPlatformOperator()
            || user.TenantId() == tenantId;
    }

    public static bool IsPlatformOperator(this ClaimsPrincipal user)
    {
        return user.IsInRole(SystemRoles.PlatformOwner)
            || user.IsInRole(SystemRoles.PlatformSupport)
            || user.IsInRole("Admin");
    }
}

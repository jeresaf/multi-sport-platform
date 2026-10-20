using SportsPlatform.Modules.Tenancy.Contracts.Access;
using SportsPlatform.Modules.Tenancy.Contracts.Tenants;

namespace SportsPlatform.Modules.Tenancy.Features.Access.GetTenantAccess;

/// <summary>
/// A tenant's access as its owner sees it: <c>status</c>, the <c>accessMode</c> derived from it, and the
/// <c>reason</c> (null when the change needs none, such as an activation) and time (<c>since</c>) of the change that put the
/// tenant in its status.
/// </summary>
public sealed record TenantAccessResponse(TenantStatus Status, TenantAccessMode AccessMode, string? Reason, DateTimeOffset Since);

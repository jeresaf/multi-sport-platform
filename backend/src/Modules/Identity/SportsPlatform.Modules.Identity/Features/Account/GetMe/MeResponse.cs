using SportsPlatform.BuildingBlocks.Privacy;
using SportsPlatform.Modules.Tenancy.Contracts.Tenants;

namespace SportsPlatform.Modules.Identity.Features.Account.GetMe;

/// <summary>The signed-in user: their own email, platform roles and the tenants they are an active member of.</summary>
public sealed record MeResponse(
    Guid Id,
    [property: PersonalData(PersonalDataClass.Private)] string Email,
    IReadOnlyList<string> PlatformRoles,
    IReadOnlyList<MeTenantResponse> Tenants);

/// <summary>
/// A tenant the user is an active member of: its slug (the workspace URL), display name and status, and the codes of the
/// roles they hold there (any scope). Every status is listed. A <c>SUSPENDED</c> or <c>ARCHIVED</c> tenant's workspace is
/// blocked, and its owner can read why; a <c>PROVISIONING</c> tenant's opens only for those setting it up (others get
/// <c>TENANT_PROVISIONING</c>).
/// </summary>
public sealed record MeTenantResponse(
    Guid TenantId, string Slug, string DisplayName, TenantStatus Status, IReadOnlyList<string> Roles);

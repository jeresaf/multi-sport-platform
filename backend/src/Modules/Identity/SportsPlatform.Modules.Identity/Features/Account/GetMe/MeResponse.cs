using SportsPlatform.BuildingBlocks.Privacy;

namespace SportsPlatform.Modules.Identity.Features.Account.GetMe;

/// <summary>The signed-in user: their own email, platform roles and tenants (memberships arrive in S03-T4).</summary>
public sealed record MeResponse(
    Guid Id,
    [property: PersonalData(PersonalDataClass.Private)] string Email,
    IReadOnlyList<string> PlatformRoles,
    IReadOnlyList<MeTenantResponse> Tenants);

/// <summary>A tenant the user is an active member of, with the user's role codes there.</summary>
public sealed record MeTenantResponse(Guid TenantId, IReadOnlyList<string> Roles);

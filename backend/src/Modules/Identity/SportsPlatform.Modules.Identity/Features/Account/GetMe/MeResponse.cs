using SportsPlatform.BuildingBlocks.Privacy;

namespace SportsPlatform.Modules.Identity.Features.Account.GetMe;

/// <summary>The signed-in user: their own email, platform roles and the tenants they are an active member of.</summary>
public sealed record MeResponse(
    Guid Id,
    [property: PersonalData(PersonalDataClass.Private)] string Email,
    IReadOnlyList<string> PlatformRoles,
    IReadOnlyList<MeTenantResponse> Tenants);

/// <summary>A tenant the user is an active member of, with the codes of the roles they hold there (any scope).</summary>
public sealed record MeTenantResponse(Guid TenantId, IReadOnlyList<string> Roles);

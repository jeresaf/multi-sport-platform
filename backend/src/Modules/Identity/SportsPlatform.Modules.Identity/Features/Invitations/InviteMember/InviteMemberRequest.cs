using System.Text.Json.Serialization;
using SportsPlatform.BuildingBlocks.Privacy;

namespace SportsPlatform.Modules.Identity.Features.Invitations.InviteMember;

/// <summary>Invite a person to the tenant: their email and the tenant roles they get, tenant-wide.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record InviteMemberRequest(
    [property: PersonalData(PersonalDataClass.Private)] string? Email,
    IReadOnlyList<string>? RoleCodes);

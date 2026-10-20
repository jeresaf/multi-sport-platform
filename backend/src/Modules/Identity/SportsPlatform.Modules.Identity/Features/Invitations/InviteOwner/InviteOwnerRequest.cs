using System.Text.Json.Serialization;
using SportsPlatform.BuildingBlocks.Privacy;

namespace SportsPlatform.Modules.Identity.Features.Invitations.InviteOwner;

/// <summary>Invite a tenant's owner (platform console).</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record InviteOwnerRequest([property: PersonalData(PersonalDataClass.Private)] string? Email);

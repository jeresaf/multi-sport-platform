using System.Text.Json.Serialization;
using SportsPlatform.BuildingBlocks.Privacy;

namespace SportsPlatform.Modules.Identity.Features.Account.ChangePassword;

/// <summary>Change the signed-in user's password.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ChangePasswordRequest(
    [property: PersonalData(PersonalDataClass.Sensitive)] string? CurrentPassword,
    [property: PersonalData(PersonalDataClass.Sensitive)] string? NewPassword);

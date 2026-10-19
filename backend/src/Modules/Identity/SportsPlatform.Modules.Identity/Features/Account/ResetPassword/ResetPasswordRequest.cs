using System.Text.Json.Serialization;
using SportsPlatform.BuildingBlocks.Privacy;

namespace SportsPlatform.Modules.Identity.Features.Account.ResetPassword;

/// <summary>Set a new password with the token from a reset email's link.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ResetPasswordRequest(
    [property: PersonalData(PersonalDataClass.Sensitive)] string? Token,
    [property: PersonalData(PersonalDataClass.Sensitive)] string? NewPassword);

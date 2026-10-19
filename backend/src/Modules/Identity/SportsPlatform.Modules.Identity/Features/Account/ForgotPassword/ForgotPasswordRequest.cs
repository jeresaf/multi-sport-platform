using System.Text.Json.Serialization;
using SportsPlatform.BuildingBlocks.Privacy;

namespace SportsPlatform.Modules.Identity.Features.Account.ForgotPassword;

/// <summary>Ask for a password reset email.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ForgotPasswordRequest([property: PersonalData(PersonalDataClass.Private)] string? Email);

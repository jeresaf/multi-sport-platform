using System.Text.Json.Serialization;
using SportsPlatform.BuildingBlocks.Privacy;

namespace SportsPlatform.Modules.Identity.Features.Account.Login;

/// <summary>Sign in with email and password.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record LoginRequest(
    [property: PersonalData(PersonalDataClass.Private)] string? Email,
    [property: PersonalData(PersonalDataClass.Sensitive)] string? Password);

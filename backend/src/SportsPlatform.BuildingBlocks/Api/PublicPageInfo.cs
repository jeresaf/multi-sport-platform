using SportsPlatform.BuildingBlocks.Privacy;

namespace SportsPlatform.BuildingBlocks.Api;

/// <summary>
/// <see cref="PageInfo"/> of a public list (S07-T5): the same members, classified <c>PUBLIC</c>, so a public response
/// passes the classification test. Use it only where the cursor is built from public fields alone (a public name's sort
/// key and an ID); an admin list's cursor may carry what isn't public, so <see cref="PageInfo"/> stays unclassified.
/// </summary>
public sealed record PublicPageInfo(
    [property: PersonalData(PersonalDataClass.Public)] string? NextCursor,
    [property: PersonalData(PersonalDataClass.Public)] bool HasMore);

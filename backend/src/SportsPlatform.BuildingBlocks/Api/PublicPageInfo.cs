using SportsPlatform.BuildingBlocks.Privacy;

namespace SportsPlatform.BuildingBlocks.Api;

// PageInfo's members classified PUBLIC, for public lists whose cursor is built from public fields alone (a public
// name's sort key and an ID), so the response passes PublicDtoClassificationTests (S07-T5a). An admin list's cursor may
// carry what isn't public, so PageInfo stays unclassified. A plain comment: a summary would reach OpenAPI.

/// <summary>The page of a public list: the cursor to send for the next page (null on the last) and whether there is one.</summary>
public sealed record PublicPageInfo(
    [property: PersonalData(PersonalDataClass.Public)] string? NextCursor,
    [property: PersonalData(PersonalDataClass.Public)] bool HasMore);

using SportsPlatform.BuildingBlocks.Privacy;
namespace SportsPlatform.Modules.Tenancy.Features.Site.GetSite;

/// <summary>
/// The public site's identity: <c>PUBLIC</c> fields only. <paramref name="PrimaryDomain"/> is the host the site
/// prefers, which may differ from the host the request came in on.
/// </summary>
public sealed record SiteResponse(
    [property: PersonalData(PersonalDataClass.Public)] string Slug,
    [property: PersonalData(PersonalDataClass.Public)] string DisplayName,
    [property: PersonalData(PersonalDataClass.Public)] string PrimaryDomain);

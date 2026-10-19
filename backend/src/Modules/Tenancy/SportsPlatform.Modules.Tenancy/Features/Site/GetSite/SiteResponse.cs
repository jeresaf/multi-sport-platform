namespace SportsPlatform.Modules.Tenancy.Features.Site.GetSite;

/// <summary>
/// The public site's identity: <c>PUBLIC</c> fields only. <paramref name="PrimaryDomain"/> is the host the site
/// prefers, which may differ from the host the request came in on.
/// </summary>
public sealed record SiteResponse(string Slug, string DisplayName, string PrimaryDomain);

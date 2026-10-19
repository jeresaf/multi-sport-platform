namespace SportsPlatform.Modules.Tenancy.Features.Domains;

/// <summary>
/// <c>Platform:BaseDomain</c>: the domain under which every tenant gets <c>&lt;slug&gt;.&lt;BaseDomain&gt;</c>. The
/// default <c>localhost</c> suits development; the real one is open question Q10. It must be a lower-case host name
/// without a port (checked when the app starts).
/// </summary>
public sealed class PlatformDomainOptions
{
    public const string SectionName = "Platform";

    public string BaseDomain { get; set; } = "localhost";
}

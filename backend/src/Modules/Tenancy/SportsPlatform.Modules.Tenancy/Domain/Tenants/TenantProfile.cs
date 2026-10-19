namespace SportsPlatform.Modules.Tenancy.Domain.Tenants;

/// <summary>
/// The fields of a tenant that the platform edits freely. <see cref="CountryCode"/> is an ISO 3166-1 alpha-2 code,
/// checked against Platform's reference data before it gets here. Settings live in <c>TenantSettings</c>.
/// </summary>
public sealed record TenantProfile(string LegalName, string DisplayName, string CountryCode)
{
    public const int LegalNameMaxLength = 200;
    public const int DisplayNameMaxLength = 100;
}

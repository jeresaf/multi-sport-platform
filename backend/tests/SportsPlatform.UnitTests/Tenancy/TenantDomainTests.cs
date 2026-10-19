using SportsPlatform.Modules.Tenancy.Domain.Domains;

namespace SportsPlatform.UnitTests.Tenancy;

public sealed class TenantDomainTests
{
    private static readonly Guid _kmsa = new("0192a1b2-0000-7000-8000-00000000a001");

    [Fact]
    public void Platform_subdomain_is_the_active_primary_slug_under_the_base_domain()
    {
        var domain = TenantDomain.PlatformSubdomain(_kmsa, "kmsa", "sports.test");

        domain.ShouldSatisfyAllConditions(
            d => d.Id.ShouldNotBe(Guid.Empty),
            d => d.TenantId.ShouldBe(_kmsa),
            d => d.Host.ShouldBe("kmsa.sports.test"),
            d => d.Type.ShouldBe(TenantDomainType.PlatformSubdomain),
            d => d.IsPrimary.ShouldBeTrue(),
            d => d.Status.ShouldBe(TenantDomainStatus.Active));
    }

    [Fact]
    public void Platform_subdomain_needs_a_tenant() =>
        Should.Throw<ArgumentException>(() => TenantDomain.PlatformSubdomain(Guid.Empty, "kmsa", "localhost"));

    [Theory]
    [InlineData("localhost:3000")]
    [InlineData("Sports.Test")]
    public void Platform_subdomain_needs_a_host_name(string baseDomain) =>
        Should.Throw<ArgumentException>(() => TenantDomain.PlatformSubdomain(_kmsa, "kmsa", baseDomain));
}

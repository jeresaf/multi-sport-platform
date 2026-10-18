using SportsPlatform.BuildingBlocks.Tenancy;

namespace SportsPlatform.UnitTests.BuildingBlocks;

/// <summary>A scope runs under one tenant (S02 D1).</summary>
public sealed class TenantContextTests
{
    private static readonly Guid _tenantA = new("0192a1b2-0000-7000-8000-0000000000a1");
    private static readonly Guid _tenantB = new("0192a1b2-0000-7000-8000-0000000000b2");

    [Fact]
    public void Is_empty_until_set()
    {
        var context = new TenantContext();

        context.TenantId.ShouldBeNull();
        ((ITenantContext)context).HasTenant.ShouldBeFalse();
    }

    [Fact]
    public void Setting_the_same_tenant_again_is_a_no_op()
    {
        var context = new TenantContext();

        context.Set(_tenantA);
        context.Set(_tenantA);

        context.TenantId.ShouldBe(_tenantA);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Another_tenant_or_none_after_a_tenant_throws(bool toOtherTenant)
    {
        var context = new TenantContext();
        context.Set(_tenantA);

        Should.Throw<InvalidOperationException>(() => context.Set(toOtherTenant ? _tenantB : null));
        context.TenantId.ShouldBe(_tenantA);
    }

    [Fact]
    public void A_tenant_after_explicitly_none_throws()
    {
        var context = new TenantContext();
        context.Set(null);

        Should.Throw<InvalidOperationException>(() => context.Set(_tenantA));
        context.TenantId.ShouldBeNull();
    }
}

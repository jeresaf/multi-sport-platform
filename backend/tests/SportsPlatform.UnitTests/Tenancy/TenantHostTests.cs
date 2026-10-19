using SportsPlatform.Modules.Tenancy.Domain.Domains;

namespace SportsPlatform.UnitTests.Tenancy;

public sealed class TenantHostTests
{
    [Theory]
    [InlineData("kmsa.localhost", "kmsa.localhost")]
    [InlineData("KMSA.Localhost", "kmsa.localhost")]
    [InlineData("kmsa.localhost:3000", "kmsa.localhost")]
    [InlineData("kmsa.localhost.", "kmsa.localhost")]
    [InlineData(" www.kmsa.example ", "www.kmsa.example")]
    [InlineData("localhost", "localhost")]
    public void Hosts_are_lower_case_without_port_or_trailing_dot(string value, string expected) =>
        TenantHost.Normalize(value).ShouldBe(expected);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("[::1]:5080")]
    [InlineData("127.0.0.1:abc")]
    [InlineData("kmsa_ug.localhost")]
    [InlineData("-kmsa.localhost")]
    [InlineData("kmsa..localhost")]
    [InlineData("kmsá.localhost")]
    [InlineData("kmsa.localhost/path")]
    public void Malformed_hosts_are_null(string? value) => TenantHost.Normalize(value).ShouldBeNull();

    [Fact]
    public void A_host_is_at_most_253_characters()
    {
        var label = new string('a', 63);
        var host = string.Join('.', label, label, label, new string('b', 61)); // 253

        TenantHost.IsValid(host).ShouldBeTrue();
        TenantHost.IsValid(host + "b").ShouldBeFalse();
    }
}

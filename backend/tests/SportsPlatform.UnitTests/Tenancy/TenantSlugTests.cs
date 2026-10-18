using SportsPlatform.Modules.Tenancy.Domain.Tenants;
using SportsPlatform.Modules.Tenancy.Features.Tenants.ListTenants;

namespace SportsPlatform.UnitTests.Tenancy;

public sealed class TenantSlugTests
{
    [Theory]
    [InlineData("kmsa")]
    [InlineData("elssc")]
    [InlineData("k2m")]
    [InlineData("kampala-metro-sports-association")]
    [InlineData("a-b")]
    [InlineData("abcdefghij-abcdefghij-abcdefghij-abcdefg")] // 40
    public void Valid_slugs(string slug) => TenantSlug.HasValidFormat(slug).ShouldBeTrue();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("km")]
    [InlineData("abcdefghij-abcdefghij-abcdefghij-abcdefgh")] // 41
    [InlineData("-kmsa")]
    [InlineData("kmsa-")]
    [InlineData("Kmsa")]
    [InlineData("kmsa_ug")]
    [InlineData("kmsa.ug")]
    [InlineData("kmsa\n")]
    [InlineData("kmsá")]
    public void Invalid_slugs(string? slug) => TenantSlug.HasValidFormat(slug).ShouldBeFalse();

    [Fact]
    public void Reserved_words_are_the_platform_subdomains() =>
        TenantSlug.Reserved.Order(StringComparer.Ordinal).ShouldBe(
            ["admin", "api", "app", "assets", "docs", "mail", "platform", "static", "status", "www"]);

    [Fact]
    public void Cursor_round_trips_a_slug()
    {
        TenantCursor.TryDecode(TenantCursor.Encode("kampala-metro"), out var slug).ShouldBeTrue();

        slug.ShouldBe("kampala-metro");
    }

    [Theory]
    [InlineData("")]
    [InlineData("not base64!")]
    [InlineData("S01TQQ")] // "KMSA": not a slug
    [InlineData("a21zYQ==")] // padded: not the canonical encoding
    public void Cursor_rejects_anything_this_list_did_not_return(string cursor) =>
        TenantCursor.TryDecode(cursor, out _).ShouldBeFalse();
}

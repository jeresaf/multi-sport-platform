using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Time.Testing;
using SportsPlatform.Modules.Platform.Contracts.Media;
using SportsPlatform.Modules.Platform.Infrastructure.Media;

namespace SportsPlatform.UnitTests.Platform.Media;

public sealed class MediaUrlTokensTests
{
    private static readonly Guid _tenantId = Guid.Parse("0196a3b2-0000-7000-8000-000000000001");
    private static readonly Guid _assetId = Guid.Parse("0196a3b2-0000-7000-8000-0000000000a1");

    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 11, 18, 9, 0, 0, TimeSpan.Zero));
    private readonly EphemeralDataProtectionProvider _dataProtection = new();

    [Fact]
    public void A_token_names_its_tenant_asset_and_variant_until_it_expires()
    {
        var tokens = new MediaUrlTokens(_dataProtection, _clock);
        var (token, expiresAt) = tokens.Issue(_tenantId, _assetId, MediaVariant.Medium);

        expiresAt.ShouldBe(_clock.GetUtcNow() + TimeSpan.FromMinutes(10));
        tokens.Read(token).ShouldBe(new MediaUrlToken(_tenantId, _assetId, MediaVariant.Medium));

        _clock.Advance(TimeSpan.FromMinutes(10) - TimeSpan.FromMilliseconds(1));
        tokens.Read(token).ShouldNotBeNull();

        _clock.Advance(TimeSpan.FromMilliseconds(1));
        tokens.Read(token).ShouldBeNull();
    }

    [Fact]
    public void A_token_is_safe_in_a_URL_path()
    {
        var (token, _) = new MediaUrlTokens(_dataProtection, _clock).Issue(_tenantId, _assetId, MediaVariant.Thumbnail);

        token.ShouldMatch("^[A-Za-z0-9_-]+$");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-token")]
    public void Anything_else_is_no_token(string? token) =>
        new MediaUrlTokens(_dataProtection, _clock).Read(token).ShouldBeNull();

    [Fact]
    public void A_changed_token_or_another_purpose_is_refused()
    {
        var tokens = new MediaUrlTokens(_dataProtection, _clock);
        var (token, _) = tokens.Issue(_tenantId, _assetId, MediaVariant.Large);
        var changed = token[..^2] + (token[^2] == 'A' ? 'B' : 'A') + token[^1];
        var otherPurpose = _dataProtection.CreateProtector("registry.duplicate-check")
            .Protect($$"""{"TenantId":"{{_tenantId}}","AssetId":"{{_assetId}}","Variant":3,"ExpiresAt":{{long.MaxValue}}}""");

        tokens.Read(changed).ShouldBeNull();
        tokens.Read(otherPurpose).ShouldBeNull();
    }
}

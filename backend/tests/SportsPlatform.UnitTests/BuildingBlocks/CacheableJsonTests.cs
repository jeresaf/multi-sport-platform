using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Net.Http.Headers;
using SportsPlatform.BuildingBlocks.Api;

namespace SportsPlatform.UnitTests.BuildingBlocks;

public sealed class CacheableJsonTests
{
    private static readonly EntityTagHeaderValue _tag = new("\"abc\"");

    [Theory]
    [InlineData("\"abc\"")]
    [InlineData("W/\"abc\"")] // weak comparison for If-None-Match (RFC 9110 §13.1.2)
    [InlineData("\"xyz\", \"abc\"")]
    [InlineData("*")]
    public void If_None_Match_matches(string header) => CacheableJson.Matches(header, _tag).ShouldBeTrue();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("\"xyz\"")]
    [InlineData("\"ABC\"")]
    [InlineData("abc")] // unquoted: not an entity tag
    [InlineData("\"abc")]
    public void If_None_Match_does_not_match(string? header) => CacheableJson.Matches(header, _tag).ShouldBeFalse();

    [Fact]
    public void The_tag_is_strong_and_depends_only_on_the_body()
    {
        var tag = CacheableJson.EntityTagOf("""{"items":[]}"""u8);

        tag.IsWeak.ShouldBeFalse();
        tag.ShouldBe(CacheableJson.EntityTagOf("""{"items":[]}"""u8));
        tag.ShouldNotBe(CacheableJson.EntityTagOf("""{"items":[1]}"""u8));
        tag.Tag.Length.ShouldBe(45); // quotes around 43 base64url characters of SHA-256
    }

    [Fact]
    public async Task Writes_the_body_with_cache_headers()
    {
        var context = NewContext();

        await CacheableJson.Ok(new Sample("UG"), TimeSpan.FromHours(1)).ExecuteAsync(context);

        context.Response.StatusCode.ShouldBe(200);
        context.Response.ContentType.ShouldBe("application/json; charset=utf-8");
        context.Response.Headers.CacheControl.ToString().ShouldBe("public, max-age=3600");
        context.Response.Headers.ETag.ToString().ShouldBe(CacheableJson.EntityTagOf("""{"code":"UG"}"""u8).ToString());
        Body(context).ShouldBe("""{"code":"UG"}""");
    }

    [Fact]
    public async Task A_matching_If_None_Match_gets_304_with_the_headers_and_no_body()
    {
        var context = NewContext();
        context.Request.Headers.IfNoneMatch = CacheableJson.EntityTagOf("""{"code":"UG"}"""u8).ToString();

        await CacheableJson.Ok(new Sample("UG"), TimeSpan.FromHours(1)).ExecuteAsync(context);

        context.Response.StatusCode.ShouldBe(304);
        context.Response.Headers.ETag.ToString().ShouldBe(context.Request.Headers.IfNoneMatch.ToString());
        context.Response.Headers.CacheControl.ToString().ShouldBe("public, max-age=3600");
        Body(context).ShouldBeEmpty();
    }

    [Fact]
    public void A_negative_max_age_is_refused() =>
        Should.Throw<ArgumentOutOfRangeException>(() => CacheableJson.Ok(new Sample("UG"), TimeSpan.FromSeconds(-1)));

    private static DefaultHttpContext NewContext() => new()
    {
        RequestServices = new ServiceCollection().BuildServiceProvider(),
        Response = { Body = new MemoryStream() },
    };

    private static string Body(HttpContext context) =>
        Encoding.UTF8.GetString(((MemoryStream)context.Response.Body).ToArray());

    private sealed record Sample(string Code);
}

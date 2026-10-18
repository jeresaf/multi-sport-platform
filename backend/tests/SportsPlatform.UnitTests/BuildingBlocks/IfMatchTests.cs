using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;
using SportsPlatform.BuildingBlocks.Api;

namespace SportsPlatform.UnitTests.BuildingBlocks;

public sealed class IfMatchTests
{
    private static readonly EntityTagHeaderValue _current = new("\"1234\"");

    [Theory]
    [InlineData(null, true)]
    [InlineData("\"1234\"", true)]
    [InlineData("\"9\", \"1234\"", true)]
    [InlineData("*", true)]
    [InlineData("\"1233\"", false)]
    [InlineData("W/\"1234\"", false)] // If-Match compares strongly
    public void Matches_the_current_tag(string? header, bool expected)
    {
        IfMatch.TryParse(header is null ? StringValues.Empty : new StringValues(header), out var ifMatch).ShouldBeTrue();

        ifMatch.IsPresent.ShouldBe(header is not null);
        ifMatch.Matches(_current).ShouldBe(expected);
    }

    [Theory]
    [InlineData("1234")]
    [InlineData("\"1234")]
    [InlineData("\"1234\" garbage")]
    [InlineData("*, \"1234\"")]
    public void A_header_that_does_not_parse_is_refused(string header) =>
        IfMatch.TryParse(new StringValues(header), out _).ShouldBeFalse();
}

using System.Text.Json.Nodes;
using SportsPlatform.Modules.SportsConfiguration.Definitions;

namespace SportsPlatform.UnitTests.SportsConfiguration;

// RFC 8785 (JSON Canonicalization Scheme), the hash of configuration content (S05 D4).
public sealed class CanonicalJsonTests
{
    [Fact]
    public void The_RFC_example_is_canonicalized_as_the_RFC_shows()
    {
        // RFC 8785 §3.2.2 and §3.2.3.
        var input = JsonNode.Parse(
            """
            {
              "numbers": [333333333.33333329, 1E30, 4.50, 2e-3, 0.000000000000000000000000001],
              "string": "\u20ac$\u000F\u000aA'\u0042\u0022\u005c\\\"\/",
              "literals": [null, true, false]
            }
            """);

        CanonicalJson.Serialize(input).ShouldBe(
            """{"literals":[null,true,false],"numbers":[333333333.3333333,1e+30,4.5,0.002,1e-27],"string":"€$\u000f\nA'B\"\\\\\"/"}""");
    }

    [Theory]
    [InlineData(0d, "0")]
    [InlineData(5e-324, "5e-324")]
    [InlineData(-5e-324, "-5e-324")]
    [InlineData(1.7976931348623157e308, "1.7976931348623157e+308")]
    [InlineData(9007199254740992d, "9007199254740992")]
    [InlineData(-9007199254740992d, "-9007199254740992")]
    [InlineData(295147905179352830000d, "295147905179352830000")]
    [InlineData(999999999999999700000d, "999999999999999700000")]
    [InlineData(1e21, "1e+21")]
    [InlineData(1e23, "1e+23")]
    [InlineData(0.000001, "0.000001")]
    [InlineData(1e-7, "1e-7")]
    [InlineData(1.5e-7, "1.5e-7")]
    [InlineData(-1.5, "-1.5")]
    [InlineData(2700d, "2700")]
    [InlineData(123.45, "123.45")]
    public void Numbers_are_written_as_ECMAScript_writes_them(double value, string expected) =>
        CanonicalJson.Serialize(JsonValue.Create(value)).ShouldBe(expected);

    [Fact]
    public void Negative_zero_is_zero() => CanonicalJson.Serialize(JsonNode.Parse("-0.0")).ShouldBe("0");

    [Fact]
    public void Members_are_sorted_by_UTF16_code_units_not_code_points()
    {
        // U+1F600 is the surrogate pair D83D DE00, which sorts before U+FB33 in UTF-16 (and after it by code point).
        var input = new JsonObject { ["\uFB33"] = 1, ["\U0001F600"] = 2, ["b"] = 3, ["B"] = 4, ["a"] = 5 };

        CanonicalJson.Serialize(input).ShouldBe("{\"B\":4,\"a\":5,\"b\":3,\"\U0001F600\":2,\"\uFB33\":1}");
    }

    [Fact]
    public void Control_characters_use_lower_case_escapes_and_the_rest_stays_as_is()
    {
        CanonicalJson.Serialize(JsonValue.Create("\u0001\u001f\u007f\u2028\u00e9<>&'"))
            .ShouldBe("\"\\u0001\\u001f\u007f\u2028\u00e9<>&'\"");
    }

    [Fact]
    public void A_lone_surrogate_has_no_hash() =>
        Should.Throw<System.Text.EncoderFallbackException>(() => CanonicalJson.Hash(JsonValue.Create("goal\uD800")));

    [Fact]
    public void The_hash_names_its_scheme_and_ignores_how_the_content_was_written()
    {
        var block = YamlJson.Parse(
            """
            code: GOAL   # a comment
            points: 1
            phases:
              - PLAY
            """);
        var flow = YamlJson.Parse("{ phases: [PLAY], points: 1.0, code: 'GOAL' }");

        CanonicalJson.Hash(block).ShouldBe(CanonicalJson.Hash(flow));
        CanonicalJson.Hash(block).ShouldMatch("^sha256:[0-9a-f]{64}$");
        CanonicalJson.Hash(block).ShouldNotBe(CanonicalJson.Hash(YamlJson.Parse("{ phases: [PLAY], points: 2, code: GOAL }")));
    }
}

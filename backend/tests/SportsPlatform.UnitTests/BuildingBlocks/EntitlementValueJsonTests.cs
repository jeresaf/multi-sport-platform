using System.Text.Json;
using SportsPlatform.BuildingBlocks.Entitlements;

namespace SportsPlatform.UnitTests.BuildingBlocks;

/// <summary>An entitlement value's one JSON shape (#63 R2): the export, the API and the jsonb columns use it.</summary>
public sealed class EntitlementValueJsonTests
{
    public static TheoryData<string, string> Values => new()
    {
        { "yes", """{"enabled":true}""" },
        { "no", """{"enabled":false}""" },
        { "120", """{"limit":120}""" },
        { "0", """{"limit":0}""" },
        { "unlimited", """{"limit":null}""" },
        { "BASIC", """{"level":"BASIC"}""" },
        { "none", """{"level":null}""" },
    };

    [Theory]
    [MemberData(nameof(Values))]
    public void A_value_is_written_as_one_typed_property_and_read_back(string value, string json)
    {
        var parsed = JsonSerializer.Deserialize<EntitlementValue>(json)!;

        parsed.ToString().ShouldBe(value);
        JsonSerializer.Serialize(parsed).ShouldBe(json);
    }

    [Fact]
    public void Any_serializer_options_give_the_same_shape()
    {
        JsonSerializer.Serialize(new { Value = EntitlementValue.Quota(8) }, JsonSerializerOptions.Web).ShouldBe("""{"value":{"limit":8}}""");
    }

    [Theory]
    [InlineData("true")]
    [InlineData("120")]
    [InlineData("{}")]
    [InlineData("""{"enabled":"yes"}""")]
    [InlineData("""{"limit":-1}""")]
    [InlineData("""{"limit":1.5}""")]
    [InlineData("""{"level":"basic"}""")]
    [InlineData("""{"enabled":true,"limit":3}""")]
    [InlineData("""{"quota":3}""")]
    public void Anything_else_is_refused(string json)
    {
        Should.Throw<JsonException>(() => JsonSerializer.Deserialize<EntitlementValue>(json));
    }
}

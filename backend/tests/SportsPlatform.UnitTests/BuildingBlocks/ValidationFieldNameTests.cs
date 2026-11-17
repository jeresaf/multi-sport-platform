using SportsPlatform.BuildingBlocks.Api;

namespace SportsPlatform.UnitTests.BuildingBlocks;

/// <summary>A validation failure's field is the request's JSON path: every segment camelCased (#22 re-review N2).</summary>
public sealed class ValidationFieldNameTests
{
    [Theory]
    [InlineData("Reason", "reason")]
    [InlineData("ExternalIdentifiers[0].Value", "externalIdentifiers[0].value")]
    [InlineData("Address.Street", "address.street")]
    [InlineData("Guardian.Contacts[12].PhoneNumber", "guardian.contacts[12].phoneNumber")]
    [InlineData("alreadyCamel", "alreadyCamel")]
    [InlineData("", "")]
    public void Every_segment_of_a_property_path_is_camel_cased(string path, string expected) =>
        RequestValidationExtensions.FieldName(path).ShouldBe(expected);
}

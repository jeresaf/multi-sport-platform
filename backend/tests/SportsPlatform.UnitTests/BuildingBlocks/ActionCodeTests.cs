using SportsPlatform.BuildingBlocks.Actions;

namespace SportsPlatform.UnitTests.BuildingBlocks;

public sealed class ActionCodeTests
{
    [Theory]
    [InlineData("person.create")]
    [InlineData("timeline.event.record")]
    [InlineData("person.read_private")]
    [InlineData("platform.outbox.redrive")]
    public void Accepts_lowercase_dotted_codes(string value)
    {
        new ActionCode(value).Value.ShouldBe(value);
        ActionCode.IsValid(value).ShouldBeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("person")]
    [InlineData("Person.Create")]
    [InlineData("person.create.")]
    [InlineData("person..create")]
    [InlineData("a.b.c.d")]
    [InlineData("person-create.now")]
    public void Rejects_other_formats(string value)
    {
        Should.Throw<ArgumentException>(() => new ActionCode(value));
        ActionCode.IsValid(value).ShouldBeFalse();
    }

    [Fact]
    public void Codes_with_the_same_value_are_equal()
    {
        new ActionCode("contest.start").ShouldBe(new ActionCode("contest.start"));
    }
}

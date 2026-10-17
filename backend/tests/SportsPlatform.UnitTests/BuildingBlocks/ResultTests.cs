using SportsPlatform.BuildingBlocks.Results;

namespace SportsPlatform.UnitTests.BuildingBlocks;

public sealed class ResultTests
{
    private static readonly Error _fixtureClash = new("CONTEST_VENUE_CLASH", "The venue is already booked.", ErrorKind.Conflict);

    [Fact]
    public void Success_has_no_error()
    {
        var result = Result.Success();

        result.IsSuccess.ShouldBeTrue();
        result.IsFailure.ShouldBeFalse();
        result.Error.ShouldBeNull();
    }

    [Fact]
    public void Failure_carries_its_error()
    {
        var result = Result.Failure(_fixtureClash);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(_fixtureClash);
    }

    [Fact]
    public void Failure_requires_an_error()
    {
        Should.Throw<ArgumentNullException>(() => Result.Failure(null!));
        Should.Throw<ArgumentNullException>(() => Result.Failure<int>(null!));
    }

    [Fact]
    public void Success_with_a_value_exposes_it()
    {
        var result = Result.Success("Kampala Metro Sports Association");

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe("Kampala Metro Sports Association");
    }

    [Fact]
    public void Reading_the_value_of_a_failure_throws_with_the_code()
    {
        Result<int> result = _fixtureClash;

        result.IsFailure.ShouldBeTrue();
        Should.Throw<InvalidOperationException>(() => result.Value).Message.ShouldContain("CONTEST_VENUE_CLASH");
    }

    [Theory]
    [InlineData("", "message")]
    [InlineData(" ", "message")]
    [InlineData("PLATFORM_X", "")]
    public void Error_requires_a_code_and_a_message(string code, string message)
    {
        Should.Throw<ArgumentException>(() => new Error(code, message, ErrorKind.Validation));
    }
}

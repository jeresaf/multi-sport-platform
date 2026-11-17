using System.Globalization;
using SportsPlatform.Modules.Registry.Contracts.People;
using SportsPlatform.Modules.Registry.Domain.People;

namespace SportsPlatform.UnitTests.Registry;

/// <summary>S06 D13: a minor is under 18 on the given date; no date of birth is unknown.</summary>
public sealed class AgeTests
{
    [Theory]
    [InlineData("2008-10-17", "2026-10-17", AgeStatus.Adult)] // the 18th birthday
    [InlineData("2008-10-18", "2026-10-17", AgeStatus.Minor)] // the day before it
    [InlineData("2008-10-16", "2026-10-17", AgeStatus.Adult)]
    [InlineData("1961-01-30", "2026-10-17", AgeStatus.Adult)]
    [InlineData("2026-10-17", "2026-10-17", AgeStatus.Minor)] // born today
    [InlineData("2008-02-29", "2026-02-28", AgeStatus.Minor)] // born on a leap day: 18 on 1 March of a year without one
    [InlineData("2008-02-29", "2026-03-01", AgeStatus.Adult)]
    [InlineData("2010-02-28", "2028-02-29", AgeStatus.Adult)]
    [InlineData("2010-03-01", "2028-02-29", AgeStatus.Minor)]
    public void A_person_is_an_adult_from_the_18th_birthday(string dateOfBirth, string today, AgeStatus expected) =>
        Age.StatusOn(DateOnly.Parse(dateOfBirth, CultureInfo.InvariantCulture), DateOnly.Parse(today, CultureInfo.InvariantCulture))
            .ShouldBe(expected);

    [Fact]
    public void A_person_without_a_date_of_birth_has_an_unknown_age() =>
        Age.StatusOn(null, new DateOnly(2026, 10, 17)).ShouldBe(AgeStatus.Unknown);

    // D13: fail-closed. Only a known adult is not treated as a minor.
    [Theory]
    [InlineData(AgeStatus.Adult, false)]
    [InlineData(AgeStatus.Minor, true)]
    [InlineData(AgeStatus.Unknown, true)]
    public void Everyone_not_known_to_be_an_adult_is_treated_as_a_minor(AgeStatus status, bool expected) =>
        Age.IsTreatedAsMinor(status).ShouldBe(expected);
}

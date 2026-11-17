using SportsPlatform.Modules.Registry.Contracts.People;

namespace SportsPlatform.Modules.Registry.Domain.People;

/// <summary>
/// Whether a person is a minor (S06 D13, ADR-0015): under 18 on a given date, the tenant's "today" for reads. Computed
/// every time, never stored. A person born on 29 February turns 18 on 1 March of a year without one.
/// </summary>
public static class Age
{
    public const int AdultYears = 18;

    /// <summary>The latest date of birth of someone who is an adult on <paramref name="today"/>.</summary>
    public static DateOnly LatestAdultBirthDate(DateOnly today) => today.AddYears(-AdultYears);

    /// <summary>
    /// Whether to treat the person as a minor: everyone not known to be an adult, so a person without a date of birth
    /// too (S06 D13, fail-closed).
    /// </summary>
    public static bool IsTreatedAsMinor(AgeStatus status) => status != AgeStatus.Adult;

    public static AgeStatus StatusOn(DateOnly? dateOfBirth, DateOnly today) => dateOfBirth switch
    {
        null => AgeStatus.Unknown,
        { } born when born <= LatestAdultBirthDate(today) => AgeStatus.Adult,
        _ => AgeStatus.Minor,
    };
}

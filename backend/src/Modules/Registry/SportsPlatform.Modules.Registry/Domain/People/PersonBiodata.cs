namespace SportsPlatform.Modules.Registry.Domain.People;

/// <summary>
/// A person's biodata as entered (S06 D2). First and last name are required; everything else is optional. A missing
/// display name becomes "first last". Nationalities are ISO 3166-1 alpha-2 codes, checked against the reference data by
/// the use case. No contact details, national ID numbers or medical data (ADR-0015).
/// </summary>
public sealed record PersonBiodata(
    string FirstName,
    string LastName,
    string? MiddleName = null,
    string? OtherNames = null,
    string? DisplayName = null,
    Gender? Gender = null,
    DateOnly? DateOfBirth = null,
    string? Nationality = null,
    string? SecondaryNationality = null,
    int? HeightCm = null,
    decimal? WeightKg = null)
{
    public const int NameMaxLength = 100;
    public const int OtherNamesMaxLength = 200;
    public const int DisplayNameMaxLength = 200;
    public const int MinHeightCm = 50;
    public const int MaxHeightCm = 260;
    public const decimal MinWeightKg = 10;
    public const decimal MaxWeightKg = 300;
}

using System.Text.RegularExpressions;
using FluentValidation;
using SportsPlatform.Modules.Platform.Contracts.Reference;
using SportsPlatform.Modules.Registry.Domain.People;

namespace SportsPlatform.Modules.Registry.Features.People;

/// <summary>
/// The shape of <see cref="IPersonDetails"/>, shared by the check and create (S06 D5, D6). Messages name the field,
/// never echo what was sent. Nationalities are active countries of the reference data. Whether a name has a letter or a
/// digit, whether an authority is reserved and whether a date of birth is in the future are
/// <see cref="PersonDetailsRules"/> (422s with their own codes). Scoped: it reads the reference data.
/// </summary>
public sealed class PersonDetailsValidator<T> : AbstractValidator<T>
    where T : IPersonDetails
{
    public const int MaxIdentifiers = 10;

    public PersonDetailsValidator(IReferenceData reference)
    {
        ArgumentNullException.ThrowIfNull(reference);

        Name(RuleFor(request => request.FirstName), "firstName", PersonBiodata.NameMaxLength, required: true);
        Name(RuleFor(request => request.LastName), "lastName", PersonBiodata.NameMaxLength, required: true);
        Name(RuleFor(request => request.MiddleName), "middleName", PersonBiodata.NameMaxLength, required: false);
        Name(RuleFor(request => request.OtherNames), "otherNames", PersonBiodata.OtherNamesMaxLength, required: false);
        Country(RuleFor(request => request.Nationality), "nationality", reference);
        Country(RuleFor(request => request.SecondaryNationality), "secondaryNationality", reference);
        RuleFor(request => request.ExternalIdentifiers)
            .Must(identifiers => identifiers is null || identifiers.Count <= MaxIdentifiers)
            .WithMessage($"'externalIdentifiers' has at most {MaxIdentifiers} entries.")
            .Must(identifiers => identifiers is null || identifiers.All(identifier => identifier is not null))
            .WithMessage("An entry of 'externalIdentifiers' must be an object.");
        RuleForEach(request => request.ExternalIdentifiers)
            .ChildRules(identifier =>
            {
                identifier.RuleFor(i => i!.Authority)
                    .Must(authority => authority is not null && !authority.Any(char.IsControl) && PersonDetailsPatterns.Authority().IsMatch(authority))
                    .WithMessage("'authority' must be an UPPER_SNAKE code of 2 to 40 characters.");
                identifier.RuleFor(i => i!.Value)
                    .Cascade(CascadeMode.Stop)
                    .Must(value => !string.IsNullOrWhiteSpace(value))
                    .WithMessage("'value' must not be empty.")
                    .Must(value => value!.Trim().Length <= ExternalIdentifier.ValueMaxLength)
                    .WithMessage($"'value' must be at most {ExternalIdentifier.ValueMaxLength} characters.")
                    // PostgreSQL refuses a NUL in text, and no identifier holds a control character.
                    .Must(value => !value!.Any(char.IsControl))
                    .WithMessage("'value' can't contain control characters.");
            })
            .When(request => request.ExternalIdentifiers is not null
                && request.ExternalIdentifiers.All(identifier => identifier is not null));
    }

    private static void Name(IRuleBuilderInitial<T, string?> rule, string field, int maxLength, bool required) => rule
        .Cascade(CascadeMode.Stop)
        .Must(name => !required || !string.IsNullOrWhiteSpace(name))
        .WithMessage($"'{field}' must not be empty.")
        .Must(name => name is null || name.Trim().Length <= maxLength)
        .WithMessage($"'{field}' must be at most {maxLength} characters.");

    private static void Country(IRuleBuilderInitial<T, string?> rule, string field, IReferenceData reference) => rule
        .Cascade(CascadeMode.Stop)
        .Must(code => code is null || PersonDetailsPatterns.CountryCode().IsMatch(code))
        .WithMessage($"'{field}' must be an ISO 3166-1 alpha-2 country code in upper case.")
        .MustAsync(async (code, cancellationToken) => code is null || await reference.IsActiveCountryAsync(code, cancellationToken))
        .WithMessage($"'{field}' must be a country of the reference data.");
}

/// <summary>
/// The shape of <see cref="IPersonBiodataDetails"/>, shared by create and update (S06 D2, D8):
/// <see cref="PersonDetailsValidator{T}"/> and the display name, height and weight bounds. Scoped: it reads the
/// reference data.
/// </summary>
public sealed class PersonBiodataValidator<T> : AbstractValidator<T>
    where T : IPersonBiodataDetails
{
    public PersonBiodataValidator(IReferenceData reference)
    {
        Include(new PersonDetailsValidator<T>(reference));
        RuleFor(request => request.DisplayName)
            .Must(name => name is null || name.Trim().Length <= PersonBiodata.DisplayNameMaxLength)
            .WithMessage($"'displayName' must be at most {PersonBiodata.DisplayNameMaxLength} characters.");
        RuleFor(request => request.HeightCm)
            .InclusiveBetween(PersonBiodata.MinHeightCm, PersonBiodata.MaxHeightCm)
            .WithMessage($"'heightCm' must be {PersonBiodata.MinHeightCm}–{PersonBiodata.MaxHeightCm}.")
            .When(request => request.HeightCm is not null);
        RuleFor(request => request.WeightKg)
            .Must(weight => weight >= PersonBiodata.MinWeightKg && weight <= PersonBiodata.MaxWeightKg
                && decimal.Round(weight!.Value, 1) == weight)
            .WithMessage($"'weightKg' must be {PersonBiodata.MinWeightKg}–{PersonBiodata.MaxWeightKg} with at most one decimal.")
            .When(request => request.WeightKg is not null);
    }
}

/// <summary>The validator's patterns (a generated regex can't be declared in a generic type).</summary>
internal static partial class PersonDetailsPatterns
{
    // \z, not $: .NET's $ also matches before a final line feed.
    [GeneratedRegex(@"^[A-Z]{2}\z", RegexOptions.CultureInvariant)]
    public static partial Regex CountryCode();

    [GeneratedRegex(ExternalIdentifier.AuthorityPattern, RegexOptions.CultureInvariant)]
    public static partial Regex Authority();
}

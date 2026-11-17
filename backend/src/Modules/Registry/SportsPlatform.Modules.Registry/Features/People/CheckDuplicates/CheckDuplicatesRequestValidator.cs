using System.Text.RegularExpressions;
using FluentValidation;
using SportsPlatform.Modules.Registry.Domain.People;

namespace SportsPlatform.Modules.Registry.Features.People.CheckDuplicates;

/// <summary>
/// The request's shape. Messages name the field, never echo what was sent. Whether a name has a letter or a digit and
/// whether an authority is reserved are the handler's (422s with their own codes).
/// </summary>
public sealed partial class CheckDuplicatesRequestValidator : AbstractValidator<CheckDuplicatesRequest>
{
    public CheckDuplicatesRequestValidator()
    {
        Name(RuleFor(request => request.FirstName), "firstName", PersonBiodata.NameMaxLength, required: true);
        Name(RuleFor(request => request.LastName), "lastName", PersonBiodata.NameMaxLength, required: true);
        Name(RuleFor(request => request.MiddleName), "middleName", PersonBiodata.NameMaxLength, required: false);
        Name(RuleFor(request => request.OtherNames), "otherNames", PersonBiodata.OtherNamesMaxLength, required: false);
        RuleFor(request => request.Nationality)
            .Must(code => code is null || CountryCode().IsMatch(code))
            .WithMessage("'nationality' must be an ISO 3166-1 alpha-2 country code in upper case.");
        RuleFor(request => request.PersonId)
            .Must(id => id != Guid.Empty)
            .WithMessage("'personId' must be a person's ID.");
        RuleFor(request => request.ExternalIdentifiers)
            .Must(identifiers => identifiers is null || identifiers.Count <= CheckDuplicatesRequest.MaxIdentifiers)
            .WithMessage($"'externalIdentifiers' has at most {CheckDuplicatesRequest.MaxIdentifiers} entries.")
            .Must(identifiers => identifiers is null || identifiers.All(identifier => identifier is not null))
            .WithMessage("An entry of 'externalIdentifiers' must be an object.");
        RuleForEach(request => request.ExternalIdentifiers)
            .ChildRules(identifier =>
            {
                identifier.RuleFor(i => i.Authority)
                    .Must(authority => authority is not null && Authority().IsMatch(authority))
                    .WithMessage("'authority' must be an UPPER_SNAKE code of 2 to 40 characters.");
                identifier.RuleFor(i => i.Value)
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

    private static void Name(
        IRuleBuilderInitial<CheckDuplicatesRequest, string?> rule, string field, int maxLength, bool required) => rule
        .Cascade(CascadeMode.Stop)
        .Must(name => !required || !string.IsNullOrWhiteSpace(name))
        .WithMessage($"'{field}' must not be empty.")
        .Must(name => name is null || name.Trim().Length <= maxLength)
        .WithMessage($"'{field}' must be at most {maxLength} characters.");

    [GeneratedRegex("^[A-Z]{2}$", RegexOptions.CultureInvariant)]
    private static partial Regex CountryCode();

    [GeneratedRegex(ExternalIdentifier.AuthorityPattern, RegexOptions.CultureInvariant)]
    private static partial Regex Authority();
}

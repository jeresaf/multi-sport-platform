using FluentValidation;
using SportsPlatform.Modules.Platform.Contracts.Reference;
using SportsPlatform.Modules.Registry.Domain.People;
using SportsPlatform.Modules.Registry.Domain.People.Duplicates;

namespace SportsPlatform.Modules.Registry.Features.People.CreatePerson;

/// <summary>
/// The request's shape: <see cref="PersonDetailsValidator{T}"/>, the display name, height and weight bounds, an
/// identifier sent once, and a reason that isn't blank. A missing token is the handler's
/// (<c>PERSON_DUPLICATE_CHECK_REQUIRED</c>). Scoped: it reads the reference data.
/// </summary>
public sealed class CreatePersonRequestValidator : AbstractValidator<CreatePersonRequest>
{
    public CreatePersonRequestValidator(IReferenceData reference)
    {
        Include(new PersonDetailsValidator<CreatePersonRequest>(reference));
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
        RuleFor(request => request.ExternalIdentifiers)
            .Must(identifiers => identifiers!
                .GroupBy(identifier => (identifier.Authority, identifier.Value?.Trim()))
                .All(group => group.Count() == 1))
            .WithMessage("'externalIdentifiers' names the same authority and value more than once.")
            .When(request => request.ExternalIdentifiers is not null
                && request.ExternalIdentifiers.All(identifier => identifier is not null));
        RuleFor(request => request.CheckToken)
            .Must(token => token is null || token.Length <= CreatePersonRequest.CheckTokenMaxLength)
            .WithMessage("'checkToken' must be the token the duplicate check answered.");
        RuleFor(request => request.OverrideReason)
            .Cascade(CascadeMode.Stop)
            .Must(reason => reason is null || !string.IsNullOrWhiteSpace(reason))
            .WithMessage("'overrideReason' must not be empty when sent.")
            .Must(reason => reason is null || reason.Trim().Length <= DuplicateDecision.ReasonMaxLength)
            .WithMessage($"'overrideReason' must be at most {DuplicateDecision.ReasonMaxLength} characters.");
    }
}

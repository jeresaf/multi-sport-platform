using FluentValidation;
using SportsPlatform.Modules.Platform.Contracts.Reference;
using SportsPlatform.Modules.Registry.Domain.People.Duplicates;
using SportsPlatform.Modules.Registry.Features.People.CreatePerson;

namespace SportsPlatform.Modules.Registry.Features.People.UpdatePerson;

/// <summary>
/// The request's shape: <see cref="PersonBiodataValidator{T}"/> as for a create, a token no longer than a check's, and
/// a reason that isn't blank. Whether a token is needed is the handler's. Scoped: it reads the reference data.
/// </summary>
public sealed class UpdatePersonRequestValidator : AbstractValidator<UpdatePersonRequest>
{
    public UpdatePersonRequestValidator(IReferenceData reference)
    {
        Include(new PersonBiodataValidator<UpdatePersonRequest>(reference));
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

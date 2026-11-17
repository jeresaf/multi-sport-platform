using FluentValidation;
using SportsPlatform.Modules.Platform.Contracts.Reference;

namespace SportsPlatform.Modules.Registry.Features.People.CheckDuplicates;

/// <summary>The request's shape: <see cref="PersonDetailsValidator{T}"/>, and a person's ID when one is named.</summary>
public sealed class CheckDuplicatesRequestValidator : AbstractValidator<CheckDuplicatesRequest>
{
    public CheckDuplicatesRequestValidator(IReferenceData reference)
    {
        Include(new PersonDetailsValidator<CheckDuplicatesRequest>(reference));
        RuleFor(request => request.PersonId)
            .Must(id => id != Guid.Empty)
            .WithMessage("'personId' must be a person's ID.");
    }
}

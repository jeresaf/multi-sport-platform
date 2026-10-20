using FluentValidation;
using SportsPlatform.Modules.Identity.Domain.Memberships;

namespace SportsPlatform.Modules.Identity.Features.Members.AssignRole;

/// <summary>A role code is required; whether the role exists and fits the scope is the handler's (<c>AUTH_ROLE_NOT_ASSIGNABLE</c>).</summary>
public sealed class AssignRoleRequestValidator : AbstractValidator<AssignRoleRequest>
{
    public AssignRoleRequestValidator()
    {
        RuleFor(request => request.RoleCode)
            .Cascade(CascadeMode.Stop)
            .Must(code => !string.IsNullOrWhiteSpace(code))
            .WithMessage("'roleCode' must not be empty.")
            .Must(code => code!.Length <= RoleAssignment.RoleCodeMaxLength)
            .WithMessage($"'roleCode' must be at most {RoleAssignment.RoleCodeMaxLength} characters.");
    }
}

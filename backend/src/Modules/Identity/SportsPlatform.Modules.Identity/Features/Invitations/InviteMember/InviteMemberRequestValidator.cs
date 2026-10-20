using FluentValidation;
using SportsPlatform.Modules.Identity.Domain.Memberships;

namespace SportsPlatform.Modules.Identity.Features.Invitations.InviteMember;

/// <summary>Shape only: whether the roles exist and fit is the handler's (<c>AUTH_ROLE_NOT_ASSIGNABLE</c>).</summary>
public sealed class InviteMemberRequestValidator : AbstractValidator<InviteMemberRequest>
{
    /// <summary>More than every tenant role, so a real request never reaches it.</summary>
    public const int MaxRoles = 10;

    public InviteMemberRequestValidator()
    {
        RuleFor(request => request.Email).InviteeEmail();
        RuleFor(request => request.RoleCodes)
            .Cascade(CascadeMode.Stop)
            .Must(codes => codes is { Count: > 0 })
            .WithMessage("'roleCodes' must name at least one role.")
            .Must(codes => codes!.Count <= MaxRoles)
            .WithMessage($"'roleCodes' must name at most {MaxRoles} roles.")
            .Must(codes => codes!.All(code => !string.IsNullOrWhiteSpace(code) && code.Length <= RoleAssignment.RoleCodeMaxLength))
            .WithMessage($"Each of 'roleCodes' must be a role code of at most {RoleAssignment.RoleCodeMaxLength} characters.");
    }
}

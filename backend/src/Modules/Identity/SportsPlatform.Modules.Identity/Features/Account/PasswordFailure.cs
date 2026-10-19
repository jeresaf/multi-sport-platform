using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using SportsPlatform.BuildingBlocks.Api;
using SportsPlatform.BuildingBlocks.Results;

namespace SportsPlatform.Modules.Identity.Features.Account;

/// <summary>
/// Why a command that sets a password failed. For <c>AUTH_PASSWORD_REJECTED</c>, <see cref="Rules"/> holds the policy's
/// messages (e.g. "Passwords must be at least 12 characters."): the answer names the field in <c>errors</c> and the
/// rules in <c>detail</c>. Messages never contain the password.
/// </summary>
public sealed record PasswordFailure(Error Error, IReadOnlyList<string> Rules)
{
    public static implicit operator PasswordFailure(Error error) => new(error, []);

    /// <summary>The password policy's refusals among <paramref name="errors"/>, as <c>AUTH_PASSWORD_REJECTED</c>.</summary>
    public static PasswordFailure Rejected(IEnumerable<IdentityError> errors) =>
        new(IdentityErrors.PasswordRejected, [.. errors.Select(e => e.Description)]);

    /// <summary>Whether <paramref name="error"/> comes from a password validator (Identity's codes start "Password").</summary>
    public static bool IsPolicyError(IdentityError error) =>
        error is not null && error.Code.StartsWith("Password", StringComparison.Ordinal) && error.Code != nameof(IdentityErrorDescriber.PasswordMismatch);

    public ProblemHttpResult ToProblem(string field)
    {
        if (Rules.Count == 0)
        {
            return Error.ToProblem();
        }

        var problem = Error.ToProblem(string.Join(" ", Rules));
        problem.ProblemDetails.Extensions[ProblemFields.Errors] = new List<FieldError> { new(field, Error.Code) };
        return problem;
    }
}

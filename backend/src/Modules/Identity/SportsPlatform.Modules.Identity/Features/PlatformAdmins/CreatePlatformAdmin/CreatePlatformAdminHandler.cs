using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Auditing;
using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.Modules.Identity.Domain.PlatformRoles;
using SportsPlatform.Modules.Identity.Infrastructure;
using SportsPlatform.Modules.Identity.Infrastructure.Configurations;
using SportsPlatform.Modules.Identity.Infrastructure.Users;

namespace SportsPlatform.Modules.Identity.Features.PlatformAdmins.CreatePlatformAdmin;

/// <summary>
/// Creates a platform administrator (S03 D16): a user with a confirmed email (the operator vouches for it) and
/// <c>PLATFORM_ADMIN</c>, saved with the audit entry in one save. Refuses a password that fails the policy and an email
/// any user already has: an existing user is never given the role or a new password here (owner, S03-T1 plan).
/// </summary>
public sealed class CreatePlatformAdminHandler(
    IdentityDbContext context,
    UserManager<User> users,
    ICurrentActor actor,
    AuditScope audit,
    TimeProvider timeProvider)
{
    public const string EntityType = "User";

    public async Task<Result<Guid>> HandleAsync(string email, string? password, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return CommonErrors.InvalidRequest;
        }

        if (string.IsNullOrEmpty(password))
        {
            return IdentityErrors.PasswordRejected;
        }

        var user = User.Create(email, emailConfirmed: true);

        // Identity's validators: the password policy first, so a refusal says nothing about the email.
        foreach (var validator in users.PasswordValidators)
        {
            if (!(await validator.ValidateAsync(users, user, password)).Succeeded)
            {
                return IdentityErrors.PasswordRejected;
            }
        }

        if (await users.FindByEmailAsync(user.Email!) is not null)
        {
            return IdentityErrors.UserExists;
        }

        // Validates the email and user name, hashes the password; the store doesn't save (UserStore).
        var created = await users.CreateAsync(user, password);
        if (!created.Succeeded)
        {
            return created.Errors.Any(e => e.Code is nameof(IdentityErrorDescriber.DuplicateEmail) or nameof(IdentityErrorDescriber.DuplicateUserName))
                ? IdentityErrors.UserExists
                : CommonErrors.InvalidRequest;
        }

        context.Add(PlatformRoleAssignment.Grant(user.Id, PlatformRoles.Admin, actor, timeProvider.GetUtcNow()));
        audit.Describe(EntityType, user.Id);

        try
        {
            var saved = await context.SaveChangesResultAsync(cancellationToken);
            return saved.IsFailure ? saved.Error! : Result.Success(user.Id);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: UserConfiguration.NormalizedEmailIndex or UserConfiguration.NormalizedUserNameIndex,
        })
        {
            // Another run created the email between the check and the insert (the user name is the email too).
            return IdentityErrors.UserExists;
        }
    }
}

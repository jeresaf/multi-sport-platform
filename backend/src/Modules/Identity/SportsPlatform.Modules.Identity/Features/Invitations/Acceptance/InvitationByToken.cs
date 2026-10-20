using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SportsPlatform.BuildingBlocks.Auditing;
using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.BuildingBlocks.Tenancy;
using SportsPlatform.Modules.Identity.Domain.Invitations;
using SportsPlatform.Modules.Identity.Features.Directory;
using SportsPlatform.Modules.Identity.Infrastructure;
using SportsPlatform.Modules.Identity.Infrastructure.Invitations;

namespace SportsPlatform.Modules.Identity.Features.Invitations.Acceptance;

/// <summary>
/// Finds the invitation an emailed token belongs to (S03-T4c2): the identity directory finds it by the token's SHA-256
/// in any tenant, then the request runs under that tenant (an account request has none until here) and the invitation
/// is read, tracked, under RLS. An unknown token is <c>AUTH_INVITATION_TOKEN_INVALID</c>; the invitation's state is the
/// caller's to check.
/// </summary>
public sealed class InvitationByToken(
    IdentityDirectory directory, IdentityDbContext context, TenantContext tenantContext, AuditScope audit)
{
    public async Task<Result<Invitation>> FindAsync(string token, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(token);
        if (await directory.FindInvitationAsync(InvitationToken.Hash(token), cancellationToken) is not { } key)
        {
            return IdentityErrors.InvitationTokenInvalid;
        }

        // Before the first tenant-data connection of the request (S02 D1): the directory reads on sp_platform.
        tenantContext.Set(key.TenantId);
        var invitation = await context.Set<Invitation>().SingleOrDefaultAsync(i => i.Id == key.InvitationId, cancellationToken)
            ?? throw new InvalidOperationException($"Invitation {key.InvitationId} was found by the directory but not in its tenant.");
        audit.Describe(InvitationEndpoints.EntityType, invitation.Id);
        return Result.Success(invitation);
    }
}

internal static class InvitationTokenRules
{
    /// <summary>A real token is 43 characters (32 bytes in base64url).</summary>
    public const int TokenMaxLength = 64;

    /// <summary>Required and of a bounded length; whether it works is the handler's. The message never echoes it.</summary>
    public static IRuleBuilderOptions<T, string?> InvitationToken<T>(this IRuleBuilderInitial<T, string?> rule) => rule
        .NotEmpty().WithMessage("'token' is required.")
        .MaximumLength(TokenMaxLength).WithMessage($"'token' must be at most {TokenMaxLength} characters.");
}

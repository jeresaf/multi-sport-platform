using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.Modules.Identity;
using SportsPlatform.Modules.Identity.Domain.Invitations;
using SportsPlatform.Modules.Identity.Domain.Roles;
using SportsPlatform.Modules.Identity.Infrastructure.Invitations;

namespace SportsPlatform.UnitTests.Identity;

/// <summary>The invitation state machine (state-machines.md, "Invitation"): pending, accepted or revoked; expiry derived.</summary>
public sealed class InvitationTests
{
    private const string _email = "Brian.Ssemakula@kmsa.example";
    private const string _normalized = "BRIAN.SSEMAKULA@KMSA.EXAMPLE";

    private static readonly DateTimeOffset _now = new(2026, 10, 20, 9, 0, 0, TimeSpan.Zero);
    private static readonly UserActor _owner = new(new Guid("0192a1b2-0000-7000-8000-00000000d001"));

    private static RoleDefinition Role(string code) => IdentityRoles.All.Single(role => role.Code == code);

    private static Invitation Pending(params string[] roles) =>
        Invitation.Create(_email, _normalized, [.. (roles.Length == 0 ? [IdentityRoles.Registrar] : roles).Select(Role)], _owner, _now).Value;

    [Fact]
    public void A_new_invitation_is_pending_for_seven_days_without_a_token()
    {
        var invitation = Pending(IdentityRoles.Registrar, IdentityRoles.Viewer, IdentityRoles.Registrar);

        invitation.ShouldSatisfyAllConditions(
            i => i.Id.ShouldNotBe(Guid.Empty),
            i => i.Email.ShouldBe(_email),
            i => i.NormalizedEmail.ShouldBe(_normalized),
            i => i.RoleCodes.ShouldBe([IdentityRoles.Registrar, IdentityRoles.Viewer]),
            i => i.Status.ShouldBe(InvitationStatus.Pending),
            i => i.InvitedBy.ShouldBe(_owner.ActorId),
            i => i.InvitedAt.ShouldBe(_now),
            i => i.ExpiresAt.ShouldBe(_now.AddDays(7)),
            i => i.TokenHash.ShouldBeNull(),
            i => i.StateAt(_now).ShouldBe(InvitationState.Pending));
    }

    [Theory]
    [InlineData(IdentityRoles.TeamManager)] // assigned per team only
    [InlineData("PLATFORM_ADMIN")]
    public void Roles_must_be_held_tenant_wide(string role) =>
        Invitation.Create(_email, _normalized, [Role(role)], _owner, _now).Error.ShouldBe(IdentityErrors.RoleNotAssignable);

    [Fact]
    public void An_invitation_names_at_least_one_role() =>
        Invitation.Create(_email, _normalized, [], _owner, _now).Error.ShouldBe(IdentityErrors.RoleNotAssignable);

    [Fact]
    public void It_expires_seven_days_after_it_was_made()
    {
        var invitation = Pending();

        invitation.StateAt(_now.AddDays(7).AddTicks(-1)).ShouldBe(InvitationState.Pending);
        invitation.StateAt(_now.AddDays(7)).ShouldBe(InvitationState.Expired);
        invitation.Status.ShouldBe(InvitationStatus.Pending); // derived, never stored
    }

    [Fact]
    public void A_pending_invitation_is_revoked_once()
    {
        var invitation = Pending();
        var later = _now.AddHours(2);

        invitation.Revoke(_owner, later).IsSuccess.ShouldBeTrue();
        invitation.ShouldSatisfyAllConditions(
            i => i.Status.ShouldBe(InvitationStatus.Revoked),
            i => i.RevokedBy.ShouldBe(_owner.ActorId),
            i => i.RevokedAt.ShouldBe(later),
            i => i.StateAt(later).ShouldBe(InvitationState.Revoked));
        invitation.Revoke(_owner, later).Error.ShouldBe(IdentityErrors.InvitationTransitionInvalid);
    }

    [Fact]
    public void An_expired_invitation_can_not_be_revoked_but_is_superseded_by_a_new_one()
    {
        var invitation = Pending();
        var expired = _now.AddDays(8);

        invitation.Revoke(_owner, expired).Error.ShouldBe(IdentityErrors.InvitationTransitionInvalid);
        invitation.Supersede(_owner, expired);

        invitation.Status.ShouldBe(InvitationStatus.Revoked);
        Should.Throw<InvalidOperationException>(() => invitation.Supersede(_owner, expired));
    }

    [Fact]
    public void A_token_is_issued_only_while_pending_and_the_newest_replaces_the_older()
    {
        var invitation = Pending();
        var (_, first) = InvitationToken.Create();
        var (_, second) = InvitationToken.Create();

        invitation.IssueToken(first, _now).ShouldBeTrue();
        invitation.IssueToken(second, _now.AddMinutes(1)).ShouldBeTrue();
        invitation.TokenHash.ShouldBe(second);
        invitation.TokenIssuedAt.ShouldBe(_now.AddMinutes(1));

        invitation.IssueToken(first, _now.AddDays(7)).ShouldBeFalse(); // expired
        invitation.Revoke(_owner, _now.AddDays(1)).IsSuccess.ShouldBeTrue();
        invitation.IssueToken(first, _now.AddDays(1)).ShouldBeFalse();
        invitation.TokenHash.ShouldBe(second);
    }

    [Fact]
    public void A_token_is_32_random_bytes_and_only_its_sha256_is_kept()
    {
        var (token, hash) = InvitationToken.Create();
        var (other, _) = InvitationToken.Create();

        token.Length.ShouldBe(43); // base64url, no padding
        token.ShouldNotBe(other);
        hash.Length.ShouldBe(32);
        InvitationToken.Hash(token).ShouldBe(hash);
    }
}

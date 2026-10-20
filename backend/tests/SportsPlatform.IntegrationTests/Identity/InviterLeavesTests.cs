using System.Net;
using SportsPlatform.BuildingBlocks.Auditing;
using SportsPlatform.BuildingBlocks.Persistence;
using SportsPlatform.IntegrationTests.Api;
using SportsPlatform.IntegrationTests.Audit;
using SportsPlatform.IntegrationTests.Infrastructure;
using SportsPlatform.IntegrationTests.Tenancy;
using SportsPlatform.Modules.Identity.Domain.Invitations;
using SportsPlatform.Modules.Identity.Domain.Roles;
using SportsPlatform.Modules.Tenancy.Contracts.Tenants;
using static SportsPlatform.IntegrationTests.Identity.InvitationRequests;
using static SportsPlatform.IntegrationTests.Identity.MemberRequests;
using static SportsPlatform.IntegrationTests.Identity.MembershipTestData;

namespace SportsPlatform.IntegrationTests.Identity;

/// <summary>
/// Q20 (owner, S03-T5): suspending or removing a member revokes the pending, unexpired invitations they sent in the
/// tenant, in the same save, so the invitees' links stop working. Other invitations are untouched.
/// </summary>
public sealed class InviterLeavesTests(PostgresFixture fixture) : DatabaseTest(fixture)
{
    private const string _brianEmail = "brian.ssemakula@kmsa.example";
    private const string _estherEmail = "esther.nabirye@kmsa.example";
    private const string _ruthEmail = "ruth.akello@kmsa.example";

    [Theory]
    [InlineData("suspend")]
    [InlineData("remove")]
    public async Task An_inviter_who_leaves_takes_their_pending_invitations_with_them(string transition)
    {
        var (kmsa, grace, daniel) = await KmsaWithOwnerAndAdminAsync();
        using var owner = App.ClientFor(grace.UserId, kmsa);
        using var admin = App.ClientFor(daniel.UserId, kmsa);
        var danielsInvitation = await admin.InvitedAsync(_brianEmail, Ct, IdentityRoles.Registrar);
        await owner.InvitedAsync(_estherEmail, Ct, IdentityRoles.Viewer);
        await AccountTestData.DeliverOutboxAsync(App.Services, Ct);
        var brianToken = TokenOf(App.Emails.Sent.Last(email => email.To == _brianEmail));
        var estherToken = TokenOf(App.Emails.Sent.Last(email => email.To == _estherEmail));

        using var changed = transition == "suspend"
            ? await owner.SuspendAsync(daniel.MembershipId, Ct)
            : await owner.RemoveAsync(daniel.MembershipId, Ct);

        changed.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var anonymous = App.CreateClient();
        await (await anonymous.LookupInvitationAsync(brianToken, Ct)).ShouldBeProblemAsync(HttpStatusCode.NotFound, "AUTH_INVITATION_TOKEN_INVALID");
        await (await anonymous.AcceptInvitationAsync(brianToken, TestPasswords.New(), Ct))
            .ShouldBeProblemAsync(HttpStatusCode.NotFound, "AUTH_INVITATION_TOKEN_INVALID");
        (await anonymous.LookupInvitationAsync(estherToken, Ct)).StatusCode.ShouldBe(HttpStatusCode.OK); // the owner's

        using var read = await owner.GetAsync(InvitationPath(danielsInvitation.InvitationId), Ct);
        (await read.ReadInvitationAsync(Ct)).ShouldSatisfyAllConditions(
            i => i.Status.ShouldBe(InvitationState.Revoked),
            i => i.RevokedAt.ShouldBe(App.Clock.GetUtcNow()));
        (await RevokedByAsync(danielsInvitation.InvitationId)).ShouldBe(grace.UserId);

        // One audit entry, the member change's, naming the revoked invitation (its ID, never the email).
        await AccountTestData.DeliverOutboxAsync(App.Services, Ct);
        var entry = (await AuditTestData.EntriesAsync(App.Services, Ct)).Single(e => e.ActionCode == $"membership.{transition}");
        entry.ShouldSatisfyAllConditions(
            e => e.Outcome.ShouldBe(AuditOutcome.Success),
            e => e.EntityId.ShouldBe(daniel.MembershipId),
            e => e.Metadata.ShouldNotBeNull().ShouldContain(danielsInvitation.InvitationId.ToString()),
            e => e.Metadata!.ShouldNotContain("@"));
        (await AuditTestData.EntriesAsync(App.Services, Ct)).ShouldNotContain(e => e.ActionCode == "invitation.revoke");
    }

    [Fact]
    public async Task Accepted_and_expired_invitations_stay_as_they_are_and_restoring_brings_none_back()
    {
        var (kmsa, grace, daniel) = await KmsaWithOwnerAndAdminAsync();
        using var owner = App.ClientFor(grace.UserId, kmsa);
        using var admin = App.ClientFor(daniel.UserId, kmsa);
        using var anonymous = App.CreateClient();

        // Ruth accepted; Brian's invitation expired; Esther's is pending.
        await admin.InvitedAsync(_ruthEmail, Ct, IdentityRoles.Viewer);
        var expired = await admin.InvitedAsync(_brianEmail, Ct, IdentityRoles.Registrar);
        await AccountTestData.DeliverOutboxAsync(App.Services, Ct);
        using (var accepted = await anonymous.AcceptInvitationAsync(TokenOf(App.Emails.Sent.Last(e => e.To == _ruthEmail)), TestPasswords.New(), Ct))
        {
            accepted.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        App.Clock.Advance(Invitation.Lifetime);
        var pending = await admin.InvitedAsync(_estherEmail, Ct, IdentityRoles.Viewer);
        await AccountTestData.DeliverOutboxAsync(App.Services, Ct);
        var estherToken = TokenOf(App.Emails.Sent.Last(e => e.To == _estherEmail));

        (await owner.SuspendAsync(daniel.MembershipId, Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await owner.RestoreAsync(daniel.MembershipId, Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);

        (await StatusesAsync()).ShouldBe(
        [
            (_brianEmail, "PENDING"), // expired: derived, final, still 410
            (_estherEmail, "REVOKED"),
            (_ruthEmail, "ACCEPTED"),
        ]);
        await (await anonymous.LookupInvitationAsync(estherToken, Ct)).ShouldBeProblemAsync(HttpStatusCode.NotFound, "AUTH_INVITATION_TOKEN_INVALID");
        using var expiredRead = await owner.GetAsync(InvitationPath(expired.InvitationId), Ct);
        (await expiredRead.ReadInvitationAsync(Ct)).Status.ShouldBe(InvitationState.Expired);
        using var pendingRead = await owner.GetAsync(InvitationPath(pending.InvitationId), Ct);
        (await pendingRead.ReadInvitationAsync(Ct)).Status.ShouldBe(InvitationState.Revoked);
    }

    [Fact]
    public async Task A_refused_suspension_revokes_nothing()
    {
        var (kmsa, grace, daniel) = await KmsaWithOwnerAndAdminAsync();
        using var owner = App.ClientFor(grace.UserId, kmsa);
        using var admin = App.ClientFor(daniel.UserId, kmsa);
        await owner.InvitedAsync(_brianEmail, Ct, IdentityRoles.Registrar);

        // Only an owner suspends an owner: the admin is refused, and the owner's invitation stays pending.
        using var refused = await admin.SuspendAsync(grace.MembershipId, Ct);

        await refused.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "AUTH_OWNER_REQUIRED");
        (await StatusesAsync()).ShouldBe([(_brianEmail, "PENDING")]);
    }

    private async Task<(Guid Tenant, Member Owner, Member Admin)> KmsaWithOwnerAndAdminAsync()
    {
        App.Authorization.UseIdentityRoles();
        var kmsa = await TenantTestData.SaveTenantAsync(App.Services, "kmsa", TenantStatus.Active, Ct);
        var grace = await CreateMemberAsync(App.Services, kmsa, "grace.nakato@kmsa.example", Ct, TenantWide(IdentityRoles.TenantOwner));
        var daniel = await CreateMemberAsync(App.Services, kmsa, "daniel.kato@kmsa.example", Ct, TenantWide(IdentityRoles.TenantAdmin));
        return (kmsa, grace, daniel);
    }

    private async Task<List<(string Email, string Status)>> StatusesAsync() =>
        await QueryAsync(
            DatabaseRoles.Platform,
            "SELECT email, status FROM identity.invitations ORDER BY email",
            r => (r.GetString(0), r.GetString(1)));

    private async Task<Guid> RevokedByAsync(Guid invitationId) =>
        (await QueryAsync(
            DatabaseRoles.Platform,
            "SELECT revoked_by FROM identity.invitations WHERE id = @id",
            r => r.GetGuid(0),
            new Npgsql.NpgsqlParameter("id", invitationId))).Single();
}

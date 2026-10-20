using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SportsPlatform.BuildingBlocks.Auditing;
using SportsPlatform.BuildingBlocks.Persistence;
using SportsPlatform.IntegrationTests.Api;
using SportsPlatform.IntegrationTests.Audit;
using SportsPlatform.IntegrationTests.Infrastructure;
using SportsPlatform.IntegrationTests.Tenancy;
using SportsPlatform.Modules.Identity.Domain.Roles;
using SportsPlatform.Modules.Identity.Features.Account.GetMe;
using SportsPlatform.Modules.Identity.Features.Invitations.AcceptInvitation;
using SportsPlatform.Modules.Identity.Features.Invitations.LookupInvitation;
using SportsPlatform.Modules.Tenancy.Contracts.Tenants;
using static SportsPlatform.IntegrationTests.Identity.InvitationRequests;
using static SportsPlatform.IntegrationTests.Identity.MemberRequests;
using static SportsPlatform.IntegrationTests.Identity.MembershipTestData;

namespace SportsPlatform.IntegrationTests.Identity;

/// <summary>
/// The invitee's side (S03 D12, T4c2): <c>POST /api/account/v1/invitations/lookup</c> and <c>…/accept</c> with the
/// emailed token. A token works once and expires; a new user sets a password, an existing one signs in first.
/// </summary>
public sealed class InvitationAcceptanceTests(PostgresFixture fixture) : DatabaseTest(fixture)
{
    private const string _brianEmail = "brian.ssemakula@kmsa.example";

    [Fact]
    public async Task The_invitee_looks_up_a_pending_invitation_without_signing_in()
    {
        var (_, _, token) = await KmsaInvitesBrianAsync(IdentityRoles.Registrar, IdentityRoles.Viewer);
        using var anonymous = App.CreateClient();

        using var response = await anonymous.LookupInvitationAsync(token, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.CacheControl!.NoStore.ShouldBeTrue();
        var invitation = (await response.Content.ReadFromJsonAsync<LookupInvitationResponse>(Json, Ct)).ShouldNotBeNull();
        invitation.ShouldSatisfyAllConditions(
            i => i.TenantDisplayName.ShouldBe("kmsa Sports"),
            i => i.Email.ShouldBe(_brianEmail),
            i => i.RoleCodes.ShouldBe([IdentityRoles.Registrar, IdentityRoles.Viewer]),
            i => i.ExpiresAt.ShouldBe(App.Clock.GetUtcNow().AddDays(7)),
            i => i.AccountExists.ShouldBeFalse());
    }

    // S03 T4 acceptance: invitation tokens are single-use, expire, and are stored hashed (tests read the table).
    [Fact]
    public async Task A_new_user_accepts_with_a_password_joins_with_the_roles_and_the_token_works_once()
    {
        var (kmsa, grace, token) = await KmsaInvitesBrianAsync(IdentityRoles.Registrar);
        using var anonymous = App.CreateClient();
        var password = TestPasswords.New();

        using var response = await anonymous.AcceptInvitationAsync(token, password, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));
        var accepted = (await response.Content.ReadFromJsonAsync<AcceptInvitationResponse>(Json, Ct)).ShouldNotBeNull();
        accepted.TenantId.ShouldBe(kmsa);
        response.Headers.Contains("Set-Cookie").ShouldBeFalse(); // accepting doesn't sign in

        // The table: accepted by the new user into the new membership; the stored hash is the token's, never the token.
        var row = (await QueryAsync(
            DatabaseRoles.Platform,
            "SELECT status, accepted_by, membership_id, row_to_json(i)::text FROM identity.invitations i",
            r => (Status: r.GetString(0), AcceptedBy: r.GetGuid(1), MembershipId: r.GetGuid(2), Json: r.GetString(3)))).ShouldHaveSingleItem();
        row.Status.ShouldBe("ACCEPTED");
        row.MembershipId.ShouldBe(accepted.MembershipId);
        row.Json.ShouldNotContain(token);
        var roles = await QueryAsync(
            DatabaseRoles.Platform,
            "SELECT role_code, granted_by FROM identity.role_assignments WHERE membership_id = @m",
            r => (r.GetString(0), r.GetGuid(1)),
            new Npgsql.NpgsqlParameter("m", accepted.MembershipId));
        roles.ShouldBe([(IdentityRoles.Registrar, grace.UserId)]); // granted by the inviter

        // The user is confirmed (holding the token proves the email) and signs in with the chosen password.
        using var session = App.CreateClient();
        (await session.LoginAsync(_brianEmail, password, Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var me = (await session.GetFromJsonAsync<MeResponse>(AccountTestData.MePath, Json, Ct)).ShouldNotBeNull();
        me.Id.ShouldBe(row.AcceptedBy);
        me.Tenants.ShouldHaveSingleItem().ShouldSatisfyAllConditions(
            t => t.TenantId.ShouldBe(kmsa),
            t => t.Roles.ShouldBe([IdentityRoles.Registrar]));

        // Once only.
        using var again = await anonymous.AcceptInvitationAsync(token, TestPasswords.New(), Ct);
        await again.ShouldBeProblemAsync(HttpStatusCode.Conflict, "AUTH_INVITATION_ALREADY_ACCEPTED");
        using var lookup = await anonymous.LookupInvitationAsync(token, Ct);
        await lookup.ShouldBeProblemAsync(HttpStatusCode.Conflict, "AUTH_INVITATION_ALREADY_ACCEPTED");
    }

    [Fact]
    public async Task An_acceptance_is_audited_under_the_tenant_without_the_email()
    {
        var (kmsa, _, token) = await KmsaInvitesBrianAsync(IdentityRoles.Registrar);
        using var anonymous = App.CreateClient();
        using var response = await anonymous.AcceptInvitationAsync(token, TestPasswords.New(), Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var accepted = (await response.Content.ReadFromJsonAsync<AcceptInvitationResponse>(Json, Ct)).ShouldNotBeNull();

        await AccountTestData.DeliverOutboxAsync(App.Services, Ct);
        var entry = (await AuditTestData.EntriesAsync(App.Services, Ct)).Single(e => e.ActionCode == "invitation.accept");
        entry.ShouldSatisfyAllConditions(
            e => e.TenantId.ShouldBe(kmsa),
            e => e.Outcome.ShouldBe(AuditOutcome.Success),
            e => e.EntityType.ShouldBe("Invitation"),
            e => e.Metadata.ShouldNotBeNull().ShouldContain(accepted.MembershipId.ToString()),
            e => e.Metadata!.ShouldNotContain("@"));
    }

    [Fact]
    public async Task A_new_user_must_choose_a_password_that_passes_the_policy()
    {
        var (_, _, token) = await KmsaInvitesBrianAsync(IdentityRoles.Registrar);
        using var anonymous = App.CreateClient();

        using var missing = await anonymous.AcceptInvitationAsync(token, password: null, Ct);
        using var tooShort = await anonymous.AcceptInvitationAsync(token, TestPasswords.TooShort(), Ct);

        await missing.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "AUTH_PASSWORD_REJECTED");
        var problem = await tooShort.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "AUTH_PASSWORD_REJECTED");
        problem.GetProperty("errors")[0].GetProperty("field").GetString().ShouldBe("password");
        (await CountAsync("SELECT count(*) FROM identity.users WHERE normalized_email = 'BRIAN.SSEMAKULA@KMSA.EXAMPLE'")).ShouldBe(0);
        using var lookup = await anonymous.LookupInvitationAsync(token, Ct);
        lookup.StatusCode.ShouldBe(HttpStatusCode.OK); // still pending
    }

    [Fact]
    public async Task An_existing_user_accepts_only_when_signed_in_as_the_invited_email()
    {
        App.Authorization.UseIdentityRoles();
        var password = TestPasswords.New();
        var brian = await AccountTestData.CreateUserAsync(App.Services, _brianEmail, password, Ct);
        var daniel = await AccountTestData.CreateUserAsync(App.Services, "daniel.kato@kmsa.example", TestPasswords.New(), Ct);
        var (kmsa, _, token) = await KmsaInvitesBrianAsync(IdentityRoles.Registrar);
        using var anonymous = App.CreateClient();
        using var asDaniel = App.CreateClient().AsUser(daniel);
        using var asBrian = App.CreateClient().AsUser(brian);

        using var lookup = await anonymous.LookupInvitationAsync(token, Ct);
        using var signedOut = await anonymous.AcceptInvitationAsync(token, TestPasswords.New(), Ct);
        using var otherUser = await asDaniel.AcceptInvitationAsync(token, password: null, Ct);
        using var invitee = await asBrian.AcceptInvitationAsync(token, TestPasswords.New(), Ct);

        (await lookup.Content.ReadFromJsonAsync<LookupInvitationResponse>(Json, Ct)).ShouldNotBeNull().AccountExists.ShouldBeTrue();
        await signedOut.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "AUTH_INVITATION_SIGN_IN_REQUIRED");
        await otherUser.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "AUTH_INVITATION_SIGN_IN_REQUIRED");
        invitee.StatusCode.ShouldBe(HttpStatusCode.OK, await invitee.Content.ReadAsStringAsync(Ct));
        (await invitee.Content.ReadFromJsonAsync<AcceptInvitationResponse>(Json, Ct)).ShouldNotBeNull().TenantId.ShouldBe(kmsa);

        // The password sent was ignored: Brian keeps his own.
        using var session = App.CreateClient();
        (await session.LoginAsync(_brianEmail, password, Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var me = (await session.GetFromJsonAsync<MeResponse>(AccountTestData.MePath, Json, Ct)).ShouldNotBeNull();
        me.Tenants.ShouldHaveSingleItem().Roles.ShouldBe([IdentityRoles.Registrar]);
    }

    [Fact]
    public async Task An_expired_invitation_can_not_be_looked_up_or_accepted()
    {
        var (_, _, token) = await KmsaInvitesBrianAsync(IdentityRoles.Registrar);
        App.Clock.Advance(TimeSpan.FromDays(7));
        using var anonymous = App.CreateClient();

        using var lookup = await anonymous.LookupInvitationAsync(token, Ct);
        using var accept = await anonymous.AcceptInvitationAsync(token, TestPasswords.New(), Ct);

        await lookup.ShouldBeProblemAsync(HttpStatusCode.Gone, "AUTH_INVITATION_EXPIRED");
        await accept.ShouldBeProblemAsync(HttpStatusCode.Gone, "AUTH_INVITATION_EXPIRED");
        (await CountAsync("SELECT count(*) FROM identity.tenant_memberships")).ShouldBe(1); // Grace only
    }

    [Fact]
    public async Task Revoked_replaced_and_unknown_tokens_answer_alike()
    {
        var (kmsa, grace, revokedToken) = await KmsaInvitesBrianAsync(IdentityRoles.Registrar);
        using var owner = App.ClientFor(grace.UserId, kmsa);
        var revoked = (await QueryAsync(DatabaseRoles.Platform, "SELECT id FROM identity.invitations", r => r.GetGuid(0))).ShouldHaveSingleItem();
        (await owner.RevokeInvitationAsync(revoked, Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);

        await owner.InvitedAsync("esther.nabirye@kmsa.example", Ct, IdentityRoles.ResultsOfficer);
        await AccountTestData.DeliverOutboxAsync(App.Services, Ct);
        var replacedToken = TokenOf(App.Emails.Sent[^1]);
        await owner.InvitedAsync("esther.nabirye@kmsa.example", Ct, IdentityRoles.Viewer); // replaces Esther's first
        using var anonymous = App.CreateClient();

        foreach (var token in new[] { revokedToken, replacedToken, "bm90LWEtcmVhbC10b2tlbi1hdC1hbGwtMzItYnl0ZXM" })
        {
            using var lookup = await anonymous.LookupInvitationAsync(token, Ct);
            using var accept = await anonymous.AcceptInvitationAsync(token, TestPasswords.New(), Ct);
            await lookup.ShouldBeProblemAsync(HttpStatusCode.NotFound, "AUTH_INVITATION_TOKEN_INVALID");
            await accept.ShouldBeProblemAsync(HttpStatusCode.NotFound, "AUTH_INVITATION_TOKEN_INVALID");
        }

        using var empty = await anonymous.LookupInvitationAsync("", Ct);
        await empty.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_INVALID_REQUEST");
    }

    [Fact]
    public async Task A_current_member_can_not_accept_again()
    {
        var (kmsa, _, token) = await KmsaInvitesBrianAsync(IdentityRoles.Registrar);
        // Brian joined another way after the invitation was sent.
        var brian = await AccountTestData.CreateUserAsync(App.Services, _brianEmail, TestPasswords.New(), Ct);
        await JoinAsync(App.Services, kmsa, brian, Ct, TenantWide(IdentityRoles.Viewer));
        using var asBrian = App.CreateClient().AsUser(brian);

        using var response = await asBrian.AcceptInvitationAsync(token, password: null, Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, "AUTH_ALREADY_MEMBER");
    }

    // #49 N3: roles are defined in code; one an invitation names may be gone when it is accepted.
    [Fact]
    public async Task An_invitation_naming_a_role_that_no_longer_exists_is_refused()
    {
        var (kmsa, _, token) = await KmsaInvitesBrianAsync(IdentityRoles.Registrar);
        await ExecuteAsync(
            DatabaseRoles.Migrator,
            $"SELECT set_config('app.tenant_id', '{kmsa}', false); UPDATE identity.invitations SET role_codes = ARRAY['RETIRED_ROLE']");
        using var anonymous = App.CreateClient();

        using var response = await anonymous.AcceptInvitationAsync(token, TestPasswords.New(), Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "AUTH_ROLE_NOT_ASSIGNABLE");
        (await CountAsync("SELECT count(*) FROM identity.users WHERE normalized_email = 'BRIAN.SSEMAKULA@KMSA.EXAMPLE'")).ShouldBe(0);
    }

    [Fact]
    public async Task No_one_joins_an_archived_tenant()
    {
        var (kmsa, _, token) = await KmsaInvitesBrianAsync(IdentityRoles.Registrar);
        await ExecuteAsync(DatabaseRoles.Migrator, $"UPDATE tenancy.tenants SET status = 'ARCHIVED' WHERE id = '{kmsa}'");
        using var anonymous = App.CreateClient();

        using var lookup = await anonymous.LookupInvitationAsync(token, Ct);
        using var accept = await anonymous.AcceptInvitationAsync(token, TestPasswords.New(), Ct);

        await lookup.ShouldBeProblemAsync(HttpStatusCode.Conflict, "AUTH_TENANT_ARCHIVED");
        await accept.ShouldBeProblemAsync(HttpStatusCode.Conflict, "AUTH_TENANT_ARCHIVED");
    }

    // #49 N3: a link used twice at once gives one membership.
    [Fact]
    public async Task The_same_link_used_twice_at_once_gives_one_user_and_one_membership()
    {
        var (_, _, token) = await KmsaInvitesBrianAsync(IdentityRoles.Registrar);
        using var first = App.CreateClient();
        using var second = App.CreateClient();

        var responses = await Task.WhenAll(
            first.AcceptInvitationAsync(token, TestPasswords.New(), Ct),
            second.AcceptInvitationAsync(token, TestPasswords.New(), Ct));

        try
        {
            responses.Count(r => r.StatusCode == HttpStatusCode.OK).ShouldBe(1);
            var refused = responses.Single(r => r.StatusCode != HttpStatusCode.OK);
            refused.StatusCode.ShouldBe(HttpStatusCode.Conflict);
            var code = (await refused.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("code").GetString();
            code.ShouldBeOneOf("AUTH_CONCURRENCY_CONFLICT", "AUTH_INVITATION_ALREADY_ACCEPTED");
            (await CountAsync("SELECT count(*) FROM identity.users WHERE normalized_email = 'BRIAN.SSEMAKULA@KMSA.EXAMPLE'")).ShouldBe(1);
            (await CountAsync("SELECT count(*) FROM identity.tenant_memberships")).ShouldBe(2); // Grace and Brian
        }
        finally
        {
            foreach (var response in responses)
            {
                response.Dispose();
            }
        }
    }

    /// <summary>KMSA with Grace as owner; Grace invites Brian; the email is delivered. Returns its token.</summary>
    private async Task<(Guid Tenant, Member Owner, string Token)> KmsaInvitesBrianAsync(params string[] roles)
    {
        App.Authorization.UseIdentityRoles();
        var kmsa = await TenantTestData.SaveTenantAsync(App.Services, "kmsa", TenantStatus.Active, Ct);
        var grace = await CreateMemberAsync(App.Services, kmsa, "grace.nakato@kmsa.example", Ct, TenantWide(IdentityRoles.TenantOwner));
        using var owner = App.ClientFor(grace.UserId, kmsa);
        await owner.InvitedAsync(_brianEmail, Ct, roles);
        await AccountTestData.DeliverOutboxAsync(App.Services, Ct);
        return (kmsa, grace, TokenOf(App.Emails.Sent[^1]));
    }

    private async Task<long> CountAsync(string sql) =>
        (await QueryAsync(DatabaseRoles.Platform, sql, r => r.GetInt64(0))).Single();
}

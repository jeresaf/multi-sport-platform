using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using SportsPlatform.BuildingBlocks.Persistence;
using SportsPlatform.IntegrationTests.Api;
using SportsPlatform.IntegrationTests.Audit;
using SportsPlatform.IntegrationTests.Infrastructure;
using SportsPlatform.IntegrationTests.Tenancy;
using SportsPlatform.Modules.Identity.Domain.Invitations;
using SportsPlatform.Modules.Identity.Domain.Roles;
using SportsPlatform.Modules.Identity.Features.Invitations.ListInvitations;
using SportsPlatform.Modules.Tenancy.Contracts.Tenants;
using static SportsPlatform.IntegrationTests.Identity.InvitationRequests;
using static SportsPlatform.IntegrationTests.Identity.MemberRequests;
using static SportsPlatform.IntegrationTests.Identity.MembershipTestData;

namespace SportsPlatform.IntegrationTests.Identity;

/// <summary>
/// <c>/api/admin/v1/invitations</c> (S03-T4c, D12, D13): invite a person with tenant-wide roles, list, read and revoke
/// invitations; the email goes out through the outbox with a token whose SHA-256 alone is stored.
/// </summary>
public sealed class InvitationEndpointsTests(PostgresFixture fixture) : DatabaseTest(fixture)
{
    private const string _brianEmail = "brian.ssemakula@kmsa.example";

    [Fact]
    public async Task An_owner_invites_a_person_and_the_email_carries_a_token_stored_only_as_its_hash()
    {
        var (kmsa, grace) = await KmsaWithOwnerAsync();
        using var client = App.ClientFor(grace.UserId, kmsa);
        var invitedAt = App.Clock.GetUtcNow();

        using var response = await client.InviteAsync(" Brian.Ssemakula@KMSA.example ", Ct, IdentityRoles.Registrar, IdentityRoles.Viewer);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var invitation = await response.ReadInvitationAsync(Ct);
        response.Headers.Location!.OriginalString.ShouldBe(InvitationPath(invitation.InvitationId));
        response.Headers.ETag!.Tag.ShouldBe($"\"{invitation.Version}\"");
        invitation.ShouldSatisfyAllConditions(
            i => i.Email.ShouldBe("Brian.Ssemakula@KMSA.example"),
            i => i.RoleCodes.ShouldBe([IdentityRoles.Registrar, IdentityRoles.Viewer]),
            i => i.Status.ShouldBe(InvitationState.Pending),
            i => i.InvitedBy.ShouldBe(grace.UserId),
            i => i.ExpiresAt.ShouldBe(invitedAt.AddDays(7)),
            i => i.EmailSentAt.ShouldBeNull());

        App.Emails.Sent.ShouldBeEmpty(); // never inside the request
        await AccountTestData.DeliverOutboxAsync(App.Services, Ct);
        var email = App.Emails.Sent.ShouldHaveSingleItem();
        email.To.ShouldBe("Brian.Ssemakula@KMSA.example");
        email.Subject.ShouldBe("You're invited to join kmsa Sports");
        email.TextBody.ShouldContain("http://localhost:3001/accept-invitation?token=");
        var token = TokenOf(email);

        // Only the token's SHA-256 is stored; no column holds the token.
        var rows = await QueryAsync(
            DatabaseRoles.Platform, "SELECT token_hash, row_to_json(i)::text FROM identity.invitations i", r => ((byte[])r[0], r.GetString(1)));
        var (hash, row) = rows.ShouldHaveSingleItem();
        hash.ShouldBe(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
        row.ShouldNotContain(token);
        using var read = await client.GetAsync(InvitationPath(invitation.InvitationId), Ct);
        (await read.ReadInvitationAsync(Ct)).EmailSentAt.ShouldBe(App.Clock.GetUtcNow());

        var entry = (await AuditTestData.EntriesAsync(App.Services, Ct)).Single(e => e.ActionCode == "membership.invite");
        entry.ShouldSatisfyAllConditions(
            e => e.TenantId.ShouldBe(kmsa),
            e => e.EntityType.ShouldBe("Invitation"),
            e => e.EntityId.ShouldBe(invitation.InvitationId),
            e => e.Metadata.ShouldNotBeNull().ShouldContain(IdentityRoles.Registrar),
            e => e.Metadata!.ShouldNotContain("@"));
    }

    [Fact]
    public async Task A_failed_send_stores_no_token_and_the_retry_sends_the_one_that_is_stored()
    {
        var (kmsa, grace) = await KmsaWithOwnerAsync();
        using var client = App.ClientFor(grace.UserId, kmsa);
        await client.InvitedAsync(_brianEmail, Ct, IdentityRoles.Registrar);

        App.Emails.FailNext();
        await AccountTestData.DeliverOutboxAsync(App.Services, Ct);
        App.Emails.Sent.ShouldBeEmpty();
        (await StoredHashesAsync()).ShouldBe([null]);

        App.Clock.Advance(TimeSpan.FromHours(1)); // past the outbox's retry delay
        await AccountTestData.DeliverOutboxAsync(App.Services, Ct);

        var token = TokenOf(App.Emails.Sent.ShouldHaveSingleItem());
        (await StoredHashesAsync()).ShouldBe([SHA256.HashData(Encoding.UTF8.GetBytes(token))]);
    }

    [Fact]
    public async Task Inviting_the_same_email_again_revokes_the_pending_invitation_and_only_the_new_one_is_sent()
    {
        var (kmsa, grace) = await KmsaWithOwnerAsync();
        using var client = App.ClientFor(grace.UserId, kmsa);
        var first = await client.InvitedAsync(_brianEmail, Ct, IdentityRoles.Viewer);
        App.Clock.Advance(TimeSpan.FromMinutes(5));

        var second = await client.InvitedAsync("BRIAN.SSEMAKULA@kmsa.example", Ct, IdentityRoles.Registrar);
        await AccountTestData.DeliverOutboxAsync(App.Services, Ct);

        using var firstRead = await client.GetAsync(InvitationPath(first.InvitationId), Ct);
        (await firstRead.ReadInvitationAsync(Ct)).Status.ShouldBe(InvitationState.Revoked);
        second.Status.ShouldBe(InvitationState.Pending);
        App.Emails.Sent.ShouldHaveSingleItem().To.ShouldBe("BRIAN.SSEMAKULA@kmsa.example");
        var entry = (await AuditTestData.EntriesAsync(App.Services, Ct)).Single(e => e.EntityId == second.InvitationId);
        entry.Metadata.ShouldNotBeNull().ShouldContain(first.InvitationId.ToString());
    }

    [Fact]
    public async Task A_current_member_can_not_be_invited_but_a_removed_one_can()
    {
        var (kmsa, grace) = await KmsaWithOwnerAsync();
        var brian = await CreateMemberAsync(App.Services, kmsa, _brianEmail, Ct, TenantWide(IdentityRoles.Registrar));
        var esther = await CreateMemberAsync(App.Services, kmsa, "esther.nabirye@kmsa.example", Ct, TenantWide(IdentityRoles.ResultsOfficer));
        using var client = App.ClientFor(grace.UserId, kmsa);
        (await client.SuspendAsync(esther.MembershipId, Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);

        using var active = await client.InviteAsync("Brian.Ssemakula@kmsa.example", Ct, IdentityRoles.Viewer);
        using var suspended = await client.InviteAsync("esther.nabirye@kmsa.example", Ct, IdentityRoles.Viewer);
        (await client.RemoveAsync(brian.MembershipId, Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        using var removed = await client.InviteAsync(_brianEmail, Ct, IdentityRoles.Viewer);

        await active.ShouldBeProblemAsync(HttpStatusCode.Conflict, "AUTH_ALREADY_MEMBER");
        await suspended.ShouldBeProblemAsync(HttpStatusCode.Conflict, "AUTH_ALREADY_MEMBER");
        removed.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Membership_elsewhere_does_not_stop_an_invitation()
    {
        var (kmsa, grace) = await KmsaWithOwnerAsync();
        var elssc = await TenantTestData.SaveTenantAsync(App.Services, "elssc", TenantStatus.Active, Ct);
        var brian = await CreateMemberAsync(App.Services, elssc, _brianEmail, Ct, TenantWide(IdentityRoles.Viewer));
        using var client = App.ClientFor(grace.UserId, kmsa);

        using var response = await client.InviteAsync(_brianEmail, Ct, IdentityRoles.Registrar);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        brian.TenantId.ShouldBe(elssc);
    }

    [Theory]
    [InlineData("SCOREKEEPER")] // no such role
    [InlineData(IdentityRoles.TeamManager)] // per team only
    [InlineData("PLATFORM_ADMIN")]
    public async Task Roles_must_be_tenant_roles_held_tenant_wide(string role)
    {
        var (kmsa, grace) = await KmsaWithOwnerAsync();
        using var client = App.ClientFor(grace.UserId, kmsa);

        using var response = await client.InviteAsync(_brianEmail, Ct, IdentityRoles.Viewer, role);

        await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "AUTH_ROLE_NOT_ASSIGNABLE");
        (await StoredHashesAsync()).ShouldBeEmpty();
    }

    [Theory]
    [InlineData("""{ "email": "brian.ssemakula@kmsa.example", "roleCodes": [] }""", "roleCodes")]
    [InlineData("""{ "email": "brian.ssemakula@kmsa.example" }""", "roleCodes")]
    [InlineData("""{ "email": "brian.ssemakula", "roleCodes": ["VIEWER"] }""", "email")]
    [InlineData("""{ "roleCodes": ["VIEWER"] }""", "email")]
    public async Task A_malformed_invitation_is_refused(string body, string field)
    {
        var (kmsa, grace) = await KmsaWithOwnerAsync();
        using var client = App.ClientFor(grace.UserId, kmsa);

        using var response = await client.PostAsync(InvitationsPath, new StringContent(body, Encoding.UTF8, "application/json"), Ct);

        var problem = await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_INVALID_REQUEST");
        problem.GetProperty("errors").EnumerateArray().ShouldContain(e => e.GetProperty("field").GetString() == field);
    }

    [Fact]
    public async Task An_admin_invites_but_only_an_owner_invites_an_owner()
    {
        var (kmsa, _) = await KmsaWithOwnerAsync();
        var daniel = await CreateMemberAsync(App.Services, kmsa, "daniel.kato@kmsa.example", Ct, TenantWide(IdentityRoles.TenantAdmin));
        using var client = App.ClientFor(daniel.UserId, kmsa);

        using var registrar = await client.InviteAsync(_brianEmail, Ct, IdentityRoles.Registrar);
        using var owner = await client.InviteAsync("moses.okello@kmsa.example", Ct, IdentityRoles.TenantOwner);

        registrar.StatusCode.ShouldBe(HttpStatusCode.Created);
        await owner.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "AUTH_OWNER_REQUIRED");
    }

    [Fact]
    public async Task A_viewer_reads_invitations_but_can_not_invite_or_revoke_and_a_registrar_can_not_read_them()
    {
        var (kmsa, grace) = await KmsaWithOwnerAsync();
        var viewer = await CreateMemberAsync(App.Services, kmsa, "sarah.achieng@kmsa.example", Ct, TenantWide(IdentityRoles.Viewer));
        var registrar = await CreateMemberAsync(App.Services, kmsa, "esther.nabirye@kmsa.example", Ct, TenantWide(IdentityRoles.Registrar));
        using var owner = App.ClientFor(grace.UserId, kmsa);
        var invitation = await owner.InvitedAsync(_brianEmail, Ct, IdentityRoles.Registrar);
        using var asViewer = App.ClientFor(viewer.UserId, kmsa);
        using var asRegistrar = App.ClientFor(registrar.UserId, kmsa);

        using var list = await asViewer.GetAsync(InvitationsPath, Ct);
        using var read = await asViewer.GetAsync(InvitationPath(invitation.InvitationId), Ct);
        using var invite = await asViewer.InviteAsync("moses.okello@kmsa.example", Ct, IdentityRoles.Viewer);
        using var revoke = await asViewer.RevokeInvitationAsync(invitation.InvitationId, Ct);
        using var registrarList = await asRegistrar.GetAsync(InvitationsPath, Ct);

        list.StatusCode.ShouldBe(HttpStatusCode.OK);
        read.StatusCode.ShouldBe(HttpStatusCode.OK);
        await invite.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "AUTH_FORBIDDEN");
        await revoke.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "AUTH_FORBIDDEN");
        await registrarList.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "AUTH_FORBIDDEN");
    }

    [Fact]
    public async Task The_list_shows_pending_ones_by_default_and_filters_by_status_with_expiry_derived()
    {
        var (kmsa, grace) = await KmsaWithOwnerAsync();
        using var client = App.ClientFor(grace.UserId, kmsa);
        var expired = await client.InvitedAsync("moses.okello@kmsa.example", Ct, IdentityRoles.Viewer);
        App.Clock.Advance(TimeSpan.FromDays(6));
        var revoked = await client.InvitedAsync("isaac.mugisha@kmsa.example", Ct, IdentityRoles.Viewer);
        (await client.RevokeInvitationAsync(revoked.InvitationId, Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        var pending = await client.InvitedAsync(_brianEmail, Ct, IdentityRoles.Registrar);
        App.Clock.Advance(TimeSpan.FromDays(1)); // the first is now 7 days old

        var current = await ListAsync(client, string.Empty);
        var onlyPending = await ListAsync(client, "?status=PENDING");
        var onlyExpired = await ListAsync(client, "?status=EXPIRED");
        var onlyRevoked = await ListAsync(client, "?status=REVOKED");
        var accepted = await ListAsync(client, "?status=ACCEPTED");
        using var badStatus = await client.GetAsync($"{InvitationsPath}?status=expired", Ct);

        current.Items.Select(i => (i.InvitationId, i.Status)).ShouldBe(
            new[] { (expired.InvitationId, InvitationState.Expired), (pending.InvitationId, InvitationState.Pending) }.OrderBy(i => i.Item1));
        onlyPending.Items.ShouldHaveSingleItem().InvitationId.ShouldBe(pending.InvitationId);
        onlyExpired.Items.ShouldHaveSingleItem().InvitationId.ShouldBe(expired.InvitationId);
        onlyRevoked.Items.ShouldHaveSingleItem().InvitationId.ShouldBe(revoked.InvitationId);
        accepted.Items.ShouldBeEmpty();
        await badStatus.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_INVALID_QUERY");
    }

    [Fact]
    public async Task The_list_pages_with_a_cursor_and_each_read_is_audited_without_emails()
    {
        var (kmsa, grace) = await KmsaWithOwnerAsync();
        using var client = App.ClientFor(grace.UserId, kmsa);
        var first = await client.InvitedAsync(_brianEmail, Ct, IdentityRoles.Registrar);
        var second = await client.InvitedAsync("esther.nabirye@kmsa.example", Ct, IdentityRoles.ResultsOfficer);

        var page1 = await ListAsync(client, "?limit=1");
        var page2 = await ListAsync(client, $"?limit=1&cursor={page1.Page.NextCursor}");

        Guid[] oldestFirst = [.. new[] { first.InvitationId, second.InvitationId }.Order()];
        page1.Items.ShouldHaveSingleItem().InvitationId.ShouldBe(oldestFirst[0]);
        page1.Page.HasMore.ShouldBeTrue();
        page2.Items.ShouldHaveSingleItem().InvitationId.ShouldBe(oldestFirst[1]);
        page2.Page.ShouldBe(new(null, false));
        var reads = (await AuditTestData.EntriesAsync(App.Services, Ct)).Where(e => e.ActionCode == "membership.read").ToList();
        reads.Count.ShouldBe(2);
        reads.ShouldAllBe(e => e.TenantId == kmsa && (e.Metadata == null || !e.Metadata.Contains('@', StringComparison.Ordinal)));
    }

    [Fact]
    public async Task A_pending_invitation_is_revoked_once_with_its_version()
    {
        var (kmsa, grace) = await KmsaWithOwnerAsync();
        using var client = App.ClientFor(grace.UserId, kmsa);
        var invitation = await client.InvitedAsync(_brianEmail, Ct, IdentityRoles.Registrar);
        await AccountTestData.DeliverOutboxAsync(App.Services, Ct); // the sent email changes the version
        var stale = invitation.Version;

        using var withStaleVersion = await client.PostAsJsonAsync($"{InvitationPath(invitation.InvitationId)}/revoke", new { version = stale }, Ct);
        using var current = await client.GetAsync(InvitationPath(invitation.InvitationId), Ct);
        var etag = current.Headers.ETag!.ToString();
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{InvitationPath(invitation.InvitationId)}/revoke");
        request.Headers.TryAddWithoutValidation("If-Match", etag);
        using var revoked = await client.SendAsync(request, Ct);
        using var again = await client.RevokeInvitationAsync(invitation.InvitationId, Ct);
        using var unknown = await client.RevokeInvitationAsync(Guid.CreateVersion7(), Ct);

        await withStaleVersion.ShouldBeProblemAsync(HttpStatusCode.Conflict, "AUTH_CONCURRENCY_CONFLICT");
        revoked.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await revoked.ReadInvitationAsync(Ct);
        body.Status.ShouldBe(InvitationState.Revoked);
        body.RevokedAt.ShouldBe(App.Clock.GetUtcNow());
        revoked.Headers.ETag!.ToString().ShouldNotBe(etag);
        await again.ShouldBeProblemAsync(HttpStatusCode.Conflict, "AUTH_INVITATION_TRANSITION_INVALID");
        await unknown.ShouldBeProblemAsync(HttpStatusCode.NotFound, "AUTH_INVITATION_NOT_FOUND");
        await AccountTestData.DeliverOutboxAsync(App.Services, Ct); // a saved change's entry goes through the outbox
        (await AuditTestData.EntriesAsync(App.Services, Ct)).Where(e => e.ActionCode == "invitation.revoke")
            .Select(e => e.ErrorCode).ShouldBe(
                ["AUTH_CONCURRENCY_CONFLICT", null, "AUTH_INVITATION_TRANSITION_INVALID", "AUTH_INVITATION_NOT_FOUND"], ignoreOrder: true);
    }

    [Fact]
    public async Task An_expired_invitation_can_not_be_revoked_and_gets_no_email()
    {
        var (kmsa, grace) = await KmsaWithOwnerAsync();
        using var client = App.ClientFor(grace.UserId, kmsa);
        var invitation = await client.InvitedAsync(_brianEmail, Ct, IdentityRoles.Registrar);
        App.Clock.Advance(TimeSpan.FromDays(7));

        await AccountTestData.DeliverOutboxAsync(App.Services, Ct);
        using var revoke = await client.RevokeInvitationAsync(invitation.InvitationId, Ct);

        App.Emails.Sent.ShouldBeEmpty();
        await revoke.ShouldBeProblemAsync(HttpStatusCode.Conflict, "AUTH_INVITATION_TRANSITION_INVALID");
    }

    [Fact]
    public async Task A_member_of_another_tenant_sees_and_changes_none_of_its_invitations()
    {
        var (kmsa, grace) = await KmsaWithOwnerAsync();
        var elssc = await TenantTestData.SaveTenantAsync(App.Services, "elssc", TenantStatus.Active, Ct);
        var joseph = await CreateMemberAsync(App.Services, elssc, "joseph.wafula@elssc.example", Ct, TenantWide(IdentityRoles.TenantOwner));
        using var kmsaOwner = App.ClientFor(grace.UserId, kmsa);
        var invitation = await kmsaOwner.InvitedAsync(_brianEmail, Ct, IdentityRoles.Registrar);
        using var elsscOwner = App.ClientFor(joseph.UserId, elssc);
        using var withKmsaHeader = App.ClientFor(joseph.UserId, kmsa);

        var list = await ListAsync(elsscOwner, string.Empty);
        using var read = await elsscOwner.GetAsync(InvitationPath(invitation.InvitationId), Ct);
        using var revoke = await elsscOwner.RevokeInvitationAsync(invitation.InvitationId, Ct);
        using var header = await withKmsaHeader.GetAsync(InvitationsPath, Ct);

        list.Items.ShouldBeEmpty();
        await read.ShouldBeProblemAsync(HttpStatusCode.NotFound, "AUTH_INVITATION_NOT_FOUND");
        await revoke.ShouldBeProblemAsync(HttpStatusCode.NotFound, "AUTH_INVITATION_NOT_FOUND");
        await header.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "AUTH_TENANT_FORBIDDEN");
        using var stillPending = await kmsaOwner.GetAsync(InvitationPath(invitation.InvitationId), Ct);
        (await stillPending.ReadInvitationAsync(Ct)).Status.ShouldBe(InvitationState.Pending);
    }

    private async Task<(Guid Tenant, Member Owner)> KmsaWithOwnerAsync()
    {
        App.Authorization.UseIdentityRoles();
        var kmsa = await TenantTestData.SaveTenantAsync(App.Services, "kmsa", TenantStatus.Active, Ct);
        return (kmsa, await CreateMemberAsync(App.Services, kmsa, "grace.nakato@kmsa.example", Ct, TenantWide(IdentityRoles.TenantOwner)));
    }

    private Task<List<byte[]?>> StoredHashesAsync() =>
        QueryAsync(DatabaseRoles.Platform, "SELECT token_hash FROM identity.invitations", r => r.IsDBNull(0) ? null : (byte[])r[0]);

    private static async Task<ListInvitationsResponse> ListAsync(HttpClient client, string query)
    {
        using var response = await client.GetAsync($"{InvitationsPath}{query}", Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<ListInvitationsResponse>(Json, Ct)).ShouldNotBeNull();
    }
}

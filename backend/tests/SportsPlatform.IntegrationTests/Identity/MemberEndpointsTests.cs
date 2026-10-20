using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using SportsPlatform.BuildingBlocks.Actions;
using SportsPlatform.BuildingBlocks.Auditing;
using SportsPlatform.BuildingBlocks.Persistence.Messaging;
using SportsPlatform.IntegrationTests.Api;
using SportsPlatform.IntegrationTests.Audit;
using SportsPlatform.IntegrationTests.Infrastructure;
using SportsPlatform.IntegrationTests.Tenancy;
using SportsPlatform.Modules.Identity.Domain.Memberships;
using SportsPlatform.Modules.Identity.Domain.Roles;
using SportsPlatform.Modules.Identity.Features.Members.ListMembers;
using SportsPlatform.Modules.Tenancy.Contracts.Tenants;
using static SportsPlatform.IntegrationTests.Identity.MemberRequests;
using static SportsPlatform.IntegrationTests.Identity.MembershipTestData;

namespace SportsPlatform.IntegrationTests.Identity;

/// <summary>
/// <c>/api/admin/v1/members</c> (S03-T4b): list and read the tenant's members, suspend, restore and remove them, assign
/// and revoke roles; each change takes effect on the member's next request.
/// </summary>
public sealed class MemberEndpointsTests(PostgresFixture fixture) : DatabaseTest(fixture)
{
    private const string _graceEmail = "grace.nakato@kmsa.example";

    [Fact]
    public async Task The_list_shows_current_members_with_email_and_active_roles()
    {
        var (kmsa, grace) = await KmsaWithOwnerAsync();
        var brian = await CreateMemberAsync(App.Services, kmsa, "brian.ssemakula@kmsa.example", Ct, TenantWide(IdentityRoles.Registrar));
        var esther = await CreateMemberAsync(App.Services, kmsa, "esther.nabirye@kmsa.example", Ct, TenantWide(IdentityRoles.ResultsOfficer));
        var daniel = await CreateMemberAsync(App.Services, kmsa, "daniel.kato@kmsa.example", Ct, TenantWide(IdentityRoles.TenantAdmin));
        using var client = App.ClientFor(grace.UserId, kmsa);
        (await client.SuspendAsync(esther.MembershipId, Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await client.RemoveAsync(daniel.MembershipId, Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);

        var current = await ListAsync(client, string.Empty);
        var removed = await ListAsync(client, "?status=REMOVED");

        // Oldest first: by membership ID (UUIDv7).
        current.Items.Select(m => (m.MembershipId, m.Email, m.Status, string.Join(',', m.Roles.Select(r => r.RoleCode)))).ShouldBe(
            new[]
            {
                (grace.MembershipId, _graceEmail, MembershipStatus.Active, IdentityRoles.TenantOwner),
                (brian.MembershipId, "brian.ssemakula@kmsa.example", MembershipStatus.Active, IdentityRoles.Registrar),
                (esther.MembershipId, "esther.nabirye@kmsa.example", MembershipStatus.Suspended, IdentityRoles.ResultsOfficer),
            }.OrderBy(m => m.Item1));
        current.Items.Single(m => m.MembershipId == esther.MembershipId).StatusReason.ShouldBe("Membership fees unpaid");
        current.Page.HasMore.ShouldBeFalse();
        removed.Items.ShouldHaveSingleItem().MembershipId.ShouldBe(daniel.MembershipId);
    }

    [Fact]
    public async Task The_list_pages_with_a_cursor_and_audits_each_read_without_the_email()
    {
        var (kmsa, grace) = await KmsaWithOwnerAsync();
        var brian = await CreateMemberAsync(App.Services, kmsa, "brian.ssemakula@kmsa.example", Ct, TenantWide(IdentityRoles.Registrar));
        using var client = App.ClientFor(grace.UserId, kmsa);

        var first = await ListAsync(client, "?limit=1");
        var second = await ListAsync(client, $"?limit=1&cursor={first.Page.NextCursor}");
        using var badCursor = await client.GetAsync($"{MembersPath}?cursor=not-a-cursor", Ct);

        Guid[] oldestFirst = [.. new[] { grace.MembershipId, brian.MembershipId }.Order()];
        first.Items.ShouldHaveSingleItem().MembershipId.ShouldBe(oldestFirst[0]);
        first.Page.HasMore.ShouldBeTrue();
        second.Items.ShouldHaveSingleItem().MembershipId.ShouldBe(oldestFirst[1]);
        second.Page.ShouldBe(new(null, false));
        await badCursor.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_INVALID_QUERY");
        var reads = (await AuditTestData.EntriesAsync(App.Services, Ct)).Where(e => e.ActionCode == "membership.read").ToList();
        reads.Count.ShouldBe(3);
        reads.Count(e => e.Outcome == AuditOutcome.Success).ShouldBe(2);
        reads.ShouldAllBe(e => e.TenantId == kmsa && (e.Metadata == null || !e.Metadata.Contains('@', StringComparison.Ordinal)));
    }

    [Fact]
    public async Task A_member_is_read_with_their_version_as_the_etag()
    {
        var (kmsa, grace) = await KmsaWithOwnerAsync();
        using var client = App.ClientFor(grace.UserId, kmsa);

        using var response = await client.GetAsync(MemberPath(grace.MembershipId), Ct);
        using var unknown = await client.GetAsync(MemberPath(Guid.CreateVersion7()), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var member = await response.ReadMemberAsync(Ct);
        member.ShouldSatisfyAllConditions(
            m => m.UserId.ShouldBe(grace.UserId),
            m => m.Email.ShouldBe(_graceEmail),
            m => m.JoinedAt.ShouldBe(JoinedAt),
            m => m.Roles.ShouldHaveSingleItem().ShouldSatisfyAllConditions(
                r => r.RoleCode.ShouldBe(IdentityRoles.TenantOwner),
                r => r.ScopeType.ShouldBe(AssignmentScope.Tenant),
                r => r.ScopeId.ShouldBeNull()));
        response.Headers.ETag.ShouldNotBeNull().Tag.ShouldBe($"\"{member.Version}\"");
        await unknown.ShouldBeProblemAsync(HttpStatusCode.NotFound, "AUTH_MEMBERSHIP_NOT_FOUND");
    }

    [Fact]
    public async Task Suspending_removing_or_revoking_the_last_role_through_the_api_denies_the_next_request()
    {
        var (kmsa, grace) = await KmsaWithOwnerAsync();
        var brian = await CreateMemberAsync(App.Services, kmsa, "brian.ssemakula@kmsa.example", Ct, TenantWide(IdentityRoles.Registrar));
        var esther = await CreateMemberAsync(App.Services, kmsa, "esther.nabirye@kmsa.example", Ct, TenantWide(IdentityRoles.ResultsOfficer));
        var daniel = await CreateMemberAsync(App.Services, kmsa, "daniel.kato@kmsa.example", Ct, TenantWide(IdentityRoles.TenantAdmin));
        using var owner = App.ClientFor(grace.UserId, kmsa);
        using var brianClient = App.ClientFor(brian.UserId, kmsa);
        using var estherClient = App.ClientFor(esther.UserId, kmsa);
        using var danielClient = App.ClientFor(daniel.UserId, kmsa);
        var danielsRole = (await (await owner.GetAsync(MemberPath(daniel.MembershipId), Ct)).ReadMemberAsync(Ct)).Roles.Single().AssignmentId;

        (await owner.RemoveAsync(brian.MembershipId, Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await owner.SuspendAsync(esther.MembershipId, Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        using var revoked = await owner.RevokeAsync(daniel.MembershipId, danielsRole, Ct);

        revoked.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await revoked.ReadMemberAsync(Ct)).Roles.ShouldBeEmpty();
        await (await brianClient.GetAsync(TenantSettingsTests.SettingsPath, Ct)).ShouldBeProblemAsync(HttpStatusCode.Forbidden, "AUTH_TENANT_FORBIDDEN");
        await (await estherClient.GetAsync(TenantSettingsTests.SettingsPath, Ct)).ShouldBeProblemAsync(HttpStatusCode.Forbidden, "AUTH_TENANT_FORBIDDEN");
        await (await danielClient.GetAsync(TenantSettingsTests.SettingsPath, Ct)).ShouldBeProblemAsync(HttpStatusCode.Forbidden, "AUTH_FORBIDDEN");

        // Restored with the role she kept; Daniel works again once a role is assigned.
        (await owner.RestoreAsync(esther.MembershipId, Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await owner.AssignAsync(daniel.MembershipId, IdentityRoles.Viewer, Ct)).StatusCode.ShouldBe(HttpStatusCode.Created);
        (await estherClient.GetAsync(TenantSettingsTests.SettingsPath, Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await danielClient.GetAsync(TenantSettingsTests.SettingsPath, Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Suspending_and_removing_are_audited_with_the_reason()
    {
        var (kmsa, grace) = await KmsaWithOwnerAsync();
        var brian = await CreateMemberAsync(App.Services, kmsa, "brian.ssemakula@kmsa.example", Ct, TenantWide(IdentityRoles.Registrar));
        using var client = App.ClientFor(grace.UserId, kmsa);

        using var suspended = await client.SuspendAsync(brian.MembershipId, Ct);
        using var noReason = await client.PostAsJsonAsync($"{MemberPath(brian.MembershipId)}/remove", new { reason = " " }, Ct);

        suspended.StatusCode.ShouldBe(HttpStatusCode.OK);
        var member = await suspended.ReadMemberAsync(Ct);
        member.ShouldSatisfyAllConditions(
            m => m.Status.ShouldBe(MembershipStatus.Suspended),
            m => m.StatusReason.ShouldBe("Membership fees unpaid"),
            m => m.StatusChangedAt.ShouldBe(App.Clock.GetUtcNow()));
        await noReason.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_REASON_REQUIRED");
        await App.Services.GetRequiredService<OutboxProcessor>().ProcessOnceAsync(Ct);
        (await AuditTestData.EntriesAsync(App.Services, Ct)).Single(e => e.ActionCode == "membership.suspend").ShouldSatisfyAllConditions(
            e => e.Outcome.ShouldBe(AuditOutcome.Success),
            e => e.ActorId.ShouldBe(grace.UserId),
            e => e.TenantId.ShouldBe(kmsa),
            e => e.EntityType.ShouldBe("TenantMembership"),
            e => e.EntityId.ShouldBe(brian.MembershipId),
            e => e.Reason.ShouldBe("Membership fees unpaid"));
    }

    [Fact]
    public async Task A_stale_version_or_etag_is_a_conflict()
    {
        var (kmsa, grace) = await KmsaWithOwnerAsync();
        var brian = await CreateMemberAsync(App.Services, kmsa, "brian.ssemakula@kmsa.example", Ct, TenantWide(IdentityRoles.Registrar));
        using var client = App.ClientFor(grace.UserId, kmsa);
        var before = await (await client.GetAsync(MemberPath(brian.MembershipId), Ct)).ReadMemberAsync(Ct);
        // At the instant Brian's role was granted (the fake clock hasn't moved): the version changes all the same.
        using var assigned = await client.AssignAsync(brian.MembershipId, IdentityRoles.Viewer, Ct);
        (await assigned.ReadMemberAsync(Ct)).Version.ShouldNotBe(before.Version);

        using var staleVersion = await client.PostAsJsonAsync(
            $"{MemberPath(brian.MembershipId)}/suspend", new { reason = "Membership fees unpaid", version = before.Version }, Ct);
        using var staleETag = new HttpRequestMessage(HttpMethod.Post, $"{MemberPath(brian.MembershipId)}/restore")
        {
            Content = JsonContent.Create(new { }),
            Headers = { { "If-Match", $"\"{before.Version}\"" } },
        };
        using var staleIfMatch = await client.SendAsync(staleETag, Ct);

        await staleVersion.ShouldBeProblemAsync(HttpStatusCode.Conflict, "AUTH_CONCURRENCY_CONFLICT");
        await staleIfMatch.ShouldBeProblemAsync(HttpStatusCode.Conflict, "AUTH_CONCURRENCY_CONFLICT");
    }

    [Fact]
    public async Task Roles_are_assigned_tenant_wide_once_and_only_tenant_roles()
    {
        var (kmsa, grace) = await KmsaWithOwnerAsync();
        var brian = await CreateMemberAsync(App.Services, kmsa, "brian.ssemakula@kmsa.example", Ct);
        using var client = App.ClientFor(grace.UserId, kmsa);

        using var assigned = await client.AssignAsync(brian.MembershipId, IdentityRoles.Registrar, Ct);
        using var again = await client.AssignAsync(brian.MembershipId, IdentityRoles.Registrar, Ct);
        using var unknown = await client.AssignAsync(brian.MembershipId, "CLUB_TREASURER", Ct);
        using var platform = await client.AssignAsync(brian.MembershipId, "PLATFORM_ADMIN", Ct);
        using var competition = await client.PostAsJsonAsync(
            $"{MemberPath(brian.MembershipId)}/roles",
            new { roleCode = IdentityRoles.CompetitionManager, scopeType = "COMPETITION", scopeId = Guid.CreateVersion7() }, Ct);
        using var empty = await client.PostAsJsonAsync($"{MemberPath(brian.MembershipId)}/roles", new { roleCode = "" }, Ct);

        assigned.StatusCode.ShouldBe(HttpStatusCode.Created);
        assigned.Headers.Location.ShouldNotBeNull().OriginalString.ShouldBe(MemberPath(brian.MembershipId));
        (await assigned.ReadMemberAsync(Ct)).Roles.ShouldHaveSingleItem().ShouldSatisfyAllConditions(
            r => r.RoleCode.ShouldBe(IdentityRoles.Registrar),
            r => r.GrantedAt.ShouldBe(App.Clock.GetUtcNow()));
        await again.ShouldBeProblemAsync(HttpStatusCode.Conflict, "AUTH_ROLE_ALREADY_ASSIGNED");
        await unknown.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "AUTH_ROLE_NOT_ASSIGNABLE");
        await platform.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "AUTH_ROLE_NOT_ASSIGNABLE");
        await competition.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "AUTH_ROLE_NOT_ASSIGNABLE");
        await empty.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_INVALID_REQUEST");
    }

    [Fact]
    public async Task Changes_the_state_machine_does_not_allow_are_refused()
    {
        var (kmsa, grace) = await KmsaWithOwnerAsync();
        var brian = await CreateMemberAsync(App.Services, kmsa, "brian.ssemakula@kmsa.example", Ct, TenantWide(IdentityRoles.Registrar));
        using var client = App.ClientFor(grace.UserId, kmsa);

        using var restoreActive = await client.RestoreAsync(brian.MembershipId, Ct);
        (await client.RemoveAsync(brian.MembershipId, Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        using var removeAgain = await client.RemoveAsync(brian.MembershipId, Ct);
        using var assignRemoved = await client.AssignAsync(brian.MembershipId, IdentityRoles.Viewer, Ct);

        await restoreActive.ShouldBeProblemAsync(HttpStatusCode.Conflict, "AUTH_MEMBERSHIP_TRANSITION_INVALID");
        await removeAgain.ShouldBeProblemAsync(HttpStatusCode.Conflict, "AUTH_MEMBERSHIP_TRANSITION_INVALID");
        await assignRemoved.ShouldBeProblemAsync(HttpStatusCode.Conflict, "AUTH_MEMBERSHIP_TRANSITION_INVALID");
    }

    [Fact]
    public async Task A_viewer_reads_members_but_changes_none()
    {
        var (kmsa, _) = await KmsaWithOwnerAsync();
        var ruth = await CreateMemberAsync(App.Services, kmsa, "ruth.atim@kmsa.example", Ct, TenantWide(IdentityRoles.Viewer));
        var brian = await CreateMemberAsync(App.Services, kmsa, "brian.ssemakula@kmsa.example", Ct, TenantWide(IdentityRoles.Registrar));
        using var viewer = App.ClientFor(ruth.UserId, kmsa);
        using var registrar = App.ClientFor(brian.UserId, kmsa);

        (await viewer.GetAsync(MembersPath, Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        await (await registrar.GetAsync(MembersPath, Ct)).ShouldBeProblemAsync(HttpStatusCode.Forbidden, "AUTH_FORBIDDEN");
        foreach (var response in new[]
                 {
                     await viewer.SuspendAsync(brian.MembershipId, Ct),
                     await viewer.RestoreAsync(brian.MembershipId, Ct),
                     await viewer.RemoveAsync(brian.MembershipId, Ct),
                     await viewer.AssignAsync(brian.MembershipId, IdentityRoles.Viewer, Ct),
                     await viewer.RevokeAsync(brian.MembershipId, Guid.CreateVersion7(), Ct),
                 })
        {
            await response.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "AUTH_FORBIDDEN");
        }
    }

    [Fact]
    public async Task A_member_of_one_tenant_can_not_read_or_change_another_tenants_members()
    {
        var (kmsa, grace) = await KmsaWithOwnerAsync();
        var elssc = await TenantTestData.SaveTenantAsync(App.Services, "elssc", TenantStatus.Active, Ct);
        var elsscOwner = await CreateMemberAsync(App.Services, elssc, "owner@elssc.example", Ct, TenantWide(IdentityRoles.TenantOwner));
        var elsscViewer = await CreateMemberAsync(App.Services, elssc, "viewer@elssc.example", Ct, TenantWide(IdentityRoles.Viewer));
        using var underElssc = App.ClientFor(grace.UserId, elssc);
        using var underKmsa = App.ClientFor(grace.UserId, kmsa);

        // B's header: refused before anything is read.
        foreach (var response in new[]
                 {
                     await underElssc.GetAsync(MembersPath, Ct),
                     await underElssc.GetAsync(MemberPath(elsscViewer.MembershipId), Ct),
                     await underElssc.SuspendAsync(elsscViewer.MembershipId, Ct),
                     await underElssc.AssignAsync(elsscViewer.MembershipId, IdentityRoles.TenantAdmin, Ct),
                 })
        {
            await response.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "AUTH_TENANT_FORBIDDEN");
        }

        // A's header with B's IDs: not found (RLS), and nothing changes.
        await (await underKmsa.GetAsync(MemberPath(elsscViewer.MembershipId), Ct)).ShouldBeProblemAsync(HttpStatusCode.NotFound, "AUTH_MEMBERSHIP_NOT_FOUND");
        await (await underKmsa.SuspendAsync(elsscViewer.MembershipId, Ct)).ShouldBeProblemAsync(HttpStatusCode.NotFound, "AUTH_MEMBERSHIP_NOT_FOUND");
        await (await underKmsa.AssignAsync(elsscViewer.MembershipId, IdentityRoles.TenantAdmin, Ct)).ShouldBeProblemAsync(HttpStatusCode.NotFound, "AUTH_MEMBERSHIP_NOT_FOUND");
        (await ListAsync(underKmsa, string.Empty)).Items.ShouldHaveSingleItem().MembershipId.ShouldBe(grace.MembershipId);
        using var elsscClient = App.ClientFor(elsscOwner.UserId, elssc);
        var viewer = await (await elsscClient.GetAsync(MemberPath(elsscViewer.MembershipId), Ct)).ReadMemberAsync(Ct);
        viewer.Status.ShouldBe(MembershipStatus.Active);
        viewer.Roles.ShouldHaveSingleItem().RoleCode.ShouldBe(IdentityRoles.Viewer);
    }

    private async Task<(Guid Tenant, Member Owner)> KmsaWithOwnerAsync()
    {
        App.Authorization.UseIdentityRoles();
        var kmsa = await TenantTestData.SaveTenantAsync(App.Services, "kmsa", TenantStatus.Active, Ct);
        return (kmsa, await CreateMemberAsync(App.Services, kmsa, _graceEmail, Ct, TenantWide(IdentityRoles.TenantOwner)));
    }

    private static async Task<ListMembersResponse> ListAsync(HttpClient client, string query)
    {
        using var response = await client.GetAsync($"{MembersPath}{query}", Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<ListMembersResponse>(Json, Ct)).ShouldNotBeNull();
    }
}

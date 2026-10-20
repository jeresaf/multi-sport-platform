using System.Net;
using System.Net.Http.Json;
using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.IntegrationTests.Api;
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
/// The owner rules over the member endpoints: a tenant keeps at least one active <c>TENANT_OWNER</c> (S03 D17,
/// <c>409 AUTH_LAST_OWNER</c>), and only an owner changes an owner or the owner role (S03-T4, <c>403 AUTH_OWNER_REQUIRED</c>).
/// </summary>
public sealed class OwnerRulesTests(PostgresFixture fixture) : DatabaseTest(fixture)
{
    [Fact]
    public async Task The_last_owner_can_not_be_suspended_removed_or_demoted()
    {
        var (kmsa, grace) = await KmsaWithOwnerAsync();
        using var client = App.ClientFor(grace.UserId, kmsa);
        var ownerRole = await OwnerAssignmentAsync(client, grace);

        await (await client.SuspendAsync(grace.MembershipId, Ct)).ShouldBeProblemAsync(HttpStatusCode.Conflict, "AUTH_LAST_OWNER");
        await (await client.RemoveAsync(grace.MembershipId, Ct)).ShouldBeProblemAsync(HttpStatusCode.Conflict, "AUTH_LAST_OWNER");
        await (await client.RevokeAsync(grace.MembershipId, ownerRole, Ct)).ShouldBeProblemAsync(HttpStatusCode.Conflict, "AUTH_LAST_OWNER");

        var member = await (await client.GetAsync(MemberPath(grace.MembershipId), Ct)).ReadMemberAsync(Ct);
        member.Status.ShouldBe(MembershipStatus.Active);
        member.Roles.ShouldHaveSingleItem().AssignmentId.ShouldBe(ownerRole);
    }

    [Fact]
    public async Task A_suspended_owner_does_not_count_as_another_owner()
    {
        var (kmsa, grace) = await KmsaWithOwnerAsync();
        var isaac = await CreateMemberAsync(App.Services, kmsa, "isaac.mugisha@kmsa.example", Ct, TenantWide(IdentityRoles.TenantOwner));
        await ChangeAsync(App.Services, isaac, m => m.Suspend("On leave", SystemActor.Instance, JoinedAt), Ct);
        using var client = App.ClientFor(grace.UserId, kmsa);

        await (await client.RemoveAsync(grace.MembershipId, Ct)).ShouldBeProblemAsync(HttpStatusCode.Conflict, "AUTH_LAST_OWNER");

        // A suspended owner's own changes take no active owner away.
        (await client.RemoveAsync(isaac.MembershipId, Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task With_a_second_active_owner_one_owner_can_go()
    {
        var (kmsa, grace) = await KmsaWithOwnerAsync();
        var isaac = await CreateMemberAsync(App.Services, kmsa, "isaac.mugisha@kmsa.example", Ct, TenantWide(IdentityRoles.TenantOwner));
        using var graceClient = App.ClientFor(grace.UserId, kmsa);
        using var isaacClient = App.ClientFor(isaac.UserId, kmsa);
        var isaacsOwnerRole = await OwnerAssignmentAsync(graceClient, isaac);

        (await graceClient.RevokeAsync(isaac.MembershipId, isaacsOwnerRole, Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await graceClient.AssignAsync(isaac.MembershipId, IdentityRoles.TenantOwner, Ct)).StatusCode.ShouldBe(HttpStatusCode.Created);
        (await isaacClient.SuspendAsync(grace.MembershipId, Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);

        // Isaac is now the only active owner.
        await (await isaacClient.RemoveAsync(isaac.MembershipId, Ct)).ShouldBeProblemAsync(HttpStatusCode.Conflict, "AUTH_LAST_OWNER");
        (await isaacClient.RestoreAsync(grace.MembershipId, Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await isaacClient.RemoveAsync(isaac.MembershipId, Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Two_owners_removing_each_other_at_once_leave_one_owner()
    {
        var (kmsa, grace) = await KmsaWithOwnerAsync();
        var isaac = await CreateMemberAsync(App.Services, kmsa, "isaac.mugisha@kmsa.example", Ct, TenantWide(IdentityRoles.TenantOwner));
        using var graceClient = App.ClientFor(grace.UserId, kmsa);
        using var isaacClient = App.ClientFor(isaac.UserId, kmsa);

        var responses = await Task.WhenAll(graceClient.RemoveAsync(isaac.MembershipId, Ct), isaacClient.RemoveAsync(grace.MembershipId, Ct));

        // One wins; the other is refused as the last owner, or (if the winner committed before it was authorized) as a
        // removed member.
        responses.Count(r => r.StatusCode == HttpStatusCode.OK).ShouldBe(1);
        responses.Single(r => r.StatusCode != HttpStatusCode.OK).StatusCode.ShouldBeOneOf(HttpStatusCode.Conflict, HttpStatusCode.Forbidden);
        var winner = responses[0].StatusCode == HttpStatusCode.OK ? graceClient : isaacClient;
        var members = await (await winner.GetAsync(MembersPath, Ct)).Content.ReadFromJsonAsync<ListMembersResponse>(Json, Ct);
        members.ShouldNotBeNull().Items.ShouldHaveSingleItem().Roles.ShouldHaveSingleItem().RoleCode.ShouldBe(IdentityRoles.TenantOwner);
        foreach (var response in responses)
        {
            response.Dispose();
        }
    }

    [Fact]
    public async Task Only_an_owner_changes_an_owner_or_the_owner_role()
    {
        var (kmsa, grace) = await KmsaWithOwnerAsync();
        var isaac = await CreateMemberAsync(App.Services, kmsa, "isaac.mugisha@kmsa.example", Ct, TenantWide(IdentityRoles.TenantOwner));
        var daniel = await CreateMemberAsync(App.Services, kmsa, "daniel.kato@kmsa.example", Ct, TenantWide(IdentityRoles.TenantAdmin));
        var brian = await CreateMemberAsync(App.Services, kmsa, "brian.ssemakula@kmsa.example", Ct, TenantWide(IdentityRoles.Registrar));
        using var admin = App.ClientFor(daniel.UserId, kmsa);
        using var owner = App.ClientFor(grace.UserId, kmsa);
        var isaacsOwnerRole = await OwnerAssignmentAsync(owner, isaac);

        foreach (var response in new[]
                 {
                     await admin.AssignAsync(brian.MembershipId, IdentityRoles.TenantOwner, Ct),
                     await admin.RevokeAsync(isaac.MembershipId, isaacsOwnerRole, Ct),
                     await admin.SuspendAsync(isaac.MembershipId, Ct),
                     await admin.RemoveAsync(isaac.MembershipId, Ct),
                 })
        {
            await response.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "AUTH_OWNER_REQUIRED");
        }

        // An admin manages every member who is not an owner.
        (await admin.AssignAsync(brian.MembershipId, IdentityRoles.TenantAdmin, Ct)).StatusCode.ShouldBe(HttpStatusCode.Created);
        (await admin.SuspendAsync(brian.MembershipId, Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);

        // An owner does what the admin couldn't.
        (await owner.SuspendAsync(isaac.MembershipId, Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        await (await admin.RestoreAsync(isaac.MembershipId, Ct)).ShouldBeProblemAsync(HttpStatusCode.Forbidden, "AUTH_OWNER_REQUIRED");
        (await owner.RestoreAsync(isaac.MembershipId, Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await owner.AssignAsync(daniel.MembershipId, IdentityRoles.TenantOwner, Ct)).StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    private async Task<(Guid Tenant, Member Owner)> KmsaWithOwnerAsync()
    {
        App.Authorization.UseIdentityRoles();
        var kmsa = await TenantTestData.SaveTenantAsync(App.Services, "kmsa", TenantStatus.Active, Ct);
        return (kmsa, await CreateMemberAsync(App.Services, kmsa, "grace.nakato@kmsa.example", Ct, TenantWide(IdentityRoles.TenantOwner)));
    }

    private static async Task<Guid> OwnerAssignmentAsync(HttpClient client, Member member)
    {
        using var response = await client.GetAsync(MemberPath(member.MembershipId), Ct);
        return (await response.ReadMemberAsync(Ct)).Roles.Single(r => r.RoleCode == IdentityRoles.TenantOwner).AssignmentId;
    }
}

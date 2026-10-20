using System.Net;
using System.Net.Http.Json;
using SportsPlatform.BuildingBlocks.Persistence;
using SportsPlatform.IntegrationTests.Api;
using SportsPlatform.IntegrationTests.Audit;
using SportsPlatform.IntegrationTests.Infrastructure;
using SportsPlatform.IntegrationTests.Tenancy;
using SportsPlatform.Modules.Identity.Domain.Invitations;
using SportsPlatform.Modules.Identity.Domain.PlatformRoles;
using SportsPlatform.Modules.Identity.Domain.Roles;
using SportsPlatform.Modules.Tenancy.Contracts.Tenants;
using static SportsPlatform.IntegrationTests.Identity.InvitationRequests;
using static SportsPlatform.IntegrationTests.Identity.MembershipTestData;

namespace SportsPlatform.IntegrationTests.Identity;

/// <summary>
/// <c>POST /api/platform/v1/tenants/{tenantId}/owner-invitations</c> (<c>tenant.owner.invite</c>, S03-T4c): the platform
/// console invites a new tenant's owner, under the tenant named in the route.
/// </summary>
public sealed class OwnerInvitationTests(PostgresFixture fixture) : DatabaseTest(fixture)
{
    private const string _graceEmail = "grace.nakato@kmsa.example";

    [Fact]
    public async Task A_platform_admin_invites_the_owner_of_a_tenant_being_set_up()
    {
        App.Authorization.UseIdentityRoles();
        var kmsa = await TenantTestData.SaveTenantAsync(App.Services, "kmsa", TenantStatus.Provisioning, Ct);
        var adminId = await AccountTestData.CreateUserAsync(App.Services, "asha.mutesi@msp.example", TestPasswords.New(), Ct, platformRole: PlatformRoles.Admin);
        using var client = App.CreateClient().AsUser(adminId);

        using var response = await client.PostAsJsonAsync(OwnerInvitationsPath(kmsa), new { email = _graceEmail }, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        response.Headers.Location.ShouldBeNull();
        var invitation = await response.ReadInvitationAsync(Ct);
        invitation.ShouldSatisfyAllConditions(
            i => i.RoleCodes.ShouldBe([IdentityRoles.TenantOwner]),
            i => i.Status.ShouldBe(InvitationState.Pending),
            i => i.InvitedBy.ShouldBe(adminId));
        (await QueryAsync(DatabaseRoles.Platform, "SELECT tenant_id FROM identity.invitations", r => r.GetGuid(0))).ShouldBe([kmsa]);

        await AccountTestData.DeliverOutboxAsync(App.Services, Ct);
        App.Emails.Sent.ShouldHaveSingleItem().ShouldSatisfyAllConditions(
            m => m.To.ShouldBe(_graceEmail),
            m => m.Subject.ShouldBe("You're invited to join kmsa Sports"));
        var entry = (await AuditTestData.EntriesAsync(App.Services, Ct)).Single(e => e.ActionCode == "tenant.owner.invite");
        entry.ShouldSatisfyAllConditions(
            e => e.TenantId.ShouldBe(kmsa),
            e => e.ActorId.ShouldBe(adminId),
            e => e.EntityId.ShouldBe(invitation.InvitationId),
            e => e.Metadata.ShouldNotBeNull().ShouldNotContain("@"));
    }

    [Fact]
    public async Task Support_staff_and_tenant_owners_can_not_invite_an_owner_through_the_platform_api()
    {
        App.Authorization.UseIdentityRoles();
        var kmsa = await TenantTestData.SaveTenantAsync(App.Services, "kmsa", TenantStatus.Active, Ct);
        var supportId = await AccountTestData.CreateUserAsync(App.Services, "joel.okello@msp.example", TestPasswords.New(), Ct, platformRole: PlatformRoles.Support);
        var grace = await CreateMemberAsync(App.Services, kmsa, _graceEmail, Ct, TenantWide(IdentityRoles.TenantOwner));
        using var support = App.CreateClient().AsUser(supportId);
        using var owner = App.CreateClient().AsUser(grace.UserId);

        using var bySupport = await support.PostAsJsonAsync(OwnerInvitationsPath(kmsa), new { email = "daniel.kato@kmsa.example" }, Ct);
        using var byOwner = await owner.PostAsJsonAsync(OwnerInvitationsPath(kmsa), new { email = "daniel.kato@kmsa.example" }, Ct);

        await bySupport.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "AUTH_FORBIDDEN");
        await byOwner.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "AUTH_FORBIDDEN");
        (await QueryAsync(DatabaseRoles.Platform, "SELECT count(*) FROM identity.invitations", r => r.GetInt64(0))).ShouldBe([0L]);
    }

    [Fact]
    public async Task An_unknown_tenant_is_not_found_and_a_current_owner_can_not_be_invited_again()
    {
        App.Authorization.UseIdentityRoles();
        var kmsa = await TenantTestData.SaveTenantAsync(App.Services, "kmsa", TenantStatus.Active, Ct);
        await CreateMemberAsync(App.Services, kmsa, _graceEmail, Ct, TenantWide(IdentityRoles.TenantOwner));
        var adminId = await AccountTestData.CreateUserAsync(App.Services, "asha.mutesi@msp.example", TestPasswords.New(), Ct, platformRole: PlatformRoles.Admin);
        using var client = App.CreateClient().AsUser(adminId);

        using var unknown = await client.PostAsJsonAsync(OwnerInvitationsPath(Guid.CreateVersion7()), new { email = _graceEmail }, Ct);
        using var member = await client.PostAsJsonAsync(OwnerInvitationsPath(kmsa), new { email = _graceEmail }, Ct);
        using var noEmail = await client.PostAsJsonAsync(OwnerInvitationsPath(kmsa), new { }, Ct);

        await unknown.ShouldBeProblemAsync(HttpStatusCode.NotFound, "TENANT_NOT_FOUND");
        await member.ShouldBeProblemAsync(HttpStatusCode.Conflict, "AUTH_ALREADY_MEMBER");
        await noEmail.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_INVALID_REQUEST");
    }
}

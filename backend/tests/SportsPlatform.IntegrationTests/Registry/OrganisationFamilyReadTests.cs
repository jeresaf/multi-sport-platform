using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SportsPlatform.IntegrationTests.Identity;
using SportsPlatform.IntegrationTests.Infrastructure;
using SportsPlatform.IntegrationTests.Tenancy;
using SportsPlatform.Modules.Identity.Domain.Roles;
using SportsPlatform.Modules.Registry.Domain.Organisations;
using SportsPlatform.Modules.Tenancy.Contracts.Tenants;
using static SportsPlatform.IntegrationTests.Identity.MembershipTestData;
using static SportsPlatform.IntegrationTests.Registry.OrganisationRequests;

namespace SportsPlatform.IntegrationTests.Registry;

/// <summary>
/// An organisation's parent and children in the reads (S07-T2c-2; D5, D19): the read's current <c>parent</c> (any
/// status) and <c>children</c> (archived ones left out, sorted by display name; owner, plan), following the open links;
/// the directory's <c>parentId</c> (direct children, with the other filters and the cursor); the tenant's own only.
/// </summary>
public sealed class OrganisationFamilyReadTests(PostgresFixture fixture) : DatabaseTest(fixture)
{
    [Fact]
    public async Task An_organisation_without_a_parent_or_children_reads_null_and_an_empty_list()
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var club = await SaveOrganisationAsync(App.Services, kmsa, "Ntinda Swifts SC");

        var read = await registrar.ReadJsonAsync(OrganisationPath(club.Id), Ct);

        read.GetProperty("parent").ValueKind.ShouldBe(JsonValueKind.Null);
        read.GetProperty("children").GetArrayLength().ShouldBe(0);
    }

    // Sorted as the directory sorts (the sort key: case, accents and punctuation aside), so a name typed in lower case
    // isn't put after the others.
    [Fact]
    public async Task The_read_shows_the_current_parent_and_the_children_sorted_by_display_name()
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var association = await SaveOrganisationAsync(App.Services, kmsa, "Kampala Metro Sports Association", OrganisationType.Association);
        var swifts = await SaveOrganisationAsync(App.Services, kmsa, "Ntinda Swifts SC");
        var kestrels = await SaveOrganisationAsync(App.Services, kmsa, "Kisaasi Kestrels SC");
        var breakers = await SaveOrganisationAsync(App.Services, kmsa, "bunga Breakers FC", status: OrganisationStatus.Suspended);
        foreach (var club in new[] { swifts, kestrels, breakers })
        {
            await SaveParentAsync(App.Services, kmsa, club.Id, association.Id);
        }

        var parent = await registrar.ReadJsonAsync(OrganisationPath(association.Id), Ct);
        var child = await registrar.ReadJsonAsync(OrganisationPath(kestrels.Id), Ct);

        parent.GetProperty("parent").ValueKind.ShouldBe(JsonValueKind.Null);
        Children(parent).ShouldBe(
        [
            (breakers.Id, "bunga Breakers FC", breakers.Slug, "SUSPENDED"),
            (kestrels.Id, "Kisaasi Kestrels SC", kestrels.Slug, "ACTIVE"),
            (swifts.Id, "Ntinda Swifts SC", swifts.Slug, "ACTIVE"),
        ]);
        Summary(child.GetProperty("parent")).ShouldBe((association.Id, "Kampala Metro Sports Association", association.Slug, "ACTIVE"));
        child.GetProperty("children").GetArrayLength().ShouldBe(0);
    }

    // Owner, plan: archived children are left out of the read (the directory's parentId with status=ARCHIVED finds
    // them); a parent is shown whatever its status.
    [Fact]
    public async Task Archived_children_are_left_out_and_a_parent_shows_whatever_its_status()
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var association = await SaveOrganisationAsync(App.Services, kmsa, "Kampala Metro Sports Association", OrganisationType.Association);
        var swifts = await SaveOrganisationAsync(App.Services, kmsa, "Ntinda Swifts SC");
        var sparrows = await SaveOrganisationAsync(App.Services, kmsa, "Seeta Sparrows FC");
        await SaveParentAsync(App.Services, kmsa, swifts.Id, association.Id);
        await SaveParentAsync(App.Services, kmsa, sparrows.Id, association.Id);
        await ChangeStatusAsync(registrar, sparrows.Id, "ARCHIVED");
        await ChangeStatusAsync(registrar, association.Id, "ARCHIVED");

        var parent = await registrar.ReadJsonAsync(OrganisationPath(association.Id), Ct);
        var current = await registrar.ReadJsonAsync($"{OrganisationsPath}?parentId={association.Id}", Ct);
        var archived = await registrar.ReadJsonAsync($"{OrganisationsPath}?parentId={association.Id}&status=ARCHIVED", Ct);

        Children(parent).Select(c => c.Id).ShouldBe([swifts.Id]);
        current.OrganisationNames().ShouldBe(["Ntinda Swifts SC"]);
        archived.OrganisationNames().ShouldBe(["Seeta Sparrows FC"]);
        (await registrar.ReadJsonAsync(OrganisationPath(swifts.Id), Ct)).GetProperty("parent").GetProperty("status").GetString()
            .ShouldBe("ARCHIVED");
    }

    [Fact]
    public async Task A_new_parent_moves_the_child_from_one_read_to_the_other_and_clearing_removes_it()
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var association = await SaveOrganisationAsync(App.Services, kmsa, "Kampala Metro Sports Association", OrganisationType.Association);
        var kestrels = await SaveOrganisationAsync(App.Services, kmsa, "Kisaasi Kestrels SC");
        var swifts = await SaveOrganisationAsync(App.Services, kmsa, "Ntinda Swifts SC");
        await SaveParentAsync(App.Services, kmsa, swifts.Id, association.Id);

        // The parent's endpoint answers the organisation with the new fields.
        var moved = await SetParentAsync(registrar, swifts.Id, kestrels.Id);

        moved.GetProperty("parent").GetProperty("id").GetGuid().ShouldBe(kestrels.Id);
        Children(await registrar.ReadJsonAsync(OrganisationPath(association.Id), Ct)).ShouldBeEmpty();
        Children(await registrar.ReadJsonAsync(OrganisationPath(kestrels.Id), Ct)).Select(c => c.Id).ShouldBe([swifts.Id]);

        var cleared = await SetParentAsync(registrar, swifts.Id, null);

        cleared.GetProperty("parent").ValueKind.ShouldBe(JsonValueKind.Null);
        Children(await registrar.ReadJsonAsync(OrganisationPath(kestrels.Id), Ct)).ShouldBeEmpty();
    }

    // Direct children only: a grandchild isn't listed under the association, and an ended link keeps nothing.
    [Fact]
    public async Task ParentId_lists_the_direct_children_only()
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var association = await SaveOrganisationAsync(App.Services, kmsa, "Kampala Metro Sports Association", OrganisationType.Association);
        var swifts = await SaveOrganisationAsync(App.Services, kmsa, "Ntinda Swifts SC");
        var rugby = await SaveOrganisationAsync(App.Services, kmsa, "Ntinda Swifts RFC");
        var breakers = await SaveOrganisationAsync(App.Services, kmsa, "Bunga Breakers FC");
        await SaveOrganisationAsync(App.Services, kmsa, "Kisaasi Kestrels SC");
        await SaveParentAsync(App.Services, kmsa, swifts.Id, association.Id);
        await SaveParentAsync(App.Services, kmsa, rugby.Id, swifts.Id);
        await SaveParentAsync(App.Services, kmsa, breakers.Id, association.Id);
        await SaveParentAsync(App.Services, kmsa, breakers.Id, null);

        (await registrar.ReadJsonAsync($"{OrganisationsPath}?parentId={association.Id}", Ct)).OrganisationNames()
            .ShouldBe(["Ntinda Swifts SC"]);
        (await registrar.ReadJsonAsync($"{OrganisationsPath}?parentId={swifts.Id}", Ct)).OrganisationNames()
            .ShouldBe(["Ntinda Swifts RFC"]);
    }

    [Fact]
    public async Task ParentId_combines_with_the_other_filters_and_pages_by_cursor()
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var association = await SaveOrganisationAsync(App.Services, kmsa, "Kampala Metro Sports Association", OrganisationType.Association);
        foreach (var (name, type) in new[]
                 {
                     ("Ntinda Swifts SC", OrganisationType.Club),
                     ("Kisaasi Kestrels SC", OrganisationType.Club),
                     ("Bunga Breakers FC", OrganisationType.Club),
                     ("Kansanga Harriers FC", OrganisationType.Academy),
                 })
        {
            var child = await SaveOrganisationAsync(App.Services, kmsa, name, type);
            await SaveParentAsync(App.Services, kmsa, child.Id, association.Id);
        }

        await SaveOrganisationAsync(App.Services, kmsa, "Kyebando Ravens FC");

        var byParent = $"{OrganisationsPath}?parentId={association.Id}";
        (await registrar.ReadJsonAsync($"{byParent}&type=CLUB&q=k", Ct)).OrganisationNames().ShouldBe(["Kisaasi Kestrels SC"]);

        var first = await registrar.ReadJsonAsync($"{byParent}&type=CLUB&limit=2", Ct);
        var cursor = first.GetProperty("page").GetProperty("nextCursor").GetString();
        var second = await registrar.ReadJsonAsync($"{byParent}&type=CLUB&limit=2&cursor={Uri.EscapeDataString(cursor!)}", Ct);

        first.OrganisationNames().ShouldBe(["Bunga Breakers FC", "Kisaasi Kestrels SC"]);
        second.OrganisationNames().ShouldBe(["Ntinda Swifts SC"]);
        second.GetProperty("page").GetProperty("hasMore").GetBoolean().ShouldBeFalse();
    }

    // An unknown ID, or another tenant's organisation with children, keeps none (a filter, not a lookup: no 404).
    [Fact]
    public async Task An_unknown_parentId_or_another_tenants_keeps_none()
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var (elssc, _) = await TenantAsync("elssc", IdentityRoles.Registrar);
        var council = await SaveOrganisationAsync(App.Services, elssc, "Eastern Lakes Schools Sports Council", OrganisationType.Association);
        var school = await SaveOrganisationAsync(App.Services, elssc, "Lakeshore High School", OrganisationType.School);
        await SaveParentAsync(App.Services, elssc, school.Id, council.Id);
        await SaveOrganisationAsync(App.Services, kmsa, "Ntinda Swifts SC");

        (await registrar.ReadJsonAsync($"{OrganisationsPath}?parentId={council.Id}", Ct)).OrganisationNames().ShouldBeEmpty();
        (await registrar.ReadJsonAsync($"{OrganisationsPath}?parentId={Guid.CreateVersion7()}", Ct)).OrganisationNames().ShouldBeEmpty();
    }

    private static List<(Guid Id, string? DisplayName, string? Slug, string? Status)> Children(JsonElement organisation) =>
        [.. organisation.GetProperty("children").EnumerateArray().Select(Summary)];

    private static (Guid, string?, string?, string?) Summary(JsonElement summary) => (
        summary.GetProperty("id").GetGuid(),
        summary.GetProperty("displayName").GetString(),
        summary.GetProperty("slug").GetString(),
        summary.GetProperty("status").GetString());

    private static async Task<JsonElement> SetParentAsync(HttpClient client, Guid organisationId, Guid? parentId)
    {
        using var response = await client.PostAsJsonAsync(
            OrganisationParentPath(organisationId), new { parentId, reason = "Affiliated for the season" }, Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));
        return await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
    }

    private static async Task ChangeStatusAsync(HttpClient client, Guid organisationId, string status)
    {
        using var response = await client.PostAsJsonAsync(
            OrganisationStatusPath(organisationId), new { status, reason = "No longer runs competitions" }, Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));
    }

    /// <summary>An active tenant and a client of a member with <paramref name="role"/> tenant-wide.</summary>
    private async Task<(Guid Tenant, HttpClient Client)> TenantAsync(string slug, string role)
    {
        App.Authorization.UseIdentityRoles();
        var tenant = await TenantTestData.SaveTenantAsync(App.Services, slug, TenantStatus.Active, Ct);
        var member = await CreateMemberAsync(App.Services, tenant, MailboxOf(role, $"{slug}.example"), Ct, TenantWide(role));
        return (tenant, App.ClientFor(member.UserId, tenant));
    }
}

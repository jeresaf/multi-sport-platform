using System.Net.Http.Json;
using System.Text.Json;
using SportsPlatform.IntegrationTests.Identity;
using SportsPlatform.IntegrationTests.Infrastructure;
using SportsPlatform.IntegrationTests.Tenancy;
using SportsPlatform.Modules.Identity.Domain.Roles;
using SportsPlatform.Modules.Registry.Domain.Teams;
using SportsPlatform.Modules.Tenancy.Contracts.Tenants;
using static SportsPlatform.IntegrationTests.Identity.MembershipTestData;
using static SportsPlatform.IntegrationTests.Registry.OrganisationRequests;
using static SportsPlatform.IntegrationTests.Registry.TeamRequests;
using static SportsPlatform.IntegrationTests.Tenancy.TestSportDefinitions;

namespace SportsPlatform.IntegrationTests.Registry;

/// <summary>
/// An organisation's teams in the reads (S07-T3b-2; D19): every team of the organisation but archived ones, in the teams
/// directory's order (name, category, ID; owner, plan 2026-11-24), not paged; another organisation's and another
/// tenant's teams left out; the same on every endpoint that answers the organisation.
/// </summary>
public sealed class OrganisationTeamsReadTests(PostgresFixture fixture) : DatabaseTest(fixture)
{
    [Fact]
    public async Task An_organisation_without_teams_reads_an_empty_list()
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var club = await SaveOrganisationAsync(App.Services, kmsa, "Ntinda Swifts SC");

        (await registrar.ReadJsonAsync(OrganisationPath(club.Id), Ct)).GetProperty("teams").GetArrayLength().ShouldBe(0);
    }

    // S07 Demo: Ntinda Swifts SC shows its Senior Men, Women and Under-17 teams (and, in tests, a second sport's).
    [Fact]
    public async Task The_read_shows_the_organisations_teams_in_the_directorys_order_without_archived_ones()
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var swifts = await SaveOrganisationAsync(App.Services, kmsa, "Ntinda Swifts SC");
        var kestrels = await SaveOrganisationAsync(App.Services, kmsa, "Kisaasi Kestrels SC");
        var women = new TeamCategory(GenderCategory.Women, AgeGroup.Senior, TeamLevel.First);
        var under17 = new TeamCategory(GenderCategory.Men, AgeGroup.U17, TeamLevel.First);
        var youth = await SaveTeamAsync(App.Services, kmsa, "Ntinda Swifts FC", category: under17, organisationId: swifts.Id);
        var men = await SaveTeamAsync(App.Services, kmsa, "Ntinda Swifts FC", organisationId: swifts.Id);
        var swiftsWomen = await SaveTeamAsync(App.Services, kmsa, "Ntinda Swifts FC", category: women, organisationId: swifts.Id, status: TeamStatus.Disbanded);
        var hoop = await SaveTeamAsync(App.Services, kmsa, "Ntinda Swifts BC", variant: Testhoop, organisationId: swifts.Id, status: TeamStatus.Suspended);
        await SaveTeamAsync(App.Services, kmsa, "Ntinda Swifts Veterans FC", organisationId: swifts.Id, status: TeamStatus.Archived);
        await SaveTeamAsync(App.Services, kmsa, "Kisaasi Kestrels FC", organisationId: kestrels.Id);
        await SaveTeamAsync(App.Services, kmsa, "Ntinda Rangers FC");

        var read = await registrar.ReadJsonAsync(OrganisationPath(swifts.Id), Ct);

        Teams(read).ShouldBe(
        [
            (hoop.Id, "Ntinda Swifts BC", hoop.Slug, Testhoop, "MEN", "SENIOR", "FIRST", "SUSPENDED"),
            (men.Id, "Ntinda Swifts FC", men.Slug, TestballFull, "MEN", "SENIOR", "FIRST", "ACTIVE"),
            (swiftsWomen.Id, "Ntinda Swifts FC", swiftsWomen.Slug, TestballFull, "WOMEN", "SENIOR", "FIRST", "DISBANDED"),
            (youth.Id, "Ntinda Swifts FC", youth.Slug, TestballFull, "MEN", "U17", "FIRST", "ACTIVE"),
        ]);
        read.GetProperty("teams")[0].EnumerateObject().Select(property => property.Name).ShouldBe(
            ["id", "name", "slug", "variant", "genderCategory", "ageGroup", "level", "status"]);
    }

    [Fact]
    public async Task A_status_change_answers_the_organisation_with_its_teams()
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var club = await SaveOrganisationAsync(App.Services, kmsa, "Ntinda Swifts SC");
        var team = await SaveTeamAsync(App.Services, kmsa, "Ntinda Swifts FC", organisationId: club.Id);

        using var response = await registrar.PostAsJsonAsync(
            OrganisationStatusPath(club.Id), new { status = "SUSPENDED", reason = "Affiliation fees unpaid" }, Ct);
        response.EnsureSuccessStatusCode();
        var answered = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);

        Teams(answered).Select(t => t.Id).ShouldBe([team.Id]);
    }

    [Fact]
    public async Task Another_tenants_teams_never_show()
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var (elssc, _) = await TenantAsync("elssc", IdentityRoles.TenantOwner);
        var club = await SaveOrganisationAsync(App.Services, kmsa, "Ntinda Swifts SC");
        await SaveTeamAsync(App.Services, elssc, "Lakeshore High School FC");

        (await registrar.ReadJsonAsync(OrganisationPath(club.Id), Ct)).GetProperty("teams").GetArrayLength().ShouldBe(0);
    }

    private static List<(Guid Id, string?, string?, string?, string?, string?, string?, string?)> Teams(JsonElement organisation) =>
    [
        .. organisation.GetProperty("teams").EnumerateArray().Select(team => (
            team.GetProperty("id").GetGuid(), team.GetProperty("name").GetString(), team.GetProperty("slug").GetString(),
            team.GetProperty("variant").GetString(), team.GetProperty("genderCategory").GetString(),
            team.GetProperty("ageGroup").GetString(), team.GetProperty("level").GetString(), team.GetProperty("status").GetString())),
    ];

    /// <summary>An active tenant and a client of a member with <paramref name="role"/> tenant-wide.</summary>
    private async Task<(Guid Tenant, HttpClient Client)> TenantAsync(string slug, string role)
    {
        App.Authorization.UseIdentityRoles();
        var tenant = await TenantTestData.SaveTenantAsync(App.Services, slug, TenantStatus.Active, Ct);
        var member = await CreateMemberAsync(App.Services, tenant, MailboxOf(role, $"{slug}.example"), Ct, TenantWide(role));
        return (tenant, App.ClientFor(member.UserId, tenant));
    }
}

using System.Net;
using System.Text.Json;
using SportsPlatform.IntegrationTests.Api;
using SportsPlatform.IntegrationTests.Identity;
using SportsPlatform.IntegrationTests.Infrastructure;
using SportsPlatform.IntegrationTests.Tenancy;
using SportsPlatform.Modules.Identity.Domain.Roles;
using SportsPlatform.Modules.Registry.Domain.Organisations;
using SportsPlatform.Modules.Registry.Domain.Teams;
using SportsPlatform.Modules.Tenancy.Contracts.Tenants;
using static SportsPlatform.IntegrationTests.Identity.MembershipTestData;
using static SportsPlatform.IntegrationTests.Registry.OrganisationRequests;
using static SportsPlatform.IntegrationTests.Registry.PeopleRequests;
using static SportsPlatform.IntegrationTests.Registry.TeamRequests;
using static SportsPlatform.IntegrationTests.Tenancy.TestSportDefinitions;

namespace SportsPlatform.IntegrationTests.Registry;

/// <summary>
/// The team directory (S07-T3b-2; D19): sorted by name (the sort key), then category (age group, gender, level; owner,
/// plan 2026-11-24), then ID, paged by a cursor that carries the category rank; filtered by variant, organisation,
/// category parts and status; searched by the name and the short name; the roles (D20), the read-only tenant and the
/// tenant's own teams only.
/// </summary>
public sealed class TeamDirectoryTests(PostgresFixture fixture) : DatabaseTest(fixture)
{
    [Fact]
    public async Task The_directory_is_sorted_by_name_ignoring_case_accents_and_punctuation_and_leaves_archived_ones_out()
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        foreach (var name in new[] { "Seeta Sparrows FC", "bukoto Comets FC", "Kisaasi Kestrels SC" })
        {
            await SaveTeamAsync(App.Services, kmsa, name);
        }

        await SaveTeamAsync(App.Services, kmsa, "Énsibuko Eagles FC");
        await SaveTeamAsync(App.Services, kmsa, "Ggaba-Gulls RFC");
        await SaveTeamAsync(App.Services, kmsa, "Kireka Kingfishers FC", status: TeamStatus.Archived);
        await SaveTeamAsync(App.Services, kmsa, "Mutungo Marlins RFC", status: TeamStatus.Disbanded);

        var page = await registrar.ReadJsonAsync(TeamsPath, Ct);

        TeamNames(page).ShouldBe([
            "bukoto Comets FC", "Énsibuko Eagles FC", "Ggaba-Gulls RFC", "Kisaasi Kestrels SC", "Mutungo Marlins RFC",
            "Seeta Sparrows FC",
        ]);
        page.GetProperty("page").GetProperty("hasMore").GetBoolean().ShouldBeFalse();
        page.GetProperty("page").GetProperty("nextCursor").ValueKind.ShouldBe(JsonValueKind.Null);
        TeamNames(await registrar.ReadJsonAsync($"{TeamsPath}?status=ARCHIVED", Ct)).ShouldBe(["Kireka Kingfishers FC"]);
        TeamNames(await registrar.ReadJsonAsync($"{TeamsPath}?status=DISBANDED", Ct)).ShouldBe(["Mutungo Marlins RFC"]);
    }

    // The codes as text would put U17 before U6 and MEN before WOMEN only by chance: the rank is the owner's order.
    [Fact]
    public async Task Teams_with_one_name_are_sorted_by_age_group_then_gender_then_level()
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var categories = new[]
        {
            new TeamCategory(GenderCategory.Men, AgeGroup.Veterans, TeamLevel.First),
            new TeamCategory(GenderCategory.Women, AgeGroup.U17, TeamLevel.First),
            new TeamCategory(GenderCategory.Men, AgeGroup.U17, TeamLevel.First),
            new TeamCategory(GenderCategory.Men, AgeGroup.U6, TeamLevel.First),
            new TeamCategory(GenderCategory.Mixed, AgeGroup.Senior, TeamLevel.First),
            new TeamCategory(GenderCategory.Women, AgeGroup.Senior, TeamLevel.First),
            new TeamCategory(GenderCategory.Men, AgeGroup.Senior, TeamLevel.Academy),
            new TeamCategory(GenderCategory.Men, AgeGroup.Senior, TeamLevel.Reserve),
            new TeamCategory(GenderCategory.Men, AgeGroup.Senior, TeamLevel.First),
        };
        foreach (var category in categories)
        {
            await SaveTeamAsync(App.Services, kmsa, "Ntinda Swifts FC", category: category);
        }

        var page = await registrar.ReadJsonAsync(TeamsPath, Ct);

        Categories(page).ShouldBe([
            ("SENIOR", "MEN", "FIRST"), ("SENIOR", "MEN", "RESERVE"), ("SENIOR", "MEN", "ACADEMY"), ("SENIOR", "WOMEN", "FIRST"),
            ("SENIOR", "MIXED", "FIRST"), ("U6", "MEN", "FIRST"), ("U17", "MEN", "FIRST"), ("U17", "WOMEN", "FIRST"),
            ("VETERANS", "MEN", "FIRST"),
        ]);
    }

    [Fact]
    public async Task An_item_carries_what_recognises_the_team_and_its_organisation()
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var club = await SaveOrganisationAsync(App.Services, kmsa, "Ntinda Swifts SC", status: OrganisationStatus.Dissolved);
        var team = await SaveTeamAsync(App.Services, kmsa, "Ntinda Swifts FC", organisationId: club.Id, shortName: "Swifts");
        await SaveTeamAsync(App.Services, kmsa, "Kisaasi Kestrels FC");

        var items = (await registrar.ReadJsonAsync(TeamsPath, Ct)).GetProperty("items").EnumerateArray().ToList();

        items[0].EnumerateObject().Select(property => property.Name).ShouldBe(
            ["id", "name", "shortName", "slug", "variant", "genderCategory", "ageGroup", "level", "organisation", "status"]);
        items[0].GetProperty("organisation").ValueKind.ShouldBe(JsonValueKind.Null);
        var item = items[1];
        (item.GetProperty("id").GetGuid(), item.GetProperty("shortName").GetString(), item.GetProperty("slug").GetString(),
            item.GetProperty("variant").GetString(), item.GetProperty("genderCategory").GetString(),
            item.GetProperty("ageGroup").GetString(), item.GetProperty("level").GetString(), item.GetProperty("status").GetString())
            .ShouldBe((team.Id, "Swifts", team.Slug, TestballFull, "MEN", "SENIOR", "FIRST", "ACTIVE"));
        var organisation = item.GetProperty("organisation");
        organisation.EnumerateObject().Select(property => property.Name).ShouldBe(["id", "displayName", "slug"]);
        (organisation.GetProperty("id").GetGuid(), organisation.GetProperty("displayName").GetString(), organisation.GetProperty("slug").GetString())
            .ShouldBe((club.Id, "Ntinda Swifts SC", club.Slug));
    }

    // Teams of one club usually share a name: the cursor must carry the category rank, then the ID, to page them.
    [Fact]
    public async Task The_directory_pages_by_cursor_through_teams_that_share_a_name_and_a_category()
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var women = new TeamCategory(GenderCategory.Women, AgeGroup.Senior, TeamLevel.First);
        var under17 = new TeamCategory(GenderCategory.Men, AgeGroup.U17, TeamLevel.First);
        var expected = new List<Guid>
        {
            (await SaveTeamAsync(App.Services, kmsa, "Bukoto Comets FC")).Id,
        };
        var tiedMen = new List<Guid>();
        foreach (var name in new[] { "Ntinda Swifts FC", "NTINDA SWIFTS FC", "Ntinda Swifts F.C" })
        {
            tiedMen.Add((await SaveTeamAsync(App.Services, kmsa, name)).Id);
        }

        expected.AddRange(tiedMen.Order());
        expected.Add((await SaveTeamAsync(App.Services, kmsa, "Ntinda Swifts FC", category: women)).Id);
        expected.Add((await SaveTeamAsync(App.Services, kmsa, "Ntinda Swifts FC", category: under17)).Id);
        expected.Add((await SaveTeamAsync(App.Services, kmsa, "Seeta Sparrows FC")).Id);

        var ids = new List<Guid>();
        string? cursor = null;
        var pages = 0;
        do
        {
            var page = await registrar.ReadJsonAsync($"{TeamsPath}?limit=2{(cursor is null ? "" : $"&cursor={cursor}")}", Ct);
            ids.AddRange(page.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetGuid()));
            cursor = page.GetProperty("page").GetProperty("nextCursor").GetString();
            page.GetProperty("page").GetProperty("hasMore").GetBoolean().ShouldBe(cursor is not null);
            pages++;
        }
        while (cursor is not null);

        pages.ShouldBe(4);
        ids.ShouldBe(expected);
    }

    [Fact]
    public async Task The_filters_keep_the_teams_that_have_them_and_combine()
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var swifts = await SaveOrganisationAsync(App.Services, kmsa, "Ntinda Swifts SC");
        var kestrels = await SaveOrganisationAsync(App.Services, kmsa, "Kisaasi Kestrels SC");
        var women = new TeamCategory(GenderCategory.Women, AgeGroup.Senior, TeamLevel.First);
        var under17 = new TeamCategory(GenderCategory.Men, AgeGroup.U17, TeamLevel.First);
        var reserve = new TeamCategory(GenderCategory.Men, AgeGroup.Senior, TeamLevel.Reserve);
        var men = await SaveTeamAsync(App.Services, kmsa, "Ntinda Swifts FC", organisationId: swifts.Id);
        var swiftsWomen = await SaveTeamAsync(App.Services, kmsa, "Ntinda Swifts FC", category: women, organisationId: swifts.Id);
        var youth = await SaveTeamAsync(App.Services, kmsa, "Ntinda Swifts FC", category: under17, organisationId: swifts.Id, status: TeamStatus.Suspended);
        var hoop = await SaveTeamAsync(App.Services, kmsa, "Ntinda Swifts BC", variant: Testhoop, organisationId: swifts.Id);
        var kestrelsReserve = await SaveTeamAsync(App.Services, kmsa, "Kisaasi Kestrels FC", category: reserve, organisationId: kestrels.Id);
        var sevens = await SaveTeamAsync(App.Services, kmsa, "Seeta Sparrows FC", variant: TestballShort, category: women);

        async Task<List<Guid>> IdsAsync(string query) =>
            [.. (await registrar.ReadJsonAsync($"{TeamsPath}?{query}", Ct)).GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetGuid())];

        (await IdsAsync($"variant={TestballFull}")).ShouldBe([kestrelsReserve.Id, men.Id, swiftsWomen.Id, youth.Id]);
        (await IdsAsync($"variant={Testhoop}")).ShouldBe([hoop.Id]);
        (await IdsAsync("variant=NETBALL_FULL")).ShouldBeEmpty();
        (await IdsAsync($"organisationId={swifts.Id}")).ShouldBe([hoop.Id, men.Id, swiftsWomen.Id, youth.Id]);
        (await IdsAsync($"organisationId={Guid.CreateVersion7()}")).ShouldBeEmpty();
        (await IdsAsync("genderCategory=WOMEN")).ShouldBe([swiftsWomen.Id, sevens.Id]);
        (await IdsAsync("ageGroup=U17")).ShouldBe([youth.Id]);
        (await IdsAsync("level=RESERVE")).ShouldBe([kestrelsReserve.Id]);
        (await IdsAsync("status=SUSPENDED")).ShouldBe([youth.Id]);
        (await IdsAsync($"organisationId={swifts.Id}&variant={TestballFull}&genderCategory=MEN")).ShouldBe([men.Id, youth.Id]);
        (await IdsAsync($"organisationId={swifts.Id}&genderCategory=MEN&status=SUSPENDED")).ShouldBe([youth.Id]);
        (await IdsAsync($"organisationId={kestrels.Id}&level=FIRST")).ShouldBeEmpty();
    }

    [Theory]
    [InlineData("ntin", "Ntinda Swifts FC")] // a prefix of the name
    [InlineData("SPARROWS", "Seeta Sparrows FC")] // a word of it
    [InlineData("Sééta spa", "Seeta Sparrows FC")] // accents and case aside
    [InlineData("FC Comets Bukoto", "Bukoto Comets FC")] // the name key: word order aside
    [InlineData("Bukotto Commets", "Bukoto Comets FC")] // similarity
    [InlineData("nsf", "Ntinda Swifts FC")] // a prefix of the short name's key
    public async Task Search_matches_the_name_by_prefix_or_similarity_and_the_short_name_by_prefix(string q, string expected)
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        await SaveTeamAsync(App.Services, kmsa, "Bukoto Comets FC");
        await SaveTeamAsync(App.Services, kmsa, "Ntinda Swifts FC", shortName: "NSFC");
        await SaveTeamAsync(App.Services, kmsa, "Seeta Sparrows FC");
        await SaveTeamAsync(App.Services, kmsa, "Naguru Nightjars Club");

        TeamNames(await registrar.ReadJsonAsync($"{TeamsPath}?q={Uri.EscapeDataString(q)}", Ct)).ShouldBe([expected]);
    }

    [Fact]
    public async Task Search_combines_with_the_filters_and_the_default_status()
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var women = new TeamCategory(GenderCategory.Women, AgeGroup.Senior, TeamLevel.First);
        await SaveTeamAsync(App.Services, kmsa, "Lakeshore Rovers FC");
        await SaveTeamAsync(App.Services, kmsa, "Lakeshore Rovers FC", category: women);
        await SaveTeamAsync(App.Services, kmsa, "Lakeshore Stars FC", status: TeamStatus.Archived);

        TeamNames(await registrar.ReadJsonAsync($"{TeamsPath}?q=Lakeshore", Ct)).ShouldBe(["Lakeshore Rovers FC", "Lakeshore Rovers FC"]);
        Categories(await registrar.ReadJsonAsync($"{TeamsPath}?q=Lakeshore&genderCategory=WOMEN", Ct)).ShouldBe([("SENIOR", "WOMEN", "FIRST")]);
        TeamNames(await registrar.ReadJsonAsync($"{TeamsPath}?q=Lakeshore&status=ARCHIVED", Ct)).ShouldBe(["Lakeshore Stars FC"]);
    }

    [Theory]
    [InlineData("Entebbe")]
    [InlineData("--")] // no letter or digit: nothing until identifiers join (T3d)
    [InlineData("arrows")] // inside a word
    public async Task Search_without_a_match_answers_an_empty_page(string q)
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        await SaveTeamAsync(App.Services, kmsa, "Seeta Sparrows FC");

        var page = await registrar.ReadJsonAsync($"{TeamsPath}?q={Uri.EscapeDataString(q)}", Ct);

        TeamNames(page).ShouldBeEmpty();
        page.GetProperty("page").GetProperty("hasMore").GetBoolean().ShouldBeFalse();
    }

    [Theory]
    [InlineData("limit=0", "limit")]
    [InlineData("limit=101", "limit")]
    [InlineData("limit=two", "limit")]
    [InlineData("status=DEFUNCT", "status")]
    [InlineData("status=active", "status")]
    [InlineData("genderCategory=BOYS", "genderCategory")]
    [InlineData("ageGroup=U5", "ageGroup")]
    [InlineData("ageGroup=senior", "ageGroup")]
    [InlineData("level=SECOND", "level")]
    [InlineData("variant=testball%20full", "variant")]
    [InlineData("cursor=not-a-cursor", "cursor")]
    [InlineData("organisationId=not-an-id", "organisationId")]
    [InlineData("organisationId=0193c7a0f4e27b3c9d1e2f3a4b5c6d7e", "organisationId")]
    [InlineData("q=Ntinda%00", "q")]
    public async Task A_bad_query_value_is_refused_naming_its_parameter(string query, string field)
    {
        var (_, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);

        using var response = await registrar.GetAsync($"{TeamsPath}?{query}", Ct);

        var problem = await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_INVALID_QUERY");
        Fields(problem).ShouldBe([(field, "VALIDATION_INVALID_QUERY")]);
    }

    // Another directory's cursor (sort key and ID only) isn't this one's.
    [Fact]
    public async Task An_organisation_directory_cursor_is_refused()
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        await SaveOrganisationAsync(App.Services, kmsa, "Ntinda Swifts SC");
        await SaveOrganisationAsync(App.Services, kmsa, "Seeta Sparrows SC");
        var cursor = (await registrar.ReadJsonAsync($"{OrganisationsPath}?limit=1", Ct))
            .GetProperty("page").GetProperty("nextCursor").GetString();

        using var response = await registrar.GetAsync($"{TeamsPath}?cursor={cursor}", Ct);

        var problem = await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_INVALID_QUERY");
        Fields(problem).ShouldBe([("cursor", "VALIDATION_INVALID_QUERY")]);
    }

    [Fact]
    public async Task A_tenant_lists_only_its_own_teams()
    {
        var (kmsa, kmsaRegistrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var (elssc, elsscOwner) = await TenantAsync("elssc", IdentityRoles.TenantOwner);
        var kmsaClub = await SaveOrganisationAsync(App.Services, kmsa, "Ntinda Swifts SC");
        await SaveTeamAsync(App.Services, kmsa, "Ntinda Swifts FC", organisationId: kmsaClub.Id);
        await SaveTeamAsync(App.Services, elssc, "Lakeshore High School FC");

        TeamNames(await kmsaRegistrar.ReadJsonAsync(TeamsPath, Ct)).ShouldBe(["Ntinda Swifts FC"]);
        TeamNames(await elsscOwner.ReadJsonAsync(TeamsPath, Ct)).ShouldBe(["Lakeshore High School FC"]);
        TeamNames(await elsscOwner.ReadJsonAsync($"{TeamsPath}?q=Ntinda", Ct)).ShouldBeEmpty();
        TeamNames(await elsscOwner.ReadJsonAsync($"{TeamsPath}?organisationId={kmsaClub.Id}", Ct)).ShouldBeEmpty();
    }

    // D20: every read role lists teams; the scorer gets none in S07, nor does a team manager (S08's TEAM scope).
    [Theory]
    [InlineData(IdentityRoles.TenantOwner, true)]
    [InlineData(IdentityRoles.TenantAdmin, true)]
    [InlineData(IdentityRoles.Registrar, true)]
    [InlineData(IdentityRoles.CompetitionManager, true)]
    [InlineData(IdentityRoles.ResultsOfficer, true)]
    [InlineData(IdentityRoles.DataEntry, true)]
    [InlineData(IdentityRoles.Viewer, true)]
    [InlineData(IdentityRoles.Scorer, false)]
    public async Task Every_read_role_lists_teams(string role, bool reads)
    {
        var (_, client) = await TenantAsync("kmsa", role);

        using var response = await client.GetAsync(TeamsPath, Ct);

        if (reads)
        {
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
        }
        else
        {
            await response.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "AUTH_FORBIDDEN");
        }
    }

    [Fact]
    public async Task A_read_only_tenant_lists_its_teams()
    {
        App.Authorization.UseIdentityRoles();
        var kmsa = await TenantTestData.SaveTenantAsync(
            App.Services, "kmsa", TenantStatus.Active, publicEnabled: false, withDomain: true, withSubscription: false, Ct);
        await TenantTestData.SavePeriodAsync(App.Services, kmsa, new DateOnly(2025, 1, 1), new DateOnly(2025, 12, 31), Ct, cancelReason: "Pilot paused");
        var member = await CreateMemberAsync(App.Services, kmsa, "brian.ssemakula@kmsa.example", Ct, TenantWide(IdentityRoles.Registrar));
        await SaveTeamAsync(App.Services, kmsa, "Ntinda Swifts FC");

        TeamNames(await App.ClientFor(member.UserId, kmsa).ReadJsonAsync(TeamsPath, Ct)).ShouldBe(["Ntinda Swifts FC"]);
    }

    private static List<string?> TeamNames(JsonElement page) =>
        [.. page.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("name").GetString())];

    private static List<(string?, string?, string?)> Categories(JsonElement page) =>
    [
        .. page.GetProperty("items").EnumerateArray().Select(item => (
            item.GetProperty("ageGroup").GetString(), item.GetProperty("genderCategory").GetString(), item.GetProperty("level").GetString())),
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

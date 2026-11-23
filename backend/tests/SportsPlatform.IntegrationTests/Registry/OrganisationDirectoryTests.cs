using System.Net;
using System.Text.Json;
using SportsPlatform.IntegrationTests.Api;
using SportsPlatform.IntegrationTests.Identity;
using SportsPlatform.IntegrationTests.Infrastructure;
using SportsPlatform.IntegrationTests.Tenancy;
using SportsPlatform.Modules.Identity.Domain.Roles;
using SportsPlatform.Modules.Registry.Domain.Organisations;
using SportsPlatform.Modules.Tenancy.Contracts.Tenants;
using static SportsPlatform.IntegrationTests.Identity.MembershipTestData;
using static SportsPlatform.IntegrationTests.Registry.OrganisationRequests;
using static SportsPlatform.IntegrationTests.Registry.PeopleRequests;

namespace SportsPlatform.IntegrationTests.Registry;

/// <summary>
/// The organisation directory (S07-T2b-2; D19): sorted by sort key (the normalised display name, word order kept) and
/// ID, paged by cursor, filtered by type and status, searched by the display, legal and short names; the roles (D20),
/// the read-only tenant and the tenant's own organisations only.
/// </summary>
public sealed class OrganisationDirectoryTests(PostgresFixture fixture) : DatabaseTest(fixture)
{
    [Fact]
    public async Task The_directory_is_sorted_by_display_name_ignoring_case_accents_and_punctuation_and_leaves_archived_ones_out()
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        foreach (var name in new[] { "Seeta Sparrows FC", "bukoto Comets FC", "Kisaasi Kestrels SC", "Ntinda Swifts SC" })
        {
            await SaveOrganisationAsync(App.Services, kmsa, name);
        }

        // Case is ignored ("bukoto"), "É" sorts as "e" and the hyphen is dropped ("ggabagulls").
        await SaveOrganisationAsync(App.Services, kmsa, "Énsibuko Eagles FC");
        await SaveOrganisationAsync(App.Services, kmsa, "Ggaba-Gulls RFC");
        await SaveOrganisationAsync(App.Services, kmsa, "Kireka Kingfishers FC", status: OrganisationStatus.Archived);
        await SaveOrganisationAsync(App.Services, kmsa, "Mutungo Marlins RFC", status: OrganisationStatus.Dissolved);

        var page = await registrar.ReadJsonAsync(OrganisationsPath, Ct);

        page.OrganisationNames().ShouldBe([
            "bukoto Comets FC", "Énsibuko Eagles FC", "Ggaba-Gulls RFC", "Kisaasi Kestrels SC", "Mutungo Marlins RFC",
            "Ntinda Swifts SC", "Seeta Sparrows FC",
        ]);
        page.GetProperty("page").GetProperty("hasMore").GetBoolean().ShouldBeFalse();
        page.GetProperty("page").GetProperty("nextCursor").ValueKind.ShouldBe(JsonValueKind.Null);
        (await registrar.ReadJsonAsync($"{OrganisationsPath}?status=ARCHIVED", Ct)).OrganisationNames().ShouldBe(["Kireka Kingfishers FC"]);
        (await registrar.ReadJsonAsync($"{OrganisationsPath}?status=DISSOLVED", Ct)).OrganisationNames().ShouldBe(["Mutungo Marlins RFC"]);
    }

    [Fact]
    public async Task An_item_carries_what_recognises_the_organisation_and_nothing_internal()
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var organisation = await SaveOrganisationAsync(App.Services, kmsa, "Ntinda Swifts SC", shortName: "NSSC", city: null);

        var item = (await registrar.ReadJsonAsync(OrganisationsPath, Ct)).GetProperty("items").EnumerateArray().Single();

        item.EnumerateObject().Select(property => property.Name).ShouldBe(
            ["id", "displayName", "shortName", "slug", "type", "city", "country", "status"]);
        (item.GetProperty("id").GetGuid(), item.GetProperty("shortName").GetString(), item.GetProperty("slug").GetString(),
            item.GetProperty("type").GetString(), item.GetProperty("country").GetString(), item.GetProperty("status").GetString())
            .ShouldBe((organisation.Id, "NSSC", organisation.Slug, "CLUB", "UG", "ACTIVE"));
        item.GetProperty("city").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task The_directory_pages_by_cursor_through_organisations_that_share_a_sort_key()
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        // Three names with one sort key ("kisaasi kestrels sc"): the ID breaks the tie.
        var tied = new List<Guid>();
        foreach (var name in new[] { "Kisaasi Kestrels SC", "KISAASI KESTRELS SC", "Kisaasi Kestrels S.C" })
        {
            tied.Add((await SaveOrganisationAsync(App.Services, kmsa, name)).Id);
        }

        await SaveOrganisationAsync(App.Services, kmsa, "Bukoto Comets FC");
        await SaveOrganisationAsync(App.Services, kmsa, "Seeta Sparrows FC");

        var ids = new List<Guid>();
        var names = new List<string?>();
        string? cursor = null;
        var pages = 0;
        do
        {
            var page = await registrar.ReadJsonAsync($"{OrganisationsPath}?limit=2{(cursor is null ? "" : $"&cursor={cursor}")}", Ct);
            names.AddRange(page.OrganisationNames());
            ids.AddRange(page.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetGuid()));
            cursor = page.GetProperty("page").GetProperty("nextCursor").GetString();
            page.GetProperty("page").GetProperty("hasMore").GetBoolean().ShouldBe(cursor is not null);
            pages++;
        }
        while (cursor is not null);

        pages.ShouldBe(3);
        names[0].ShouldBe("Bukoto Comets FC");
        names[^1].ShouldBe("Seeta Sparrows FC");
        ids.Skip(1).Take(3).ShouldBe([.. tied.Order()]);
        ids.Distinct().Count().ShouldBe(5);
    }

    [Fact]
    public async Task Type_keeps_the_organisations_of_that_type()
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        await SaveOrganisationAsync(App.Services, kmsa, "Ntinda Swifts SC");
        await SaveOrganisationAsync(App.Services, kmsa, "Lakeshore High School", type: OrganisationType.School);
        await SaveOrganisationAsync(App.Services, kmsa, "Mabira Hills College", type: OrganisationType.School);
        await SaveOrganisationAsync(App.Services, kmsa, "Sunrise Academy", type: OrganisationType.School, status: OrganisationStatus.Suspended);

        (await registrar.ReadJsonAsync($"{OrganisationsPath}?type=SCHOOL", Ct)).OrganisationNames()
            .ShouldBe(["Lakeshore High School", "Mabira Hills College", "Sunrise Academy"]);
        (await registrar.ReadJsonAsync($"{OrganisationsPath}?type=SCHOOL&status=SUSPENDED", Ct)).OrganisationNames()
            .ShouldBe(["Sunrise Academy"]);
        (await registrar.ReadJsonAsync($"{OrganisationsPath}?type=CLUB", Ct)).OrganisationNames().ShouldBe(["Ntinda Swifts SC"]);
        (await registrar.ReadJsonAsync($"{OrganisationsPath}?type=FEDERATION", Ct)).OrganisationNames().ShouldBeEmpty();
    }

    [Theory]
    [InlineData("ntin", "Ntinda Swifts SC")] // a prefix of the display name
    [InlineData("SPARROWS", "Seeta Sparrows FC")] // a word of it
    [InlineData("Sééta spa", "Seeta Sparrows FC")] // accents and case aside
    [InlineData("FC Comets Bukoto", "Bukoto Comets FC")] // the name key: word order aside
    [InlineData("Bukotto Commets", "Bukoto Comets FC")] // similarity
    [InlineData("nss", "Ntinda Swifts SC")] // a prefix of the short name's key
    [InlineData("Company Football", "Kisaasi Kestrels SC")] // a prefix of the legal name's key
    public async Task Search_matches_the_display_name_by_prefix_or_similarity_and_the_legal_or_short_name_by_prefix(
        string q, string expected)
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        await SaveOrganisationAsync(App.Services, kmsa, "Bukoto Comets FC");
        await SaveOrganisationAsync(App.Services, kmsa, "Ntinda Swifts SC", shortName: "NSSC");
        await SaveOrganisationAsync(App.Services, kmsa, "Kisaasi Kestrels SC", legalName: "Kestrels Football Company Limited");
        await SaveOrganisationAsync(App.Services, kmsa, "Seeta Sparrows FC");
        await SaveOrganisationAsync(App.Services, kmsa, "Naguru Nightjars Club");

        (await registrar.ReadJsonAsync($"{OrganisationsPath}?q={Uri.EscapeDataString(q)}", Ct)).OrganisationNames().ShouldBe([expected]);
    }

    [Theory]
    [InlineData("Entebbe")]
    [InlineData("--")]
    [InlineData("arrows")] // inside a word
    [InlineData("Limited")] // a word of the legal name, not a prefix of its key
    public async Task Search_without_a_match_answers_an_empty_page(string q)
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        await SaveOrganisationAsync(App.Services, kmsa, "Seeta Sparrows FC", legalName: "Seeta Sparrows Football Club Limited");

        var page = await registrar.ReadJsonAsync($"{OrganisationsPath}?q={Uri.EscapeDataString(q)}", Ct);

        page.OrganisationNames().ShouldBeEmpty();
        page.GetProperty("page").GetProperty("hasMore").GetBoolean().ShouldBeFalse();
    }

    [Theory]
    [InlineData("limit=0", "limit")]
    [InlineData("limit=101", "limit")]
    [InlineData("limit=two", "limit")]
    [InlineData("status=DEFUNCT", "status")]
    [InlineData("status=active", "status")]
    [InlineData("type=club", "type")]
    [InlineData("type=TEAM", "type")]
    [InlineData("cursor=not-a-cursor", "cursor")]
    [InlineData("q=Ntinda%00", "q")]
    public async Task A_bad_query_value_is_refused_naming_its_parameter(string query, string field)
    {
        var (_, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);

        using var response = await registrar.GetAsync($"{OrganisationsPath}?{query}", Ct);

        var problem = await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_INVALID_QUERY");
        Fields(problem).ShouldBe([(field, "VALIDATION_INVALID_QUERY")]);
    }

    [Fact]
    public async Task A_q_longer_than_100_characters_is_refused()
    {
        var (_, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);

        using var response = await registrar.GetAsync($"{OrganisationsPath}?q={new string('a', 101)}", Ct);

        var problem = await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_INVALID_QUERY");
        Fields(problem).ShouldBe([("q", "VALIDATION_INVALID_QUERY")]);
    }

    [Fact]
    public async Task A_tenant_lists_only_its_own_organisations()
    {
        var (kmsa, kmsaRegistrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var (elssc, elsscOwner) = await TenantAsync("elssc", IdentityRoles.TenantOwner);
        await SaveOrganisationAsync(App.Services, kmsa, "Ntinda Swifts SC");
        await SaveOrganisationAsync(App.Services, elssc, "Lakeshore High School", type: OrganisationType.School);

        (await kmsaRegistrar.ReadJsonAsync(OrganisationsPath, Ct)).OrganisationNames().ShouldBe(["Ntinda Swifts SC"]);
        (await elsscOwner.ReadJsonAsync(OrganisationsPath, Ct)).OrganisationNames().ShouldBe(["Lakeshore High School"]);
        (await elsscOwner.ReadJsonAsync($"{OrganisationsPath}?q=Ntinda", Ct)).OrganisationNames().ShouldBeEmpty();
    }

    // D20: every read role lists organisations; the scorer gets none in S07 (a team manager holds no tenant-wide role).
    [Theory]
    [InlineData(IdentityRoles.TenantOwner, true)]
    [InlineData(IdentityRoles.TenantAdmin, true)]
    [InlineData(IdentityRoles.Registrar, true)]
    [InlineData(IdentityRoles.CompetitionManager, true)]
    [InlineData(IdentityRoles.ResultsOfficer, true)]
    [InlineData(IdentityRoles.DataEntry, true)]
    [InlineData(IdentityRoles.Viewer, true)]
    [InlineData(IdentityRoles.Scorer, false)]
    public async Task Every_read_role_lists_organisations(string role, bool reads)
    {
        var (_, client) = await TenantAsync("kmsa", role);

        using var response = await client.GetAsync(OrganisationsPath, Ct);

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
    public async Task A_read_only_tenant_lists_its_organisations()
    {
        App.Authorization.UseIdentityRoles();
        var kmsa = await TenantTestData.SaveTenantAsync(
            App.Services, "kmsa", TenantStatus.Active, publicEnabled: false, withDomain: true, withSubscription: false, Ct);
        await TenantTestData.SavePeriodAsync(App.Services, kmsa, new DateOnly(2025, 1, 1), new DateOnly(2025, 12, 31), Ct, cancelReason: "Pilot paused");
        var member = await CreateMemberAsync(App.Services, kmsa, "brian.ssemakula@kmsa.example", Ct, TenantWide(IdentityRoles.Registrar));
        await SaveOrganisationAsync(App.Services, kmsa, "Ntinda Swifts SC");

        (await App.ClientFor(member.UserId, kmsa).ReadJsonAsync(OrganisationsPath, Ct)).OrganisationNames().ShouldBe(["Ntinda Swifts SC"]);
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

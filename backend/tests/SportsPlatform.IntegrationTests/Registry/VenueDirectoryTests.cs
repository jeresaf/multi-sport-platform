using System.Net;
using System.Text.Json;
using SportsPlatform.IntegrationTests.Api;
using SportsPlatform.IntegrationTests.Identity;
using SportsPlatform.IntegrationTests.Infrastructure;
using SportsPlatform.IntegrationTests.Tenancy;
using SportsPlatform.Modules.Identity.Domain.Roles;
using SportsPlatform.Modules.Registry.Domain.Venues;
using SportsPlatform.Modules.Tenancy.Contracts.Tenants;
using static SportsPlatform.IntegrationTests.Identity.MembershipTestData;
using static SportsPlatform.IntegrationTests.Registry.PeopleRequests;
using static SportsPlatform.IntegrationTests.Registry.VenueRequests;
using static SportsPlatform.IntegrationTests.Tenancy.TestSportDefinitions;

namespace SportsPlatform.IntegrationTests.Registry;

/// <summary>
/// The venue directory (S07-T1b-2; D19): sorted by sort key (the normalised name, word order kept) and ID, paged by
/// cursor, filtered by variant, city and status, searched by name; the roles (D20), the read-only tenant and the
/// tenant's own venues only.
/// </summary>
public sealed class VenueDirectoryTests(PostgresFixture fixture) : DatabaseTest(fixture)
{
    [Fact]
    public async Task The_directory_is_sorted_by_name_ignoring_case_accents_and_punctuation_and_leaves_archived_venues_out()
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        foreach (var name in new[] { "Seeta Hill Sports Field", "bukoto Community Stadium", "Kisaasi Recreation Ground", "Kiwatule Meadow Ground" })
        {
            await SaveVenueAsync(App.Services, kmsa, name);
        }

        // Case is ignored ("bukoto"), "É" sorts as "e" and the hyphen is dropped ("ggabalakeside").
        await SaveVenueAsync(App.Services, kmsa, "Énsibuko Grounds");
        await SaveVenueAsync(App.Services, kmsa, "Ggaba-Lakeside Rugby Ground");
        await SaveVenueAsync(App.Services, kmsa, "Metro Indoor Arena", status: VenueStatus.Archived);
        await SaveVenueAsync(App.Services, kmsa, "Naguru Courts", status: VenueStatus.Closed);

        var page = await registrar.ReadJsonAsync(VenuesPath, Ct);

        page.VenueNames().ShouldBe([
            "bukoto Community Stadium", "Énsibuko Grounds", "Ggaba-Lakeside Rugby Ground", "Kisaasi Recreation Ground",
            "Kiwatule Meadow Ground", "Naguru Courts", "Seeta Hill Sports Field",
        ]);
        page.GetProperty("page").GetProperty("hasMore").GetBoolean().ShouldBeFalse();
        page.GetProperty("page").GetProperty("nextCursor").ValueKind.ShouldBe(JsonValueKind.Null);
        (await registrar.ReadJsonAsync($"{VenuesPath}?status=ARCHIVED", Ct)).VenueNames().ShouldBe(["Metro Indoor Arena"]);
        (await registrar.ReadJsonAsync($"{VenuesPath}?status=CLOSED", Ct)).VenueNames().ShouldBe(["Naguru Courts"]);
    }

    [Fact]
    public async Task An_item_carries_what_recognises_the_venue()
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var venue = await SaveVenueAsync(App.Services, kmsa, "Bukoto Community Stadium", supportedVariants: [TestballFull, TestballShort]);

        var item = (await registrar.ReadJsonAsync(VenuesPath, Ct)).GetProperty("items").EnumerateArray().Single();

        item.EnumerateObject().Select(property => property.Name).ShouldBe(
            ["id", "name", "shortName", "slug", "type", "city", "country", "supportedVariants", "status"]);
        (item.GetProperty("id").GetGuid(), item.GetProperty("slug").GetString(), item.GetProperty("type").GetString(),
            item.GetProperty("city").GetString(), item.GetProperty("country").GetString(), item.GetProperty("status").GetString())
            .ShouldBe((venue.Id, venue.Slug, "SPORTS_GROUND", "Kampala", "UG", "ACTIVE"));
        item.GetProperty("shortName").ValueKind.ShouldBe(JsonValueKind.Null);
        item.GetProperty("supportedVariants").EnumerateArray().Select(v => v.GetString()).ShouldBe([TestballFull, TestballShort]);
    }

    [Fact]
    public async Task The_directory_pages_by_cursor_through_venues_that_share_a_sort_key()
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        // Three names with one sort key ("kisaasi recreation ground"): the ID breaks the tie.
        var tied = new List<Guid>();
        foreach (var name in new[] { "Kisaasi Recreation Ground", "KISAASI RECREATION GROUND", "Kisaasi Recreation Ground." })
        {
            tied.Add((await SaveVenueAsync(App.Services, kmsa, name)).Id);
        }

        await SaveVenueAsync(App.Services, kmsa, "Bukoto Community Stadium");
        await SaveVenueAsync(App.Services, kmsa, "Seeta Hill Sports Field");

        var ids = new List<Guid>();
        var names = new List<string?>();
        string? cursor = null;
        var pages = 0;
        do
        {
            var page = await registrar.ReadJsonAsync($"{VenuesPath}?limit=2{(cursor is null ? "" : $"&cursor={cursor}")}", Ct);
            names.AddRange(page.VenueNames());
            ids.AddRange(page.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetGuid()));
            cursor = page.GetProperty("page").GetProperty("nextCursor").GetString();
            page.GetProperty("page").GetProperty("hasMore").GetBoolean().ShouldBe(cursor is not null);
            pages++;
        }
        while (cursor is not null);

        pages.ShouldBe(3);
        names[0].ShouldBe("Bukoto Community Stadium");
        names[^1].ShouldBe("Seeta Hill Sports Field");
        ids.Skip(1).Take(3).ShouldBe([.. tied.Order()]);
        ids.Distinct().Count().ShouldBe(5);
    }

    [Fact]
    public async Task Variant_and_city_keep_the_venues_that_match()
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        await SaveVenueAsync(App.Services, kmsa, "Bukoto Community Stadium", supportedVariants: [TestballFull]);
        await SaveVenueAsync(App.Services, kmsa, "Kisaasi Recreation Ground", supportedVariants: [TestballFull, TestballShort]);
        await SaveVenueAsync(App.Services, kmsa, "Metro Indoor Arena", city: "Naguru");
        await SaveVenueAsync(App.Services, kmsa, "Seeta Hill Sports Field", city: "Mukono", supportedVariants: [TestballShort]);

        (await registrar.ReadJsonAsync($"{VenuesPath}?variant={TestballFull}", Ct)).VenueNames()
            .ShouldBe(["Bukoto Community Stadium", "Kisaasi Recreation Ground"]);
        (await registrar.ReadJsonAsync($"{VenuesPath}?variant={TestballShort}&city=Mukono", Ct)).VenueNames()
            .ShouldBe(["Seeta Hill Sports Field"]);
        // The city as the duplicate warning compares it: case, accents and punctuation aside.
        (await registrar.ReadJsonAsync($"{VenuesPath}?city=%20NAGURU.%20", Ct)).VenueNames().ShouldBe(["Metro Indoor Arena"]);
        (await registrar.ReadJsonAsync($"{VenuesPath}?variant=UNHOSTED_VARIANT", Ct)).VenueNames().ShouldBeEmpty();
        (await registrar.ReadJsonAsync($"{VenuesPath}?city=Entebbe", Ct)).VenueNames().ShouldBeEmpty();
    }

    [Theory]
    [InlineData("kis", "Kisaasi Recreation Ground")]
    [InlineData("KISAASI rec", "Kisaasi Recreation Ground")]
    [InlineData("Meadow", "Kiwatule Meadow Ground")]
    [InlineData("hill sports", "Seeta Hill Sports Field")]
    [InlineData("Ground Kiwatule", "Kiwatule Meadow Ground")]
    [InlineData("Bukotto Comunity Stadium", "Bukoto Community Stadium")]
    [InlineData("Énsi", "Ensibuko Grounds")]
    public async Task Search_matches_the_name_or_a_word_by_prefix_and_the_name_by_similarity(string q, string expected)
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        foreach (var name in new[] { "Bukoto Community Stadium", "Kisaasi Recreation Ground", "Kiwatule Meadow Ground", "Seeta Hill Sports Field", "Ensibuko Grounds" })
        {
            await SaveVenueAsync(App.Services, kmsa, name);
        }

        (await registrar.ReadJsonAsync($"{VenuesPath}?q={Uri.EscapeDataString(q)}", Ct)).VenueNames().ShouldBe([expected]);
    }

    [Theory]
    [InlineData("Entebbe")]
    [InlineData("--")]
    [InlineData("ound")]
    public async Task Search_without_a_match_answers_an_empty_page(string q)
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        await SaveVenueAsync(App.Services, kmsa, "Kisaasi Recreation Ground");

        var page = await registrar.ReadJsonAsync($"{VenuesPath}?q={Uri.EscapeDataString(q)}", Ct);

        page.VenueNames().ShouldBeEmpty();
        page.GetProperty("page").GetProperty("hasMore").GetBoolean().ShouldBeFalse();
    }

    [Theory]
    [InlineData("limit=0", "limit")]
    [InlineData("limit=101", "limit")]
    [InlineData("limit=two", "limit")]
    [InlineData("status=DEMOLISHED", "status")]
    [InlineData("status=active", "status")]
    [InlineData("variant=testball_full", "variant")]
    [InlineData("cursor=not-a-cursor", "cursor")]
    [InlineData("q=Kisaasi%00", "q")]
    public async Task A_bad_query_value_is_refused_naming_its_parameter(string query, string field)
    {
        var (_, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);

        using var response = await registrar.GetAsync($"{VenuesPath}?{query}", Ct);

        var problem = await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_INVALID_QUERY");
        Fields(problem).ShouldBe([(field, "VALIDATION_INVALID_QUERY")]);
    }

    [Fact]
    public async Task A_tenant_lists_only_its_own_venues()
    {
        var (kmsa, kmsaRegistrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var (elssc, elsscOwner) = await TenantAsync("elssc", IdentityRoles.TenantOwner);
        await SaveVenueAsync(App.Services, kmsa, "Kisaasi Recreation Ground");
        await SaveVenueAsync(App.Services, elssc, "Lakeshore High School Playing Fields");

        (await kmsaRegistrar.ReadJsonAsync(VenuesPath, Ct)).VenueNames().ShouldBe(["Kisaasi Recreation Ground"]);
        (await elsscOwner.ReadJsonAsync(VenuesPath, Ct)).VenueNames().ShouldBe(["Lakeshore High School Playing Fields"]);
        (await elsscOwner.ReadJsonAsync($"{VenuesPath}?q=Kisaasi", Ct)).VenueNames().ShouldBeEmpty();
    }

    // D20: every read role lists venues; the scorer gets none in S07 (a team manager holds no tenant-wide role).
    [Theory]
    [InlineData(IdentityRoles.TenantOwner, true)]
    [InlineData(IdentityRoles.TenantAdmin, true)]
    [InlineData(IdentityRoles.Registrar, true)]
    [InlineData(IdentityRoles.CompetitionManager, true)]
    [InlineData(IdentityRoles.ResultsOfficer, true)]
    [InlineData(IdentityRoles.DataEntry, true)]
    [InlineData(IdentityRoles.Viewer, true)]
    [InlineData(IdentityRoles.Scorer, false)]
    public async Task Every_read_role_lists_venues(string role, bool reads)
    {
        var (_, client) = await TenantAsync("kmsa", role);

        using var response = await client.GetAsync(VenuesPath, Ct);

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
    public async Task A_read_only_tenant_lists_its_venues()
    {
        App.Authorization.UseIdentityRoles();
        var kmsa = await TenantTestData.SaveTenantAsync(
            App.Services, "kmsa", TenantStatus.Active, publicEnabled: false, withDomain: true, withSubscription: false, Ct);
        await TenantTestData.SavePeriodAsync(App.Services, kmsa, new DateOnly(2025, 1, 1), new DateOnly(2025, 12, 31), Ct, cancelReason: "Pilot paused");
        var member = await CreateMemberAsync(App.Services, kmsa, "brian.ssemakula@kmsa.example", Ct, TenantWide(IdentityRoles.Registrar));
        await SaveVenueAsync(App.Services, kmsa, "Kisaasi Recreation Ground");

        (await App.ClientFor(member.UserId, kmsa).ReadJsonAsync(VenuesPath, Ct)).VenueNames().ShouldBe(["Kisaasi Recreation Ground"]);
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

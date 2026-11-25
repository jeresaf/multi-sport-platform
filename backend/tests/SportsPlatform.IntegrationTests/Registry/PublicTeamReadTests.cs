using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.IntegrationTests.Identity;
using SportsPlatform.IntegrationTests.Infrastructure;
using SportsPlatform.IntegrationTests.Tenancy;
using SportsPlatform.Modules.Identity.Domain.Roles;
using SportsPlatform.Modules.Registry.Domain.Organisations;
using SportsPlatform.Modules.Registry.Domain.People;
using SportsPlatform.Modules.Registry.Domain.Teams;
using SportsPlatform.Modules.Registry.Domain.Venues;
using SportsPlatform.Modules.Tenancy.Contracts.Tenants;
using SportsPlatform.UnitTests.Platform.Media;
using static SportsPlatform.IntegrationTests.Identity.MembershipTestData;
using static SportsPlatform.IntegrationTests.Registry.OrganisationRequests;
using static SportsPlatform.IntegrationTests.Registry.TeamRequests;
using static SportsPlatform.IntegrationTests.Registry.VenueRequests;
using static SportsPlatform.IntegrationTests.Tenancy.TestSportDefinitions;

namespace SportsPlatform.IntegrationTests.Registry;

/// <summary>
/// The public team reads (S07-T5b; D16, D17; owner, plan 2026-11-25): by ID or slug, <c>PUBLIC</c> fields only, former
/// names with their dates, every status but <c>ARCHIVED</c>, an archived organisation or home venue as null, the logo's
/// and badge's public paths, the directory (<c>ACTIVE</c> by default, <c>ARCHIVED</c> refused, the filters, search on
/// public text and former names only, the cursor), the cache headers, and nothing for another tenant's host.
/// </summary>
public sealed class PublicTeamReadTests(PostgresFixture fixture) : RegistryImageTest(fixture)
{
    private const string _kmsaHost = "kmsa.localhost";
    private const string _elsscHost = "elssc.localhost";
    private const string _teams = "/api/public/v1/teams";

    private static readonly DateOnly _renamedOn = new(2026, 1, 1);

    // Allow-lists (as review #134 N2): a new field must be added here on purpose, and nothing INTERNAL is among them (D16).
    private static readonly string[] _teamFields =
    [
        "id", "variant", "genderCategory", "ageGroup", "level", "name", "shortName", "nameSince", "slug", "code", "motto",
        "primaryColour", "secondaryColour", "tertiaryColour", "foundedYear", "organisation", "homeVenue", "status", "logo",
        "badge", "formerNames",
    ];

    private static readonly string[] _teamItemFields =
    [
        "id", "name", "shortName", "slug", "variant", "genderCategory", "ageGroup", "level", "organisation", "status", "logo",
        "badge",
    ];

    // T5 acceptance 1 and 2: PUBLIC fields only, by ID and by slug; the former names with their dates, the organisation
    // and the home venue; no nickname or identifier; cacheable for a minute with a strong ETag.
    [Fact]
    public async Task A_team_is_read_by_ID_or_slug_with_its_former_names_and_public_fields_only()
    {
        var (kmsa, _) = await PublicTenantAsync("kmsa");
        var meadow = await SaveVenueAsync(App.Services, kmsa, "Kiwatule Meadow Ground");
        var bunga = await SaveOrganisationAsync(App.Services, kmsa, "Bunga Lakeside SC");
        var team = await SaveTeamAsync(
            App.Services, kmsa, "Bunga Breakers FC", organisationId: bunga.Id, shortName: "Bunga", homeVenueId: meadow.Id,
            renamedTo: ("Bunga Lakeside FC", _renamedOn), alias: ("The Lakers", AliasKind.Nickname),
            identifier: ("FUFA", "BLF-0031"));

        var byId = await PublicAsync($"{_teams}/{team.Id}");
        var bySlug = await PublicAsync($"{_teams}/{team.Slug}");

        byId.Status.ShouldBe(StatusCodes.Status200OK, byId.Body);
        bySlug.Body.ShouldBe(byId.Body);
        var read = byId.Json;
        FieldsOf(read).ShouldBe(_teamFields, ignoreOrder: true);
        read.GetProperty("name").GetString().ShouldBe("Bunga Lakeside FC");
        read.GetProperty("nameSince").GetString().ShouldBe("2026-01-01");
        read.GetProperty("organisation").GetProperty("id").GetGuid().ShouldBe(bunga.Id);
        read.GetProperty("homeVenue").GetProperty("id").GetGuid().ShouldBe(meadow.Id);
        read.GetProperty("logo").ValueKind.ShouldBe(JsonValueKind.Null);
        var former = read.GetProperty("formerNames").EnumerateArray().ShouldHaveSingleItem();
        (former.GetProperty("name").GetString(), former.GetProperty("validFrom").GetString(), former.GetProperty("validTo").GetString())
            .ShouldBe(("Bunga Breakers FC", "2020-01-01", "2025-12-31"));
        byId.Body.ShouldNotContain("The Lakers");
        byId.Body.ShouldNotContain("BLF-0031");
        FieldsOf((await PublicAsync(_teams)).Json.GetProperty("items")[0]).ShouldBe(_teamItemFields, ignoreOrder: true);

        byId.Headers.CacheControl.ToString().ShouldBe("public, max-age=60");
        (await PublicAsync($"{_teams}/{team.Slug}", ("If-None-Match", byId.Headers.ETag.ToString()))).Status
            .ShouldBe(StatusCodes.Status304NotModified);
    }

    // T5 acceptance 1: an archived team is 404 by ID and by slug; an unknown or malformed key too; a disbanded team shows;
    // another tenant's host never sees it.
    [Fact]
    public async Task An_archived_team_or_another_tenants_is_not_found()
    {
        var (kmsa, _) = await PublicTenantAsync("kmsa");
        await PublicTenantAsync("elssc");
        var archived = await SaveTeamAsync(App.Services, kmsa, "Kyanja Rovers FC", status: TeamStatus.Archived);
        var disbanded = await SaveTeamAsync(
            App.Services, kmsa, "Seeta Sparrows FC", category: new(GenderCategory.Men, AgeGroup.U17, TeamLevel.First),
            status: TeamStatus.Disbanded);

        foreach (var key in new[] { archived.Id.ToString(), archived.Slug, Guid.NewGuid().ToString(), "Not-A-Slug", "x" })
        {
            (await PublicAsync($"{_teams}/{key}")).ShouldBeProblem(StatusCodes.Status404NotFound, "TEAM_NOT_FOUND");
        }

        (await PublicAsync($"{_teams}/{disbanded.Slug}")).Json.GetProperty("status").GetString().ShouldBe("DISBANDED");
        (await PublicAsync($"{_teams}/{disbanded.Id}", host: _elsscHost)).ShouldBeProblem(StatusCodes.Status404NotFound, "TEAM_NOT_FOUND");
        (await PublicAsync($"{_teams}/{disbanded.Slug}", host: _elsscHost)).ShouldBeProblem(StatusCodes.Status404NotFound, "TEAM_NOT_FOUND");
        (await PublicAsync($"{_teams}?status=DISBANDED", host: _elsscHost)).Json.GetProperty("items").GetArrayLength().ShouldBe(0);
    }

    // T5 acceptance 2, D17: an archived organisation or home venue is null, on the read and in the directory; a dissolved
    // organisation still shows; an archived organisation's ID filters out every team (review #135 Q1).
    [Fact]
    public async Task An_archived_organisation_or_home_venue_is_embedded_as_null()
    {
        var (kmsa, _) = await PublicTenantAsync("kmsa");
        var oldGround = await SaveVenueAsync(App.Services, kmsa, "Kisaasi Old Ground", status: VenueStatus.Archived);
        var kestrels = await SaveOrganisationAsync(App.Services, kmsa, "Kisaasi Kestrels SC");
        var team = await SaveTeamAsync(App.Services, kmsa, "Kisaasi Kestrels FC", organisationId: kestrels.Id, homeVenueId: oldGround.Id);

        var read = (await PublicAsync($"{_teams}/{team.Id}")).Json;
        read.GetProperty("homeVenue").ValueKind.ShouldBe(JsonValueKind.Null);
        read.GetProperty("organisation").GetProperty("id").GetGuid().ShouldBe(kestrels.Id);

        await ChangeOrganisationStatusAsync(kmsa, kestrels.Id, OrganisationStatus.Dissolved);
        (await PublicAsync($"{_teams}/{team.Id}")).Json.GetProperty("organisation").GetProperty("id").GetGuid().ShouldBe(kestrels.Id);
        (await PublicAsync(_teams)).Json.GetProperty("items")[0].GetProperty("organisation").GetProperty("id").GetGuid().ShouldBe(kestrels.Id);

        await ChangeOrganisationStatusAsync(kmsa, kestrels.Id, OrganisationStatus.Archived);
        (await PublicAsync($"{_teams}/{team.Id}")).Json.GetProperty("organisation").ValueKind.ShouldBe(JsonValueKind.Null);
        var item = (await PublicAsync(_teams)).Json.GetProperty("items").EnumerateArray().ShouldHaveSingleItem();
        item.GetProperty("organisation").ValueKind.ShouldBe(JsonValueKind.Null);

        // Review #135 Q1 (owner): an archived organisation's ID keeps no teams, as an unknown one.
        Ids((await PublicAsync($"{_teams}?organisationId={kestrels.Id}")).Json).ShouldBeEmpty();
    }

    // T5 acceptance 2, D15 (owner, 2026-11-25): the logo and the badge are linked by their two public paths in the read
    // and the directory, served on the tenant's host only.
    [Fact]
    public async Task A_teams_logo_and_badge_are_linked_by_their_public_paths()
    {
        var (kmsa, registrar) = await PublicTenantAsync("kmsa");
        await PublicTenantAsync("elssc");
        var team = await SaveTeamAsync(App.Services, kmsa, "Ntinda Swifts FC");
        await OkAsync(await registrar.PutAsync($"{TeamPath(team.Id)}/logo", Form(TestImages.TransparentPng(800, 400)), Ct));
        await OkAsync(await registrar.PutAsync($"{TeamPath(team.Id)}/badge", Form(TestImages.Png(300, 300)), Ct));

        var read = (await PublicAsync($"{_teams}/{team.Id}")).Json;
        var item = (await PublicAsync(_teams)).Json.GetProperty("items")[0];

        foreach (var image in new[] { "logo", "badge" })
        {
            var paths = read.GetProperty(image);
            item.GetProperty(image).GetRawText().ShouldBe(paths.GetRawText(), image);
            paths.TryGetProperty("expiresAt", out _).ShouldBeFalse();
            foreach (var property in new[] { "thumbnailUrl", "mediumUrl" })
            {
                var path = paths.GetProperty(property).GetString().ShouldNotBeNull();
                path.ShouldStartWith("/api/public/v1/assets/");
                var served = await PublicAsync(path);
                served.Status.ShouldBe(StatusCodes.Status200OK, $"{image} {property}");
                served.Headers.ContentType.ToString().ShouldBe("image/webp");
                (await PublicAsync(path, host: _elsscHost)).ShouldBeProblem(StatusCodes.Status404NotFound, "MEDIA_NOT_FOUND");
            }
        }

        read.GetProperty("logo").GetProperty("mediumUrl").GetString().ShouldNotBe(read.GetProperty("badge").GetProperty("mediumUrl").GetString());
    }

    // D17, owner (plan 2026-11-25): ACTIVE by default, one status when asked, ARCHIVED refused; variant, organisation,
    // category and level filters; the admin directory's order and cursor.
    [Fact]
    public async Task The_team_directory_lists_active_teams_by_default_filters_and_pages_them()
    {
        var (kmsa, _) = await PublicTenantAsync("kmsa");
        await TenantTestData.EnableSportAsync(App.Services, kmsa, Testhoop, Ct);
        var swifts = await SaveOrganisationAsync(App.Services, kmsa, "Ntinda Swifts SC");
        var men = await SaveTeamAsync(App.Services, kmsa, "Ntinda Swifts FC", organisationId: swifts.Id);
        var women = await SaveTeamAsync(
            App.Services, kmsa, "Ntinda Swifts FC", category: new(GenderCategory.Women, AgeGroup.Senior, TeamLevel.First),
            organisationId: swifts.Id);
        var under17 = await SaveTeamAsync(
            App.Services, kmsa, "Ntinda Swifts FC", category: new(GenderCategory.Men, AgeGroup.U17, TeamLevel.First),
            organisationId: swifts.Id);
        var reserves = await SaveTeamAsync(
            App.Services, kmsa, "Ntinda Swifts FC", category: new(GenderCategory.Men, AgeGroup.Senior, TeamLevel.Reserve),
            organisationId: swifts.Id);
        var hoops = await SaveTeamAsync(App.Services, kmsa, "Seeta Sparrows BC", variant: Testhoop);
        var suspended = await SaveTeamAsync(App.Services, kmsa, "Kireka Kingfishers FC", status: TeamStatus.Suspended);
        await SaveTeamAsync(App.Services, kmsa, "Bukoto Harriers FC", status: TeamStatus.Archived);

        var first = (await PublicAsync($"{_teams}?limit=3")).Json;
        var second = (await PublicAsync(
            $"{_teams}?limit=3&cursor={Uri.EscapeDataString(first.GetProperty("page").GetProperty("nextCursor").GetString()!)}")).Json;

        // Name, then age group, gender and level.
        Ids(first).ShouldBe([men.Id, reserves.Id, women.Id]);
        first.GetProperty("page").GetProperty("hasMore").GetBoolean().ShouldBeTrue();
        Ids(second).ShouldBe([under17.Id, hoops.Id]);
        second.GetProperty("page").GetProperty("hasMore").GetBoolean().ShouldBeFalse();
        Ids((await PublicAsync($"{_teams}?status=SUSPENDED")).Json).ShouldBe([suspended.Id]);
        Ids((await PublicAsync($"{_teams}?variant={Testhoop}")).Json).ShouldBe([hoops.Id]);
        Ids((await PublicAsync($"{_teams}?organisationId={swifts.Id}&genderCategory=MEN")).Json).ShouldBe([men.Id, reserves.Id, under17.Id]);
        Ids((await PublicAsync($"{_teams}?ageGroup=U17")).Json).ShouldBe([under17.Id]);
        Ids((await PublicAsync($"{_teams}?level=RESERVE")).Json).ShouldBe([reserves.Id]);
        Ids((await PublicAsync($"{_teams}?organisationId={Guid.NewGuid()}")).Json).ShouldBeEmpty();

        foreach (var query in new[]
                 {
                     "status=ARCHIVED", "status=active", "genderCategory=BOYS", "ageGroup=U99", "level=TOP",
                     "organisationId=nonsense", "variant=not%20a%20code", "limit=0", "limit=101", "cursor=nonsense",
                 })
        {
            (await PublicAsync($"{_teams}?{query}")).ShouldBeProblem(StatusCodes.Status400BadRequest, "VALIDATION_INVALID_QUERY");
        }
    }

    // D16, owner (plan 2026-11-25): q matches the name, the short name and a former name; never a nickname, a spelling or
    // an identifier (pre-review: a spelling too, so a filter on the kind can't let one through).
    [Fact]
    public async Task The_team_directory_searches_public_names_and_former_names_only()
    {
        var (kmsa, _) = await PublicTenantAsync("kmsa");
        var team = await SaveTeamAsync(
            App.Services, kmsa, "Bunga Breakers FC", shortName: "BBFC", renamedTo: ("Bunga Lakeside FC", _renamedOn),
            alias: ("Lake Wanderers", AliasKind.Nickname), identifier: ("FUFA", "BLF-0031"));
        await SaveTeamAsync(App.Services, kmsa, "Seeta Sparrows FC");
        await SaveTeamAsync(App.Services, kmsa, "Kireka Kingfishers FC", alias: ("Kyreka Kingfisherz", AliasKind.Spelling));

        foreach (var q in new[] { "bunga", "lakesi", "Bunga Lakeside", "bbfc", "breakers", "Bunga Breakers FC" })
        {
            Ids((await PublicAsync($"{_teams}?q={Uri.EscapeDataString(q)}")).Json).ShouldBe([team.Id], q);
        }

        foreach (var q in new[] { "wanderers", "kyreka", "BLF-0031", "--" })
        {
            Ids((await PublicAsync($"{_teams}?q={Uri.EscapeDataString(q)}")).Json).ShouldBeEmpty(q);
        }
    }

    private static string[] FieldsOf(JsonElement element) => [.. element.EnumerateObject().Select(property => property.Name)];

    private static Guid[] Ids(JsonElement page) =>
        [.. page.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetGuid())];

    private Task<PublicResponse> PublicAsync(string pathAndQuery, params (string Name, string Value)[] headers) =>
        App.Server.GetAsync(pathAndQuery, _kmsaHost, headers: headers);

    private Task<PublicResponse> PublicAsync(string pathAndQuery, string host) => App.Server.GetAsync(pathAndQuery, host);

    /// <summary>An active tenant whose public site is on, with the test sport, and its registrar's client.</summary>
    private async Task<(Guid Tenant, HttpClient Registrar)> PublicTenantAsync(string slug)
    {
        App.Authorization.UseIdentityRoles();
        var tenant = await TenantTestData.SaveTenantAsync(App.Services, slug, TenantStatus.Active, publicEnabled: true, withDomain: true, Ct);
        await TenantTestData.EnableSportAsync(App.Services, tenant, TestballFull, Ct);
        var member = await CreateMemberAsync(
            App.Services, tenant, MailboxOf(IdentityRoles.Registrar, $"{slug}.example"), Ct, TenantWide(IdentityRoles.Registrar));
        return (tenant, App.ClientFor(member.UserId, tenant));
    }

    private async Task ChangeOrganisationStatusAsync(Guid tenantId, Guid organisationId, OrganisationStatus status)
    {
        await using var scope = RegistryTestData.ScopeFor(App.Services, tenantId, out var context);
        var organisation = await context.Set<Organisation>().Include(o => o.StatusHistory).SingleAsync(o => o.Id == organisationId, Ct);
        organisation.ChangeStatus(status, "Set up by the test", SystemActor.Instance, RegistryTestData.Now)
            .IsSuccess.ShouldBeTrue();
        await context.SaveChangesAsync(Ct);
    }
}

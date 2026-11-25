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
/// The public organisation and venue reads (S07-T5a; D16, D17; owner, plan 2026-11-25): by ID or slug, <c>PUBLIC</c>
/// fields only, every status but <c>ARCHIVED</c>, archived embedded records as null or left out, the logo's public paths,
/// the directories (<c>ACTIVE</c> by default, <c>ARCHIVED</c> refused, search on public text only, the cursor), the cache
/// headers, and nothing for another tenant's host.
/// </summary>
public sealed class PublicOrganisationAndVenueReadTests(PostgresFixture fixture) : RegistryImageTest(fixture)
{
    private const string _kmsaHost = "kmsa.localhost";
    private const string _elsscHost = "elssc.localhost";
    private const string _organisations = "/api/public/v1/organisations";
    private const string _venues = "/api/public/v1/venues";

    private static readonly string[] _internalOrganisationFields = ["legalName", "email", "phone", "aliases", "externalIdentifiers", "version"];

    // T5 acceptance 1: PUBLIC fields only; by ID and by slug; cacheable for a minute with a strong ETag.
    [Fact]
    public async Task An_organisation_is_read_by_ID_or_slug_with_its_public_fields_only()
    {
        var (kmsa, _) = await PublicTenantAsync("kmsa");
        var meadow = await SaveVenueAsync(App.Services, kmsa, "Kiwatule Meadow Ground");
        var swifts = await SaveOrganisationAsync(
            App.Services, kmsa, "Ntinda Swifts SC", legalName: "Ntinda Swifts Sports Club Limited", shortName: "Swifts",
            primaryVenueId: meadow.Id, alias: ("Swifts of Ntinda", AliasKind.Nickname), identifier: ("FUFA", "NSW-0042"));

        var byId = await PublicAsync($"{_organisations}/{swifts.Id}");
        var bySlug = await PublicAsync($"{_organisations}/{swifts.Slug}");

        byId.Status.ShouldBe(StatusCodes.Status200OK, byId.Body);
        bySlug.Body.ShouldBe(byId.Body);
        var read = byId.Json;
        read.GetProperty("displayName").GetString().ShouldBe("Ntinda Swifts SC");
        read.GetProperty("shortName").GetString().ShouldBe("Swifts");
        read.GetProperty("slug").GetString().ShouldBe(swifts.Slug);
        read.GetProperty("type").GetString().ShouldBe("CLUB");
        read.GetProperty("primaryVenue").GetProperty("id").GetGuid().ShouldBe(meadow.Id);
        read.GetProperty("logo").ValueKind.ShouldBe(JsonValueKind.Null);
        foreach (var field in _internalOrganisationFields)
        {
            read.TryGetProperty(field, out _).ShouldBeFalse(field);
        }

        byId.Headers.CacheControl.ToString().ShouldBe("public, max-age=60");
        var etag = byId.Headers.ETag.ToString();
        (await PublicAsync($"{_organisations}/{swifts.Slug}", ("If-None-Match", etag))).Status.ShouldBe(StatusCodes.Status304NotModified);
    }

    // T5 acceptance 1: an archived organisation is 404 by ID and by slug; an unknown or malformed key too; another
    // tenant's host never sees it.
    [Fact]
    public async Task An_archived_organisation_or_another_tenants_is_not_found()
    {
        var (kmsa, _) = await PublicTenantAsync("kmsa");
        await PublicTenantAsync("elssc");
        var archived = await SaveOrganisationAsync(App.Services, kmsa, "Bukoto Harriers AC", status: OrganisationStatus.Archived);
        var dissolved = await SaveOrganisationAsync(App.Services, kmsa, "Kyanja Rovers FC", status: OrganisationStatus.Dissolved);

        foreach (var key in new[] { archived.Id.ToString(), archived.Slug, Guid.NewGuid().ToString(), "Not-A-Slug", "x" })
        {
            (await PublicAsync($"{_organisations}/{key}")).ShouldBeProblem(StatusCodes.Status404NotFound, "ORGANISATION_NOT_FOUND");
        }

        (await PublicAsync($"{_organisations}/{dissolved.Slug}")).Json.GetProperty("status").GetString().ShouldBe("DISSOLVED");
        (await PublicAsync($"{_organisations}/{dissolved.Id}", host: _elsscHost))
            .ShouldBeProblem(StatusCodes.Status404NotFound, "ORGANISATION_NOT_FOUND");
        (await PublicAsync($"{_organisations}/{dissolved.Slug}", host: _elsscHost))
            .ShouldBeProblem(StatusCodes.Status404NotFound, "ORGANISATION_NOT_FOUND");
        (await PublicAsync($"{_organisations}?status=DISSOLVED", host: _elsscHost)).Json.GetProperty("items").GetArrayLength().ShouldBe(0);
    }

    // D17: an archived primary venue or parent is null; archived children and teams are left out; a dissolved parent shows.
    [Fact]
    public async Task Archived_records_are_not_embedded_in_an_organisation()
    {
        var (kmsa, _) = await PublicTenantAsync("kmsa");
        var closedGround = await SaveVenueAsync(App.Services, kmsa, "Kisaasi Old Ground", status: VenueStatus.Archived);
        var association = await SaveOrganisationAsync(App.Services, kmsa, "Kampala Metro Sports Association", OrganisationType.Association);
        var swifts = await SaveOrganisationAsync(App.Services, kmsa, "Ntinda Swifts SC", primaryVenueId: closedGround.Id);
        var juniors = await SaveOrganisationAsync(App.Services, kmsa, "Ntinda Swifts Juniors");
        var oldAcademy = await SaveOrganisationAsync(App.Services, kmsa, "Ntinda Swifts Academy");
        await SaveParentAsync(App.Services, kmsa, swifts.Id, association.Id);
        await SaveParentAsync(App.Services, kmsa, juniors.Id, swifts.Id);
        await SaveParentAsync(App.Services, kmsa, oldAcademy.Id, swifts.Id);
        await ChangeOrganisationStatusAsync(kmsa, oldAcademy.Id, OrganisationStatus.Archived);
        var senior = await SaveTeamAsync(App.Services, kmsa, "Ntinda Swifts FC", organisationId: swifts.Id);
        await SaveTeamAsync(App.Services, kmsa, "Ntinda Swifts Reserves", organisationId: swifts.Id, status: TeamStatus.Archived);

        var read = (await PublicAsync($"{_organisations}/{swifts.Id}")).Json;

        read.GetProperty("primaryVenue").ValueKind.ShouldBe(JsonValueKind.Null);
        read.GetProperty("parent").GetProperty("id").GetGuid().ShouldBe(association.Id);
        read.GetProperty("children").EnumerateArray().Select(c => c.GetProperty("id").GetGuid()).ShouldBe([juniors.Id]);
        read.GetProperty("teams").EnumerateArray().Select(t => t.GetProperty("id").GetGuid()).ShouldBe([senior.Id]);

        await ChangeOrganisationStatusAsync(kmsa, association.Id, OrganisationStatus.Dissolved);
        (await PublicAsync($"{_organisations}/{swifts.Id}")).Json.GetProperty("parent").GetProperty("status").GetString().ShouldBe("DISSOLVED");

        await ChangeOrganisationStatusAsync(kmsa, association.Id, OrganisationStatus.Archived);
        (await PublicAsync($"{_organisations}/{swifts.Id}")).Json.GetProperty("parent").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    // D15, owner (plan 2026-11-25): the read and the directory link the logo's two public paths, served on the tenant's
    // host only.
    [Fact]
    public async Task An_organisations_logo_is_linked_by_its_public_paths_in_the_read_and_the_directory()
    {
        var (kmsa, registrar) = await PublicTenantAsync("kmsa");
        await PublicTenantAsync("elssc");
        var swifts = await SaveOrganisationAsync(App.Services, kmsa, "Ntinda Swifts SC");
        await OkAsync(await registrar.PutAsync($"{OrganisationPath(swifts.Id)}/logo", Form(TestImages.TransparentPng(800, 400)), Ct));

        var logo = (await PublicAsync($"{_organisations}/{swifts.Id}")).Json.GetProperty("logo");
        var listed = (await PublicAsync(_organisations)).Json.GetProperty("items")[0].GetProperty("logo");

        listed.GetRawText().ShouldBe(logo.GetRawText());
        logo.TryGetProperty("expiresAt", out _).ShouldBeFalse();
        foreach (var property in new[] { "thumbnailUrl", "mediumUrl" })
        {
            var path = logo.GetProperty(property).GetString().ShouldNotBeNull();
            path.ShouldStartWith("/api/public/v1/assets/");
            var served = await PublicAsync(path);
            served.Status.ShouldBe(StatusCodes.Status200OK, property);
            served.Headers.ContentType.ToString().ShouldBe("image/webp");
            (await PublicAsync(path, host: _elsscHost)).ShouldBeProblem(StatusCodes.Status404NotFound, "MEDIA_NOT_FOUND");
        }
    }

    // D17, owner (plan 2026-11-25): ACTIVE by default, one status when asked, ARCHIVED refused; type; the cursor.
    [Fact]
    public async Task The_organisation_directory_lists_active_organisations_by_default_and_pages_them()
    {
        var (kmsa, _) = await PublicTenantAsync("kmsa");
        var names = new[] { "Bunga Breakers FC", "Kisaasi Kestrels SC", "Ntinda Swifts SC", "Seeta Sparrows FC" };
        foreach (var name in names)
        {
            await SaveOrganisationAsync(App.Services, kmsa, name);
        }

        var school = await SaveOrganisationAsync(App.Services, kmsa, "Kireka Hill School", OrganisationType.School);
        var suspended = await SaveOrganisationAsync(App.Services, kmsa, "Kyanja Rovers FC", status: OrganisationStatus.Suspended);
        await SaveOrganisationAsync(App.Services, kmsa, "Bukoto Harriers AC", status: OrganisationStatus.Archived);

        var first = (await PublicAsync($"{_organisations}?type=CLUB&limit=3")).Json;
        var second = (await PublicAsync(
            $"{_organisations}?type=CLUB&limit=3&cursor={Uri.EscapeDataString(first.GetProperty("page").GetProperty("nextCursor").GetString()!)}")).Json;

        Names(first).ShouldBe(names[..3]);
        first.GetProperty("page").GetProperty("hasMore").GetBoolean().ShouldBeTrue();
        Names(second).ShouldBe(names[3..]);
        second.GetProperty("page").GetProperty("hasMore").GetBoolean().ShouldBeFalse();
        Names((await PublicAsync(_organisations)).Json).ShouldBe([names[0], school.DisplayName, .. names[1..]]);
        Names((await PublicAsync($"{_organisations}?status=SUSPENDED")).Json).ShouldBe([suspended.DisplayName]);

        foreach (var query in new[] { "status=ARCHIVED", "status=active", "type=CASTLE", "limit=0", "limit=101", "cursor=nonsense" })
        {
            (await PublicAsync($"{_organisations}?{query}")).ShouldBeProblem(StatusCodes.Status400BadRequest, "VALIDATION_INVALID_QUERY");
        }
    }

    // D16: q matches the display name and the short name; never the legal name, an alias or an identifier.
    [Fact]
    public async Task The_organisation_directory_searches_public_names_only()
    {
        var (kmsa, _) = await PublicTenantAsync("kmsa");
        var swifts = await SaveOrganisationAsync(
            App.Services, kmsa, "Ntinda Swifts SC", legalName: "Northern Wings Holdings Limited", shortName: "NSSC",
            alias: ("Kingfisher Lads", AliasKind.Nickname), identifier: ("FUFA", "NSW-0042"));
        await SaveOrganisationAsync(App.Services, kmsa, "Seeta Sparrows FC");

        foreach (var q in new[] { "ntinda", "swif", "Ntinda Swifts", "nssc" })
        {
            Ids((await PublicAsync($"{_organisations}?q={Uri.EscapeDataString(q)}")).Json).ShouldBe([swifts.Id], q);
        }

        foreach (var q in new[] { "northern wings", "kingfisher", "NSW-0042", "--" })
        {
            Ids((await PublicAsync($"{_organisations}?q={Uri.EscapeDataString(q)}")).Json).ShouldBeEmpty(q);
        }
    }

    // T5 acceptance 1 for venues: by ID or slug, PUBLIC fields only, archived and another tenant's not found.
    [Fact]
    public async Task A_venue_is_read_by_ID_or_slug_with_its_public_fields_and_archived_ones_are_not_found()
    {
        var (kmsa, _) = await PublicTenantAsync("kmsa");
        await PublicTenantAsync("elssc");
        var meadow = await SaveVenueAsync(
            App.Services, kmsa, "Kiwatule Meadow Ground", supportedVariants: [TestballFull],
            alias: ("The Meadow", AliasKind.Nickname), identifier: ("FUFA", "VEN-0007"));
        var closed = await SaveVenueAsync(App.Services, kmsa, "Kisaasi Old Ground", status: VenueStatus.Closed);
        var archived = await SaveVenueAsync(App.Services, kmsa, "Bweyogerere Field", status: VenueStatus.Archived);

        var byId = await PublicAsync($"{_venues}/{meadow.Id}");
        byId.Status.ShouldBe(StatusCodes.Status200OK, byId.Body);
        (await PublicAsync($"{_venues}/{meadow.Slug}")).Body.ShouldBe(byId.Body);
        var read = byId.Json;
        read.GetProperty("name").GetString().ShouldBe("Kiwatule Meadow Ground");
        read.GetProperty("timeZone").GetString().ShouldBe("Africa/Kampala");
        read.GetProperty("supportedVariants").EnumerateArray().Select(v => v.GetString()).ShouldBe([TestballFull]);
        foreach (var field in new[] { "aliases", "externalIdentifiers", "version" })
        {
            read.TryGetProperty(field, out _).ShouldBeFalse(field);
        }

        byId.Headers.CacheControl.ToString().ShouldBe("public, max-age=60");
        (await PublicAsync($"{_venues}/{closed.Slug}")).Json.GetProperty("status").GetString().ShouldBe("CLOSED");
        foreach (var key in new[] { archived.Id.ToString(), archived.Slug, "Kiwatule Meadow" })
        {
            (await PublicAsync($"{_venues}/{key}")).ShouldBeProblem(StatusCodes.Status404NotFound, "VENUE_NOT_FOUND");
        }

        (await PublicAsync($"{_venues}/{meadow.Id}", host: _elsscHost)).ShouldBeProblem(StatusCodes.Status404NotFound, "VENUE_NOT_FOUND");
        (await PublicAsync(_venues, host: _elsscHost)).Json.GetProperty("items").GetArrayLength().ShouldBe(0);
    }

    // D17: ACTIVE by default, one status, ARCHIVED refused, variant, the cursor; q on the name only (D16).
    [Fact]
    public async Task The_venue_directory_filters_pages_and_searches_names_only()
    {
        var (kmsa, _) = await PublicTenantAsync("kmsa");
        var bunga = await SaveVenueAsync(App.Services, kmsa, "Bunga Lakeside Pitch", supportedVariants: [TestballFull]);
        var meadow = await SaveVenueAsync(
            App.Services, kmsa, "Kiwatule Meadow Ground", supportedVariants: [TestballFull],
            alias: ("The Paddock", AliasKind.Nickname), identifier: ("FUFA", "VEN-0007"));
        var hall = await SaveVenueAsync(App.Services, kmsa, "Ntinda Community Hall");
        var unavailable = await SaveVenueAsync(App.Services, kmsa, "Seeta Grounds", status: VenueStatus.TemporarilyUnavailable);
        await SaveVenueAsync(App.Services, kmsa, "Bweyogerere Field", status: VenueStatus.Archived);

        var first = (await PublicAsync($"{_venues}?limit=2")).Json;
        var second = (await PublicAsync(
            $"{_venues}?limit=2&cursor={Uri.EscapeDataString(first.GetProperty("page").GetProperty("nextCursor").GetString()!)}")).Json;

        Ids(first).ShouldBe([bunga.Id, meadow.Id]);
        Ids(second).ShouldBe([hall.Id]);
        Ids((await PublicAsync($"{_venues}?variant={TestballFull}")).Json).ShouldBe([bunga.Id, meadow.Id]);
        Ids((await PublicAsync($"{_venues}?status=TEMPORARILY_UNAVAILABLE")).Json).ShouldBe([unavailable.Id]);
        Ids((await PublicAsync($"{_venues}?q=meadow")).Json).ShouldBe([meadow.Id]);
        Ids((await PublicAsync($"{_venues}?q=kiwatule%20meadw")).Json).ShouldBe([meadow.Id]);
        foreach (var q in new[] { "paddock", "VEN-0007", "--" })
        {
            Ids((await PublicAsync($"{_venues}?q={Uri.EscapeDataString(q)}")).Json).ShouldBeEmpty(q);
        }

        foreach (var query in new[] { "status=ARCHIVED", "variant=not%20a%20code", "limit=101", "cursor=nonsense" })
        {
            (await PublicAsync($"{_venues}?{query}")).ShouldBeProblem(StatusCodes.Status400BadRequest, "VALIDATION_INVALID_QUERY");
        }
    }

    private static string?[] Names(JsonElement page) =>
        [.. page.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("displayName").GetString())];

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

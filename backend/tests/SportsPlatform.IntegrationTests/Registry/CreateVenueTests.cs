using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using SportsPlatform.BuildingBlocks.Auditing;
using SportsPlatform.IntegrationTests.Api;
using SportsPlatform.IntegrationTests.Audit;
using SportsPlatform.IntegrationTests.Identity;
using SportsPlatform.IntegrationTests.Infrastructure;
using SportsPlatform.IntegrationTests.Tenancy;
using SportsPlatform.Modules.Identity.Domain.Roles;
using SportsPlatform.Modules.Registry.Domain.Venues;
using SportsPlatform.Modules.Tenancy.Contracts.Tenants;
using static SportsPlatform.IntegrationTests.Identity.MembershipTestData;
using static SportsPlatform.IntegrationTests.Registry.PeopleRequests;
using static SportsPlatform.IntegrationTests.Registry.RegistryTestData;
using static SportsPlatform.IntegrationTests.Registry.VenueRequests;
using static SportsPlatform.IntegrationTests.Tenancy.TestSportDefinitions;

namespace SportsPlatform.IntegrationTests.Registry;

/// <summary>
/// Creating a venue (S07-T1a; D3, D9, D13, D21): the fields, the refusals on their field, the slug, the duplicate
/// warning with <c>duplicatesReviewed</c> and its audit entry, the read, two creates at once, the roles and the
/// read-only tenant.
/// </summary>
public sealed class CreateVenueTests(PostgresFixture fixture) : DatabaseTest(fixture)
{
    private static readonly string[] _surfaces = ["NATURAL_GRASS", "ARTIFICIAL_TURF"];

    [Fact]
    public async Task A_venue_is_created_with_its_fields_and_read_back()
    {
        await using var app = AppWithTestSports(App);
        var (kmsa, registrar) = await TenantAsync(app, "kmsa", IdentityRoles.Registrar);
        var body = KiwatuleMeadow();
        body["name"] = "  Kiwatule Meadow Ground ";
        body["shortName"] = "Kiwatule Meadow";
        body["addressLine"] = "Kiwatule Road, off Ntinda–Kisaasi Road";
        body["region"] = "Central";
        body["latitude"] = 0.371245m;
        body["longitude"] = 32.618310m;
        body["capacity"] = 1200;
        body["surfaces"] = _surfaces;

        // Published, but not enabled for KMSA: a ground can host a sport the tenant doesn't run yet (D3).
        body["supportedVariants"] = new[] { TestballFull };

        using var response = await registrar.PostAsJsonAsync(VenuesPath, body, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync(Ct));
        var venue = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        var id = venue.GetProperty("id").GetGuid();
        response.Headers.Location!.ToString().ShouldBe(VenuePath(id));
        response.Headers.ETag!.Tag.ShouldBe($"\"{venue.GetProperty("version").GetUInt32()}\"");
        venue.GetProperty("name").GetString().ShouldBe("Kiwatule Meadow Ground");
        venue.GetProperty("slug").GetString().ShouldBe("kiwatule-meadow-ground");
        venue.GetProperty("type").GetString().ShouldBe("SPORTS_GROUND");
        venue.GetProperty("country").GetString().ShouldBe("UG");
        venue.GetProperty("timeZone").GetString().ShouldBe("Africa/Kampala");
        venue.GetProperty("latitude").GetDecimal().ShouldBe(0.371245m);
        venue.GetProperty("longitude").GetDecimal().ShouldBe(32.618310m);
        venue.GetProperty("capacity").GetInt32().ShouldBe(1200);
        venue.GetProperty("surfaces").EnumerateArray().Select(s => s.GetString()).ShouldBe(_surfaces);
        venue.GetProperty("supportedVariants").EnumerateArray().Select(s => s.GetString()).ShouldBe([TestballFull]);
        venue.GetProperty("status").GetString().ShouldBe("ACTIVE");

        var read = await registrar.ReadJsonAsync(VenuePath(id), Ct);
        read.GetRawText().ShouldBe(venue.GetRawText());

        await using var scope = ScopeFor(App.Services, kmsa, out var context);
        var saved = await context.Set<Venue>().Include(v => v.StatusHistory).SingleAsync(Ct);
        (saved.NameKey, saved.CityKey, saved.Source).ShouldBe(("ground kiwatule meadow", "kampala", VenueSource.Manual));
        var first = saved.StatusHistory.ShouldHaveSingleItem();
        (first.FromStatus, first.ToStatus, first.Reason).ShouldBe((null, VenueStatus.Active, null));
    }

    // T1 acceptance: a venue without a time zone, with an unknown zone, with only one coordinate or with an unpublished
    // variant is refused with its field and code.
    [Theory]
    [InlineData("""{"name":"Kiwatule Meadow Ground","type":"FIELD","city":"Kampala"}""", 400, "VALIDATION_INVALID_REQUEST", "timeZone")]
    [InlineData("""{"name":"Kiwatule Meadow Ground","type":"FIELD","city":"Kampala","timeZone":"Africa/Atlantis"}""", 400, "VALIDATION_INVALID_REQUEST", "timeZone")]
    [InlineData("""{"name":"Kiwatule Meadow Ground","type":"FIELD","city":"Kampala","timeZone":"Africa/Kampala","latitude":0.37}""", 400, "VALIDATION_INVALID_REQUEST", "longitude")]
    [InlineData("""{"name":"Kiwatule Meadow Ground","type":"FIELD","city":"Kampala","timeZone":"Africa/Kampala","longitude":32.6}""", 400, "VALIDATION_INVALID_REQUEST", "latitude")]
    [InlineData("""{"name":"Kiwatule Meadow Ground","type":"FIELD","city":"Kampala","timeZone":"Africa/Kampala","latitude":91,"longitude":32.6}""", 400, "VALIDATION_INVALID_REQUEST", "latitude")]
    [InlineData("""{"name":"Kiwatule Meadow Ground","type":"FIELD","city":"Kampala","timeZone":"Africa/Kampala","supportedVariants":["TESTBALL_FULL","NOT_PUBLISHED"]}""", 422, "VENUE_SPORT_UNKNOWN", "supportedVariants[1]")]
    [InlineData("""{"name":" -- ","type":"FIELD","city":"Kampala","timeZone":"Africa/Kampala"}""", 422, "VENUE_NAME_INVALID", "name")]
    [InlineData("""{"name":"Kiwatule Meadow Ground","city":"Kampala","timeZone":"Africa/Kampala"}""", 400, "VALIDATION_INVALID_REQUEST", "type")]
    [InlineData("""{"name":"Kiwatule Meadow Ground","type":"FIELD","timeZone":"Africa/Kampala"}""", 400, "VALIDATION_INVALID_REQUEST", "city")]
    [InlineData("""{"name":"Kiwatule Meadow Ground","type":"FIELD","city":"Kampala","timeZone":"Africa/Kampala","country":"XX"}""", 400, "VALIDATION_INVALID_REQUEST", "country")]
    [InlineData("""{"name":"Kiwatule Meadow Ground","type":"FIELD","city":"Kampala","timeZone":"Africa/Kampala","capacity":200001}""", 400, "VALIDATION_INVALID_REQUEST", "capacity")]
    [InlineData("""{"name":"Kiwatule Meadow Ground","type":"FIELD","city":"Kampala","timeZone":"Africa/Kampala","surfaces":["CLAY","CLAY"]}""", 400, "VALIDATION_INVALID_REQUEST", "surfaces")]
    [InlineData("""{"name":"Kiwatule Meadow Ground","type":"FIELD","city":"Kampala","timeZone":"Africa/Kampala","slug":"0192a1b2-0000-7000-8000-00000000c001"}""", 400, "VALIDATION_INVALID_REQUEST", "slug")]
    [InlineData("""{"name":"Kiwatule Meadow Ground","type":"FIELD","city":"Kampala","timeZone":"Africa/Kampala","slug":"Kiwatule_Meadow"}""", 400, "VALIDATION_INVALID_REQUEST", "slug")]
    [InlineData("""{"name":"Kiwatule Meadow Ground","type":"FIELD","city":"Kampala","timeZone":"Africa/Kampala","duplicateReason":"A second ground"}""", 400, "VALIDATION_INVALID_REQUEST", "duplicateReason")]
    public async Task A_venue_that_cant_be_created_is_refused_on_its_field(string body, int status, string code, string field)
    {
        await using var app = AppWithTestSports(App);
        var (kmsa, registrar) = await TenantAsync(app, "kmsa", IdentityRoles.Registrar);

        using var response = await registrar.PostAsync(VenuesPath, new StringContent(body, System.Text.Encoding.UTF8, "application/json"), Ct);

        var problem = await response.ShouldBeProblemAsync((HttpStatusCode)status, code);
        Fields(problem).ShouldContain((field, code));
        await using var scope = ScopeFor(App.Services, kmsa, out var context);
        (await context.Set<Venue>().CountAsync(Ct)).ShouldBe(0);
    }

    [Fact]
    public async Task A_status_or_unknown_member_is_refused_and_the_country_defaults_to_the_tenants()
    {
        var (_, registrar) = await TenantAsync(App, "kmsa", IdentityRoles.Registrar);
        var withStatus = KiwatuleMeadow();
        withStatus["status"] = "CLOSED";

        using var refused = await registrar.PostAsJsonAsync(VenuesPath, withStatus, Ct);
        await refused.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_MALFORMED_REQUEST");

        var kenyan = KiwatuleMeadow();
        kenyan["name"] = "Lakeside Grounds";
        kenyan["city"] = "Kisumu";
        kenyan["country"] = "KE";
        kenyan["timeZone"] = "Africa/Nairobi";
        (await CreateVenueAsync(registrar, KiwatuleMeadow())).GetProperty("country").GetString().ShouldBe("UG");
        (await CreateVenueAsync(registrar, kenyan)).GetProperty("country").GetString().ShouldBe("KE");
    }

    // D9: a slug is proposed from the name, with -2, -3 on a clash; one sent that another venue has is refused.
    [Fact]
    public async Task Slugs_are_proposed_from_the_name_and_unique_in_the_tenant()
    {
        var (kmsa, registrar) = await TenantAsync(App, "kmsa", IdentityRoles.Registrar);
        var (_, elsscRegistrar) = await TenantAsync(App, "elssc", IdentityRoles.Registrar);
        var reviewed = KiwatuleMeadow();
        reviewed["duplicatesReviewed"] = true;

        (await CreateVenueAsync(registrar, KiwatuleMeadow())).GetProperty("slug").GetString().ShouldBe("kiwatule-meadow-ground");
        (await CreateVenueAsync(registrar, reviewed)).GetProperty("slug").GetString().ShouldBe("kiwatule-meadow-ground-2");
        (await CreateVenueAsync(registrar, reviewed)).GetProperty("slug").GetString().ShouldBe("kiwatule-meadow-ground-3");

        // Another tenant's slugs are its own.
        (await CreateVenueAsync(elsscRegistrar, KiwatuleMeadow())).GetProperty("slug").GetString().ShouldBe("kiwatule-meadow-ground");

        // Accents and punctuation go; a slug that is sent is kept.
        var naguru = KiwatuleMeadow();
        naguru["name"] = "Métro Indoor Arena (Naguru)";
        naguru["type"] = "ARENA";
        (await CreateVenueAsync(registrar, naguru)).GetProperty("slug").GetString().ShouldBe("metro-indoor-arena-naguru");
        var withSlug = KiwatuleMeadow();
        withSlug["name"] = "Bukoto Community Stadium";
        withSlug["slug"] = "bukoto";
        (await CreateVenueAsync(registrar, withSlug)).GetProperty("slug").GetString().ShouldBe("bukoto");

        var taken = KiwatuleMeadow();
        taken["name"] = "Bukoto Grounds";
        taken["city"] = "Wakiso";
        taken["slug"] = "bukoto";
        using var refused = await registrar.PostAsJsonAsync(VenuesPath, taken, Ct);
        Fields(await refused.ShouldBeProblemAsync(HttpStatusCode.UnprocessableEntity, "VENUE_SLUG_TAKEN"))
            .ShouldBe([("slug", "VENUE_SLUG_TAKEN")]);

        await using var scope = ScopeFor(App.Services, kmsa, out var context);
        (await context.Set<Venue>().CountAsync(Ct)).ShouldBe(5);
    }

    // T1 acceptance: the same name in the same city is LIKELY; duplicatesReviewed creates it, and the audit entry
    // names the candidate.
    [Fact]
    public async Task A_likely_duplicate_needs_duplicatesReviewed_and_the_audit_entry_names_it()
    {
        var (kmsa, registrar) = await TenantAsync(App, "kmsa", IdentityRoles.Registrar);
        var existing = await SaveVenueAsync(App.Services, kmsa, "Kiwatule Meadow Ground");
        var body = KiwatuleMeadow();
        body["name"] = "Ground, Kiwatule Meadow";

        using var refused = await registrar.PostAsJsonAsync(VenuesPath, body, Ct);

        var problem = await refused.ShouldBeProblemAsync(HttpStatusCode.UnprocessableEntity, "VENUE_DUPLICATE_REVIEW_REQUIRED");
        Fields(problem).ShouldBe([("duplicatesReviewed", "VENUE_DUPLICATE_REVIEW_REQUIRED")]);
        var candidate = problem.GetProperty("candidates").EnumerateArray().ShouldHaveSingleItem();
        candidate.GetProperty("venueId").GetGuid().ShouldBe(existing.Id);
        candidate.GetProperty("name").GetString().ShouldBe("Kiwatule Meadow Ground");
        candidate.GetProperty("confidence").GetString().ShouldBe("LIKELY");
        candidate.GetProperty("evidence").EnumerateArray().Select(e => e.GetProperty("code").GetString())
            .ShouldBe(["SAME_NAME_KEY", "SAME_CITY"]);

        body["duplicatesReviewed"] = true;
        body["duplicateReason"] = "  The school's ground next to the meadow ";
        var created = await CreateVenueAsync(registrar, body);

        await AccountTestData.DeliverOutboxAsync(App.Services, Ct);
        var entries = await AuditTestData.EntriesAsync(App.Services, Ct);
        entries.ShouldAllBe(entry => entry.ActionCode == "venue.create" && entry.EntityType == "Venue");
        var refusedEntry = entries.Single(e => e.ErrorCode == "VENUE_DUPLICATE_REVIEW_REQUIRED");
        var success = entries.Single(e => e.Outcome == AuditOutcome.Success);
        (refusedEntry.EntityId, refusedEntry.Reason).ShouldBe((null, null));
        (success.EntityId, success.Reason).ShouldBe((created.GetProperty("id").GetGuid(), "The school's ground next to the meadow"));
        foreach (var entry in new[] { refusedEntry, success })
        {
            var audited = JsonDocument.Parse(entry.Metadata!).RootElement.GetProperty("candidates").EnumerateArray().ShouldHaveSingleItem();
            audited.EnumerateObject().Select(property => property.Name).ShouldBe(["venueId", "confidence"]);
            (audited.GetProperty("venueId").GetGuid(), audited.GetProperty("confidence").GetString()).ShouldBe((existing.Id, "LIKELY"));
        }
    }

    [Fact]
    public async Task A_venue_nobody_matches_is_created_without_review_and_its_audit_entry_has_no_candidate()
    {
        var (kmsa, registrar) = await TenantAsync(App, "kmsa", IdentityRoles.Registrar);
        await SaveVenueAsync(App.Services, kmsa, "Kiwatule Meadow Ground", city: "Mukono");

        var created = await CreateVenueAsync(registrar, KiwatuleMeadow());

        await AccountTestData.DeliverOutboxAsync(App.Services, Ct);
        var entry = (await AuditTestData.EntriesAsync(App.Services, Ct)).ShouldHaveSingleItem();
        (entry.Outcome, entry.EntityId).ShouldBe((AuditOutcome.Success, created.GetProperty("id").GetGuid()));
        JsonDocument.Parse(entry.Metadata!).RootElement.GetProperty("candidates").GetArrayLength().ShouldBe(0);
    }

    // D21: two creates of one venue queue on the lock; the second finds the first's venue and is warned.
    [Fact]
    public async Task Two_creates_of_the_same_venue_at_once_make_one_venue()
    {
        var (kmsa, registrar) = await TenantAsync(App, "kmsa", IdentityRoles.Registrar);

        var responses = await Task.WhenAll(
            registrar.PostAsJsonAsync(VenuesPath, KiwatuleMeadow(), Ct),
            registrar.PostAsJsonAsync(VenuesPath, KiwatuleMeadow(), Ct));

        responses.Select(r => r.StatusCode).OrderBy(s => s)
            .ShouldBe([HttpStatusCode.Created, HttpStatusCode.UnprocessableEntity]);
        await responses.Single(r => r.StatusCode == HttpStatusCode.UnprocessableEntity)
            .ShouldBeProblemAsync(HttpStatusCode.UnprocessableEntity, "VENUE_DUPLICATE_REVIEW_REQUIRED");
        foreach (var response in responses)
        {
            response.Dispose();
        }

        await using var scope = ScopeFor(App.Services, kmsa, out var context);
        (await context.Set<Venue>().CountAsync(Ct)).ShouldBe(1);
    }

    [Fact]
    public async Task Another_tenants_venue_is_not_found_and_never_a_candidate()
    {
        var (kmsa, registrar) = await TenantAsync(App, "kmsa", IdentityRoles.Registrar);
        var (_, elsscOwner) = await TenantAsync(App, "elssc", IdentityRoles.TenantOwner);
        var kiwatule = await SaveVenueAsync(App.Services, kmsa, "Kiwatule Meadow Ground");

        using var read = await elsscOwner.GetAsync(VenuePath(kiwatule.Id), Ct);
        await read.ShouldBeProblemAsync(HttpStatusCode.NotFound, "VENUE_NOT_FOUND");
        (await CheckVenueAsync(elsscOwner, new { name = "Kiwatule Meadow Ground", city = "Kampala" }))
            .GetProperty("candidates").GetArrayLength().ShouldBe(0);
        (await CreateVenueAsync(elsscOwner, KiwatuleMeadow())).GetProperty("slug").GetString().ShouldBe("kiwatule-meadow-ground");
        (await registrar.ReadJsonAsync(VenuePath(kiwatule.Id), Ct)).GetProperty("name").GetString().ShouldBe("Kiwatule Meadow Ground");
    }

    // D20: creating venues (and the check) is the owner's, the admin's and the registrar's.
    [Theory]
    [InlineData(IdentityRoles.TenantOwner, true)]
    [InlineData(IdentityRoles.TenantAdmin, true)]
    [InlineData(IdentityRoles.Registrar, true)]
    [InlineData(IdentityRoles.CompetitionManager, false)]
    [InlineData(IdentityRoles.ResultsOfficer, false)]
    [InlineData(IdentityRoles.DataEntry, false)]
    [InlineData(IdentityRoles.Viewer, false)]
    [InlineData(IdentityRoles.Scorer, false)]
    public async Task Only_those_who_change_venues_may_create_and_check_them(string role, bool allowed)
    {
        var (_, client) = await TenantAsync(App, "kmsa", role);

        using var check = await client.PostAsJsonAsync(VenueDuplicateCheckPath, new { name = "Kiwatule Meadow Ground", city = "Kampala" }, Ct);
        using var create = await client.PostAsJsonAsync(VenuesPath, KiwatuleMeadow(), Ct);

        if (allowed)
        {
            check.StatusCode.ShouldBe(HttpStatusCode.OK);
            create.StatusCode.ShouldBe(HttpStatusCode.Created);
        }
        else
        {
            await check.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "AUTH_FORBIDDEN");
            await create.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "AUTH_FORBIDDEN");
        }
    }

    // D20: reading venues is every tenant-wide role's that reads people; not the scorer's.
    [Theory]
    [InlineData(IdentityRoles.TenantOwner, true)]
    [InlineData(IdentityRoles.TenantAdmin, true)]
    [InlineData(IdentityRoles.Registrar, true)]
    [InlineData(IdentityRoles.CompetitionManager, true)]
    [InlineData(IdentityRoles.ResultsOfficer, true)]
    [InlineData(IdentityRoles.DataEntry, true)]
    [InlineData(IdentityRoles.Viewer, true)]
    [InlineData(IdentityRoles.Scorer, false)]
    public async Task Every_role_that_reads_people_reads_venues(string role, bool allowed)
    {
        var (kmsa, client) = await TenantAsync(App, "kmsa", role);
        var kiwatule = await SaveVenueAsync(App.Services, kmsa, "Kiwatule Meadow Ground");

        using var response = await client.GetAsync(VenuePath(kiwatule.Id), Ct);

        if (allowed)
        {
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
        }
        else
        {
            await response.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "AUTH_FORBIDDEN");
        }
    }

    [Fact]
    public async Task A_read_only_tenant_reads_its_venues_but_creates_none()
    {
        App.Authorization.UseIdentityRoles();
        var kmsa = await TenantTestData.SaveTenantAsync(
            App.Services, "kmsa", TenantStatus.Active, publicEnabled: false, withDomain: true, withSubscription: false, Ct);
        await TenantTestData.SavePeriodAsync(App.Services, kmsa, new DateOnly(2025, 1, 1), new DateOnly(2025, 12, 31), Ct, cancelReason: "Pilot paused");
        var registrar = await MemberAsync(kmsa, "brian.ssemakula@kmsa.example", IdentityRoles.Registrar);
        var kiwatule = await SaveVenueAsync(App.Services, kmsa, "Kiwatule Meadow Ground");

        using var create = await registrar.PostAsJsonAsync(VenuesPath, KiwatuleMeadow(), Ct);
        using var read = await registrar.GetAsync(VenuePath(kiwatule.Id), Ct);

        await create.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "TENANT_READ_ONLY");
        read.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    /// <summary>An active tenant (country UG, time zone Africa/Kampala) and a client of a member with <paramref name="role"/> tenant-wide.</summary>
    private async Task<(Guid Tenant, HttpClient Client)> TenantAsync(WebApplicationFactory<Program> app, string slug, string role)
    {
        App.Authorization.UseIdentityRoles();
        var tenant = await TenantTestData.SaveTenantAsync(App.Services, slug, TenantStatus.Active, Ct);
        var member = await CreateMemberAsync(App.Services, tenant, MailboxOf(role, $"{slug}.example"), Ct, TenantWide(role));
        return (tenant, ClientFor(app, member.UserId, tenant));
    }

    private async Task<HttpClient> MemberAsync(Guid tenant, string email, string role)
    {
        var member = await CreateMemberAsync(App.Services, tenant, email, Ct, TenantWide(role));
        return App.ClientFor(member.UserId, tenant);
    }

    private HttpClient ClientFor(WebApplicationFactory<Program> app, Guid userId, Guid tenantId)
    {
        if (ReferenceEquals(app, App))
        {
            return App.ClientFor(userId, tenantId);
        }

        var client = app.CreateClient().AsUser(userId);
        client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());
        return client;
    }
}

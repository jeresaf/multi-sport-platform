using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
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

namespace SportsPlatform.IntegrationTests.Registry;

/// <summary>
/// Changing a venue (S07-T1b-1; D9, D11, D13, D21): a whole replacement in every status, the slug kept or changed, the
/// duplicate warning again when the name, city or coordinates change (the venue itself left out) with its audit entry,
/// the version, the refusals shared with create, the roles and the read-only tenant.
/// </summary>
public sealed class UpdateVenueTests(PostgresFixture fixture) : DatabaseTest(fixture)
{
    private static readonly string[] _surfaces = ["NATURAL_GRASS"];

    [Fact]
    public async Task An_update_replaces_the_fields_keeps_the_slug_and_defaults_the_country()
    {
        var (_, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var body = KiwatuleMeadow();
        body["shortName"] = "Kiwatule Meadow";
        body["region"] = "Central";
        body["country"] = "KE";
        body["latitude"] = 0.371245m;
        body["longitude"] = 32.618310m;
        body["capacity"] = 1200;
        var created = await CreateVenueAsync(registrar, body);
        var id = created.GetProperty("id").GetGuid();

        // Left out: the short name, the region, the country (the tenant's) and the slug (kept); the coordinates are unchanged.
        var changed = KiwatuleMeadow();
        changed["name"] = "Kiwatule Meadow Ground";
        changed["capacity"] = 1500;
        changed["surfaces"] = _surfaces;
        changed["latitude"] = 0.371245m;
        changed["longitude"] = 32.618310m;
        using var response = await PutAsync(registrar, VenuePath(id), changed, $"\"{created.GetProperty("version").GetUInt32()}\"");

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));
        var venue = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        response.Headers.ETag!.Tag.ShouldBe($"\"{venue.GetProperty("version").GetUInt32()}\"");
        venue.GetProperty("version").GetUInt32().ShouldNotBe(created.GetProperty("version").GetUInt32());
        venue.GetProperty("slug").GetString().ShouldBe("kiwatule-meadow-ground");
        venue.GetProperty("country").GetString().ShouldBe("UG");
        venue.GetProperty("capacity").GetInt32().ShouldBe(1500);
        venue.GetProperty("surfaces").EnumerateArray().Select(s => s.GetString()).ShouldBe(_surfaces);
        foreach (var cleared in new[] { "shortName", "region" })
        {
            venue.GetProperty(cleared).ValueKind.ShouldBe(JsonValueKind.Null, cleared);
        }

        (await registrar.ReadJsonAsync(VenuePath(id), Ct)).GetRawText().ShouldBe(venue.GetRawText());

        // Nothing the warning compares changed: no warning ran, and the entry has no metadata.
        await AccountTestData.DeliverOutboxAsync(App.Services, Ct);
        var entry = (await AuditTestData.EntriesAsync(App.Services, Ct)).Single(e => e.ActionCode == "venue.update");
        (entry.Outcome, entry.EntityType, entry.EntityId, entry.Reason, entry.Metadata).ShouldBe((AuditOutcome.Success, "Venue", id, null, null));
    }

    [Fact]
    public async Task A_slug_can_be_changed_to_a_free_one_but_not_to_another_venues()
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var kiwatule = await SaveVenueAsync(App.Services, kmsa, "Kiwatule Meadow Ground");
        var bukoto = await SaveVenueAsync(App.Services, kmsa, "Bukoto Community Stadium");

        var body = KiwatuleMeadow();
        body["slug"] = bukoto.Slug;
        using (var taken = await registrar.PutAsJsonAsync(VenuePath(kiwatule.Id), body, Ct))
        {
            var problem = await taken.ShouldBeProblemAsync(HttpStatusCode.UnprocessableEntity, "VENUE_SLUG_TAKEN");
            Fields(problem).ShouldBe([("slug", "VENUE_SLUG_TAKEN")]);
        }

        body["slug"] = Guid.NewGuid().ToString();
        using (var uuid = await registrar.PutAsJsonAsync(VenuePath(kiwatule.Id), body, Ct))
        {
            var problem = await uuid.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_INVALID_REQUEST");
            Fields(problem).ShouldBe([("slug", "VALIDATION_INVALID_REQUEST")]);
        }

        // The venue's own slug is not "taken".
        body["slug"] = kiwatule.Slug;
        (await UpdateVenueAsync(registrar, kiwatule.Id, body)).GetProperty("slug").GetString().ShouldBe(kiwatule.Slug);

        body["slug"] = "kiwatule-meadow";
        (await UpdateVenueAsync(registrar, kiwatule.Id, body)).GetProperty("slug").GetString().ShouldBe("kiwatule-meadow");
    }

    // D13 on update: a new name in the city of another venue is LIKELY; the venue itself is never a candidate, and
    // duplicatesReviewed saves it with the candidates and the reason in the audit entry.
    [Fact]
    public async Task A_changed_name_that_looks_like_another_venue_needs_duplicatesReviewed()
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var bukoto = await SaveVenueAsync(App.Services, kmsa, "Bukoto Community Stadium");
        var kiwatule = await SaveVenueAsync(App.Services, kmsa, "Kiwatule Meadow Ground");
        var body = KiwatuleMeadow();
        body["name"] = "Stadium, Bukoto Community";

        using (var refused = await registrar.PutAsJsonAsync(VenuePath(kiwatule.Id), body, Ct))
        {
            var problem = await refused.ShouldBeProblemAsync(HttpStatusCode.UnprocessableEntity, "VENUE_DUPLICATE_REVIEW_REQUIRED");
            Fields(problem).ShouldBe([("duplicatesReviewed", "VENUE_DUPLICATE_REVIEW_REQUIRED")]);
            Candidates(problem).ShouldBe([(bukoto.Id, "LIKELY")]);
        }

        (await registrar.ReadJsonAsync(VenuePath(kiwatule.Id), Ct)).GetProperty("name").GetString().ShouldBe("Kiwatule Meadow Ground");

        body["duplicatesReviewed"] = true;
        body["duplicateReason"] = " The old name of the Kiwatule ground ";
        (await UpdateVenueAsync(registrar, kiwatule.Id, body)).GetProperty("name").GetString().ShouldBe("Stadium, Bukoto Community");

        await AccountTestData.DeliverOutboxAsync(App.Services, Ct);
        var entries = (await AuditTestData.EntriesAsync(App.Services, Ct)).Where(e => e.ActionCode == "venue.update").ToList();
        entries.ShouldAllBe(entry => entry.EntityId == kiwatule.Id);
        var success = entries.Single(e => e.Outcome == AuditOutcome.Success);
        success.Reason.ShouldBe("The old name of the Kiwatule ground");
        foreach (var entry in entries)
        {
            var audited = JsonDocument.Parse(entry.Metadata!).RootElement.GetProperty("candidates").EnumerateArray().ShouldHaveSingleItem();
            (audited.GetProperty("venueId").GetGuid(), audited.GetProperty("confidence").GetString()).ShouldBe((bukoto.Id, "LIKELY"));
        }
    }

    // D13: only the name key, the city and the coordinates are compared; a venue that already looks like another one
    // takes any other change without a warning, and moving it within 200 m of a venue it shares nothing else with is
    // warned on the distance alone.
    [Fact]
    public async Task Only_a_change_to_a_compared_field_runs_the_warning()
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        await SaveVenueAsync(App.Services, kmsa, "Kiwatule Meadow Ground");
        var ntinda = await SaveVenueAsync(App.Services, kmsa, "Ntinda View Ground", city: "Wakiso", location: new GeoPoint(0.371245m, 32.618310m));
        var kiwatule = await SaveVenueAsync(App.Services, kmsa, "Kiwatule Meadow Ground");

        var body = KiwatuleMeadow();
        body["name"] = "kiwatule  meadow GROUND";
        body["capacity"] = 800;
        (await UpdateVenueAsync(registrar, kiwatule.Id, body)).GetProperty("capacity").GetInt32().ShouldBe(800);

        // About 84 m north of the Ntinda ground; the other Kiwatule venue is a candidate by name and city as before.
        body["latitude"] = 0.372000m;
        body["longitude"] = 32.618310m;
        using var moved = await registrar.PutAsJsonAsync(VenuePath(kiwatule.Id), body, Ct);
        var problem = await moved.ShouldBeProblemAsync(HttpStatusCode.UnprocessableEntity, "VENUE_DUPLICATE_REVIEW_REQUIRED");
        var near = problem.GetProperty("candidates").EnumerateArray().Single(c => c.GetProperty("venueId").GetGuid() == ntinda.Id);
        near.GetProperty("confidence").GetString().ShouldBe("LIKELY");
        near.GetProperty("evidence").EnumerateArray().Select(e => e.GetProperty("code").GetString()).ShouldBe(["NEARBY"]);
    }

    // D21 on update (review #118 R1): a create and a rename to the same venue queue on the lock; the second finds the
    // first and is warned, whichever comes first.
    [Fact]
    public async Task A_create_and_a_rename_to_the_same_venue_at_once_make_one_such_venue()
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var kisaasi = await SaveVenueAsync(App.Services, kmsa, "Kisaasi Recreation Ground");
        var rename = KiwatuleMeadow();
        rename["name"] = "Bukoto Community Stadium";
        var create = KiwatuleMeadow();
        create["name"] = "Bukoto Community Stadium";

        var responses = await Task.WhenAll(
            registrar.PutAsJsonAsync(VenuePath(kisaasi.Id), rename, Ct),
            registrar.PostAsJsonAsync(VenuesPath, create, Ct));

        var refused = responses.Where(r => r.StatusCode == HttpStatusCode.UnprocessableEntity).ShouldHaveSingleItem();
        responses.Count(r => r.IsSuccessStatusCode).ShouldBe(1);
        await refused.ShouldBeProblemAsync(HttpStatusCode.UnprocessableEntity, "VENUE_DUPLICATE_REVIEW_REQUIRED");
        foreach (var response in responses)
        {
            response.Dispose();
        }

        await using var scope = ScopeFor(App.Services, kmsa, out var context);
        (await context.Set<Venue>().CountAsync(v => v.NameKey == Venue.NameKeyOf("Bukoto Community Stadium"), Ct)).ShouldBe(1);
    }

    [Fact]
    public async Task An_archived_or_closed_venue_can_still_be_corrected()
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var archived = await SaveVenueAsync(App.Services, kmsa, "Kiwatule Meadow Ground", status: VenueStatus.Archived);
        var closed = await SaveVenueAsync(App.Services, kmsa, "Bukoto Community Stadium", status: VenueStatus.Closed);

        var body = KiwatuleMeadow();
        body["capacity"] = 300;
        var corrected = await UpdateVenueAsync(registrar, archived.Id, body);
        body["name"] = "Bukoto Community Stadium";
        var reopened = await UpdateVenueAsync(registrar, closed.Id, body);

        (corrected.GetProperty("status").GetString(), corrected.GetProperty("capacity").GetInt32()).ShouldBe(("ARCHIVED", 300));
        reopened.GetProperty("status").GetString().ShouldBe("CLOSED");
    }

    [Fact]
    public async Task A_stale_version_is_a_venue_conflict_and_an_unknown_venue_is_not_found()
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var (_, elsscOwner) = await TenantAsync("elssc", IdentityRoles.TenantOwner);
        var created = await CreateVenueAsync(registrar, KiwatuleMeadow());
        var id = created.GetProperty("id").GetGuid();
        var version = created.GetProperty("version").GetUInt32();
        var body = KiwatuleMeadow();
        body["capacity"] = 100;
        await UpdateVenueAsync(registrar, id, body);

        using (var stale = await PutAsync(registrar, VenuePath(id), body, $"\"{version}\""))
        {
            await stale.ShouldBeProblemAsync(HttpStatusCode.Conflict, "VENUE_CONCURRENCY_CONFLICT");
        }

        body["version"] = version;
        using (var staleBody = await registrar.PutAsJsonAsync(VenuePath(id), body, Ct))
        {
            await staleBody.ShouldBeProblemAsync(HttpStatusCode.Conflict, "VENUE_CONCURRENCY_CONFLICT");
        }

        body.Remove("version");
        using (var unknown = await registrar.PutAsJsonAsync(VenuePath(Guid.NewGuid()), body, Ct))
        {
            await unknown.ShouldBeProblemAsync(HttpStatusCode.NotFound, "VENUE_NOT_FOUND");
        }

        using (var otherTenants = await elsscOwner.PutAsJsonAsync(VenuePath(id), body, Ct))
        {
            await otherTenants.ShouldBeProblemAsync(HttpStatusCode.NotFound, "VENUE_NOT_FOUND");
        }

        await using var scope = ScopeFor(App.Services, kmsa, out var context);
        (await context.Set<Venue>().SingleAsync(v => v.Id == id, Ct)).Capacity.ShouldBe(100);
    }

    // The details' rules are create's (D3), on their field.
    [Theory]
    [InlineData("""{"name":"Kiwatule Meadow Ground","type":"FIELD","city":"Kampala"}""", 400, "VALIDATION_INVALID_REQUEST", "timeZone")]
    [InlineData("""{"name":"Kiwatule Meadow Ground","type":"FIELD","city":"Kampala","timeZone":"Africa/Kampala","latitude":0.37}""", 400, "VALIDATION_INVALID_REQUEST", "longitude")]
    [InlineData("""{"name":"Kiwatule Meadow Ground","type":"FIELD","city":"Kampala","timeZone":"Africa/Kampala","supportedVariants":["NOT_PUBLISHED"]}""", 422, "VENUE_SPORT_UNKNOWN", "supportedVariants[0]")]
    [InlineData("""{"name":" -- ","type":"FIELD","city":"Kampala","timeZone":"Africa/Kampala"}""", 422, "VENUE_NAME_INVALID", "name")]
    [InlineData("""{"name":"Kiwatule Meadow Ground","type":"FIELD","city":"Kampala","timeZone":"Africa/Kampala","duplicateReason":"Reviewed"}""", 400, "VALIDATION_INVALID_REQUEST", "duplicateReason")]
    [InlineData("""{"name":"Kiwatule Meadow Ground","type":"FIELD","city":"Kampala","timeZone":"Africa/Kampala","status":"CLOSED"}""", 400, "VALIDATION_MALFORMED_REQUEST", null)]
    public async Task A_change_that_cant_be_saved_is_refused_on_its_field(string body, int status, string code, string? field)
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var kiwatule = await SaveVenueAsync(App.Services, kmsa, "Kiwatule Meadow Ground");

        using var content = new StringContent(body, System.Text.Encoding.UTF8, "application/json");
        using var response = await registrar.PutAsync(VenuePath(kiwatule.Id), content, Ct);

        var problem = await response.ShouldBeProblemAsync((HttpStatusCode)status, code);
        if (field is not null)
        {
            Fields(problem).ShouldContain((field, code));
        }
    }

    // D20: changing venues is the owner's, the admin's and the registrar's.
    [Theory]
    [InlineData(IdentityRoles.TenantOwner, true)]
    [InlineData(IdentityRoles.TenantAdmin, true)]
    [InlineData(IdentityRoles.Registrar, true)]
    [InlineData(IdentityRoles.CompetitionManager, false)]
    [InlineData(IdentityRoles.ResultsOfficer, false)]
    [InlineData(IdentityRoles.DataEntry, false)]
    [InlineData(IdentityRoles.Viewer, false)]
    [InlineData(IdentityRoles.Scorer, false)]
    public async Task Only_those_who_change_venues_may_update_them(string role, bool allowed)
    {
        var (kmsa, client) = await TenantAsync("kmsa", role);
        var kiwatule = await SaveVenueAsync(App.Services, kmsa, "Kiwatule Meadow Ground");

        using var response = await client.PutAsJsonAsync(VenuePath(kiwatule.Id), KiwatuleMeadow(), Ct);

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
    public async Task A_read_only_tenant_changes_no_venue()
    {
        App.Authorization.UseIdentityRoles();
        var kmsa = await TenantTestData.SaveTenantAsync(
            App.Services, "kmsa", TenantStatus.Active, publicEnabled: false, withDomain: true, withSubscription: false, Ct);
        await TenantTestData.SavePeriodAsync(App.Services, kmsa, new DateOnly(2025, 1, 1), new DateOnly(2025, 12, 31), Ct, cancelReason: "Pilot paused");
        var member = await CreateMemberAsync(App.Services, kmsa, "brian.ssemakula@kmsa.example", Ct, TenantWide(IdentityRoles.Registrar));
        var registrar = App.ClientFor(member.UserId, kmsa);
        var kiwatule = await SaveVenueAsync(App.Services, kmsa, "Kiwatule Meadow Ground");

        using var response = await registrar.PutAsJsonAsync(VenuePath(kiwatule.Id), KiwatuleMeadow(), Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "TENANT_READ_ONLY");
    }

    private static async Task<JsonElement> UpdateVenueAsync(HttpClient client, Guid venueId, object body)
    {
        using var response = await client.PutAsJsonAsync(VenuePath(venueId), body, Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));
        return await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
    }

    private static async Task<HttpResponseMessage> PutAsync(HttpClient client, string path, object body, string ifMatch)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, path) { Content = JsonContent.Create(body) };
        request.Headers.TryAddWithoutValidation("If-Match", ifMatch);
        return await client.SendAsync(request, Ct);
    }

    /// <summary>An active tenant (country UG, time zone Africa/Kampala) and a client of a member with <paramref name="role"/> tenant-wide.</summary>
    private async Task<(Guid Tenant, HttpClient Client)> TenantAsync(string slug, string role)
    {
        App.Authorization.UseIdentityRoles();
        var tenant = await TenantTestData.SaveTenantAsync(App.Services, slug, TenantStatus.Active, Ct);
        var member = await CreateMemberAsync(App.Services, tenant, MailboxOf(role, $"{slug}.example"), Ct, TenantWide(role));
        return (tenant, App.ClientFor(member.UserId, tenant));
    }
}

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Auditing;
using SportsPlatform.IntegrationTests.Api;
using SportsPlatform.IntegrationTests.Audit;
using SportsPlatform.IntegrationTests.Identity;
using SportsPlatform.IntegrationTests.Infrastructure;
using SportsPlatform.IntegrationTests.Tenancy;
using SportsPlatform.Modules.Identity.Domain.Roles;
using SportsPlatform.Modules.Registry.Domain.Organisations;
using SportsPlatform.Modules.Registry.Domain.Venues;
using SportsPlatform.Modules.Tenancy.Contracts.Tenants;
using static SportsPlatform.IntegrationTests.Identity.MembershipTestData;
using static SportsPlatform.IntegrationTests.Registry.OrganisationRequests;
using static SportsPlatform.IntegrationTests.Registry.PeopleRequests;
using static SportsPlatform.IntegrationTests.Registry.RegistryTestData;
using static SportsPlatform.IntegrationTests.Registry.VenueRequests;

namespace SportsPlatform.IntegrationTests.Registry;

/// <summary>
/// Changing an organisation (S07-T2b-1; D4, D6, D9, D11, D13, D21): a whole replacement in every status, the slug kept or
/// changed, the primary venue checked only when it changes, the duplicate warning again when a name, the type or the
/// city changes (the organisation itself left out) with its audit entry, the version, the refusals shared with create,
/// the roles and the read-only tenant.
/// </summary>
public sealed class UpdateOrganisationTests(PostgresFixture fixture) : DatabaseTest(fixture)
{
    [Fact]
    public async Task An_update_replaces_the_fields_keeps_the_slug_and_defaults_the_country()
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var venue = await SaveVenueAsync(App.Services, kmsa, "Kiwatule Meadow Ground");
        var body = NtindaSwifts();
        body["legalName"] = "Ntinda Swifts Sports Club Limited";
        body["shortName"] = "Ntinda Swifts";
        body["country"] = "KE";
        body["city"] = "Kampala";
        body["foundedYear"] = 1998;
        body["website"] = "https://ntinda-swifts.example";
        var created = await CreateOrganisationAsync(registrar, body);
        var id = created.GetProperty("id").GetGuid();

        // Left out: the legal and short names, the country (the tenant's), the website and the slug (kept).
        var changed = NtindaSwifts();
        changed["city"] = "Kampala";
        changed["foundedYear"] = 1999;
        changed["email"] = "secretary@ntinda-swifts.example";
        changed["phone"] = "+256 414 000 111";
        changed["description"] = "A multi-sport club in Ntinda.";
        changed["primaryVenueId"] = venue.Id;
        using var response = await PutAsync(registrar, OrganisationPath(id), changed, $"\"{created.GetProperty("version").GetUInt32()}\"");

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));
        var organisation = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        response.Headers.ETag!.Tag.ShouldBe($"\"{organisation.GetProperty("version").GetUInt32()}\"");
        organisation.GetProperty("version").GetUInt32().ShouldNotBe(created.GetProperty("version").GetUInt32());
        organisation.GetProperty("slug").GetString().ShouldBe(created.GetProperty("slug").GetString());
        organisation.GetProperty("country").GetString().ShouldBe("UG");
        organisation.GetProperty("foundedYear").GetInt32().ShouldBe(1999);
        organisation.GetProperty("email").GetString().ShouldBe("secretary@ntinda-swifts.example");
        organisation.GetProperty("primaryVenue").GetProperty("id").GetGuid().ShouldBe(venue.Id);
        foreach (var cleared in new[] { "legalName", "shortName", "website" })
        {
            organisation.GetProperty(cleared).ValueKind.ShouldBe(JsonValueKind.Null, cleared);
        }

        (await registrar.ReadJsonAsync(OrganisationPath(id), Ct)).GetRawText().ShouldBe(organisation.GetRawText());

        // The legal and short names were cleared (keys that only shrink the comparison), so the warning ran and found none.
        await AccountTestData.DeliverOutboxAsync(App.Services, Ct);
        var entry = (await AuditTestData.EntriesAsync(App.Services, Ct)).Single(e => e.ActionCode == "organisation.update");
        (entry.Outcome, entry.EntityType, entry.EntityId, entry.Reason).ShouldBe((AuditOutcome.Success, "Organisation", id, null));
    }

    [Fact]
    public async Task A_change_to_no_compared_field_runs_no_warning_and_audits_no_candidates()
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);

        // Two records that already look alike (saved without the warning): a correction to one isn't warned.
        await SaveOrganisationAsync(App.Services, kmsa, "Ntinda Swifts SC");
        var ntinda = await SaveOrganisationAsync(App.Services, kmsa, "Ntinda Swifts SC");
        var body = NtindaSwifts();
        body["displayName"] = "NTINDA  swifts SC";
        body["city"] = "kampala";
        body["description"] = "Recorded twice: see the other record.";

        var updated = await UpdateOrganisationAsync(registrar, ntinda.Id, body);

        updated.GetProperty("displayName").GetString().ShouldBe("NTINDA  swifts SC");
        await AccountTestData.DeliverOutboxAsync(App.Services, Ct);
        var entry = (await AuditTestData.EntriesAsync(App.Services, Ct)).Single(e => e.ActionCode == "organisation.update");
        entry.Metadata.ShouldBeNull();
    }

    public static TheoryData<string> ComparedFields() => ["displayName", "legalName", "shortName", "type", "city"];

    // D13 on update: each compared field alone brings Bunga Breakers FC (CLUB, Kampala) in as LIKELY. The organisation
    // being changed starts unlike it (another name, SCHOOL, Entebbe), or, for the type and city, with its name.
    [Theory]
    [MemberData(nameof(ComparedFields))]
    public async Task A_change_to_a_compared_field_that_looks_like_another_organisation_needs_duplicatesReviewed(string field)
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var bunga = await SaveOrganisationAsync(App.Services, kmsa, "Bunga Breakers FC");
        var startName = field is "type" or "city" ? "Bunga Breakers FC" : "Seeta Hill School";
        var changing = await SaveOrganisationAsync(App.Services, kmsa, startName, OrganisationType.School, "Entebbe");
        var body = new Dictionary<string, object?> { ["type"] = "SCHOOL", ["displayName"] = startName, ["city"] = "Entebbe" };
        switch (field)
        {
            case "displayName":
                body["displayName"] = "Breakers F.C., Bunga";
                body["city"] = "Kampala";
                break;
            case "legalName":
                body["legalName"] = "Bunga Breakers FC";
                body["type"] = "CLUB";
                break;
            case "shortName":
                body["shortName"] = "Bunga Breakers FC";
                body["city"] = "Kampala";
                break;
            case "type":
                body["type"] = "CLUB";
                break;
            default:
                body["city"] = " kampala ";
                break;
        }

        using (var refused = await registrar.PutAsJsonAsync(OrganisationPath(changing.Id), body, Ct))
        {
            var problem = await refused.ShouldBeProblemAsync(HttpStatusCode.UnprocessableEntity, "ORGANISATION_DUPLICATE_REVIEW_REQUIRED");
            Fields(problem).ShouldBe([("duplicatesReviewed", "ORGANISATION_DUPLICATE_REVIEW_REQUIRED")]);
            OrganisationCandidates(problem).ShouldBe([(bunga.Id, "LIKELY")]);
        }

        body["duplicatesReviewed"] = true;
        body["duplicateReason"] = " The school's club side, recorded on its own ";
        await UpdateOrganisationAsync(registrar, changing.Id, body);

        await AccountTestData.DeliverOutboxAsync(App.Services, Ct);
        var entries = (await AuditTestData.EntriesAsync(App.Services, Ct)).Where(e => e.ActionCode == "organisation.update").ToList();
        entries.Count.ShouldBe(2);
        entries.ShouldAllBe(entry => entry.EntityId == changing.Id);
        entries.Single(e => e.Outcome == AuditOutcome.Success).Reason.ShouldBe("The school's club side, recorded on its own");
        foreach (var entry in entries)
        {
            var audited = JsonDocument.Parse(entry.Metadata!).RootElement.GetProperty("candidates").EnumerateArray().ShouldHaveSingleItem();
            (audited.GetProperty("organisationId").GetGuid(), audited.GetProperty("confidence").GetString()).ShouldBe((bunga.Id, "LIKELY"));
        }
    }

    [Fact]
    public async Task A_slug_can_be_changed_to_a_free_one_but_not_to_another_organisations()
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var ntinda = await SaveOrganisationAsync(App.Services, kmsa, "Ntinda Swifts SC");
        var bunga = await SaveOrganisationAsync(App.Services, kmsa, "Bunga Breakers FC");

        var body = NtindaSwifts();
        body["city"] = "Kampala";
        body["slug"] = bunga.Slug;
        using (var taken = await registrar.PutAsJsonAsync(OrganisationPath(ntinda.Id), body, Ct))
        {
            var problem = await taken.ShouldBeProblemAsync(HttpStatusCode.UnprocessableEntity, "ORGANISATION_SLUG_TAKEN");
            Fields(problem).ShouldBe([("slug", "ORGANISATION_SLUG_TAKEN")]);
        }

        body["slug"] = Guid.NewGuid().ToString();
        using (var uuid = await registrar.PutAsJsonAsync(OrganisationPath(ntinda.Id), body, Ct))
        {
            var problem = await uuid.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_INVALID_REQUEST");
            Fields(problem).ShouldBe([("slug", "VALIDATION_INVALID_REQUEST")]);
        }

        // The organisation's own slug is not "taken".
        body["slug"] = ntinda.Slug;
        (await UpdateOrganisationAsync(registrar, ntinda.Id, body)).GetProperty("slug").GetString().ShouldBe(ntinda.Slug);

        body["slug"] = "ntinda-swifts";
        (await UpdateOrganisationAsync(registrar, ntinda.Id, body)).GetProperty("slug").GetString().ShouldBe("ntinda-swifts");
    }

    // D6: a reference is checked only when the request sets or changes it.
    [Fact]
    public async Task A_kept_primary_venue_closed_since_stays_but_a_closed_venue_cant_be_chosen()
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var kiwatule = await SaveVenueAsync(App.Services, kmsa, "Kiwatule Meadow Ground");
        var bukoto = await SaveVenueAsync(App.Services, kmsa, "Bukoto Community Stadium", status: VenueStatus.Closed);
        var ntinda = await SaveOrganisationAsync(App.Services, kmsa, "Ntinda Swifts SC", primaryVenueId: kiwatule.Id);
        await using (var scope = ScopeFor(App.Services, kmsa, out var context))
        {
            var venue = await context.Set<Venue>().Include(v => v.StatusHistory).SingleAsync(v => v.Id == kiwatule.Id, Ct);
            venue.ChangeStatus(VenueStatus.Closed, "The ground was sold", SystemActor.Instance, Now)
                .IsSuccess.ShouldBeTrue();
            await context.SaveChangesAsync(Ct);
        }

        var body = NtindaSwifts();
        body["city"] = "Kampala";
        body["primaryVenueId"] = kiwatule.Id;
        body["description"] = "Plays at Kiwatule while it looks for a ground.";
        (await UpdateOrganisationAsync(registrar, ntinda.Id, body)).GetProperty("primaryVenue").GetProperty("id").GetGuid().ShouldBe(kiwatule.Id);

        body["primaryVenueId"] = bukoto.Id;
        using var refused = await registrar.PutAsJsonAsync(OrganisationPath(ntinda.Id), body, Ct);
        var problem = await refused.ShouldBeProblemAsync(HttpStatusCode.UnprocessableEntity, "ORGANISATION_PRIMARY_VENUE_UNAVAILABLE");
        Fields(problem).ShouldBe([("primaryVenueId", "ORGANISATION_PRIMARY_VENUE_UNAVAILABLE")]);
    }

    // D21 on update: a create and a rename to the same organisation queue on the lock; the second finds the first and is
    // warned, whichever comes first.
    [Fact]
    public async Task A_create_and_a_rename_to_the_same_organisation_at_once_make_one_such_organisation()
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var seeta = await SaveOrganisationAsync(App.Services, kmsa, "Seeta Sparrows FC");
        var rename = new Dictionary<string, object?> { ["type"] = "CLUB", ["displayName"] = "Bunga Breakers FC", ["city"] = "Kampala" };
        var create = new Dictionary<string, object?> { ["type"] = "CLUB", ["displayName"] = "Bunga Breakers FC", ["city"] = "Kampala" };

        var responses = await Task.WhenAll(
            registrar.PutAsJsonAsync(OrganisationPath(seeta.Id), rename, Ct),
            registrar.PostAsJsonAsync(OrganisationsPath, create, Ct));

        var refused = responses.Where(r => r.StatusCode == HttpStatusCode.UnprocessableEntity).ShouldHaveSingleItem();
        responses.Count(r => r.IsSuccessStatusCode).ShouldBe(1);
        await refused.ShouldBeProblemAsync(HttpStatusCode.UnprocessableEntity, "ORGANISATION_DUPLICATE_REVIEW_REQUIRED");
        foreach (var response in responses)
        {
            response.Dispose();
        }

        await using var scope = ScopeFor(App.Services, kmsa, out var context);
        (await context.Set<Organisation>().CountAsync(o => o.DisplayNameKey == Organisation.NameKeyOf("Bunga Breakers FC"), Ct)).ShouldBe(1);
    }

    [Theory]
    [InlineData(OrganisationStatus.Archived)]
    [InlineData(OrganisationStatus.Dissolved)]
    [InlineData(OrganisationStatus.Suspended)]
    public async Task An_organisation_can_be_corrected_in_any_status(OrganisationStatus status)
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var ntinda = await SaveOrganisationAsync(App.Services, kmsa, "Ntinda Swifts SC", status: status);

        var body = NtindaSwifts();
        body["city"] = "Kampala";
        body["foundedYear"] = 1998;
        var corrected = await UpdateOrganisationAsync(registrar, ntinda.Id, body);

        (corrected.GetProperty("status").GetString(), corrected.GetProperty("foundedYear").GetInt32())
            .ShouldBe((status.ToString().ToUpperInvariant(), 1998));
    }

    [Fact]
    public async Task A_stale_version_is_an_organisation_conflict_and_an_unknown_organisation_is_not_found()
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var (_, elsscOwner) = await TenantAsync("elssc", IdentityRoles.TenantOwner);
        var created = await CreateOrganisationAsync(registrar, NtindaSwifts());
        var id = created.GetProperty("id").GetGuid();
        var version = created.GetProperty("version").GetUInt32();
        var body = NtindaSwifts();
        body["foundedYear"] = 1998;
        await UpdateOrganisationAsync(registrar, id, body);

        using (var stale = await PutAsync(registrar, OrganisationPath(id), body, $"\"{version}\""))
        {
            await stale.ShouldBeProblemAsync(HttpStatusCode.Conflict, "ORGANISATION_CONCURRENCY_CONFLICT");
        }

        body["version"] = version;
        using (var staleBody = await registrar.PutAsJsonAsync(OrganisationPath(id), body, Ct))
        {
            await staleBody.ShouldBeProblemAsync(HttpStatusCode.Conflict, "ORGANISATION_CONCURRENCY_CONFLICT");
        }

        body.Remove("version");
        using (var unknown = await registrar.PutAsJsonAsync(OrganisationPath(Guid.NewGuid()), body, Ct))
        {
            await unknown.ShouldBeProblemAsync(HttpStatusCode.NotFound, "ORGANISATION_NOT_FOUND");
        }

        using (var otherTenants = await elsscOwner.PutAsJsonAsync(OrganisationPath(id), body, Ct))
        {
            await otherTenants.ShouldBeProblemAsync(HttpStatusCode.NotFound, "ORGANISATION_NOT_FOUND");
        }

        await using var scope = ScopeFor(App.Services, kmsa, out var context);
        (await context.Set<Organisation>().SingleAsync(o => o.Id == id, Ct)).FoundedYear.ShouldBe(1998);
    }

    // The details' rules are create's (D4), on their field.
    [Theory]
    [InlineData("""{"displayName":"Ntinda Swifts SC"}""", 400, "VALIDATION_INVALID_REQUEST", "type")]
    [InlineData("""{"type":"CLUB","displayName":" -- "}""", 422, "ORGANISATION_NAME_INVALID", "displayName")]
    [InlineData("""{"type":"CLUB","displayName":"Ntinda Swifts SC","website":"ftp://ntinda.example"}""", 400, "VALIDATION_INVALID_REQUEST", "website")]
    [InlineData("""{"type":"CLUB","displayName":"Ntinda Swifts SC","foundedYear":2999}""", 400, "VALIDATION_INVALID_REQUEST", "foundedYear")]
    [InlineData("""{"type":"CLUB","displayName":"Ntinda Swifts SC","duplicateReason":"Reviewed"}""", 400, "VALIDATION_INVALID_REQUEST", "duplicateReason")]
    [InlineData("""{"type":"CLUB","displayName":"Ntinda Swifts SC","status":"DISSOLVED"}""", 400, "VALIDATION_MALFORMED_REQUEST", null)]
    public async Task A_change_that_cant_be_saved_is_refused_on_its_field(string body, int status, string code, string? field)
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var ntinda = await SaveOrganisationAsync(App.Services, kmsa, "Ntinda Swifts SC");

        using var content = new StringContent(body, System.Text.Encoding.UTF8, "application/json");
        using var response = await registrar.PutAsync(OrganisationPath(ntinda.Id), content, Ct);

        var problem = await response.ShouldBeProblemAsync((HttpStatusCode)status, code);
        if (field is not null)
        {
            Fields(problem).ShouldContain((field, code));
        }
    }

    // D20: changing organisations is the owner's, the admin's and the registrar's.
    [Theory]
    [InlineData(IdentityRoles.TenantOwner, true)]
    [InlineData(IdentityRoles.TenantAdmin, true)]
    [InlineData(IdentityRoles.Registrar, true)]
    [InlineData(IdentityRoles.CompetitionManager, false)]
    [InlineData(IdentityRoles.ResultsOfficer, false)]
    [InlineData(IdentityRoles.DataEntry, false)]
    [InlineData(IdentityRoles.Viewer, false)]
    [InlineData(IdentityRoles.Scorer, false)]
    public async Task Only_those_who_change_organisations_may_update_them(string role, bool allowed)
    {
        var (kmsa, client) = await TenantAsync("kmsa", role);
        var ntinda = await SaveOrganisationAsync(App.Services, kmsa, "Ntinda Swifts SC");

        using var response = await client.PutAsJsonAsync(OrganisationPath(ntinda.Id), NtindaSwifts(), Ct);

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
    public async Task A_read_only_tenant_changes_no_organisation()
    {
        App.Authorization.UseIdentityRoles();
        var kmsa = await TenantTestData.SaveTenantAsync(
            App.Services, "kmsa", TenantStatus.Active, publicEnabled: false, withDomain: true, withSubscription: false, Ct);
        await TenantTestData.SavePeriodAsync(App.Services, kmsa, new DateOnly(2025, 1, 1), new DateOnly(2025, 12, 31), Ct, cancelReason: "Pilot paused");
        var member = await CreateMemberAsync(App.Services, kmsa, "brian.ssemakula@kmsa.example", Ct, TenantWide(IdentityRoles.Registrar));
        var registrar = App.ClientFor(member.UserId, kmsa);
        var ntinda = await SaveOrganisationAsync(App.Services, kmsa, "Ntinda Swifts SC");

        using var response = await registrar.PutAsJsonAsync(OrganisationPath(ntinda.Id), NtindaSwifts(), Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "TENANT_READ_ONLY");
    }

    private static async Task<JsonElement> UpdateOrganisationAsync(HttpClient client, Guid organisationId, object body)
    {
        using var response = await client.PutAsJsonAsync(OrganisationPath(organisationId), body, Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));
        return await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
    }

    private static async Task<HttpResponseMessage> PutAsync(HttpClient client, string path, object body, string ifMatch)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, path) { Content = JsonContent.Create(body) };
        request.Headers.TryAddWithoutValidation("If-Match", ifMatch);
        return await client.SendAsync(request, Ct);
    }

    /// <summary>An active tenant (country UG) and a client of a member with <paramref name="role"/> tenant-wide.</summary>
    private async Task<(Guid Tenant, HttpClient Client)> TenantAsync(string slug, string role)
    {
        App.Authorization.UseIdentityRoles();
        var tenant = await TenantTestData.SaveTenantAsync(App.Services, slug, TenantStatus.Active, Ct);
        var member = await CreateMemberAsync(App.Services, tenant, MailboxOf(role, $"{slug}.example"), Ct, TenantWide(role));
        return (tenant, App.ClientFor(member.UserId, tenant));
    }
}

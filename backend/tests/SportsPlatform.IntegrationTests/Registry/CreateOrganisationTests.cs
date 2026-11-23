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
/// Creating an organisation (S07-T2a; D4, D9, D13, D21): the fields, the refusals on their field, the primary venue, the
/// slug, the duplicate warning with <c>duplicatesReviewed</c> and its audit entry, the read, two creates at once, the
/// roles and the read-only tenant.
/// </summary>
public sealed class CreateOrganisationTests(PostgresFixture fixture) : DatabaseTest(fixture)
{
    [Fact]
    public async Task An_organisation_is_created_with_its_fields_and_read_back()
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var venue = await SaveVenueAsync(App.Services, kmsa, "Kiwatule Meadow Ground");
        var body = NtindaSwifts();
        body["displayName"] = "  Ntinda Swifts SC ";
        body["legalName"] = "Ntinda Swifts Sports Club Limited";
        body["shortName"] = "Swifts";
        body["city"] = "Kampala";
        body["foundedYear"] = 1998;
        body["website"] = "https://ntindaswifts.example";
        body["email"] = "office@ntindaswifts.example";
        body["phone"] = "+256 414 220 310";
        body["description"] = "A multi-sport club in Ntinda: football for men, women and under-17s.";
        body["primaryVenueId"] = venue.Id;

        using var response = await registrar.PostAsJsonAsync(OrganisationsPath, body, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync(Ct));
        var organisation = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        var id = organisation.GetProperty("id").GetGuid();
        response.Headers.Location!.ToString().ShouldBe(OrganisationPath(id));
        response.Headers.ETag!.Tag.ShouldBe($"\"{organisation.GetProperty("version").GetUInt32()}\"");
        organisation.GetProperty("type").GetString().ShouldBe("CLUB");
        organisation.GetProperty("displayName").GetString().ShouldBe("Ntinda Swifts SC");
        organisation.GetProperty("legalName").GetString().ShouldBe("Ntinda Swifts Sports Club Limited");
        organisation.GetProperty("shortName").GetString().ShouldBe("Swifts");
        organisation.GetProperty("slug").GetString().ShouldBe("ntinda-swifts-sc");
        organisation.GetProperty("country").GetString().ShouldBe("UG");
        organisation.GetProperty("city").GetString().ShouldBe("Kampala");
        organisation.GetProperty("foundedYear").GetInt32().ShouldBe(1998);
        organisation.GetProperty("website").GetString().ShouldBe("https://ntindaswifts.example");
        organisation.GetProperty("email").GetString().ShouldBe("office@ntindaswifts.example");
        organisation.GetProperty("phone").GetString().ShouldBe("+256 414 220 310");
        organisation.GetProperty("description").GetString().ShouldStartWith("A multi-sport club");
        var primaryVenue = organisation.GetProperty("primaryVenue");
        (primaryVenue.GetProperty("id").GetGuid(), primaryVenue.GetProperty("name").GetString(), primaryVenue.GetProperty("slug").GetString())
            .ShouldBe((venue.Id, "Kiwatule Meadow Ground", venue.Slug));
        organisation.GetProperty("status").GetString().ShouldBe("ACTIVE");

        var read = await registrar.ReadJsonAsync(OrganisationPath(id), Ct);
        read.GetRawText().ShouldBe(organisation.GetRawText());

        await using var scope = ScopeFor(App.Services, kmsa, out var context);
        var saved = await context.Set<Organisation>().Include(o => o.StatusHistory).SingleAsync(Ct);
        (saved.DisplayNameKey, saved.LegalNameKey, saved.ShortNameKey, saved.SortKey, saved.CityKey, saved.Source).ShouldBe(
            ("ntinda sc swifts", "club limited ntinda sports swifts", "swifts", "ntinda swifts sc", "kampala", OrganisationSource.Manual));
        var first = saved.StatusHistory.ShouldHaveSingleItem();
        (first.FromStatus, first.ToStatus, first.Reason).ShouldBe((null, OrganisationStatus.Active, null));
    }

    [Fact]
    public async Task Only_the_required_fields_make_an_organisation_with_the_tenants_country()
    {
        var (_, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);

        var organisation = await CreateOrganisationAsync(registrar, NtindaSwifts());

        organisation.GetProperty("country").GetString().ShouldBe("UG");
        foreach (var field in new[] { "legalName", "shortName", "city", "foundedYear", "website", "email", "phone", "description", "primaryVenue" })
        {
            organisation.GetProperty(field).ValueKind.ShouldBe(JsonValueKind.Null, field);
        }

        var kenyan = NtindaSwifts();
        kenyan["displayName"] = "Kisumu Lakeside Academy";
        kenyan["type"] = "ACADEMY";
        kenyan["country"] = "KE";
        (await CreateOrganisationAsync(registrar, kenyan)).GetProperty("country").GetString().ShouldBe("KE");
    }

    // A founded year after the tenant's year (2099) is refused on its field, as one before 1800.
    [Theory]
    [InlineData("""{"displayName":"Ntinda Swifts SC"}""", 400, "VALIDATION_INVALID_REQUEST", "type")]
    [InlineData("""{"type":"CLUB"}""", 400, "VALIDATION_INVALID_REQUEST", "displayName")]
    [InlineData("""{"type":"CLUB","displayName":" -- "}""", 422, "ORGANISATION_NAME_INVALID", "displayName")]
    [InlineData("""{"type":"CLUB","displayName":"Ntinda Swifts SC","foundedYear":1799}""", 400, "VALIDATION_INVALID_REQUEST", "foundedYear")]
    [InlineData("""{"type":"CLUB","displayName":"Ntinda Swifts SC","foundedYear":2099}""", 400, "VALIDATION_INVALID_REQUEST", "foundedYear")]
    [InlineData("""{"type":"CLUB","displayName":"Ntinda Swifts SC","website":"ntindaswifts.example"}""", 400, "VALIDATION_INVALID_REQUEST", "website")]
    [InlineData("""{"type":"CLUB","displayName":"Ntinda Swifts SC","website":"ftp://ntindaswifts.example"}""", 400, "VALIDATION_INVALID_REQUEST", "website")]
    [InlineData("""{"type":"CLUB","displayName":"Ntinda Swifts SC","email":"office at ntindaswifts"}""", 400, "VALIDATION_INVALID_REQUEST", "email")]
    [InlineData("""{"type":"CLUB","displayName":"Ntinda Swifts SC","phone":"call the office"}""", 400, "VALIDATION_INVALID_REQUEST", "phone")]
    [InlineData("""{"type":"CLUB","displayName":"Ntinda Swifts SC","country":"XX"}""", 400, "VALIDATION_INVALID_REQUEST", "country")]
    [InlineData("""{"type":"CLUB","displayName":"Ntinda Swifts SC","primaryVenueId":"0192a1b2-0000-7000-8000-0000000000ff"}""", 422, "ORGANISATION_PRIMARY_VENUE_UNAVAILABLE", "primaryVenueId")]
    [InlineData("""{"type":"CLUB","displayName":"Ntinda Swifts SC","slug":"0192a1b2-0000-7000-8000-00000000c001"}""", 400, "VALIDATION_INVALID_REQUEST", "slug")]
    [InlineData("""{"type":"CLUB","displayName":"Ntinda Swifts SC","duplicateReason":"A second club"}""", 400, "VALIDATION_INVALID_REQUEST", "duplicateReason")]
    public async Task An_organisation_that_cant_be_created_is_refused_on_its_field(string body, int status, string code, string field)
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);

        using var response = await registrar.PostAsync(
            OrganisationsPath, new StringContent(body, System.Text.Encoding.UTF8, "application/json"), Ct);

        var problem = await response.ShouldBeProblemAsync((HttpStatusCode)status, code);
        Fields(problem).ShouldContain((field, code));
        await using var scope = ScopeFor(App.Services, kmsa, out var context);
        (await context.Set<Organisation>().CountAsync(Ct)).ShouldBe(0);
    }

    // D4's length limits (review #121 R1): one past each is refused on its field; the limit itself is accepted.
    [Theory]
    [InlineData("displayName", 120)]
    [InlineData("legalName", 200)]
    [InlineData("shortName", 40)]
    [InlineData("city", 100)]
    [InlineData("description", 2_000)]
    [InlineData("website", 200)]
    [InlineData("email", 200)]
    [InlineData("phone", 30)]
    [InlineData("duplicateReason", 500)]
    public async Task A_text_longer_than_its_limit_is_refused_on_its_field(string field, int limit)
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);

        using var refused = await registrar.PostAsJsonAsync(OrganisationsPath, WithText(field, limit + 1), Ct);
        Fields(await refused.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_INVALID_REQUEST"))
            .ShouldBe([(field, "VALIDATION_INVALID_REQUEST")]);

        var accepted = await CreateOrganisationAsync(registrar, WithText(field, limit));
        if (field != "duplicateReason")
        {
            accepted.GetProperty(field).GetString()!.Length.ShouldBe(limit);
        }

        await using var scope = ScopeFor(App.Services, kmsa, out var context);
        (await context.Set<Organisation>().CountAsync(Ct)).ShouldBe(1);
    }

    // D4: the founded year runs to the tenant's year (Africa/Kampala), edges included.
    [Fact]
    public async Task The_founded_year_may_be_this_year_but_not_next_year()
    {
        var (_, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var kampala = TimeZoneInfo.FindSystemTimeZoneById("Africa/Kampala");
        var year = TimeZoneInfo.ConvertTime(App.Clock.GetUtcNow(), kampala).Year;
        var thisYear = NtindaSwifts();
        thisYear["foundedYear"] = year;
        var nextYear = NtindaSwifts();
        nextYear["displayName"] = "Seeta Sparrows FC";
        nextYear["foundedYear"] = year + 1;

        (await CreateOrganisationAsync(registrar, thisYear)).GetProperty("foundedYear").GetInt32().ShouldBe(year);
        using var refused = await registrar.PostAsJsonAsync(OrganisationsPath, nextYear, Ct);
        Fields(await refused.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_INVALID_REQUEST"))
            .ShouldBe([("foundedYear", "VALIDATION_INVALID_REQUEST")]);
    }

    [Fact]
    public async Task A_status_or_unknown_member_is_refused()
    {
        var (_, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var withStatus = NtindaSwifts();
        withStatus["status"] = "DISSOLVED";

        using var refused = await registrar.PostAsJsonAsync(OrganisationsPath, withStatus, Ct);

        await refused.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_MALFORMED_REQUEST");
    }

    // D4: the primary venue is a venue of the tenant that isn't closed or archived; a temporarily unavailable one will do.
    [Theory]
    [InlineData(VenueStatus.Closed, false)]
    [InlineData(VenueStatus.Archived, false)]
    [InlineData(VenueStatus.TemporarilyUnavailable, true)]
    public async Task The_primary_venue_must_be_open(VenueStatus status, bool accepted)
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var venue = await SaveVenueAsync(App.Services, kmsa, "Bukoto Community Stadium", status: status);
        var body = NtindaSwifts();
        body["primaryVenueId"] = venue.Id;

        using var response = await registrar.PostAsJsonAsync(OrganisationsPath, body, Ct);

        if (accepted)
        {
            response.StatusCode.ShouldBe(HttpStatusCode.Created);
        }
        else
        {
            Fields(await response.ShouldBeProblemAsync(HttpStatusCode.UnprocessableEntity, "ORGANISATION_PRIMARY_VENUE_UNAVAILABLE"))
                .ShouldBe([("primaryVenueId", "ORGANISATION_PRIMARY_VENUE_UNAVAILABLE")]);
        }
    }

    [Fact]
    public async Task Another_tenants_venue_is_not_a_primary_venue()
    {
        var (_, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var elssc = await TenantTestData.SaveTenantAsync(App.Services, "elssc", TenantStatus.Active, Ct);
        var lakeshore = await SaveVenueAsync(App.Services, elssc, "Lakeshore High School Playing Fields");
        var body = NtindaSwifts();
        body["primaryVenueId"] = lakeshore.Id;

        using var response = await registrar.PostAsJsonAsync(OrganisationsPath, body, Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.UnprocessableEntity, "ORGANISATION_PRIMARY_VENUE_UNAVAILABLE");
    }

    // D9: a slug is proposed from the display name, with -2, -3 on a clash; one sent that another organisation has is refused.
    [Fact]
    public async Task Slugs_are_proposed_from_the_display_name_and_unique_in_the_tenant()
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var (_, elsscRegistrar) = await TenantAsync("elssc", IdentityRoles.Registrar);
        var reviewed = NtindaSwifts();
        reviewed["duplicatesReviewed"] = true;

        (await CreateOrganisationAsync(registrar, NtindaSwifts())).GetProperty("slug").GetString().ShouldBe("ntinda-swifts-sc");
        (await CreateOrganisationAsync(registrar, reviewed)).GetProperty("slug").GetString().ShouldBe("ntinda-swifts-sc-2");
        (await CreateOrganisationAsync(elsscRegistrar, NtindaSwifts())).GetProperty("slug").GetString().ShouldBe("ntinda-swifts-sc");

        var withSlug = NtindaSwifts();
        withSlug["displayName"] = "Kisaasi Kestrels SC";
        withSlug["slug"] = "kestrels";
        (await CreateOrganisationAsync(registrar, withSlug)).GetProperty("slug").GetString().ShouldBe("kestrels");
        var taken = NtindaSwifts();
        taken["displayName"] = "Seeta Sparrows FC";
        taken["slug"] = "kestrels";
        using var refused = await registrar.PostAsJsonAsync(OrganisationsPath, taken, Ct);
        Fields(await refused.ShouldBeProblemAsync(HttpStatusCode.UnprocessableEntity, "ORGANISATION_SLUG_TAKEN"))
            .ShouldBe([("slug", "ORGANISATION_SLUG_TAKEN")]);

        await using var scope = ScopeFor(App.Services, kmsa, out var context);
        (await context.Set<Organisation>().CountAsync(Ct)).ShouldBe(3);
    }

    [Fact]
    public async Task A_likely_duplicate_needs_duplicatesReviewed_and_the_audit_entry_names_it()
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var existing = await SaveOrganisationAsync(App.Services, kmsa, "Ntinda Swifts SC");
        var body = NtindaSwifts();
        body["displayName"] = "SC Ntinda Swifts";

        using var refused = await registrar.PostAsJsonAsync(OrganisationsPath, body, Ct);

        var problem = await refused.ShouldBeProblemAsync(HttpStatusCode.UnprocessableEntity, "ORGANISATION_DUPLICATE_REVIEW_REQUIRED");
        Fields(problem).ShouldBe([("duplicatesReviewed", "ORGANISATION_DUPLICATE_REVIEW_REQUIRED")]);
        var candidate = problem.GetProperty("candidates").EnumerateArray().ShouldHaveSingleItem();
        (candidate.GetProperty("organisationId").GetGuid(), candidate.GetProperty("confidence").GetString()).ShouldBe((existing.Id, "LIKELY"));

        body["duplicatesReviewed"] = true;
        body["duplicateReason"] = "  The Ntinda school's own club ";
        var created = await CreateOrganisationAsync(registrar, body);

        await AccountTestData.DeliverOutboxAsync(App.Services, Ct);
        var entries = await AuditTestData.EntriesAsync(App.Services, Ct);
        entries.ShouldAllBe(entry => entry.ActionCode == "organisation.create" && entry.EntityType == "Organisation");
        var refusedEntry = entries.Single(e => e.ErrorCode == "ORGANISATION_DUPLICATE_REVIEW_REQUIRED");
        var success = entries.Single(e => e.Outcome == AuditOutcome.Success);
        (refusedEntry.EntityId, refusedEntry.Reason).ShouldBe((null, null));
        (success.EntityId, success.Reason).ShouldBe((created.GetProperty("id").GetGuid(), "The Ntinda school's own club"));
        foreach (var entry in new[] { refusedEntry, success })
        {
            var audited = JsonDocument.Parse(entry.Metadata!).RootElement.GetProperty("candidates").EnumerateArray().ShouldHaveSingleItem();
            audited.EnumerateObject().Select(property => property.Name).Order().ShouldBe(["confidence", "organisationId"]);
            (audited.GetProperty("organisationId").GetGuid(), audited.GetProperty("confidence").GetString()).ShouldBe((existing.Id, "LIKELY"));
        }
    }

    // D21: two creates of one organisation queue on the lock; the second finds the first's and is warned.
    [Fact]
    public async Task Two_creates_of_the_same_organisation_at_once_make_one()
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);

        var responses = await Task.WhenAll(
            registrar.PostAsJsonAsync(OrganisationsPath, NtindaSwifts(), Ct),
            registrar.PostAsJsonAsync(OrganisationsPath, NtindaSwifts(), Ct));

        responses.Select(r => r.StatusCode).OrderBy(s => s).ShouldBe([HttpStatusCode.Created, HttpStatusCode.UnprocessableEntity]);
        await responses.Single(r => r.StatusCode == HttpStatusCode.UnprocessableEntity)
            .ShouldBeProblemAsync(HttpStatusCode.UnprocessableEntity, "ORGANISATION_DUPLICATE_REVIEW_REQUIRED");
        foreach (var response in responses)
        {
            response.Dispose();
        }

        await using var scope = ScopeFor(App.Services, kmsa, out var context);
        (await context.Set<Organisation>().CountAsync(Ct)).ShouldBe(1);
    }

    [Fact]
    public async Task Another_tenants_organisation_is_not_found_and_never_a_candidate()
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var (_, elsscOwner) = await TenantAsync("elssc", IdentityRoles.TenantOwner);
        var swifts = await SaveOrganisationAsync(App.Services, kmsa, "Ntinda Swifts SC");

        using var read = await elsscOwner.GetAsync(OrganisationPath(swifts.Id), Ct);
        await read.ShouldBeProblemAsync(HttpStatusCode.NotFound, "ORGANISATION_NOT_FOUND");
        (await CheckOrganisationAsync(elsscOwner, new { type = "CLUB", displayName = "Ntinda Swifts SC", city = "Kampala" }))
            .GetProperty("candidates").GetArrayLength().ShouldBe(0);
        (await registrar.ReadJsonAsync(OrganisationPath(swifts.Id), Ct)).GetProperty("displayName").GetString().ShouldBe("Ntinda Swifts SC");
    }

    // D20: creating organisations (and the check) is the owner's, the admin's and the registrar's.
    [Theory]
    [InlineData(IdentityRoles.TenantOwner, true)]
    [InlineData(IdentityRoles.TenantAdmin, true)]
    [InlineData(IdentityRoles.Registrar, true)]
    [InlineData(IdentityRoles.CompetitionManager, false)]
    [InlineData(IdentityRoles.ResultsOfficer, false)]
    [InlineData(IdentityRoles.DataEntry, false)]
    [InlineData(IdentityRoles.Viewer, false)]
    [InlineData(IdentityRoles.Scorer, false)]
    public async Task Only_those_who_change_organisations_may_create_and_check_them(string role, bool allowed)
    {
        var (_, client) = await TenantAsync("kmsa", role);

        using var check = await client.PostAsJsonAsync(OrganisationDuplicateCheckPath, new { type = "CLUB", displayName = "Ntinda Swifts SC" }, Ct);
        using var create = await client.PostAsJsonAsync(OrganisationsPath, NtindaSwifts(), Ct);

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

    // D20: reading organisations is every tenant-wide role's that reads people; not the scorer's.
    [Theory]
    [InlineData(IdentityRoles.TenantOwner, true)]
    [InlineData(IdentityRoles.TenantAdmin, true)]
    [InlineData(IdentityRoles.Registrar, true)]
    [InlineData(IdentityRoles.CompetitionManager, true)]
    [InlineData(IdentityRoles.ResultsOfficer, true)]
    [InlineData(IdentityRoles.DataEntry, true)]
    [InlineData(IdentityRoles.Viewer, true)]
    [InlineData(IdentityRoles.Scorer, false)]
    public async Task Every_role_that_reads_people_reads_organisations(string role, bool allowed)
    {
        var (kmsa, client) = await TenantAsync("kmsa", role);
        var swifts = await SaveOrganisationAsync(App.Services, kmsa, "Ntinda Swifts SC");

        using var response = await client.GetAsync(OrganisationPath(swifts.Id), Ct);

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
    public async Task A_read_only_tenant_reads_its_organisations_but_creates_none()
    {
        App.Authorization.UseIdentityRoles();
        var kmsa = await TenantTestData.SaveTenantAsync(
            App.Services, "kmsa", TenantStatus.Active, publicEnabled: false, withDomain: true, withSubscription: false, Ct);
        await TenantTestData.SavePeriodAsync(App.Services, kmsa, new DateOnly(2025, 1, 1), new DateOnly(2025, 12, 31), Ct, cancelReason: "Pilot paused");
        var member = await CreateMemberAsync(App.Services, kmsa, "brian.ssemakula@kmsa.example", Ct, TenantWide(IdentityRoles.Registrar));
        var registrar = App.ClientFor(member.UserId, kmsa);
        var swifts = await SaveOrganisationAsync(App.Services, kmsa, "Ntinda Swifts SC");

        using var create = await registrar.PostAsJsonAsync(OrganisationsPath, NtindaSwifts(), Ct);
        using var read = await registrar.GetAsync(OrganisationPath(swifts.Id), Ct);

        await create.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "TENANT_READ_ONLY");
        read.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    // A valid body whose field is exactly length characters long, in that field's own format.
    private static Dictionary<string, object?> WithText(string field, int length)
    {
        var body = NtindaSwifts();
        body[field] = field switch
        {
            "website" => "https://" + new string('n', length - "https://.example".Length) + ".example",
            "email" => new string('o', length - "@ntinda.example".Length) + "@ntinda.example",
            "phone" => "+" + new string('4', length - 1),
            _ => new string('n', length),
        };
        if (field == "duplicateReason")
        {
            body["duplicatesReviewed"] = true;
        }

        return body;
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

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
using SportsPlatform.Modules.Registry.Domain.Teams;
using SportsPlatform.Modules.Registry.Domain.Venues;
using SportsPlatform.Modules.Tenancy.Contracts.Tenants;
using static SportsPlatform.IntegrationTests.Identity.MembershipTestData;
using static SportsPlatform.IntegrationTests.Registry.OrganisationRequests;
using static SportsPlatform.IntegrationTests.Registry.PeopleRequests;
using static SportsPlatform.IntegrationTests.Registry.RegistryTestData;
using static SportsPlatform.IntegrationTests.Registry.TeamRequests;
using static SportsPlatform.IntegrationTests.Registry.VenueRequests;
using static SportsPlatform.IntegrationTests.Tenancy.TestSportDefinitions;

namespace SportsPlatform.IntegrationTests.Registry;

/// <summary>
/// Changing a team (S07-T3b-1; D6, D8, D9, D11, D13, D21): a whole replacement in every status and whatever the variant's
/// state, the name kept (another one is <c>TEAM_RENAME_REQUIRED</c>), the slug kept or changed, the organisation and home
/// venue checked only when they change (T2 acceptance 2), the duplicate warning again when the short name, the category
/// or the organisation changes (the team itself left out) with its audit entry, the version, the roles and the read-only
/// tenant.
/// </summary>
public sealed class UpdateTeamTests(PostgresFixture fixture) : DatabaseTest(fixture)
{
    [Fact]
    public async Task An_update_replaces_the_fields_and_keeps_the_name_variant_and_slug()
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var club = await SaveOrganisationAsync(App.Services, kmsa, "Ntinda Swifts SC");
        var ground = await SaveVenueAsync(App.Services, kmsa, "Kiwatule Meadow Ground");
        var body = NtindaSwiftsMen();
        body["shortName"] = "Ntinda Swifts";
        body["code"] = "NTS";
        body["motto"] = "Swift and sure";
        body["primaryColour"] = "#0A3D91";
        body["secondaryColour"] = "#FFFFFF";
        body["nameSince"] = "2020-01-01";
        var created = await CreateTeamAsync(registrar, body);
        var id = created.GetProperty("id").GetGuid();

        // Left out: the short name, the motto, the secondary colour, the level (FIRST) and the slug (kept).
        var changed = Body(new()
        {
            ["name"] = "Ntinda Swifts FC",
            ["organisationId"] = club.Id,
            ["homeVenueId"] = ground.Id,
            ["code"] = "NSW",
            ["primaryColour"] = "#ff0000",
            ["foundedYear"] = 1998,
        });
        using var response = await PutAsync(registrar, TeamPath(id), changed, $"\"{created.GetProperty("version").GetUInt32()}\"");

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));
        var team = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        response.Headers.ETag!.Tag.ShouldBe($"\"{team.GetProperty("version").GetUInt32()}\"");
        team.GetProperty("version").GetUInt32().ShouldNotBe(created.GetProperty("version").GetUInt32());
        (team.GetProperty("name").GetString(), team.GetProperty("variant").GetString(), team.GetProperty("slug").GetString(),
                team.GetProperty("nameSince").GetString())
            .ShouldBe(("Ntinda Swifts FC", TestballFull, created.GetProperty("slug").GetString(), "2020-01-01"));
        (team.GetProperty("code").GetString(), team.GetProperty("primaryColour").GetString(), team.GetProperty("foundedYear").GetInt32())
            .ShouldBe(("NSW", "#FF0000", 1998));
        team.GetProperty("organisation").GetProperty("id").GetGuid().ShouldBe(club.Id);
        team.GetProperty("homeVenue").GetProperty("id").GetGuid().ShouldBe(ground.Id);
        foreach (var cleared in new[] { "shortName", "motto", "secondaryColour", "tertiaryColour" })
        {
            team.GetProperty(cleared).ValueKind.ShouldBe(JsonValueKind.Null, cleared);
        }

        (await registrar.ReadJsonAsync(TeamPath(id), Ct)).GetRawText().ShouldBe(team.GetRawText());

        // The short name and the organisation changed: the warning ran and found none.
        await AccountTestData.DeliverOutboxAsync(App.Services, Ct);
        var entry = (await AuditTestData.EntriesAsync(App.Services, Ct)).Single(e => e.ActionCode == "team.update");
        (entry.Outcome, entry.EntityType, entry.EntityId, entry.Reason).ShouldBe((AuditOutcome.Success, "Team", id, null));
        JsonDocument.Parse(entry.Metadata!).RootElement.GetProperty("candidates").GetArrayLength().ShouldBe(0);
    }

    // D8: PUT can't rename. The current name may be sent (a form posts the whole team); another one is refused.
    [Theory]
    [InlineData("Ntinda Swifts FC ", HttpStatusCode.OK, null)]
    [InlineData("Ntinda Swifts F.C.", HttpStatusCode.UnprocessableEntity, "TEAM_RENAME_REQUIRED")]
    [InlineData("ntinda swifts fc", HttpStatusCode.UnprocessableEntity, "TEAM_RENAME_REQUIRED")]
    [InlineData("Bunga Lakeside FC", HttpStatusCode.UnprocessableEntity, "TEAM_RENAME_REQUIRED")]
    public async Task An_update_cant_rename_the_team(string name, HttpStatusCode status, string? code)
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var team = await SaveTeamAsync(App.Services, kmsa, "Ntinda Swifts FC");

        using var response = await registrar.PutAsJsonAsync(TeamPath(team.Id), Body(new() { ["name"] = name, ["code"] = "NTS" }), Ct);

        if (code is null)
        {
            response.StatusCode.ShouldBe(status, await response.Content.ReadAsStringAsync(Ct));
            return;
        }

        Fields(await response.ShouldBeProblemAsync(status, code)).ShouldBe([("name", code)]);
        await using var scope = ScopeFor(App.Services, kmsa, out var context);
        var saved = await context.Set<Team>().SingleAsync(Ct);
        (saved.Name, saved.Code).ShouldBe(("Ntinda Swifts FC", null));
    }

    [Fact]
    public async Task A_change_to_no_compared_field_runs_no_warning_and_audits_no_candidates()
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var team = await SaveTeamAsync(App.Services, kmsa, "Bunga Breakers FC");
        await SaveTeamAsync(App.Services, kmsa, "Bunga Breakers FC");

        var updated = await UpdateTeamAsync(registrar, team.Id, Body(new() { ["motto"] = "By the lake" }));

        updated.GetProperty("motto").GetString().ShouldBe("By the lake");
        await AccountTestData.DeliverOutboxAsync(App.Services, Ct);
        var entry = (await AuditTestData.EntriesAsync(App.Services, Ct)).Single(e => e.ActionCode == "team.update");
        entry.Metadata.ShouldBeNull();
    }

    // D13: a change to the short name, the category or the organisation runs the warning again, the team itself left out.
    [Theory]
    [InlineData("shortName")]
    [InlineData("category")]
    [InlineData("organisationId")]
    public async Task A_change_to_a_compared_field_that_looks_like_another_team_needs_duplicatesReviewed(string field)
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var club = await SaveOrganisationAsync(App.Services, kmsa, "Bunga Breakers SC");
        var team = await SaveTeamAsync(App.Services, kmsa, "Bunga Breakers FC", category: Category(AgeGroup.U17), shortName: "Breakers");
        // The other team shares the changed team's category, except when the change is to the category itself.
        var other = await SaveTeamAsync(
            App.Services,
            kmsa,
            field == "shortName" ? "Lakeside Breakers" : "Bunga Breakers FC",
            category: field == "category" ? SeniorMen : Category(AgeGroup.U17),
            organisationId: club.Id);
        var changed = field switch
        {
            "shortName" => Body(new() { ["name"] = "Bunga Breakers FC", ["shortName"] = "Lakeside Breakers", ["ageGroup"] = "U17" }),
            "category" => Body(new() { ["shortName"] = "Breakers" }),
            _ => Body(new() { ["shortName"] = "Breakers", ["ageGroup"] = "U17", ["organisationId"] = club.Id }),
        };

        using var refused = await registrar.PutAsJsonAsync(TeamPath(team.Id), changed, Ct);

        var problem = await refused.ShouldBeProblemAsync(HttpStatusCode.UnprocessableEntity, "TEAM_DUPLICATE_REVIEW_REQUIRED");
        TeamCandidates(problem).ShouldBe([(other.Id, "LIKELY")]);
        problem.GetProperty("candidates")[0].GetProperty("organisation").GetProperty("id").GetGuid().ShouldBe(club.Id);

        changed["duplicatesReviewed"] = true;
        changed["duplicateReason"] = "Another club's side";
        await UpdateTeamAsync(registrar, team.Id, changed);

        await AccountTestData.DeliverOutboxAsync(App.Services, Ct);
        var entries = (await AuditTestData.EntriesAsync(App.Services, Ct)).Where(e => e.ActionCode == "team.update").ToList();
        entries.Count.ShouldBe(2);
        var saved = entries.Single(e => e.Outcome == AuditOutcome.Success);
        saved.Reason.ShouldBe("Another club's side");
        JsonDocument.Parse(saved.Metadata!).RootElement.GetProperty("candidates")[0].GetProperty("teamId").GetGuid().ShouldBe(other.Id);
    }

    [Fact]
    public async Task A_slug_can_be_changed_to_a_free_one_but_not_to_another_teams()
    {
        var (_, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var swifts = await CreateTeamAsync(registrar, NtindaSwiftsMen());
        var kestrels = NtindaSwiftsMen();
        kestrels["name"] = "Kisaasi Kestrels FC";
        var kestrelsSlug = (await CreateTeamAsync(registrar, kestrels)).GetProperty("slug").GetString();
        var id = swifts.GetProperty("id").GetGuid();

        using var taken = await registrar.PutAsJsonAsync(TeamPath(id), Body(new() { ["slug"] = kestrelsSlug }), Ct);
        Fields(await taken.ShouldBeProblemAsync(HttpStatusCode.UnprocessableEntity, "TEAM_SLUG_TAKEN")).ShouldBe([("slug", "TEAM_SLUG_TAKEN")]);

        using var uuid = await registrar.PutAsJsonAsync(TeamPath(id), Body(new() { ["slug"] = Guid.NewGuid().ToString() }), Ct);
        await uuid.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_INVALID_REQUEST");

        (await UpdateTeamAsync(registrar, id, Body(new() { ["slug"] = "ntinda-swifts" }))).GetProperty("slug").GetString().ShouldBe("ntinda-swifts");
    }

    // D6 and T2 acceptance 2: a kept organisation or home venue isn't checked again, a new one is.
    [Fact]
    public async Task A_kept_dissolved_organisation_and_closed_venue_stay_but_cant_be_chosen()
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var club = await SaveOrganisationAsync(App.Services, kmsa, "Seeta Sparrows SC", status: OrganisationStatus.Dissolved);
        var ground = await SaveVenueAsync(App.Services, kmsa, "Seeta Hill Sports Field", status: VenueStatus.Closed);
        var kept = await SaveTeamAsync(App.Services, kmsa, "Seeta Sparrows FC", organisationId: club.Id);
        await using (var scope = ScopeFor(App.Services, kmsa, out var context))
        {
            await context.Database.ExecuteSqlAsync($"UPDATE registry.teams SET home_venue_id = {ground.Id} WHERE id = {kept.Id}", Ct);
        }

        var updated = await UpdateTeamAsync(
            registrar, kept.Id, Body(new() { ["organisationId"] = club.Id, ["homeVenueId"] = ground.Id, ["motto"] = "Sing and play" }));
        (updated.GetProperty("organisation").GetProperty("id").GetGuid(), updated.GetProperty("homeVenue").GetProperty("id").GetGuid())
            .ShouldBe((club.Id, ground.Id));

        var other = await SaveTeamAsync(App.Services, kmsa, "Kireka Kingfishers FC");
        using var organisation = await registrar.PutAsJsonAsync(TeamPath(other.Id), Body(new() { ["organisationId"] = club.Id }), Ct);
        Fields(await organisation.ShouldBeProblemAsync(HttpStatusCode.UnprocessableEntity, "TEAM_ORGANISATION_UNAVAILABLE"))
            .ShouldBe([("organisationId", "TEAM_ORGANISATION_UNAVAILABLE")]);
        using var venue = await registrar.PutAsJsonAsync(TeamPath(other.Id), Body(new() { ["homeVenueId"] = ground.Id }), Ct);
        Fields(await venue.ShouldBeProblemAsync(HttpStatusCode.UnprocessableEntity, "TEAM_HOME_VENUE_UNAVAILABLE"))
            .ShouldBe([("homeVenueId", "TEAM_HOME_VENUE_UNAVAILABLE")]);
    }

    [Theory]
    [InlineData(TeamStatus.Archived)]
    [InlineData(TeamStatus.Disbanded)]
    [InlineData(TeamStatus.Suspended)]
    public async Task A_team_can_be_corrected_in_any_status(TeamStatus status)
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var team = await SaveTeamAsync(App.Services, kmsa, "Bunga Breakers FC", status: status);

        var corrected = await UpdateTeamAsync(registrar, team.Id, Body(new() { ["foundedYear"] = 2005 }));

        (corrected.GetProperty("status").GetString(), corrected.GetProperty("foundedYear").GetInt32())
            .ShouldBe((status.ToString().ToUpperInvariant(), 2005));
    }

    // T3 acceptance 4: a variant disabled since keeps its teams, which can still be updated.
    [Fact]
    public async Task A_team_of_a_variant_disabled_since_can_still_be_updated()
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var team = await CreateTeamAsync(registrar, NtindaSwiftsMen());
        await TenantTestData.DisableSportAsync(App.Services, kmsa, TestballFull, Ct);

        var updated = await UpdateTeamAsync(registrar, team.GetProperty("id").GetGuid(), Body(new() { ["code"] = "NTS" }));

        (updated.GetProperty("variant").GetString(), updated.GetProperty("code").GetString()).ShouldBe((TestballFull, "NTS"));
    }

    [Fact]
    public async Task A_stale_version_is_a_team_conflict_and_an_unknown_team_is_not_found()
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var (_, elsscOwner) = await TenantAsync("elssc", IdentityRoles.TenantOwner);
        var created = await CreateTeamAsync(registrar, NtindaSwiftsMen());
        var id = created.GetProperty("id").GetGuid();
        var version = created.GetProperty("version").GetUInt32();
        var body = Body(new() { ["foundedYear"] = 1998 });
        await UpdateTeamAsync(registrar, id, body);

        using (var stale = await PutAsync(registrar, TeamPath(id), body, $"\"{version}\""))
        {
            await stale.ShouldBeProblemAsync(HttpStatusCode.Conflict, "TEAM_CONCURRENCY_CONFLICT");
        }

        body["version"] = version;
        using (var staleBody = await registrar.PutAsJsonAsync(TeamPath(id), body, Ct))
        {
            await staleBody.ShouldBeProblemAsync(HttpStatusCode.Conflict, "TEAM_CONCURRENCY_CONFLICT");
        }

        body.Remove("version");
        using (var unknown = await registrar.PutAsJsonAsync(TeamPath(Guid.NewGuid()), body, Ct))
        {
            await unknown.ShouldBeProblemAsync(HttpStatusCode.NotFound, "TEAM_NOT_FOUND");
        }

        using (var otherTenants = await elsscOwner.PutAsJsonAsync(TeamPath(id), body, Ct))
        {
            await otherTenants.ShouldBeProblemAsync(HttpStatusCode.NotFound, "TEAM_NOT_FOUND");
        }

        await using var scope = ScopeFor(App.Services, kmsa, out var context);
        (await context.Set<Team>().SingleAsync(t => t.Id == id, Ct)).FoundedYear.ShouldBe(1998);
    }

    // The details' rules are create's (D6, D7), on their field; the variant and the status can't be sent.
    [Theory]
    [InlineData("""{"ageGroup":"SENIOR"}""", 400, "VALIDATION_INVALID_REQUEST", "genderCategory")]
    [InlineData("""{"genderCategory":"MEN","ageGroup":"SENIOR","secondaryColour":"#FFFFFF"}""", 422, "TEAM_COLOURS_INVALID", "secondaryColour")]
    [InlineData("""{"genderCategory":"MEN","ageGroup":"SENIOR","code":"nts"}""", 400, "VALIDATION_INVALID_REQUEST", "code")]
    [InlineData("""{"genderCategory":"MEN","ageGroup":"SENIOR","foundedYear":2999}""", 400, "VALIDATION_INVALID_REQUEST", "foundedYear")]
    [InlineData("""{"genderCategory":"MEN","ageGroup":"SENIOR","name":"   "}""", 400, "VALIDATION_INVALID_REQUEST", "name")]
    [InlineData("""{"genderCategory":"MEN","ageGroup":"SENIOR","duplicateReason":"Reviewed"}""", 400, "VALIDATION_INVALID_REQUEST", "duplicateReason")]
    [InlineData("""{"genderCategory":"MEN","ageGroup":"SENIOR","variant":"TESTBALL_FULL"}""", 400, "VALIDATION_MALFORMED_REQUEST", null)]
    [InlineData("""{"genderCategory":"MEN","ageGroup":"SENIOR","status":"DISBANDED"}""", 400, "VALIDATION_MALFORMED_REQUEST", null)]
    public async Task A_change_that_cant_be_saved_is_refused_on_its_field(string body, int status, string code, string? field)
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var team = await SaveTeamAsync(App.Services, kmsa, "Ntinda Swifts FC");

        using var content = new StringContent(body, System.Text.Encoding.UTF8, "application/json");
        using var response = await registrar.PutAsync(TeamPath(team.Id), content, Ct);

        var problem = await response.ShouldBeProblemAsync((HttpStatusCode)status, code);
        if (field is not null)
        {
            Fields(problem).ShouldContain((field, code));
        }
    }

    // D20: changing teams is the owner's, the admin's and the registrar's.
    [Theory]
    [InlineData(IdentityRoles.TenantOwner, true)]
    [InlineData(IdentityRoles.TenantAdmin, true)]
    [InlineData(IdentityRoles.Registrar, true)]
    [InlineData(IdentityRoles.CompetitionManager, false)]
    [InlineData(IdentityRoles.ResultsOfficer, false)]
    [InlineData(IdentityRoles.DataEntry, false)]
    [InlineData(IdentityRoles.Viewer, false)]
    [InlineData(IdentityRoles.Scorer, false)]
    public async Task Only_those_who_change_teams_may_update_them(string role, bool allowed)
    {
        var (kmsa, client) = await TenantAsync("kmsa", role);
        var team = await SaveTeamAsync(App.Services, kmsa, "Ntinda Swifts FC");

        using var response = await client.PutAsJsonAsync(TeamPath(team.Id), Body(new()), Ct);

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
    public async Task A_read_only_tenant_changes_no_team()
    {
        App.Authorization.UseIdentityRoles();
        var kmsa = await TenantTestData.SaveTenantAsync(
            App.Services, "kmsa", TenantStatus.Active, publicEnabled: false, withDomain: true, withSubscription: false, Ct);
        await TenantTestData.SavePeriodAsync(App.Services, kmsa, new DateOnly(2025, 1, 1), new DateOnly(2025, 12, 31), Ct, cancelReason: "Pilot paused");
        var member = await CreateMemberAsync(App.Services, kmsa, "brian.ssemakula@kmsa.example", Ct, TenantWide(IdentityRoles.Registrar));
        var registrar = App.ClientFor(member.UserId, kmsa);
        var team = await SaveTeamAsync(App.Services, kmsa, "Ntinda Swifts FC");

        using var response = await registrar.PutAsJsonAsync(TeamPath(team.Id), Body(new()), Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "TENANT_READ_ONLY");
    }

    private static TeamCategory Category(AgeGroup ageGroup) => new(GenderCategory.Men, ageGroup, TeamLevel.First);

    // An update's body: Senior Men unless replaced, and the extra fields.
    private static Dictionary<string, object?> Body(Dictionary<string, object?> fields)
    {
        var body = new Dictionary<string, object?> { ["genderCategory"] = "MEN", ["ageGroup"] = "SENIOR" };
        foreach (var (key, value) in fields)
        {
            body[key] = value;
        }

        return body;
    }

    private static async Task<JsonElement> UpdateTeamAsync(HttpClient client, Guid teamId, object body)
    {
        using var response = await client.PutAsJsonAsync(TeamPath(teamId), body, Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));
        return await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
    }

    private static async Task<HttpResponseMessage> PutAsync(HttpClient client, string path, object body, string ifMatch)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, path) { Content = JsonContent.Create(body) };
        request.Headers.TryAddWithoutValidation("If-Match", ifMatch);
        return await client.SendAsync(request, Ct);
    }

    /// <summary>An active tenant with <see cref="TestballFull"/> enabled and a client of a member with <paramref name="role"/>.</summary>
    private async Task<(Guid Tenant, HttpClient Client)> TenantAsync(string slug, string role)
    {
        App.Authorization.UseIdentityRoles();
        var tenant = await TenantTestData.SaveTenantAsync(App.Services, slug, TenantStatus.Active, Ct);
        await TenantTestData.EnableSportAsync(App.Services, tenant, TestballFull, Ct);
        var member = await CreateMemberAsync(App.Services, tenant, MailboxOf(role, $"{slug}.example"), Ct, TenantWide(role));
        return (tenant, App.ClientFor(member.UserId, tenant));
    }
}

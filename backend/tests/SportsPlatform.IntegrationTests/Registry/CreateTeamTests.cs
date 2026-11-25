using System.Globalization;
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
/// Creating a team (S07-T3a; D6–D9, D13, D21): the fields, the refusals on their field, the enabled variant, the
/// organisation and home venue, the name's start, the slug, several sports and categories of one organisation, the
/// duplicate warning with <c>duplicatesReviewed</c> and its audit entry, the read, two creates at once, the roles and the
/// read-only tenant. <c>MAX_TEAMS</c> is <see cref="TeamQuotaTests"/>.
/// </summary>
public sealed class CreateTeamTests(PostgresFixture fixture) : DatabaseTest(fixture)
{
    [Fact]
    public async Task A_team_is_created_with_its_fields_and_read_back()
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var club = await SaveOrganisationAsync(App.Services, kmsa, "Ntinda Swifts SC");
        var ground = await SaveVenueAsync(App.Services, kmsa, "Kiwatule Meadow Ground");
        var body = NtindaSwiftsMen();
        body["name"] = "  Ntinda Swifts FC ";
        body["shortName"] = "Ntinda Swifts";
        body["level"] = "RESERVE";
        body["organisationId"] = club.Id;
        body["code"] = "NTS";
        body["motto"] = "Swift and sure";
        body["primaryColour"] = "#0a3d91";
        body["secondaryColour"] = "#FFFFFF";
        body["tertiaryColour"] = "#F2C200";
        body["homeVenueId"] = ground.Id;
        body["foundedYear"] = 1998;
        body["nameSince"] = "2020-01-01";

        using var response = await registrar.PostAsJsonAsync(TeamsPath, body, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync(Ct));
        var team = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        var id = team.GetProperty("id").GetGuid();
        response.Headers.Location!.ToString().ShouldBe(TeamPath(id));
        response.Headers.ETag!.Tag.ShouldBe($"\"{team.GetProperty("version").GetUInt32()}\"");
        team.EnumerateObject().Select(property => property.Name).ShouldBe(
        [
            "id", "variant", "genderCategory", "ageGroup", "level", "name", "shortName", "nameSince", "slug", "code", "motto",
            "primaryColour", "secondaryColour", "tertiaryColour", "foundedYear", "organisation", "homeVenue", "status", "formerNames",
            "aliases", "version",
        ]);
        (team.GetProperty("formerNames").GetArrayLength(), team.GetProperty("aliases").GetArrayLength()).ShouldBe((0, 0));
        (team.GetProperty("variant").GetString(), team.GetProperty("genderCategory").GetString(), team.GetProperty("ageGroup").GetString(),
                team.GetProperty("level").GetString())
            .ShouldBe((TestballFull, "MEN", "SENIOR", "RESERVE"));
        (team.GetProperty("name").GetString(), team.GetProperty("shortName").GetString(), team.GetProperty("nameSince").GetString())
            .ShouldBe(("Ntinda Swifts FC", "Ntinda Swifts", "2020-01-01"));
        team.GetProperty("slug").GetString().ShouldBe("ntinda-swifts-fc-senior-men-reserve");
        (team.GetProperty("code").GetString(), team.GetProperty("motto").GetString()).ShouldBe(("NTS", "Swift and sure"));
        (team.GetProperty("primaryColour").GetString(), team.GetProperty("secondaryColour").GetString(),
                team.GetProperty("tertiaryColour").GetString())
            .ShouldBe(("#0A3D91", "#FFFFFF", "#F2C200"));
        team.GetProperty("foundedYear").GetInt32().ShouldBe(1998);
        var organisation = team.GetProperty("organisation");
        (organisation.GetProperty("id").GetGuid(), organisation.GetProperty("displayName").GetString(), organisation.GetProperty("slug").GetString())
            .ShouldBe((club.Id, "Ntinda Swifts SC", club.Slug));
        var homeVenue = team.GetProperty("homeVenue");
        (homeVenue.GetProperty("id").GetGuid(), homeVenue.GetProperty("name").GetString(), homeVenue.GetProperty("slug").GetString())
            .ShouldBe((ground.Id, "Kiwatule Meadow Ground", ground.Slug));
        team.GetProperty("status").GetString().ShouldBe("ACTIVE");

        var read = await registrar.ReadJsonAsync(TeamPath(id), Ct);
        read.GetRawText().ShouldBe(team.GetRawText());

        await using var scope = ScopeFor(App.Services, kmsa, out var context);
        var saved = await context.Set<Team>().Include(t => t.StatusHistory).SingleAsync(Ct);
        (saved.NameKey, saved.ShortNameKey, saved.SortKey, saved.Source).ShouldBe(("fc ntinda swifts", "ntinda swifts", "ntinda swifts fc", TeamSource.Manual));
        (saved.LogoAssetId, saved.BadgeAssetId).ShouldBe((null, null));
        var first = saved.StatusHistory.ShouldHaveSingleItem();
        (first.FromStatus, first.ToStatus, first.Reason).ShouldBe((null, TeamStatus.Active, null));
    }

    [Fact]
    public async Task Only_the_required_fields_make_a_first_team_named_since_today()
    {
        var (_, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);

        var team = await CreateTeamAsync(registrar, NtindaSwiftsMen());

        team.GetProperty("level").GetString().ShouldBe("FIRST");
        team.GetProperty("nameSince").GetString().ShouldBe(KampalaToday().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        team.GetProperty("slug").GetString().ShouldBe("ntinda-swifts-fc-senior-men");
        foreach (var field in new[]
        {
            "shortName", "code", "motto", "primaryColour", "secondaryColour", "tertiaryColour", "foundedYear", "organisation", "homeVenue",
        })
        {
            team.GetProperty(field).ValueKind.ShouldBe(JsonValueKind.Null, field);
        }
    }

    // A founded year after the tenant's year (2099) and a name's start after its date are refused on their field.
    [Theory]
    [InlineData("""{"name":"Ntinda Swifts FC","genderCategory":"MEN","ageGroup":"SENIOR"}""", 400, "VALIDATION_INVALID_REQUEST", "variant")]
    [InlineData("""{"variant":"TESTBALL_FULL","genderCategory":"MEN","ageGroup":"SENIOR"}""", 400, "VALIDATION_INVALID_REQUEST", "name")]
    [InlineData("""{"variant":"TESTBALL_FULL","name":"Ntinda Swifts FC","ageGroup":"SENIOR"}""", 400, "VALIDATION_INVALID_REQUEST", "genderCategory")]
    [InlineData("""{"variant":"TESTBALL_FULL","name":"Ntinda Swifts FC","genderCategory":"MEN"}""", 400, "VALIDATION_INVALID_REQUEST", "ageGroup")]
    [InlineData("""{"variant":"TESTHOOP_FULL","name":"Ntinda Swifts FC","genderCategory":"MEN","ageGroup":"SENIOR"}""", 422, "TEAM_SPORT_NOT_ENABLED", "variant")]
    [InlineData("""{"variant":"FOOTBALL_11","name":"Ntinda Swifts FC","genderCategory":"MEN","ageGroup":"SENIOR"}""", 422, "TEAM_SPORT_NOT_ENABLED", "variant")]
    [InlineData("""{"variant":"TESTBALL_FULL","name":" -- ","genderCategory":"MEN","ageGroup":"SENIOR"}""", 422, "TEAM_NAME_INVALID", "name")]
    [InlineData("""{"variant":"TESTBALL_FULL","name":"Ntinda Swifts FC","genderCategory":"MEN","ageGroup":"SENIOR","code":"nts"}""", 400, "VALIDATION_INVALID_REQUEST", "code")]
    [InlineData("""{"variant":"TESTBALL_FULL","name":"Ntinda Swifts FC","genderCategory":"MEN","ageGroup":"SENIOR","code":"NTSW"}""", 400, "VALIDATION_INVALID_REQUEST", "code")]
    [InlineData("""{"variant":"TESTBALL_FULL","name":"Ntinda Swifts FC","genderCategory":"MEN","ageGroup":"SENIOR","primaryColour":"blue"}""", 400, "VALIDATION_INVALID_REQUEST", "primaryColour")]
    [InlineData("""{"variant":"TESTBALL_FULL","name":"Ntinda Swifts FC","genderCategory":"MEN","ageGroup":"SENIOR","tertiaryColour":"#12345"}""", 400, "VALIDATION_INVALID_REQUEST", "tertiaryColour")]
    [InlineData("""{"variant":"TESTBALL_FULL","name":"Ntinda Swifts FC","genderCategory":"MEN","ageGroup":"SENIOR","secondaryColour":"#FFFFFF"}""", 422, "TEAM_COLOURS_INVALID", "secondaryColour")]
    [InlineData("""{"variant":"TESTBALL_FULL","name":"Ntinda Swifts FC","genderCategory":"MEN","ageGroup":"SENIOR","primaryColour":"#0A3D91","tertiaryColour":"#F2C200"}""", 422, "TEAM_COLOURS_INVALID", "tertiaryColour")]
    [InlineData("""{"variant":"TESTBALL_FULL","name":"Ntinda Swifts FC","genderCategory":"MEN","ageGroup":"SENIOR","foundedYear":1799}""", 400, "VALIDATION_INVALID_REQUEST", "foundedYear")]
    [InlineData("""{"variant":"TESTBALL_FULL","name":"Ntinda Swifts FC","genderCategory":"MEN","ageGroup":"SENIOR","foundedYear":2099}""", 400, "VALIDATION_INVALID_REQUEST", "foundedYear")]
    [InlineData("""{"variant":"TESTBALL_FULL","name":"Ntinda Swifts FC","genderCategory":"MEN","ageGroup":"SENIOR","nameSince":"1799-12-31"}""", 400, "VALIDATION_INVALID_REQUEST", "nameSince")]
    [InlineData("""{"variant":"TESTBALL_FULL","name":"Ntinda Swifts FC","genderCategory":"MEN","ageGroup":"SENIOR","nameSince":"2099-01-01"}""", 400, "VALIDATION_INVALID_REQUEST", "nameSince")]
    [InlineData("""{"variant":"TESTBALL_FULL","name":"Ntinda Swifts FC","genderCategory":"MEN","ageGroup":"SENIOR","organisationId":"0192a1b2-0000-7000-8000-0000000000ff"}""", 422, "TEAM_ORGANISATION_UNAVAILABLE", "organisationId")]
    [InlineData("""{"variant":"TESTBALL_FULL","name":"Ntinda Swifts FC","genderCategory":"MEN","ageGroup":"SENIOR","homeVenueId":"0192a1b2-0000-7000-8000-0000000000ff"}""", 422, "TEAM_HOME_VENUE_UNAVAILABLE", "homeVenueId")]
    [InlineData("""{"variant":"TESTBALL_FULL","name":"Ntinda Swifts FC","genderCategory":"MEN","ageGroup":"SENIOR","slug":"0192a1b2-0000-7000-8000-00000000c001"}""", 400, "VALIDATION_INVALID_REQUEST", "slug")]
    [InlineData("""{"variant":"TESTBALL_FULL","name":"Ntinda Swifts FC","genderCategory":"MEN","ageGroup":"SENIOR","duplicateReason":"A second club"}""", 400, "VALIDATION_INVALID_REQUEST", "duplicateReason")]
    public async Task A_team_that_cant_be_created_is_refused_on_its_field(string body, int status, string code, string field)
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);

        using var response = await registrar.PostAsync(TeamsPath, new StringContent(body, System.Text.Encoding.UTF8, "application/json"), Ct);

        var problem = await response.ShouldBeProblemAsync((HttpStatusCode)status, code);
        Fields(problem).ShouldContain((field, code));
        await using var scope = ScopeFor(App.Services, kmsa, out var context);
        (await context.Set<Team>().CountAsync(Ct)).ShouldBe(0);
    }

    // D6's length limits: one past each is refused on its field; the limit itself is accepted.
    [Theory]
    [InlineData("name", 120)]
    [InlineData("shortName", 30)]
    [InlineData("motto", 120)]
    [InlineData("duplicateReason", 500)]
    public async Task A_text_longer_than_its_limit_is_refused_on_its_field(string field, int limit)
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);

        using var refused = await registrar.PostAsJsonAsync(TeamsPath, WithText(field, limit + 1), Ct);
        Fields(await refused.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_INVALID_REQUEST"))
            .ShouldBe([(field, "VALIDATION_INVALID_REQUEST")]);

        var accepted = await CreateTeamAsync(registrar, WithText(field, limit));
        if (field != "duplicateReason")
        {
            accepted.GetProperty(field).GetString()!.Length.ShouldBe(limit);
        }

        await using var scope = ScopeFor(App.Services, kmsa, out var context);
        (await context.Set<Team>().CountAsync(Ct)).ShouldBe(1);
    }

    // D6, D8: the founded year runs to the tenant's year and the name's start to its date (Africa/Kampala), edges included.
    [Fact]
    public async Task The_name_may_start_today_and_the_team_be_founded_this_year_but_not_later()
    {
        var (_, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var today = KampalaToday();
        var body = NtindaSwiftsMen();
        body["nameSince"] = today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        body["foundedYear"] = today.Year;

        var team = await CreateTeamAsync(registrar, body);

        (team.GetProperty("nameSince").GetString(), team.GetProperty("foundedYear").GetInt32()).ShouldBe((today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), today.Year));
        var tomorrow = NtindaSwiftsMen();
        tomorrow["name"] = "Seeta Sparrows FC";
        tomorrow["nameSince"] = today.AddDays(1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        using var lateName = await registrar.PostAsJsonAsync(TeamsPath, tomorrow, Ct);
        Fields(await lateName.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_INVALID_REQUEST"))
            .ShouldBe([("nameSince", "VALIDATION_INVALID_REQUEST")]);
        var nextYear = NtindaSwiftsMen();
        nextYear["name"] = "Seeta Sparrows FC";
        nextYear["foundedYear"] = today.Year + 1;
        using var lateYear = await registrar.PostAsJsonAsync(TeamsPath, nextYear, Ct);
        Fields(await lateYear.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_INVALID_REQUEST"))
            .ShouldBe([("foundedYear", "VALIDATION_INVALID_REQUEST")]);
    }

    [Fact]
    public async Task A_status_or_unknown_member_or_category_is_refused()
    {
        var (_, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var withStatus = NtindaSwiftsMen();
        withStatus["status"] = "DISBANDED";
        var unknownAgeGroup = NtindaSwiftsMen();
        unknownAgeGroup["ageGroup"] = "U5";

        using var status = await registrar.PostAsJsonAsync(TeamsPath, withStatus, Ct);
        using var ageGroup = await registrar.PostAsJsonAsync(TeamsPath, unknownAgeGroup, Ct);

        await status.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_MALFORMED_REQUEST");
        await ageGroup.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_MALFORMED_REQUEST");
    }

    // D6 (owner, S07-T3a plan: D6's code, not D10's): the organisation isn't dissolved or archived; any other status will do.
    [Theory]
    [InlineData(OrganisationStatus.Dissolved, false)]
    [InlineData(OrganisationStatus.Archived, false)]
    [InlineData(OrganisationStatus.Suspended, true)]
    [InlineData(OrganisationStatus.Inactive, true)]
    public async Task The_organisation_must_not_be_dissolved_or_archived(OrganisationStatus status, bool accepted)
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var club = await SaveOrganisationAsync(App.Services, kmsa, "Seeta Sparrows SC", status: status);
        var body = NtindaSwiftsMen();
        body["organisationId"] = club.Id;

        using var response = await registrar.PostAsJsonAsync(TeamsPath, body, Ct);

        if (accepted)
        {
            response.StatusCode.ShouldBe(HttpStatusCode.Created);
        }
        else
        {
            Fields(await response.ShouldBeProblemAsync(HttpStatusCode.UnprocessableEntity, "TEAM_ORGANISATION_UNAVAILABLE"))
                .ShouldBe([("organisationId", "TEAM_ORGANISATION_UNAVAILABLE")]);
        }
    }

    // D6: the home venue isn't closed or archived; a temporarily unavailable one will do.
    [Theory]
    [InlineData(VenueStatus.Closed, false)]
    [InlineData(VenueStatus.Archived, false)]
    [InlineData(VenueStatus.TemporarilyUnavailable, true)]
    public async Task The_home_venue_must_be_open(VenueStatus status, bool accepted)
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var venue = await SaveVenueAsync(App.Services, kmsa, "Bukoto Community Stadium", status: status);
        var body = NtindaSwiftsMen();
        body["homeVenueId"] = venue.Id;

        using var response = await registrar.PostAsJsonAsync(TeamsPath, body, Ct);

        if (accepted)
        {
            response.StatusCode.ShouldBe(HttpStatusCode.Created);
        }
        else
        {
            Fields(await response.ShouldBeProblemAsync(HttpStatusCode.UnprocessableEntity, "TEAM_HOME_VENUE_UNAVAILABLE"))
                .ShouldBe([("homeVenueId", "TEAM_HOME_VENUE_UNAVAILABLE")]);
        }
    }

    [Fact]
    public async Task Another_tenants_organisation_and_venue_are_unavailable()
    {
        var (_, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var elssc = await TenantTestData.SaveTenantAsync(App.Services, "elssc", TenantStatus.Active, Ct);
        var school = await SaveOrganisationAsync(App.Services, elssc, "Lakeshore High School", type: OrganisationType.School);
        var fields = await SaveVenueAsync(App.Services, elssc, "Lakeshore High School Playing Fields");
        var withOrganisation = NtindaSwiftsMen();
        withOrganisation["organisationId"] = school.Id;
        var withVenue = NtindaSwiftsMen();
        withVenue["homeVenueId"] = fields.Id;

        using var organisation = await registrar.PostAsJsonAsync(TeamsPath, withOrganisation, Ct);
        using var venue = await registrar.PostAsJsonAsync(TeamsPath, withVenue, Ct);

        await organisation.ShouldBeProblemAsync(HttpStatusCode.UnprocessableEntity, "TEAM_ORGANISATION_UNAVAILABLE");
        await venue.ShouldBeProblemAsync(HttpStatusCode.UnprocessableEntity, "TEAM_HOME_VENUE_UNAVAILABLE");
    }

    // D9 (owner, S07-T3a plan): a slug is proposed from the name, the age group and gender, and a level other than FIRST,
    // with -2, -3 on a clash; one sent that another team has is refused.
    [Fact]
    public async Task Slugs_are_proposed_from_the_name_and_category_and_unique_in_the_tenant()
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var (_, elsscRegistrar) = await TenantAsync("elssc", IdentityRoles.Registrar);
        var reviewed = NtindaSwiftsMen();
        reviewed["duplicatesReviewed"] = true;
        var underSeventeen = NtindaSwiftsMen();
        underSeventeen["ageGroup"] = "U17";
        var women = NtindaSwiftsMen();
        women["genderCategory"] = "WOMEN";
        women["level"] = "DEVELOPMENT";

        (await CreateTeamAsync(registrar, NtindaSwiftsMen())).GetProperty("slug").GetString().ShouldBe("ntinda-swifts-fc-senior-men");
        (await CreateTeamAsync(registrar, reviewed)).GetProperty("slug").GetString().ShouldBe("ntinda-swifts-fc-senior-men-2");
        (await CreateTeamAsync(registrar, underSeventeen)).GetProperty("slug").GetString().ShouldBe("ntinda-swifts-fc-u17-men");
        (await CreateTeamAsync(registrar, women)).GetProperty("slug").GetString().ShouldBe("ntinda-swifts-fc-senior-women-development");
        (await CreateTeamAsync(elsscRegistrar, NtindaSwiftsMen())).GetProperty("slug").GetString().ShouldBe("ntinda-swifts-fc-senior-men");

        var withSlug = NtindaSwiftsMen();
        withSlug["name"] = "Kisaasi Kestrels FC";
        withSlug["slug"] = "kestrels";
        (await CreateTeamAsync(registrar, withSlug)).GetProperty("slug").GetString().ShouldBe("kestrels");
        var taken = NtindaSwiftsMen();
        taken["name"] = "Seeta Sparrows FC";
        taken["slug"] = "kestrels";
        using var refused = await registrar.PostAsJsonAsync(TeamsPath, taken, Ct);
        Fields(await refused.ShouldBeProblemAsync(HttpStatusCode.UnprocessableEntity, "TEAM_SLUG_TAKEN")).ShouldBe([("slug", "TEAM_SLUG_TAKEN")]);

        await using var scope = ScopeFor(App.Services, kmsa, out var context);
        (await context.Set<Team>().CountAsync(Ct)).ShouldBe(5);
    }

    // T3 acceptance: one organisation runs teams in two sports (test variants of two test sports) and in several
    // categories of one; none of them is warned about the others (owner, S07-T3a: only the same category is compared).
    [Fact]
    public async Task One_organisation_runs_teams_in_two_sports_and_several_categories()
    {
        await using var app = AppWithTestSports(App);
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar, app);
        await TenantTestData.EnableSportAsync(App.Services, kmsa, Testhoop, Ct);
        var club = await SaveOrganisationAsync(App.Services, kmsa, "Ntinda Swifts SC");
        var teams = new List<(string Variant, string Gender, string AgeGroup)>
        {
            (TestballFull, "MEN", "SENIOR"),
            (TestballFull, "WOMEN", "SENIOR"),
            (TestballFull, "MEN", "U17"),
            (Testhoop, "MEN", "SENIOR"),
        };

        foreach (var (variant, gender, ageGroup) in teams)
        {
            var body = NtindaSwiftsMen();
            body["variant"] = variant;
            body["genderCategory"] = gender;
            body["ageGroup"] = ageGroup;
            body["organisationId"] = club.Id;
            var team = await CreateTeamAsync(registrar, body);
            team.GetProperty("organisation").GetProperty("id").GetGuid().ShouldBe(club.Id);
        }

        await using var scope = ScopeFor(App.Services, kmsa, out var context);
        (await context.Set<Team>().Where(t => t.OrganisationId == club.Id).Select(t => t.Variant).ToListAsync(Ct))
            .Order().ShouldBe([TestballFull, TestballFull, TestballFull, Testhoop]);
    }

    [Fact]
    public async Task A_likely_duplicate_needs_duplicatesReviewed_and_the_audit_entry_names_it()
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var existing = await SaveTeamAsync(App.Services, kmsa, "Kisaasi Kestrels FC");
        var body = NtindaSwiftsMen();
        body["name"] = "FC Kisaasi Kestrels";

        using var refused = await registrar.PostAsJsonAsync(TeamsPath, body, Ct);

        var problem = await refused.ShouldBeProblemAsync(HttpStatusCode.UnprocessableEntity, "TEAM_DUPLICATE_REVIEW_REQUIRED");
        Fields(problem).ShouldBe([("duplicatesReviewed", "TEAM_DUPLICATE_REVIEW_REQUIRED")]);
        var candidate = problem.GetProperty("candidates").EnumerateArray().ShouldHaveSingleItem();
        (candidate.GetProperty("teamId").GetGuid(), candidate.GetProperty("confidence").GetString()).ShouldBe((existing.Id, "LIKELY"));

        body["duplicatesReviewed"] = true;
        body["duplicateReason"] = "  The Kisaasi school's own team ";
        var created = await CreateTeamAsync(registrar, body);

        await AccountTestData.DeliverOutboxAsync(App.Services, Ct);
        var entries = await AuditTestData.EntriesAsync(App.Services, Ct);
        entries.ShouldAllBe(entry => entry.ActionCode == "team.create" && entry.EntityType == "Team");
        var refusedEntry = entries.Single(e => e.ErrorCode == "TEAM_DUPLICATE_REVIEW_REQUIRED");
        var success = entries.Single(e => e.Outcome == AuditOutcome.Success);
        (refusedEntry.EntityId, refusedEntry.Reason).ShouldBe((null, null));
        (success.EntityId, success.Reason).ShouldBe((created.GetProperty("id").GetGuid(), "The Kisaasi school's own team"));
        foreach (var entry in new[] { refusedEntry, success })
        {
            var audited = JsonDocument.Parse(entry.Metadata!).RootElement.GetProperty("candidates").EnumerateArray().ShouldHaveSingleItem();
            audited.EnumerateObject().Select(property => property.Name).Order().ShouldBe(["confidence", "teamId"]);
            (audited.GetProperty("teamId").GetGuid(), audited.GetProperty("confidence").GetString()).ShouldBe((existing.Id, "LIKELY"));
        }
    }

    // D21: two creates of one team queue on the lock; the second finds the first's and is warned.
    [Fact]
    public async Task Two_creates_of_the_same_team_at_once_make_one()
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);

        var responses = await Task.WhenAll(
            registrar.PostAsJsonAsync(TeamsPath, NtindaSwiftsMen(), Ct),
            registrar.PostAsJsonAsync(TeamsPath, NtindaSwiftsMen(), Ct));

        responses.Select(r => r.StatusCode).OrderBy(s => s).ShouldBe([HttpStatusCode.Created, HttpStatusCode.UnprocessableEntity]);
        await responses.Single(r => r.StatusCode == HttpStatusCode.UnprocessableEntity)
            .ShouldBeProblemAsync(HttpStatusCode.UnprocessableEntity, "TEAM_DUPLICATE_REVIEW_REQUIRED");
        foreach (var response in responses)
        {
            response.Dispose();
        }

        await using var scope = ScopeFor(App.Services, kmsa, out var context);
        (await context.Set<Team>().CountAsync(Ct)).ShouldBe(1);
    }

    // T3 acceptance (with T3b's update): a disabled variant takes no new team.
    [Fact]
    public async Task A_disabled_variant_takes_no_new_team()
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        await CreateTeamAsync(registrar, NtindaSwiftsMen());
        await TenantTestData.DisableSportAsync(App.Services, kmsa, TestballFull, Ct);
        var women = NtindaSwiftsMen();
        women["genderCategory"] = "WOMEN";

        using var refused = await registrar.PostAsJsonAsync(TeamsPath, women, Ct);

        Fields(await refused.ShouldBeProblemAsync(HttpStatusCode.UnprocessableEntity, "TEAM_SPORT_NOT_ENABLED"))
            .ShouldBe([("variant", "TEAM_SPORT_NOT_ENABLED")]);
    }

    [Fact]
    public async Task Another_tenants_team_is_not_found_and_never_a_candidate()
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var (_, elsscOwner) = await TenantAsync("elssc", IdentityRoles.TenantOwner);
        var swifts = await SaveTeamAsync(App.Services, kmsa, "Ntinda Swifts FC");

        using var read = await elsscOwner.GetAsync(TeamPath(swifts.Id), Ct);
        await read.ShouldBeProblemAsync(HttpStatusCode.NotFound, "TEAM_NOT_FOUND");
        (await CheckTeamAsync(elsscOwner, NtindaSwiftsMen())).GetProperty("candidates").GetArrayLength().ShouldBe(0);
        (await registrar.ReadJsonAsync(TeamPath(swifts.Id), Ct)).GetProperty("name").GetString().ShouldBe("Ntinda Swifts FC");
    }

    // D20: creating teams (and the check) is the owner's, the admin's and the registrar's.
    [Theory]
    [InlineData(IdentityRoles.TenantOwner, true)]
    [InlineData(IdentityRoles.TenantAdmin, true)]
    [InlineData(IdentityRoles.Registrar, true)]
    [InlineData(IdentityRoles.CompetitionManager, false)]
    [InlineData(IdentityRoles.ResultsOfficer, false)]
    [InlineData(IdentityRoles.DataEntry, false)]
    [InlineData(IdentityRoles.Viewer, false)]
    [InlineData(IdentityRoles.Scorer, false)]
    public async Task Only_those_who_change_teams_may_create_and_check_them(string role, bool allowed)
    {
        var (_, client) = await TenantAsync("kmsa", role);

        using var check = await client.PostAsJsonAsync(TeamDuplicateCheckPath, NtindaSwiftsMen(), Ct);
        using var create = await client.PostAsJsonAsync(TeamsPath, NtindaSwiftsMen(), Ct);

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

    // D20: reading teams is every tenant-wide role's that reads people; not the scorer's (S08 gives a team manager their own team).
    [Theory]
    [InlineData(IdentityRoles.TenantOwner, true)]
    [InlineData(IdentityRoles.TenantAdmin, true)]
    [InlineData(IdentityRoles.Registrar, true)]
    [InlineData(IdentityRoles.CompetitionManager, true)]
    [InlineData(IdentityRoles.ResultsOfficer, true)]
    [InlineData(IdentityRoles.DataEntry, true)]
    [InlineData(IdentityRoles.Viewer, true)]
    [InlineData(IdentityRoles.Scorer, false)]
    public async Task Every_role_that_reads_people_reads_teams(string role, bool allowed)
    {
        var (kmsa, client) = await TenantAsync("kmsa", role);
        var swifts = await SaveTeamAsync(App.Services, kmsa, "Ntinda Swifts FC");

        using var response = await client.GetAsync(TeamPath(swifts.Id), Ct);

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
    public async Task A_read_only_tenant_reads_its_teams_but_creates_none()
    {
        App.Authorization.UseIdentityRoles();
        var kmsa = await TenantTestData.SaveTenantAsync(
            App.Services, "kmsa", TenantStatus.Active, publicEnabled: false, withDomain: true, withSubscription: false, Ct);
        await TenantTestData.SavePeriodAsync(App.Services, kmsa, new DateOnly(2025, 1, 1), new DateOnly(2025, 12, 31), Ct, cancelReason: "Pilot paused");
        await TenantTestData.EnableSportAsync(App.Services, kmsa, TestballFull, Ct);
        var member = await CreateMemberAsync(App.Services, kmsa, "brian.ssemakula@kmsa.example", Ct, TenantWide(IdentityRoles.Registrar));
        var registrar = App.ClientFor(member.UserId, kmsa);
        var swifts = await SaveTeamAsync(App.Services, kmsa, "Ntinda Swifts FC");

        using var create = await registrar.PostAsJsonAsync(TeamsPath, NtindaSwiftsMen(), Ct);
        using var read = await registrar.GetAsync(TeamPath(swifts.Id), Ct);

        await create.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "TENANT_READ_ONLY");
        read.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    // A valid body whose field is exactly length characters long.
    private static Dictionary<string, object?> WithText(string field, int length)
    {
        var body = NtindaSwiftsMen();
        body[field] = new string('n', length);
        if (field == "duplicateReason")
        {
            body["duplicatesReviewed"] = true;
        }

        return body;
    }

    private DateOnly KampalaToday() =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(App.Clock.GetUtcNow(), TimeZoneInfo.FindSystemTimeZoneById("Africa/Kampala")).DateTime);

    /// <summary>
    /// An active tenant (country UG, time zone Africa/Kampala) with <see cref="TestballFull"/> enabled, and a client of a
    /// member with <paramref name="role"/> tenant-wide, on <paramref name="app"/> when given.
    /// </summary>
    private async Task<(Guid Tenant, HttpClient Client)> TenantAsync(string slug, string role, WebApplicationFactory<Program>? app = null)
    {
        App.Authorization.UseIdentityRoles();
        var tenant = await TenantTestData.SaveTenantAsync(App.Services, slug, TenantStatus.Active, Ct);
        await TenantTestData.EnableSportAsync(App.Services, tenant, TestballFull, Ct);
        var member = await CreateMemberAsync(App.Services, tenant, MailboxOf(role, $"{slug}.example"), Ct, TenantWide(role));
        return (tenant, app is null ? App.ClientFor(member.UserId, tenant) : ClientFor(app, member.UserId, tenant));
    }

    private static HttpClient ClientFor(WebApplicationFactory<Program> app, Guid userId, Guid tenantId)
    {
        var client = app.CreateClient().AsUser(userId);
        client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());
        return client;
    }
}

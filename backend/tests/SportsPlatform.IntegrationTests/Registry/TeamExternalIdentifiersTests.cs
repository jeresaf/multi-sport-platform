using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SportsPlatform.BuildingBlocks.Actions;
using SportsPlatform.BuildingBlocks.Auditing;
using SportsPlatform.IntegrationTests.Api;
using SportsPlatform.IntegrationTests.Audit;
using SportsPlatform.IntegrationTests.Identity;
using SportsPlatform.IntegrationTests.Infrastructure;
using SportsPlatform.IntegrationTests.Tenancy;
using SportsPlatform.Modules.Identity.Domain.Roles;
using SportsPlatform.Modules.Registry.Domain.People;
using SportsPlatform.Modules.Registry.Domain.Teams;
using SportsPlatform.Modules.Tenancy.Contracts.Tenants;
using static SportsPlatform.IntegrationTests.Identity.MembershipTestData;
using static SportsPlatform.IntegrationTests.Registry.OrganisationRequests;
using static SportsPlatform.IntegrationTests.Registry.PeopleRequests;
using static SportsPlatform.IntegrationTests.Registry.RegistryTestData;
using static SportsPlatform.IntegrationTests.Registry.TeamRequests;
using static SportsPlatform.IntegrationTests.Tenancy.TestSportDefinitions;

namespace SportsPlatform.IntegrationTests.Registry;

/// <summary>
/// A team's external identifiers over HTTP (S07-T3d; D10, D11, D19, D20, D21): adding and ending them, the exact check
/// against other teams only, the disbanded and archived guards, the version, the audit, the directory search, the roles,
/// another tenant and the read-only tenant.
/// </summary>
public sealed class TeamExternalIdentifiersTests(PostgresFixture fixture) : DatabaseTest(fixture)
{
    private const string _teamNo = "KMSA_TEAM_NO";

    [Fact]
    public async Task Adding_an_identifier_shows_it_moves_the_version_and_is_audited()
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var team = await SaveTeamAsync(App.Services, kmsa, "Bunga Lakeside FC");
        var before = (await registrar.ReadJsonAsync(TeamPath(team.Id), Ct)).GetProperty("version").GetUInt32();

        using (var malformed = await registrar.PostAsJsonAsync(TeamIdentifiersPath(team.Id), new { authority = "kmsa team", value = " " }, Ct))
        {
            var problem = await malformed.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_INVALID_REQUEST");
            Fields(problem).Select(field => field.Item1).ShouldBe(["authority", "value"], ignoreOrder: true);
        }

        var added = await OkAsync(await registrar.PostAsJsonAsync(
            TeamIdentifiersPath(team.Id), new { authority = _teamNo, value = " KMSA-T-0201 ", validFrom = "2026-01-10", version = before }, Ct));

        added.GetProperty("version").GetUInt32().ShouldNotBe(before);
        var identifier = added.GetProperty("externalIdentifiers").EnumerateArray().ShouldHaveSingleItem();
        (identifier.GetProperty("authority").GetString(), identifier.GetProperty("value").GetString(), identifier.GetProperty("validFrom").GetString())
            .ShouldBe((_teamNo, "KMSA-T-0201", "2026-01-10"));
        identifier.GetProperty("validTo").ValueKind.ShouldBe(JsonValueKind.Null);
        (await registrar.ReadJsonAsync(TeamPath(team.Id), Ct)).GetProperty("externalIdentifiers").GetArrayLength().ShouldBe(1);

        // The national-document authorities are a person rule only (D10).
        await OkAsync(await registrar.PostAsJsonAsync(TeamIdentifiersPath(team.Id), new { authority = "NIN", value = "LAKE-77" }, Ct));
        using (var stale = await registrar.PostAsJsonAsync(TeamIdentifiersPath(team.Id), new { authority = "UFA_TEAM_NO", value = "UFA-9", version = before }, Ct))
        {
            await stale.ShouldBeProblemAsync(HttpStatusCode.Conflict, "TEAM_CONCURRENCY_CONFLICT");
        }

        await AccountTestData.DeliverOutboxAsync(App.Services, Ct);
        var entry = (await AuditTestData.EntriesAsync(App.Services, Ct))
            .First(e => e.ActionCode == "team.external_id.add" && e.Outcome == AuditOutcome.Success && e.Metadata!.Contains(_teamNo, StringComparison.Ordinal));
        var metadata = JsonDocument.Parse(entry.Metadata!).RootElement;
        (metadata.GetProperty("identifierId").GetGuid(), metadata.GetProperty("authority").GetString())
            .ShouldBe((identifier.GetProperty("id").GetGuid(), _teamNo));
        (entry.EntityType, entry.EntityId).ShouldBe(("Team", team.Id));
    }

    [Fact]
    public async Task An_identifier_any_team_has_or_had_is_refused_but_another_entity_types_is_not()
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var bunga = await SaveTeamAsync(App.Services, kmsa, "Bunga Lakeside FC");
        var kisaasi = await SaveTeamAsync(App.Services, kmsa, "Kisaasi Kestrels FC");
        var identifierId = await AddIdentifierAsync(registrar, bunga.Id, "KMSA-T-0147");
        await OkAsync(await registrar.PostAsJsonAsync(TeamIdentifierPath(bunga.Id, identifierId, "end"), new { }, Ct));

        foreach (var (team, value) in new[] { (kisaasi.Id, "KMSA-T-0147"), (kisaasi.Id, " KMSA-T-0147 "), (bunga.Id, "KMSA-T-0147") })
        {
            using var taken = await registrar.PostAsJsonAsync(TeamIdentifiersPath(team), new { authority = _teamNo, value }, Ct);
            var problem = await taken.ShouldBeProblemAsync(HttpStatusCode.Conflict, "TEAM_EXTERNAL_ID_EXISTS");
            Fields(problem).ShouldBe([("value", "TEAM_EXTERNAL_ID_EXISTS")]);
        }

        // Other entity types: a person or an organisation may hold the value a team had, and a team theirs.
        var person = await SavePersonAsync(App.Services, kmsa, IvanMukasa, Ct);
        using (var personal = await registrar.PostAsJsonAsync(
                   $"{PersonPath(person.Id)}/external-identifiers", new { authority = _teamNo, value = "KMSA-T-0147" }, Ct))
        {
            personal.StatusCode.ShouldBe(HttpStatusCode.OK, await personal.Content.ReadAsStringAsync(Ct));
        }

        var organisation = await SaveOrganisationAsync(App.Services, kmsa, "Ntinda Swifts SC");
        await SaveIdentifierAsync(kmsa, ExternalIdentifier.ForOrganisation(organisation.Id, _teamNo, "KMSA-T-0388"));
        await OkAsync(await registrar.PostAsJsonAsync(TeamIdentifiersPath(kisaasi.Id), new { authority = _teamNo, value = "KMSA-T-0388" }, Ct));

        // Another authority may give the same value.
        await OkAsync(await registrar.PostAsJsonAsync(TeamIdentifiersPath(kisaasi.Id), new { authority = "UFA_TEAM_NO", value = "KMSA-T-0147" }, Ct));
    }

    [Fact]
    public async Task A_stale_If_Match_is_a_team_conflict_for_both_identifier_changes()
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var team = await SaveTeamAsync(App.Services, kmsa, "Bunga Lakeside FC");
        var identifierId = await AddIdentifierAsync(registrar, team.Id, "KMSA-T-0147");
        var read = await registrar.ReadJsonAsync(TeamPath(team.Id), Ct);
        var stale = $"\"{read.GetProperty("version").GetUInt32() + 1}\"";

        foreach (var (path, body) in Requests(team.Id, identifierId))
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
            request.Headers.TryAddWithoutValidation("If-Match", stale);
            using var response = await registrar.SendAsync(request, Ct);
            await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, "TEAM_CONCURRENCY_CONFLICT");
        }
    }

    // The outcome only: the teams lock refuses the second add before its insert, and without the lock the unique index
    // would refuse it on save with the same answer, so this doesn't prove the lock is taken.
    [Fact]
    public async Task Two_teams_given_one_value_at_once_leave_one_refused()
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var bunga = await SaveTeamAsync(App.Services, kmsa, "Bunga Lakeside FC");
        var kisaasi = await SaveTeamAsync(App.Services, kmsa, "Kisaasi Kestrels FC");

        var responses = await Task.WhenAll(
            registrar.PostAsJsonAsync(TeamIdentifiersPath(bunga.Id), new { authority = _teamNo, value = "KMSA-T-0388" }, Ct),
            registrar.PostAsJsonAsync(TeamIdentifiersPath(kisaasi.Id), new { authority = _teamNo, value = "KMSA-T-0388" }, Ct));

        responses.Select(response => response.StatusCode).ShouldBe([HttpStatusCode.OK, HttpStatusCode.Conflict], ignoreOrder: true);
        await responses.Single(response => response.StatusCode == HttpStatusCode.Conflict)
            .ShouldBeProblemAsync(HttpStatusCode.Conflict, "TEAM_EXTERNAL_ID_EXISTS");
        foreach (var response in responses)
        {
            response.Dispose();
        }
    }

    [Fact]
    public async Task Ending_an_identifier_sets_a_date_from_its_start_to_today_once()
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var team = await SaveTeamAsync(App.Services, kmsa, "Bunga Lakeside FC");
        var other = await SaveTeamAsync(App.Services, kmsa, "Kisaasi Kestrels FC");
        var today = TenantToday();
        var teamNo = await AddIdentifierAsync(registrar, team.Id, "KMSA-T-0147", validFrom: today.AddDays(-30));
        var leagueNo = await AddIdentifierAsync(registrar, team.Id, "UFA-9", authority: "UFA_TEAM_NO");

        foreach (var validTo in new[] { today.AddDays(1), today.AddDays(-31) })
        {
            using var invalid = await registrar.PostAsJsonAsync(TeamIdentifierPath(team.Id, teamNo, "end"), new { validTo }, Ct);
            var problem = await invalid.ShouldBeProblemAsync(HttpStatusCode.UnprocessableEntity, "TEAM_EXTERNAL_ID_VALIDITY_INVALID");
            Fields(problem).ShouldBe([("validTo", "TEAM_EXTERNAL_ID_VALIDITY_INVALID")]);
        }

        using (var blank = await registrar.PostAsJsonAsync(TeamIdentifierPath(team.Id, teamNo, "end"), new { reason = " " }, Ct))
        {
            await blank.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_INVALID_REQUEST");
        }

        // Another team's identifier, an unknown one, and an organisation's with the same ID space are not this team's.
        var organisation = await SaveOrganisationAsync(App.Services, kmsa, "Ntinda Swifts SC");
        var organisationNo = await SaveIdentifierAsync(kmsa, ExternalIdentifier.ForOrganisation(organisation.Id, "KMSA_CLUB_NO", "KMSA-0147"));
        foreach (var (owner, id) in new[] { (other.Id, teamNo), (team.Id, Guid.CreateVersion7()), (team.Id, organisationNo) })
        {
            using var missing = await registrar.PostAsync(TeamIdentifierPath(owner, id, "end"), null, Ct);
            await missing.ShouldBeProblemAsync(HttpStatusCode.NotFound, "TEAM_EXTERNAL_ID_NOT_FOUND");
        }

        var before = (await registrar.ReadJsonAsync(TeamPath(team.Id), Ct)).GetProperty("version").GetUInt32();
        var first = await OkAsync(await registrar.PostAsJsonAsync(
            TeamIdentifierPath(team.Id, teamNo, "end"), new { validTo = today.AddDays(-30), reason = "Renumbered by the association" }, Ct));
        first.GetProperty("version").GetUInt32().ShouldNotBe(before);
        var ended = await OkAsync(await registrar.PostAsync(TeamIdentifierPath(team.Id, leagueNo, "end"), null, Ct));

        // By authority: KMSA_TEAM_NO before UFA_TEAM_NO.
        ended.GetProperty("externalIdentifiers").EnumerateArray()
            .Select(i => (i.GetProperty("id").GetGuid(), i.GetProperty("validTo").GetString()))
            .ShouldBe([(teamNo, Iso(today.AddDays(-30))), (leagueNo, Iso(today))]);
        using var again = await registrar.PostAsync(TeamIdentifierPath(team.Id, leagueNo, "end"), null, Ct);
        await again.ShouldBeProblemAsync(HttpStatusCode.Conflict, "TEAM_EXTERNAL_ID_ENDED");
        await AccountTestData.DeliverOutboxAsync(App.Services, Ct);
        (await AuditTestData.EntriesAsync(App.Services, Ct))
            .Where(e => e.ActionCode == "team.external_id.end" && e.Outcome == AuditOutcome.Success)
            .Select(e => e.Reason).ShouldBe(["Renumbered by the association", null], ignoreOrder: true);
    }

    // D10: a disbanded or archived team takes no new identifier; ending stays allowed.
    [Theory]
    [InlineData(TeamStatus.Active, null)]
    [InlineData(TeamStatus.Inactive, null)]
    [InlineData(TeamStatus.Suspended, null)]
    [InlineData(TeamStatus.Disbanded, "TEAM_DISBANDED")]
    [InlineData(TeamStatus.Archived, "TEAM_ARCHIVED")]
    public async Task Only_a_team_that_isnt_disbanded_or_archived_takes_a_new_identifier(TeamStatus status, string? refusal)
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var team = await SaveTeamAsync(App.Services, kmsa, "Bunga Lakeside FC", status: status);
        var identifierId = await SaveIdentifierAsync(kmsa, ExternalIdentifier.ForTeam(team.Id, _teamNo, "KMSA-T-0147"));

        using (var response = await registrar.PostAsJsonAsync(TeamIdentifiersPath(team.Id), new { authority = "UFA_TEAM_NO", value = "UFA-9" }, Ct))
        {
            if (refusal is null)
            {
                response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));
            }
            else
            {
                await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, refusal);
            }
        }

        await OkAsync(await registrar.PostAsync(TeamIdentifierPath(team.Id, identifierId, "end"), null, Ct));
    }

    [Fact]
    public async Task Another_tenants_team_is_not_found()
    {
        var (kmsa, _) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var (elssc, elsscOwner) = await TenantAsync("elssc", IdentityRoles.TenantOwner);
        var team = await SaveTeamAsync(App.Services, kmsa, "Bunga Lakeside FC");
        var identifierId = await SaveIdentifierAsync(kmsa, ExternalIdentifier.ForTeam(team.Id, _teamNo, "KMSA-T-0147"));

        foreach (var (path, body) in Requests(team.Id, identifierId))
        {
            using var response = await elsscOwner.PostAsJsonAsync(path, body, Ct);
            await response.ShouldBeProblemAsync(HttpStatusCode.NotFound, "TEAM_NOT_FOUND");
        }

        // Another tenant's identifier value is not taken here.
        var own = await SaveTeamAsync(App.Services, elssc, "Lakeshore High School FC");
        await OkAsync(await elsscOwner.PostAsJsonAsync(TeamIdentifiersPath(own.Id), new { authority = _teamNo, value = "KMSA-T-0147" }, Ct));
    }

    // D20: identifiers are the owner's, the admin's and the registrar's.
    [Theory]
    [InlineData(IdentityRoles.TenantOwner, true)]
    [InlineData(IdentityRoles.TenantAdmin, true)]
    [InlineData(IdentityRoles.Registrar, true)]
    [InlineData(IdentityRoles.CompetitionManager, false)]
    [InlineData(IdentityRoles.ResultsOfficer, false)]
    [InlineData(IdentityRoles.DataEntry, false)]
    [InlineData(IdentityRoles.Viewer, false)]
    [InlineData(IdentityRoles.Scorer, false)]
    public async Task Only_those_who_change_teams_may_change_identifiers(string role, bool allowed)
    {
        var (kmsa, client) = await TenantAsync("kmsa", role);
        var team = await SaveTeamAsync(App.Services, kmsa, "Bunga Lakeside FC");
        var identifierId = await SaveIdentifierAsync(kmsa, ExternalIdentifier.ForTeam(team.Id, _teamNo, "KMSA-T-0147"));

        foreach (var (path, body) in Requests(team.Id, identifierId))
        {
            using var response = await client.PostAsJsonAsync(path, body, Ct);
            if (allowed)
            {
                response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));
            }
            else
            {
                await response.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "AUTH_FORBIDDEN");
            }
        }
    }

    // D20: a team manager gets their own team through the TEAM scope in S08, not in S07.
    [Fact]
    public async Task A_team_manager_changes_no_identifier_of_their_team()
    {
        var (kmsa, _) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var team = await SaveTeamAsync(App.Services, kmsa, "Bunga Lakeside FC");
        var identifierId = await SaveIdentifierAsync(kmsa, ExternalIdentifier.ForTeam(team.Id, _teamNo, "KMSA-T-0147"));
        var manager = await CreateMemberAsync(
            App.Services, kmsa, MailboxOf(IdentityRoles.TeamManager, "kmsa.example"), Ct, (IdentityRoles.TeamManager, AssignmentScope.Team, team.Id));
        var client = App.ClientFor(manager.UserId, kmsa);

        foreach (var (path, body) in Requests(team.Id, identifierId))
        {
            using var response = await client.PostAsJsonAsync(path, body, Ct);
            await response.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "AUTH_FORBIDDEN");
        }
    }

    [Fact]
    public async Task A_read_only_tenant_changes_no_identifier()
    {
        App.Authorization.UseIdentityRoles();
        var kmsa = await TenantTestData.SaveTenantAsync(
            App.Services, "kmsa", TenantStatus.Active, publicEnabled: false, withDomain: true, withSubscription: false, Ct);
        await TenantTestData.SavePeriodAsync(App.Services, kmsa, new DateOnly(2025, 1, 1), new DateOnly(2025, 12, 31), Ct, cancelReason: "Pilot paused");
        var team = await SaveTeamAsync(App.Services, kmsa, "Bunga Lakeside FC");
        var member = await CreateMemberAsync(App.Services, kmsa, "brian.ssemakula@kmsa.example", Ct, TenantWide(IdentityRoles.Registrar));
        var registrar = App.ClientFor(member.UserId, kmsa);

        foreach (var (path, body) in Requests(team.Id, Guid.CreateVersion7()))
        {
            using var response = await registrar.PostAsJsonAsync(path, body, Ct);
            await response.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "TENANT_READ_ONLY");
        }
    }

    [Theory]
    [InlineData("KMSA-T-0147")]
    [InlineData("0147")]
    public async Task The_directory_finds_a_team_by_an_identifiers_exact_value_ended_ones_included(string q)
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var team = await SaveTeamAsync(App.Services, kmsa, "Bunga Lakeside FC");
        var other = await SaveTeamAsync(App.Services, kmsa, "Kisaasi Kestrels FC");
        var identifierId = await SaveIdentifierAsync(kmsa, ExternalIdentifier.ForTeam(team.Id, _teamNo, q));
        await SaveIdentifierAsync(kmsa, ExternalIdentifier.ForTeam(other.Id, _teamNo, $"{q}-B"));
        await OkAsync(await registrar.PostAsync(TeamIdentifierPath(team.Id, identifierId, "end"), null, Ct));

        TeamNames(await registrar.ReadJsonAsync($"{TeamsPath}?q={Uri.EscapeDataString(q)}", Ct)).ShouldBe(["Bunga Lakeside FC"]);

        // A person's or an organisation's identifier with the same value finds no team.
        var person = await SavePersonAsync(App.Services, kmsa, IvanMukasa, Ct);
        var organisation = await SaveOrganisationAsync(App.Services, kmsa, "Ntinda Swifts SC");
        await SaveIdentifierAsync(kmsa, ExternalIdentifier.ForPerson(person.Id, "UFA_PLAYER_NO", "UFA-PL-88"));
        await SaveIdentifierAsync(kmsa, ExternalIdentifier.ForOrganisation(organisation.Id, "KMSA_CLUB_NO", "KMSA-0388"));

        TeamNames(await registrar.ReadJsonAsync($"{TeamsPath}?q=UFA-PL-88", Ct)).ShouldBeEmpty();
        TeamNames(await registrar.ReadJsonAsync($"{TeamsPath}?q=KMSA-0388", Ct)).ShouldBeEmpty();
    }

    [Fact]
    public async Task Text_without_a_letter_or_digit_finds_only_an_identifier()
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var team = await SaveTeamAsync(App.Services, kmsa, "Bunga Lakeside FC");
        await SaveTeamAsync(App.Services, kmsa, "Kisaasi Kestrels FC");
        await SaveIdentifierAsync(kmsa, ExternalIdentifier.ForTeam(team.Id, _teamNo, "--"));

        TeamNames(await registrar.ReadJsonAsync($"{TeamsPath}?q=--", Ct)).ShouldBe(["Bunga Lakeside FC"]);
        TeamNames(await registrar.ReadJsonAsync($"{TeamsPath}?q=%2B%2B", Ct)).ShouldBeEmpty();
    }

    // The two requests, in the order that succeeds on a team with the identifier given.
    private static (string Path, object Body)[] Requests(Guid teamId, Guid identifierId) =>
    [
        (TeamIdentifiersPath(teamId), new { authority = "UFA_TEAM_NO", value = "UFA-9" }),
        (TeamIdentifierPath(teamId, identifierId, "end"), new { }),
    ];

    private static async Task<Guid> AddIdentifierAsync(
        HttpClient client, Guid teamId, string value, string authority = _teamNo, DateOnly? validFrom = null)
    {
        var team = await OkAsync(await client.PostAsJsonAsync(TeamIdentifiersPath(teamId), new { authority, value, validFrom }, Ct));
        return team.GetProperty("externalIdentifiers").EnumerateArray()
            .Single(i => i.GetProperty("authority").GetString() == authority && i.GetProperty("value").GetString() == value)
            .GetProperty("id").GetGuid();
    }

    // An identifier saved directly (the test's starting point).
    private async Task<Guid> SaveIdentifierAsync(Guid tenantId, ExternalIdentifier identifier)
    {
        await using var scope = ScopeFor(App.Services, tenantId, out var context);
        context.Add(identifier);
        await context.SaveChangesAsync(Ct);
        return identifier.Id;
    }

    private static List<string?> TeamNames(JsonElement page) =>
        [.. page.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("name").GetString())];

    private static string Iso(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    // The team answered, with its version as the ETag.
    private static async Task<JsonElement> OkAsync(HttpResponseMessage response)
    {
        using (response)
        {
            response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));
            var team = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
            response.Headers.ETag.ShouldNotBeNull().Tag.ShouldBe($"\"{team.GetProperty("version").GetUInt32()}\"");
            return team;
        }
    }

    // The test tenants' zone is Africa/Kampala (UTC+3, no daylight saving).
    private DateOnly TenantToday() => DateOnly.FromDateTime(App.Clock.GetUtcNow().AddHours(3).UtcDateTime);

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

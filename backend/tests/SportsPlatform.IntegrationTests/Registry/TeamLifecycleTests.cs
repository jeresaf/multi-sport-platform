using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SportsPlatform.BuildingBlocks.Actions;
using SportsPlatform.BuildingBlocks.Auditing;
using SportsPlatform.IntegrationTests.Api;
using SportsPlatform.IntegrationTests.Audit;
using SportsPlatform.IntegrationTests.Identity;
using SportsPlatform.IntegrationTests.Infrastructure;
using SportsPlatform.IntegrationTests.Tenancy;
using SportsPlatform.Modules.Identity.Domain.Roles;
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
/// The team lifecycle over HTTP (S07-T3b-1; D12, Q55): every move from every status that isn't archived, with its
/// history row and audit entry; unarchiving only to the status before archiving; the reason, the version, the history
/// read, the roles and the read-only tenant; a variant disabled since doesn't stop a move (T3 acceptance 4), and an
/// organisation's moves leave its teams alone (T2 acceptance 2). The quota on reform and unarchive is
/// <see cref="TeamQuotaTests"/>.
/// </summary>
public sealed class TeamLifecycleTests(PostgresFixture fixture) : DatabaseTest(fixture)
{
    private const string _reason = "League fees unpaid for the season";

    private static readonly Dictionary<TeamStatus, string> _codes = new()
    {
        [TeamStatus.Active] = "ACTIVE",
        [TeamStatus.Inactive] = "INACTIVE",
        [TeamStatus.Suspended] = "SUSPENDED",
        [TeamStatus.Disbanded] = "DISBANDED",
        [TeamStatus.Archived] = "ARCHIVED",
    };

    // D12: the moves from each status that isn't archived; every other pair is refused.
    private static readonly HashSet<(TeamStatus, TeamStatus)> _allowed =
    [
        (TeamStatus.Active, TeamStatus.Inactive),
        (TeamStatus.Active, TeamStatus.Suspended),
        (TeamStatus.Active, TeamStatus.Disbanded),
        (TeamStatus.Active, TeamStatus.Archived),
        (TeamStatus.Inactive, TeamStatus.Active),
        (TeamStatus.Inactive, TeamStatus.Suspended),
        (TeamStatus.Inactive, TeamStatus.Disbanded),
        (TeamStatus.Inactive, TeamStatus.Archived),
        (TeamStatus.Suspended, TeamStatus.Active),
        (TeamStatus.Suspended, TeamStatus.Inactive),
        (TeamStatus.Suspended, TeamStatus.Disbanded),
        (TeamStatus.Suspended, TeamStatus.Archived),
        (TeamStatus.Disbanded, TeamStatus.Active),
        (TeamStatus.Disbanded, TeamStatus.Archived),
    ];

    public static TheoryData<TeamStatus, TeamStatus> EveryPair()
    {
        var data = new TheoryData<TeamStatus, TeamStatus>();
        foreach (var from in Enum.GetValues<TeamStatus>().Where(status => status != TeamStatus.Archived))
        {
            foreach (var to in Enum.GetValues<TeamStatus>())
            {
                data.Add(from, to);
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(EveryPair))]
    public async Task Each_status_allows_only_its_moves(TeamStatus from, TeamStatus to)
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var team = await SaveTeamAsync(App.Services, kmsa, "Ntinda Swifts FC", status: from);

        using var response = await registrar.PostAsJsonAsync(
            TeamStatusPath(team.Id), new { status = _codes[to], reason = _reason }, Ct);

        await AccountTestData.DeliverOutboxAsync(App.Services, Ct);
        var entry = (await AuditTestData.EntriesAsync(App.Services, Ct)).ShouldHaveSingleItem();
        (entry.ActionCode, entry.EntityType, entry.EntityId, entry.Reason)
            .ShouldBe(("team.status.change", "Team", team.Id, _reason));
        await using var scope = ScopeFor(App.Services, kmsa, out var context);
        var saved = await context.Set<Team>().AsNoTracking().Include(t => t.StatusHistory).SingleAsync(Ct);
        if (_allowed.Contains((from, to)))
        {
            response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));
            (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("status").GetString().ShouldBe(_codes[to]);
            saved.Status.ShouldBe(to);
            saved.StatusHistory.ShouldContain(c => c.FromStatus == from && c.ToStatus == to && c.Reason == _reason);
            entry.Outcome.ShouldBe(AuditOutcome.Success);
        }
        else
        {
            await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, "TEAM_STATUS_INVALID");
            saved.Status.ShouldBe(from);

            // No row for a refused move: the creation row, and the test's setup move for a status other than ACTIVE.
            saved.StatusHistory.Count.ShouldBe(from == TeamStatus.Active ? 1 : 2);
            entry.ErrorCode.ShouldBe("TEAM_STATUS_INVALID");
        }
    }

    // Q55: archiving a disbanded team and unarchiving it never reforms it.
    [Theory]
    [InlineData(TeamStatus.Active)]
    [InlineData(TeamStatus.Inactive)]
    [InlineData(TeamStatus.Suspended)]
    [InlineData(TeamStatus.Disbanded)]
    public async Task Unarchiving_goes_back_only_to_the_status_before_archiving(TeamStatus before)
    {
        var (_, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var id = (await CreateTeamAsync(registrar, NtindaSwiftsMen())).GetProperty("id").GetGuid();
        if (before != TeamStatus.Active)
        {
            App.Clock.Advance(TimeSpan.FromMinutes(1));
            await MoveAsync(registrar, id, before, _reason);
        }

        App.Clock.Advance(TimeSpan.FromMinutes(1));
        await MoveAsync(registrar, id, TeamStatus.Archived, "Folded into the first team");
        App.Clock.Advance(TimeSpan.FromMinutes(1));

        foreach (var other in _codes.Keys.Where(status => status != before))
        {
            using var refused = await registrar.PostAsJsonAsync(
                TeamStatusPath(id), new { status = _codes[other], reason = "Archived by mistake" }, Ct);
            await refused.ShouldBeProblemAsync(HttpStatusCode.Conflict, "TEAM_STATUS_INVALID");
        }

        (await MoveAsync(registrar, id, before, "Archived by mistake")).GetProperty("status").GetString().ShouldBe(_codes[before]);
    }

    [Fact]
    public async Task A_second_archive_is_undone_to_the_status_it_archived()
    {
        var (_, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var id = (await CreateTeamAsync(registrar, NtindaSwiftsMen())).GetProperty("id").GetGuid();

        foreach (var (to, reason) in new[]
        {
            (TeamStatus.Archived, "Recorded twice"),
            (TeamStatus.Active, "Recorded once after all"),
            (TeamStatus.Suspended, "League fees unpaid"),
            (TeamStatus.Archived, "Folded into the first team"),
        })
        {
            App.Clock.Advance(TimeSpan.FromMinutes(1));
            await MoveAsync(registrar, id, to, reason);
        }

        using var active = await registrar.PostAsJsonAsync(TeamStatusPath(id), new { status = "ACTIVE", reason = "Fees paid" }, Ct);
        await active.ShouldBeProblemAsync(HttpStatusCode.Conflict, "TEAM_STATUS_INVALID");
        (await MoveAsync(registrar, id, TeamStatus.Suspended, "Archived by mistake"))
            .GetProperty("status").GetString().ShouldBe("SUSPENDED");
    }

    [Fact]
    public async Task The_history_lists_every_move_newest_first_with_who_and_why()
    {
        var (_, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var (_, elsscOwner) = await TenantAsync("elssc", IdentityRoles.TenantOwner);
        var id = (await CreateTeamAsync(registrar, NtindaSwiftsMen())).GetProperty("id").GetGuid();
        var created = App.Clock.GetUtcNow();
        App.Clock.Advance(TimeSpan.FromMinutes(2));
        await MoveAsync(registrar, id, TeamStatus.Suspended, $" {_reason}  ");
        App.Clock.Advance(TimeSpan.FromMinutes(40));
        await MoveAsync(registrar, id, TeamStatus.Active, "Fees paid");

        var history = await registrar.ReadJsonAsync(TeamStatusChangesPath(id), Ct);

        var items = history.GetProperty("items").EnumerateArray().ToList();
        items.Select(i => (i.GetProperty("fromStatus").GetString(), i.GetProperty("toStatus").GetString(), i.GetProperty("reason").GetString()))
            .ShouldBe(
            [
                ("SUSPENDED", "ACTIVE", "Fees paid"),
                ("ACTIVE", "SUSPENDED", _reason),
                (null, "ACTIVE", null),
            ]);
        items[^1].GetProperty("occurredAt").GetDateTimeOffset().ShouldBe(created);
        items.ShouldAllBe(i => i.GetProperty("actorKind").GetString() == "USER");

        using (var unknown = await registrar.GetAsync(TeamStatusChangesPath(Guid.NewGuid()), Ct))
        {
            await unknown.ShouldBeProblemAsync(HttpStatusCode.NotFound, "TEAM_NOT_FOUND");
        }

        using var otherTenants = await elsscOwner.GetAsync(TeamStatusChangesPath(id), Ct);
        await otherTenants.ShouldBeProblemAsync(HttpStatusCode.NotFound, "TEAM_NOT_FOUND");
    }

    [Theory]
    [InlineData("""{"status":"SUSPENDED"}""", "VALIDATION_INVALID_REQUEST", "reason", "VALIDATION_REASON_REQUIRED")]
    [InlineData("""{"status":"SUSPENDED","reason":"   "}""", "VALIDATION_INVALID_REQUEST", "reason", "VALIDATION_REASON_REQUIRED")]
    [InlineData("""{"reason":"League fees unpaid"}""", "VALIDATION_INVALID_REQUEST", "status", "VALIDATION_INVALID_REQUEST")]
    [InlineData("""{"status":"MERGED","reason":"League fees unpaid"}""", "VALIDATION_MALFORMED_REQUEST", null, null)]
    public async Task A_move_without_a_status_or_a_reason_is_refused(string body, string code, string? field, string? fieldCode)
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var team = await SaveTeamAsync(App.Services, kmsa, "Ntinda Swifts FC");

        using var content = new StringContent(body, System.Text.Encoding.UTF8, "application/json");
        using var response = await registrar.PostAsync(TeamStatusPath(team.Id), content, Ct);

        var problem = await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, code);
        if (field is not null)
        {
            Fields(problem).ShouldBe([(field, fieldCode)]);
        }
    }

    [Fact]
    public async Task A_reason_longer_than_500_characters_is_refused()
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var team = await SaveTeamAsync(App.Services, kmsa, "Ntinda Swifts FC");

        using var longest = await registrar.PostAsJsonAsync(
            TeamStatusPath(team.Id), new { status = "SUSPENDED", reason = new string('r', 500) }, Ct);
        using var tooLong = await registrar.PostAsJsonAsync(
            TeamStatusPath(team.Id), new { status = "ACTIVE", reason = new string('r', 501) }, Ct);

        longest.StatusCode.ShouldBe(HttpStatusCode.OK);
        var problem = await tooLong.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_INVALID_REQUEST");
        Fields(problem).ShouldBe([("reason", "VALIDATION_REASON_REQUIRED")]);
    }

    [Fact]
    public async Task A_move_from_a_stale_version_is_a_team_conflict_and_a_move_changes_the_version()
    {
        var (_, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var created = await CreateTeamAsync(registrar, NtindaSwiftsMen());
        var id = created.GetProperty("id").GetGuid();
        var version = created.GetProperty("version").GetUInt32();

        var moved = await MoveAsync(registrar, id, TeamStatus.Suspended, _reason, $"\"{version}\"");
        moved.GetProperty("version").GetUInt32().ShouldNotBe(version);

        using var request = new HttpRequestMessage(HttpMethod.Post, TeamStatusPath(id))
        {
            Content = JsonContent.Create(new { status = "ACTIVE", reason = "Fees paid" }),
        };
        request.Headers.TryAddWithoutValidation("If-Match", $"\"{version}\"");
        using var stale = await registrar.SendAsync(request, Ct);
        await stale.ShouldBeProblemAsync(HttpStatusCode.Conflict, "TEAM_CONCURRENCY_CONFLICT");

        using var staleBody = await registrar.PostAsJsonAsync(
            TeamStatusPath(id), new { status = "ACTIVE", reason = "Fees paid", version }, Ct);
        await staleBody.ShouldBeProblemAsync(HttpStatusCode.Conflict, "TEAM_CONCURRENCY_CONFLICT");

        using var unknown = await registrar.PostAsJsonAsync(
            TeamStatusPath(Guid.NewGuid()), new { status = "SUSPENDED", reason = _reason }, Ct);
        await unknown.ShouldBeProblemAsync(HttpStatusCode.NotFound, "TEAM_NOT_FOUND");
    }

    [Fact]
    public async Task Another_tenants_team_cant_be_moved()
    {
        var (kmsa, _) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var (_, elsscOwner) = await TenantAsync("elssc", IdentityRoles.TenantOwner);
        var team = await SaveTeamAsync(App.Services, kmsa, "Ntinda Swifts FC");

        using var response = await elsscOwner.PostAsJsonAsync(
            TeamStatusPath(team.Id), new { status = "SUSPENDED", reason = _reason }, Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.NotFound, "TEAM_NOT_FOUND");
        await using var scope = ScopeFor(App.Services, kmsa, out var context);
        var saved = await context.Set<Team>().AsNoTracking().Include(t => t.StatusHistory).SingleAsync(Ct);
        (saved.Status, saved.StatusHistory.Count).ShouldBe((TeamStatus.Active, 1));
    }

    // D20: moving teams is the owner's, the admin's and the registrar's; the history is read with the record.
    [Theory]
    [InlineData(IdentityRoles.TenantOwner, true, true)]
    [InlineData(IdentityRoles.TenantAdmin, true, true)]
    [InlineData(IdentityRoles.Registrar, true, true)]
    [InlineData(IdentityRoles.CompetitionManager, false, true)]
    [InlineData(IdentityRoles.ResultsOfficer, false, true)]
    [InlineData(IdentityRoles.DataEntry, false, true)]
    [InlineData(IdentityRoles.Viewer, false, true)]
    [InlineData(IdentityRoles.Scorer, false, false)]
    public async Task Only_those_who_change_teams_may_move_them(string role, bool changes, bool reads)
    {
        var (kmsa, client) = await TenantAsync("kmsa", role);
        var team = await SaveTeamAsync(App.Services, kmsa, "Ntinda Swifts FC");

        using var move = await client.PostAsJsonAsync(
            TeamStatusPath(team.Id), new { status = "SUSPENDED", reason = _reason }, Ct);
        using var history = await client.GetAsync(TeamStatusChangesPath(team.Id), Ct);

        if (changes)
        {
            move.StatusCode.ShouldBe(HttpStatusCode.OK);
        }
        else
        {
            await move.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "AUTH_FORBIDDEN");
        }

        if (reads)
        {
            history.StatusCode.ShouldBe(HttpStatusCode.OK);
        }
        else
        {
            await history.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "AUTH_FORBIDDEN");
        }
    }

    [Fact]
    public async Task A_read_only_tenant_reads_the_history_but_moves_no_team()
    {
        App.Authorization.UseIdentityRoles();
        var kmsa = await TenantTestData.SaveTenantAsync(
            App.Services, "kmsa", TenantStatus.Active, publicEnabled: false, withDomain: true, withSubscription: false, Ct);
        await TenantTestData.SavePeriodAsync(App.Services, kmsa, new DateOnly(2025, 1, 1), new DateOnly(2025, 12, 31), Ct, cancelReason: "Pilot paused");
        var member = await CreateMemberAsync(App.Services, kmsa, "brian.ssemakula@kmsa.example", Ct, TenantWide(IdentityRoles.Registrar));
        var registrar = App.ClientFor(member.UserId, kmsa);
        var team = await SaveTeamAsync(App.Services, kmsa, "Ntinda Swifts FC");

        using var move = await registrar.PostAsJsonAsync(
            TeamStatusPath(team.Id), new { status = "SUSPENDED", reason = _reason }, Ct);
        using var history = await registrar.GetAsync(TeamStatusChangesPath(team.Id), Ct);

        await move.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "TENANT_READ_ONLY");
        history.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    // D20, review #128 R3: a team manager, even of this very team, changes and moves no team in S07 (S08's TEAM scope).
    [Fact]
    public async Task A_team_manager_of_the_team_changes_moves_and_reads_nothing()
    {
        var (kmsa, _) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var team = await SaveTeamAsync(App.Services, kmsa, "Ntinda Swifts FC");
        var manager = await CreateMemberAsync(
            App.Services, kmsa, MailboxOf(IdentityRoles.TeamManager, "kmsa.example"), Ct, (IdentityRoles.TeamManager, AssignmentScope.Team, team.Id));
        var client = App.ClientFor(manager.UserId, kmsa);

        using var update = await client.PutAsJsonAsync(TeamPath(team.Id), new { genderCategory = "MEN", ageGroup = "SENIOR" }, Ct);
        using var move = await client.PostAsJsonAsync(TeamStatusPath(team.Id), new { status = "SUSPENDED", reason = _reason }, Ct);
        using var history = await client.GetAsync(TeamStatusChangesPath(team.Id), Ct);

        await update.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "AUTH_FORBIDDEN");
        await move.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "AUTH_FORBIDDEN");
        await history.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "AUTH_FORBIDDEN");
    }

    // T3 acceptance 4: a variant disabled since keeps its teams, which still change status.
    [Fact]
    public async Task A_team_of_a_variant_disabled_since_still_changes_status()
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var id = (await CreateTeamAsync(registrar, NtindaSwiftsMen())).GetProperty("id").GetGuid();
        await TenantTestData.DisableSportAsync(App.Services, kmsa, TestballFull, Ct);

        await MoveAsync(registrar, id, TeamStatus.Disbanded, "Not enough players this season");
        App.Clock.Advance(TimeSpan.FromMinutes(1));
        (await MoveAsync(registrar, id, TeamStatus.Active, "Reformed for the new season")).GetProperty("status").GetString().ShouldBe("ACTIVE");
    }

    // D12 and T2 acceptance 2: suspending or dissolving an organisation leaves its teams' statuses alone.
    [Theory]
    [InlineData("SUSPENDED")]
    [InlineData("DISSOLVED")]
    public async Task An_organisations_move_leaves_its_teams_statuses_alone(string organisationStatus)
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var club = await SaveOrganisationAsync(App.Services, kmsa, "Seeta Sparrows SC");
        var first = await SaveTeamAsync(App.Services, kmsa, "Seeta Sparrows FC", organisationId: club.Id);
        var youth = await SaveTeamAsync(
            App.Services, kmsa, "Seeta Sparrows FC", category: new(GenderCategory.Men, AgeGroup.U17, TeamLevel.First),
            organisationId: club.Id, status: TeamStatus.Inactive);

        using var moved = await registrar.PostAsJsonAsync(
            OrganisationStatusPath(club.Id), new { status = organisationStatus, reason = "Club wound up by its members" }, Ct);

        moved.StatusCode.ShouldBe(HttpStatusCode.OK, await moved.Content.ReadAsStringAsync(Ct));
        await using var scope = ScopeFor(App.Services, kmsa, out var context);
        var teams = await context.Set<Team>().AsNoTracking().Include(t => t.StatusHistory).ToListAsync(Ct);
        teams.Single(t => t.Id == first.Id).Status.ShouldBe(TeamStatus.Active);
        teams.Single(t => t.Id == youth.Id).Status.ShouldBe(TeamStatus.Inactive);
        teams.Single(t => t.Id == first.Id).StatusHistory.Count.ShouldBe(1);
        teams.Single(t => t.Id == youth.Id).StatusHistory.Count.ShouldBe(2);
    }

    private static async Task<JsonElement> MoveAsync(
        HttpClient client, Guid teamId, TeamStatus to, string reason, string? ifMatch = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, TeamStatusPath(teamId))
        {
            Content = JsonContent.Create(new { status = _codes[to], reason }),
        };
        if (ifMatch is not null)
        {
            request.Headers.TryAddWithoutValidation("If-Match", ifMatch);
        }

        using var response = await client.SendAsync(request, Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));
        response.Headers.ETag.ShouldNotBeNull();
        return await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
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

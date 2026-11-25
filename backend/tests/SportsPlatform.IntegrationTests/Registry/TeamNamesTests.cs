using System.Globalization;
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
using SportsPlatform.Modules.Registry.Domain.People;
using SportsPlatform.Modules.Registry.Domain.Teams;
using SportsPlatform.Modules.Tenancy.Contracts.Tenants;
using static SportsPlatform.IntegrationTests.Identity.MembershipTestData;
using static SportsPlatform.IntegrationTests.Registry.PeopleRequests;
using static SportsPlatform.IntegrationTests.Registry.RegistryTestData;
using static SportsPlatform.IntegrationTests.Registry.TeamRequests;
using static SportsPlatform.IntegrationTests.Tenancy.TestSportDefinitions;

namespace SportsPlatform.IntegrationTests.Registry;

/// <summary>
/// A team's names over HTTP (S07-T3c; D8, D10, D13, D18, D20, D21): the rename and its former name with dates, short name
/// and code, the name on a date read back from the database, what a rename keeps or clears, the date rules, the duplicate
/// warning, nicknames and spellings, the directory search, the statuses that take none, the version, the audit, the roles,
/// another tenant and the read-only tenant.
/// </summary>
public sealed class TeamNamesTests(PostgresFixture fixture) : DatabaseTest(fixture)
{
    private static readonly DateOnly _firstNameSince = new(2020, 1, 1);

    [Fact]
    public async Task A_rename_keeps_the_old_name_with_its_dates_and_the_name_on_a_date_reads_it_back()
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var team = await SaveTeamAsync(App.Services, kmsa, "Bunga Lakeside FC", shortName: "Bunga Lakeside");
        var read = await registrar.ReadJsonAsync(TeamPath(team.Id), Ct);
        var effectiveOn = TenantToday().AddDays(-100);

        var renamed = await OkAsync(await registrar.PostAsJsonAsync(
            TeamRenamePath(team.Id),
            new { name = "  Bunga Breakers FC ", shortName = "Bunga Breakers", code = "BBK", effectiveOn, version = read.GetProperty("version").GetUInt32() },
            Ct));

        (renamed.GetProperty("name").GetString(), renamed.GetProperty("shortName").GetString(), renamed.GetProperty("code").GetString())
            .ShouldBe(("Bunga Breakers FC", "Bunga Breakers", "BBK"));
        renamed.GetProperty("nameSince").GetString().ShouldBe(Iso(effectiveOn));
        renamed.GetProperty("slug").GetString().ShouldBe(team.Slug);
        renamed.GetProperty("version").GetUInt32().ShouldNotBe(read.GetProperty("version").GetUInt32());
        var former = renamed.GetProperty("formerNames").EnumerateArray().ShouldHaveSingleItem();
        (former.GetProperty("name").GetString(), former.GetProperty("shortName").GetString(), former.GetProperty("code").ValueKind)
            .ShouldBe(("Bunga Lakeside FC", "Bunga Lakeside", JsonValueKind.Null));
        (former.GetProperty("validFrom").GetString(), former.GetProperty("validTo").GetString())
            .ShouldBe((Iso(_firstNameSince), Iso(effectiveOn.AddDays(-1))));
        renamed.GetProperty("aliases").GetArrayLength().ShouldBe(0);

        await using (var scope = ScopeFor(App.Services, kmsa, out var context))
        {
            var saved = await context.Set<Team>().Include(t => t.Aliases).AsNoTracking().SingleAsync(Ct);
            saved.NameOn(_firstNameSince.AddDays(-1)).ShouldBe(new TeamNameOnDate("Bunga Lakeside FC", "Bunga Lakeside", null));
            saved.NameOn(effectiveOn.AddDays(-1)).ShouldBe(new TeamNameOnDate("Bunga Lakeside FC", "Bunga Lakeside", null));
            saved.NameOn(effectiveOn).ShouldBe(new TeamNameOnDate("Bunga Breakers FC", "Bunga Breakers", "BBK"));
        }

        await AccountTestData.DeliverOutboxAsync(App.Services, Ct);
        var entry = (await AuditTestData.EntriesAsync(App.Services, Ct)).ShouldHaveSingleItem();
        (entry.ActionCode, entry.EntityType, entry.EntityId, entry.Outcome).ShouldBe(("team.rename", "Team", team.Id, AuditOutcome.Success));
    }

    [Fact]
    public async Task A_rename_keeps_a_short_name_or_code_it_leaves_out_and_clears_one_sent_as_null()
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var team = await SaveTeamAsync(App.Services, kmsa, "Bunga Lakeside FC", shortName: "Bunga Lakeside");

        var kept = await OkAsync(await registrar.PostAsJsonAsync(
            TeamRenamePath(team.Id), new { name = "Bunga Breakers FC", code = "BBK", effectiveOn = TenantToday().AddDays(-200) }, Ct));
        (kept.GetProperty("shortName").GetString(), kept.GetProperty("code").GetString()).ShouldBe(("Bunga Lakeside", "BBK"));

        var cleared = await OkAsync(await registrar.PostAsJsonAsync(
            TeamRenamePath(team.Id), new { name = "Bunga Lakers FC", shortName = (string?)null, code = (string?)null, effectiveOn = TenantToday() }, Ct));
        (cleared.GetProperty("shortName").ValueKind, cleared.GetProperty("code").ValueKind).ShouldBe((JsonValueKind.Null, JsonValueKind.Null));
        cleared.GetProperty("formerNames").EnumerateArray()
            .Select(f => (f.GetProperty("name").GetString(), f.GetProperty("shortName").GetString(), f.GetProperty("code").GetString()))
            .ShouldBe([("Bunga Breakers FC", "Bunga Lakeside", "BBK"), ("Bunga Lakeside FC", "Bunga Lakeside", null)]);
    }

    [Fact]
    public async Task A_rename_needs_a_date_after_the_current_names_start_not_after_today_and_a_new_name()
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var team = await SaveTeamAsync(App.Services, kmsa, "Bunga Lakeside FC");
        var today = TenantToday();

        foreach (var effectiveOn in new[] { _firstNameSince, _firstNameSince.AddDays(-1), today.AddDays(1) })
        {
            using var refused = await registrar.PostAsJsonAsync(TeamRenamePath(team.Id), new { name = "Bunga Breakers FC", effectiveOn }, Ct);
            Fields(await refused.ShouldBeProblemAsync(HttpStatusCode.UnprocessableEntity, "TEAM_RENAME_DATE_INVALID"))
                .ShouldBe([("effectiveOn", "TEAM_RENAME_DATE_INVALID")]);
        }

        foreach (var (name, code) in new[] { ("Bunga Lakeside FC", "TEAM_NAME_UNCHANGED"), ("--", "TEAM_NAME_INVALID") })
        {
            using var refused = await registrar.PostAsJsonAsync(TeamRenamePath(team.Id), new { name, effectiveOn = today }, Ct);
            Fields(await refused.ShouldBeProblemAsync(HttpStatusCode.UnprocessableEntity, code)).ShouldBe([("name", code)]);
        }

        using (var malformed = await registrar.PostAsJsonAsync(TeamRenamePath(team.Id), new { name = " ", code = "bbk", slug = "bunga" }, Ct))
        {
            await malformed.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_MALFORMED_REQUEST");
        }

        using (var invalid = await registrar.PostAsJsonAsync(TeamRenamePath(team.Id), new { name = " ", code = "bbk" }, Ct))
        {
            Fields(await invalid.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_INVALID_REQUEST")).Select(field => field.Item1)
                .ShouldBe(["name", "code", "effectiveOn"], ignoreOrder: true);
        }

        await using var scope = ScopeFor(App.Services, kmsa, out var context);
        (await context.Set<TeamAlias>().CountAsync(Ct)).ShouldBe(0);
    }

    [Fact]
    public async Task A_new_name_like_another_teams_shows_it_first_and_a_new_spelling_of_the_same_name_does_not()
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var team = await SaveTeamAsync(App.Services, kmsa, "Bunga Lakeside FC");
        var other = await SaveTeamAsync(App.Services, kmsa, "Bunga Breakers FC");
        var effectiveOn = TenantToday().AddDays(-10);

        await OkAsync(await registrar.PostAsJsonAsync(TeamRenamePath(team.Id), new { name = "Bunga Lakeside F.C.", effectiveOn = effectiveOn.AddDays(-10) }, Ct));
        using (var refused = await registrar.PostAsJsonAsync(TeamRenamePath(team.Id), new { name = "Breakers, Bunga FC", effectiveOn }, Ct))
        {
            TeamCandidates(await refused.ShouldBeProblemAsync(HttpStatusCode.UnprocessableEntity, "TEAM_DUPLICATE_REVIEW_REQUIRED"))
                .ShouldBe([(other.Id, "LIKELY")]);
        }

        var renamed = await OkAsync(await registrar.PostAsJsonAsync(
            TeamRenamePath(team.Id), new { name = "Breakers, Bunga FC", effectiveOn, duplicatesReviewed = true, duplicateReason = "Another club's side" }, Ct));
        renamed.GetProperty("name").GetString().ShouldBe("Breakers, Bunga FC");

        await AccountTestData.DeliverOutboxAsync(App.Services, Ct);
        var entries = (await AuditTestData.EntriesAsync(App.Services, Ct)).Where(e => e.ActionCode == "team.rename").ToList();
        entries.Count.ShouldBe(3);
        entries.Where(e => e.Outcome == AuditOutcome.Success).Select(e => e.Metadata is null).ShouldBe([true, false], ignoreOrder: true);
        var saved = entries.Single(e => e.Outcome == AuditOutcome.Success && e.Metadata is not null);
        saved.Reason.ShouldBe("Another club's side");
        JsonDocument.Parse(saved.Metadata!).RootElement.GetProperty("candidates")[0].GetProperty("teamId").GetGuid().ShouldBe(other.Id);
    }

    // D21 (review #130 R1): a rename that changes the name's key holds the teams' lock while the warning runs and it
    // saves, so two teams renamed to one name at once queue (the test holds the lock until both wait) and the second is
    // warned about the first. A new spelling with the same key takes no lock.
    [Fact]
    public async Task Renames_to_one_new_name_queue_on_the_teams_lock_and_the_second_is_warned()
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var bunga = await SaveTeamAsync(App.Services, kmsa, "Bunga Lakeside FC");
        var kisaasi = await SaveTeamAsync(App.Services, kmsa, "Kisaasi Kestrels FC");
        var effectiveOn = TenantToday();

        HttpResponseMessage[] responses;
        await using (var holder = ScopeFor(App.Services, kmsa, out var holding))
        {
            await using var held = await holding.Database.BeginTransactionAsync(Ct);
            var key = $"registry.teams:{kmsa}";
            await holding.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock(hashtextextended({key}, 0))", Ct);

            // A new spelling of the same name doesn't wait for the lock.
            await OkAsync(await registrar.PostAsJsonAsync(TeamRenamePath(kisaasi.Id), new { name = "Kisaasi Kestrels F.C.", effectiveOn = effectiveOn.AddDays(-1) }, Ct));

            var renames = new[]
            {
                registrar.PostAsJsonAsync(TeamRenamePath(bunga.Id), new { name = "Kawempe Lions FC", effectiveOn }, Ct),
                registrar.PostAsJsonAsync(TeamRenamePath(kisaasi.Id), new { name = "Kawempe Lions FC", effectiveOn }, Ct),
            };

            var deadline = DateTimeOffset.UtcNow.AddSeconds(30);
            while (await WaitingOnAdvisoryLocksAsync(holding) < 2)
            {
                renames.ShouldAllBe(rename => !rename.IsCompleted, "A rename finished without waiting for the teams' lock.");
                DateTimeOffset.UtcNow.ShouldBeLessThan(deadline, "The two renames never both waited on the teams' lock.");
                await Task.Delay(TimeSpan.FromMilliseconds(20), Ct);
            }

            await held.CommitAsync(Ct);
            responses = await Task.WhenAll(renames);
        }

        try
        {
            responses.Select(r => r.StatusCode).ShouldBe([HttpStatusCode.OK, HttpStatusCode.UnprocessableEntity], ignoreOrder: true);
            var refused = responses.Single(r => r.StatusCode == HttpStatusCode.UnprocessableEntity);
            var saved = responses.Single(r => r.StatusCode == HttpStatusCode.OK);
            var savedId = (await saved.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("id").GetGuid();
            TeamCandidates(await refused.ShouldBeProblemAsync(HttpStatusCode.UnprocessableEntity, "TEAM_DUPLICATE_REVIEW_REQUIRED"))
                .ShouldBe([(savedId, "LIKELY")]);
        }
        finally
        {
            foreach (var response in responses)
            {
                response.Dispose();
            }
        }
    }

    // D13 (owner, pre-review): a current alias, former names included, is a name key on both sides for LIKELY; removed
    // aliases and other categories don't count.
    [Fact]
    public async Task A_former_name_or_nickname_makes_a_likely_duplicate_on_either_side()
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var renamed = await SaveTeamAsync(App.Services, kmsa, "Bunga Lakeside FC");
        await OkAsync(await registrar.PostAsJsonAsync(TeamRenamePath(renamed.Id), new { name = "Bunga Breakers FC", effectiveOn = TenantToday() }, Ct));
        var nicknamed = await SaveTeamAsync(App.Services, kmsa, "Ntinda Swifts FC");
        var nickname = await OkAsync(await registrar.PostAsJsonAsync(TeamAliasesPath(nicknamed.Id), new { name = "The Swallows", kind = "NICKNAME" }, Ct));

        // The other side's former name and nickname.
        TeamCandidates(await CheckTeamAsync(registrar, new { variant = TestballFull, name = "Lakeside, Bunga FC", genderCategory = "MEN", ageGroup = "SENIOR" }))
            .ShouldBe([(renamed.Id, "LIKELY")]);
        TeamCandidates(await CheckTeamAsync(registrar, new { variant = TestballFull, name = "Kisaasi FC", shortName = "Swallows, The", genderCategory = "MEN", ageGroup = "SENIOR" }))
            .ShouldBe([(nicknamed.Id, "LIKELY")]);
        TeamCandidates(await CheckTeamAsync(registrar, new { variant = TestballFull, name = "Bunga Lakeside FC", genderCategory = "MEN", ageGroup = "U17" }))
            .ShouldBeEmpty();

        // This side's own alias: a rename of a team with the spelling "Bunga Breakers F.C." is warned about the Bunga team.
        var kiwatule = await SaveTeamAsync(App.Services, kmsa, "Kiwatule Athletic FC");
        await OkAsync(await registrar.PostAsJsonAsync(TeamAliasesPath(kiwatule.Id), new { name = "Bunga Breakers F.C.", kind = "SPELLING" }, Ct));
        using (var refused = await registrar.PostAsJsonAsync(TeamRenamePath(kiwatule.Id), new { name = "Kiwatule Kites FC", effectiveOn = TenantToday() }, Ct))
        {
            TeamCandidates(await refused.ShouldBeProblemAsync(HttpStatusCode.UnprocessableEntity, "TEAM_DUPLICATE_REVIEW_REQUIRED"))
                .ShouldBe([(renamed.Id, "LIKELY")]);
        }

        // A removed alias no longer counts.
        var aliasId = nickname.GetProperty("aliases")[0].GetProperty("id").GetGuid();
        await OkAsync(await registrar.PostAsJsonAsync(TeamAliasPath(nicknamed.Id, aliasId, "remove"), new { reason = "Never used" }, Ct));
        TeamCandidates(await CheckTeamAsync(registrar, new { variant = TestballFull, name = "Kisaasi FC", shortName = "The Swallows", genderCategory = "MEN", ageGroup = "SENIOR" }))
            .ShouldBeEmpty();
    }

    [Fact]
    public async Task A_nickname_or_spelling_is_shown_and_searched_until_it_is_removed_and_a_former_name_stays()
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var team = await SaveTeamAsync(App.Services, kmsa, "Bunga Lakeside FC");
        await SaveTeamAsync(App.Services, kmsa, "Ntinda Swifts FC");
        var renamed = await OkAsync(await registrar.PostAsJsonAsync(
            TeamRenamePath(team.Id), new { name = "Bunga Breakers FC", effectiveOn = TenantToday() }, Ct));
        var formerId = renamed.GetProperty("formerNames")[0].GetProperty("id").GetGuid();

        var added = await OkAsync(await registrar.PostAsJsonAsync(
            TeamAliasesPath(team.Id), new { name = "  The  Lake Boys ", kind = "NICKNAME", version = renamed.GetProperty("version").GetUInt32() }, Ct));

        added.GetProperty("version").GetUInt32().ShouldNotBe(renamed.GetProperty("version").GetUInt32());
        var alias = added.GetProperty("aliases").EnumerateArray().ShouldHaveSingleItem();
        (alias.GetProperty("name").GetString(), alias.GetProperty("kind").GetString()).ShouldBe(("The Lake Boys", "NICKNAME"));
        foreach (var q in new[] { "lake%20boys", "Lake%20Boyz", "lakeside", "Bunga%20Lakesde" })
        {
            TeamNames(await registrar.ReadJsonAsync($"{TeamsPath}?q={q}", Ct)).ShouldBe(["Bunga Breakers FC"]);
        }

        var aliasId = alias.GetProperty("id").GetGuid();
        using (var former = await registrar.PostAsJsonAsync(TeamAliasPath(team.Id, formerId, "remove"), new { reason = "Wrong name" }, Ct))
        {
            await former.ShouldBeProblemAsync(HttpStatusCode.Conflict, "TEAM_ALIAS_FORMER_NAME");
        }

        var removed = await OkAsync(await registrar.PostAsJsonAsync(TeamAliasPath(team.Id, aliasId, "remove"), new { reason = " Never used " }, Ct));

        removed.GetProperty("aliases").GetArrayLength().ShouldBe(0);
        removed.GetProperty("formerNames").GetArrayLength().ShouldBe(1);
        TeamNames(await registrar.ReadJsonAsync($"{TeamsPath}?q=lake%20boys", Ct)).ShouldBeEmpty();
        await using (var scope = ScopeFor(App.Services, kmsa, out var context))
        {
            var kept = await context.Set<TeamAlias>().AsNoTracking().SingleAsync(a => a.Id == aliasId, Ct);
            (kept.RemoveReason, kept.RemovedAt).ShouldBe(("Never used", App.Clock.GetUtcNow()));
        }

        using var again = await registrar.PostAsJsonAsync(TeamAliasPath(team.Id, aliasId, "remove"), new { reason = "Never used" }, Ct);
        await again.ShouldBeProblemAsync(HttpStatusCode.Conflict, "TEAM_ALIAS_REMOVED");
        await AccountTestData.DeliverOutboxAsync(App.Services, Ct);
        var entries = (await AuditTestData.EntriesAsync(App.Services, Ct))
            .Where(e => e.ActionCode.StartsWith("team.alias.", StringComparison.Ordinal) && e.Outcome == AuditOutcome.Success).ToList();
        entries.Select(e => (e.ActionCode, e.EntityId, e.Reason))
            .ShouldBe([("team.alias.add", team.Id, null), ("team.alias.remove", team.Id, "Never used")], ignoreOrder: true);
        entries.ShouldAllBe(e => JsonDocument.Parse(e.Metadata!).RootElement.GetProperty("aliasId").GetGuid() == aliasId);
    }

    [Fact]
    public async Task An_alias_the_team_is_known_by_a_former_name_kind_or_another_teams_alias_is_refused()
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var team = await SaveTeamAsync(App.Services, kmsa, "Bunga Lakeside FC", shortName: "Lakeside");
        var other = await SaveTeamAsync(App.Services, kmsa, "Ntinda Swifts FC");
        var added = await OkAsync(await registrar.PostAsJsonAsync(TeamAliasesPath(team.Id), new { name = "The Lake Boys", kind = "NICKNAME" }, Ct));
        var aliasId = added.GetProperty("aliases")[0].GetProperty("id").GetGuid();

        foreach (var name in new[] { "FC Lakeside, Bunga", "LAKESIDE", "Boys, the LAKE" })
        {
            using var known = await registrar.PostAsJsonAsync(TeamAliasesPath(team.Id), new { name, kind = "SPELLING" }, Ct);
            Fields(await known.ShouldBeProblemAsync(HttpStatusCode.Conflict, "TEAM_ALIAS_EXISTS")).ShouldBe([("name", "TEAM_ALIAS_EXISTS")]);
        }

        using (var blank = await registrar.PostAsJsonAsync(TeamAliasesPath(team.Id), new { name = "--", kind = "SPELLING" }, Ct))
        {
            Fields(await blank.ShouldBeProblemAsync(HttpStatusCode.UnprocessableEntity, "TEAM_NAME_INVALID")).ShouldBe([("name", "TEAM_NAME_INVALID")]);
        }

        using (var former = await registrar.PostAsJsonAsync(TeamAliasesPath(team.Id), new { name = "Bunga FC", kind = "FORMER_NAME" }, Ct))
        {
            Fields(await former.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_INVALID_REQUEST")).Select(field => field.Item1).ShouldBe(["kind"]);
        }

        using (var noReason = await registrar.PostAsJsonAsync(TeamAliasPath(team.Id, aliasId, "remove"), new { reason = " " }, Ct))
        {
            await noReason.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_REASON_REQUIRED");
        }

        foreach (var (owner, id) in new[] { (other.Id, aliasId), (team.Id, Guid.CreateVersion7()) })
        {
            using var missing = await registrar.PostAsJsonAsync(TeamAliasPath(owner, id, "remove"), new { reason = "Misspelt" }, Ct);
            await missing.ShouldBeProblemAsync(HttpStatusCode.NotFound, "TEAM_ALIAS_NOT_FOUND");
        }
    }

    // D10 (owner, T3c plan): a disbanded or archived team takes no new name or alias; removing stays allowed.
    [Theory]
    [InlineData(TeamStatus.Active, null)]
    [InlineData(TeamStatus.Inactive, null)]
    [InlineData(TeamStatus.Suspended, null)]
    [InlineData(TeamStatus.Disbanded, "TEAM_DISBANDED")]
    [InlineData(TeamStatus.Archived, "TEAM_ARCHIVED")]
    public async Task Only_a_team_that_isnt_disbanded_or_archived_takes_a_new_name_or_alias(TeamStatus status, string? refusal)
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var team = await SaveTeamAsync(App.Services, kmsa, "Bunga Lakeside FC");
        var aliasId = (await OkAsync(await registrar.PostAsJsonAsync(TeamAliasesPath(team.Id), new { name = "The Lake Boys", kind = "NICKNAME" }, Ct)))
            .GetProperty("aliases")[0].GetProperty("id").GetGuid();
        if (status == TeamStatus.Archived)
        {
            await MoveTeamAsync(registrar, team.Id, "DISBANDED", "Not enough players");
        }

        if (status != TeamStatus.Active)
        {
            await MoveTeamAsync(registrar, team.Id, status.ToString().ToUpperInvariant(), "Set up by the test");
        }

        foreach (var (path, body) in new (string, object)[]
        {
            (TeamRenamePath(team.Id), new { name = "Bunga Breakers FC", effectiveOn = TenantToday() }),
            (TeamAliasesPath(team.Id), new { name = "Bunga Lake Side", kind = "SPELLING" }),
        })
        {
            using var response = await registrar.PostAsJsonAsync(path, body, Ct);
            if (refusal is null)
            {
                response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));
            }
            else
            {
                await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, refusal);
            }
        }

        await OkAsync(await registrar.PostAsJsonAsync(TeamAliasPath(team.Id, aliasId, "remove"), new { reason = "Never used" }, Ct));
    }

    [Fact]
    public async Task A_stale_If_Match_is_a_team_conflict_for_every_name_change()
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var team = await SaveTeamAsync(App.Services, kmsa, "Bunga Lakeside FC");
        var read = await OkAsync(await registrar.PostAsJsonAsync(TeamAliasesPath(team.Id), new { name = "The Lake Boys", kind = "NICKNAME" }, Ct));
        var aliasId = read.GetProperty("aliases")[0].GetProperty("id").GetGuid();
        var stale = $"\"{read.GetProperty("version").GetUInt32() + 1}\"";

        foreach (var (path, body) in Requests(team.Id, aliasId))
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
            request.Headers.TryAddWithoutValidation("If-Match", stale);
            using var response = await registrar.SendAsync(request, Ct);
            await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, "TEAM_CONCURRENCY_CONFLICT");
        }
    }

    [Fact]
    public async Task Another_tenants_team_is_not_found()
    {
        var (kmsa, _) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var (_, elsscOwner) = await TenantAsync("elssc", IdentityRoles.TenantOwner);
        var team = await SaveTeamAsync(App.Services, kmsa, "Bunga Lakeside FC");

        foreach (var (path, body) in Requests(team.Id, Guid.CreateVersion7()))
        {
            using var response = await elsscOwner.PostAsJsonAsync(path, body, Ct);
            await response.ShouldBeProblemAsync(HttpStatusCode.NotFound, "TEAM_NOT_FOUND");
        }
    }

    // D20: renaming and aliases are the owner's, the admin's and the registrar's.
    [Theory]
    [InlineData(IdentityRoles.TenantOwner, true)]
    [InlineData(IdentityRoles.TenantAdmin, true)]
    [InlineData(IdentityRoles.Registrar, true)]
    [InlineData(IdentityRoles.CompetitionManager, false)]
    [InlineData(IdentityRoles.ResultsOfficer, false)]
    [InlineData(IdentityRoles.DataEntry, false)]
    [InlineData(IdentityRoles.Viewer, false)]
    [InlineData(IdentityRoles.Scorer, false)]
    public async Task Only_those_who_change_teams_may_rename_them_and_change_aliases(string role, bool allowed)
    {
        var (kmsa, client) = await TenantAsync("kmsa", role);
        var team = await SaveTeamAsync(App.Services, kmsa, "Bunga Lakeside FC");
        Guid aliasId;
        await using (var scope = ScopeFor(App.Services, kmsa, out var context))
        {
            var tracked = await context.Set<Team>().Include(t => t.Aliases).SingleAsync(Ct);
            aliasId = tracked.AddAlias("The Lake Boys", AliasKind.Nickname).Value.Id;
            await context.SaveChangesAsync(Ct);
        }

        foreach (var (path, body) in Requests(team.Id, aliasId))
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

    [Fact]
    public async Task A_read_only_tenant_changes_no_name_or_alias()
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

    private (string Path, object Body)[] Requests(Guid teamId, Guid aliasId) =>
    [
        (TeamRenamePath(teamId), new { name = "Bunga Breakers FC", effectiveOn = TenantToday() }),
        (TeamAliasesPath(teamId), new { name = "Bunga Lake Side", kind = "SPELLING" }),
        (TeamAliasPath(teamId, aliasId, "remove"), new { reason = "Never used" }),
    ];

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

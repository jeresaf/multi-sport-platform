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
using SportsPlatform.Modules.Tenancy.Contracts.Tenants;
using static SportsPlatform.IntegrationTests.Identity.MembershipTestData;
using static SportsPlatform.IntegrationTests.Registry.OrganisationRequests;
using static SportsPlatform.IntegrationTests.Registry.PeopleRequests;
using static SportsPlatform.IntegrationTests.Registry.RegistryTestData;

namespace SportsPlatform.IntegrationTests.Registry;

/// <summary>
/// An organisation's parent over HTTP (S07-T2c; D5, D10, D21): setting, changing and clearing it with the dated
/// history; the cycle check, also for two changes at once under the lock (T2 acceptance 1); unavailable parents; a
/// dissolved or archived child; the versions; the reason; the roles, the read-only tenant and another tenant.
/// </summary>
public sealed class OrganisationParentTests(PostgresFixture fixture) : DatabaseTest(fixture)
{
    private const string _reason = "Affiliated to the association for the season";

    [Fact]
    public async Task Setting_a_parent_opens_a_link_from_the_tenants_today_and_answers_the_organisation()
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var association = await SaveOrganisationAsync(App.Services, kmsa, "Kampala Metro Sports Association", OrganisationType.Association);
        var club = await SaveOrganisationAsync(App.Services, kmsa, "Ntinda Swifts SC");

        var answer = await SetParentAsync(registrar, club.Id, association.Id, $"  {_reason} ");

        answer.GetProperty("id").GetGuid().ShouldBe(club.Id);
        var link = (await LinksAsync(registrar, club.Id)).ShouldHaveSingleItem();
        link.GetProperty("parent").GetProperty("id").GetGuid().ShouldBe(association.Id);
        link.GetProperty("parent").GetProperty("displayName").GetString().ShouldBe("Kampala Metro Sports Association");
        link.GetProperty("parent").GetProperty("status").GetString().ShouldBe("ACTIVE");
        link.GetProperty("validFrom").GetString().ShouldBe(Day(KampalaToday()));
        link.GetProperty("validTo").ValueKind.ShouldBe(JsonValueKind.Null);
        link.GetProperty("reason").GetString().ShouldBe(_reason);
        link.GetProperty("actorKind").GetString().ShouldBe("USER");
        link.GetProperty("setAt").GetDateTimeOffset().ShouldBe(App.Clock.GetUtcNow());

        await AccountTestData.DeliverOutboxAsync(App.Services, Ct);
        var entry = (await AuditTestData.EntriesAsync(App.Services, Ct)).ShouldHaveSingleItem();
        (entry.ActionCode, entry.EntityType, entry.EntityId, entry.Reason, entry.Outcome)
            .ShouldBe(("organisation.parent.set", "Organisation", club.Id, _reason, AuditOutcome.Success));
    }

    // T2 acceptance 1 (history): a new parent ends the open link on the tenant's today; both links stay with their dates.
    [Fact]
    public async Task A_new_parent_ends_the_open_link_and_the_history_keeps_both_with_their_dates()
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var association = await SaveOrganisationAsync(App.Services, kmsa, "Kampala Metro Sports Association", OrganisationType.Association);
        var kestrels = await SaveOrganisationAsync(App.Services, kmsa, "Kisaasi Kestrels SC");
        var club = await SaveOrganisationAsync(App.Services, kmsa, "Ntinda Swifts SC");

        await SetParentAsync(registrar, club.Id, association.Id, _reason);
        var opened = KampalaToday();
        App.Clock.Advance(TimeSpan.FromDays(40));
        await SetParentAsync(registrar, club.Id, kestrels.Id, "Moved under Kisaasi Kestrels SC");
        var changed = KampalaToday();

        var links = await LinksAsync(registrar, club.Id);
        links.Select(l => (
                l.GetProperty("parent").GetProperty("id").GetGuid(),
                l.GetProperty("validFrom").GetString(),
                l.GetProperty("validTo").GetString(),
                l.GetProperty("reason").GetString()))
            .ShouldBe(
            [
                (kestrels.Id, Day(changed), null, "Moved under Kisaasi Kestrels SC"),
                (association.Id, Day(opened), Day(changed), _reason),
            ]);
    }

    [Fact]
    public async Task Clearing_the_parent_ends_the_open_link_without_a_new_one()
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var association = await SaveOrganisationAsync(App.Services, kmsa, "Kampala Metro Sports Association", OrganisationType.Association);
        var club = await SaveOrganisationAsync(App.Services, kmsa, "Ntinda Swifts SC");
        await SetParentAsync(registrar, club.Id, association.Id, _reason);

        await SetParentAsync(registrar, club.Id, null, "Left the association");

        var link = (await LinksAsync(registrar, club.Id)).ShouldHaveSingleItem();
        (link.GetProperty("validFrom").GetString(), link.GetProperty("validTo").GetString())
            .ShouldBe((Day(KampalaToday()), Day(KampalaToday())));
    }

    // Owner, plan: the parent it already has (or none when it has none) changes nothing: no link, no new version.
    [Fact]
    public async Task Setting_the_parent_it_already_has_changes_nothing()
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var association = await SaveOrganisationAsync(App.Services, kmsa, "Kampala Metro Sports Association", OrganisationType.Association);
        var club = await SaveOrganisationAsync(App.Services, kmsa, "Ntinda Swifts SC");
        var before = (await SetParentAsync(registrar, club.Id, null, "No parent yet")).GetProperty("version").GetUInt32();
        var set = (await SetParentAsync(registrar, club.Id, association.Id, _reason)).GetProperty("version").GetUInt32();

        var again = await SetParentAsync(registrar, club.Id, association.Id, "Affiliated again");

        set.ShouldNotBe(before);
        again.GetProperty("version").GetUInt32().ShouldBe(set);
        (await LinksAsync(registrar, club.Id)).ShouldHaveSingleItem().GetProperty("reason").GetString().ShouldBe(_reason);

        // The request is still audited, with its reason, though nothing was saved.
        await AccountTestData.DeliverOutboxAsync(App.Services, Ct);
        (await AuditTestData.EntriesAsync(App.Services, Ct))
            .Where(entry => entry.Reason == "Affiliated again")
            .ShouldHaveSingleItem()
            .ShouldSatisfyAllConditions(
                entry => entry.ActionCode.ShouldBe("organisation.parent.set"),
                entry => entry.EntityId.ShouldBe(club.Id),
                entry => entry.Outcome.ShouldBe(AuditOutcome.Success));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task The_organisation_or_one_below_it_is_a_cycle(int depth)
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var association = await SaveOrganisationAsync(App.Services, kmsa, "Kampala Metro Sports Association", OrganisationType.Association);
        var club = await SaveOrganisationAsync(App.Services, kmsa, "Ntinda Swifts SC");
        var rugby = await SaveOrganisationAsync(App.Services, kmsa, "Ntinda Swifts RFC");
        await SetParentAsync(registrar, club.Id, association.Id, _reason);
        await SetParentAsync(registrar, rugby.Id, club.Id, "The club's rugby section");
        var below = new[] { association.Id, club.Id, rugby.Id }[depth];

        using var response = await registrar.PostAsJsonAsync(OrganisationParentPath(association.Id), new { parentId = below, reason = _reason }, Ct);

        var problem = await response.ShouldBeProblemAsync(HttpStatusCode.UnprocessableEntity, "ORGANISATION_PARENT_CYCLE");
        Fields(problem).ShouldBe([("parentId", "ORGANISATION_PARENT_CYCLE")]);
        (await LinksAsync(registrar, association.Id)).ShouldBeEmpty();
    }

    // An ended link doesn't count: an organisation that was below another and was moved away can become its parent.
    [Fact]
    public async Task An_ended_link_is_no_cycle()
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var association = await SaveOrganisationAsync(App.Services, kmsa, "Kampala Metro Sports Association", OrganisationType.Association);
        var club = await SaveOrganisationAsync(App.Services, kmsa, "Ntinda Swifts SC");
        await SetParentAsync(registrar, club.Id, association.Id, _reason);
        await SetParentAsync(registrar, club.Id, null, "Left the association");

        await SetParentAsync(registrar, association.Id, club.Id, "The club now runs the association");
    }

    // T2 acceptance 1 (at once): the test holds the organisations lock until both changes wait on it, so they overlap
    // for certain. Released, one sets its parent and the other, reading after it, finds the cycle. Without the lock
    // both would see no link and both would be saved.
    [Fact]
    public async Task Two_changes_that_would_close_a_cycle_together_save_one()
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var colleague = App.ClientFor(
            (await CreateMemberAsync(App.Services, kmsa, "brian.ssemakula@kmsa.example", Ct, TenantWide(IdentityRoles.Registrar))).UserId, kmsa);
        var association = await SaveOrganisationAsync(App.Services, kmsa, "Kampala Metro Sports Association", OrganisationType.Association);
        var club = await SaveOrganisationAsync(App.Services, kmsa, "Ntinda Swifts SC");

        HttpResponseMessage[] responses;
        await using (var holder = ScopeFor(App.Services, kmsa, out var holding))
        {
            await using var held = await holding.Database.BeginTransactionAsync(Ct);
            var key = $"registry.organisations:{kmsa}";
            await holding.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock(hashtextextended({key}, 0))", Ct);
            var changes = new[]
            {
                registrar.PostAsJsonAsync(OrganisationParentPath(club.Id), new { parentId = association.Id, reason = _reason }, Ct),
                colleague.PostAsJsonAsync(OrganisationParentPath(association.Id), new { parentId = club.Id, reason = _reason }, Ct),
            };

            var deadline = DateTimeOffset.UtcNow.AddSeconds(30);
            while (await WaitingOnAdvisoryLocksAsync(holding) < 2)
            {
                changes.ShouldAllBe(change => !change.IsCompleted, "A change finished without waiting for the organisations lock.");
                DateTimeOffset.UtcNow.ShouldBeLessThan(deadline, "The two changes never both waited on the organisations lock.");
                await Task.Delay(TimeSpan.FromMilliseconds(20), Ct);
            }

            await held.CommitAsync(Ct);
            responses = await Task.WhenAll(changes);
        }

        try
        {
            responses.Select(response => response.StatusCode)
                .ShouldBe([HttpStatusCode.OK, HttpStatusCode.UnprocessableEntity], ignoreOrder: true);
            await responses.Single(response => response.StatusCode == HttpStatusCode.UnprocessableEntity)
                .ShouldBeProblemAsync(HttpStatusCode.UnprocessableEntity, "ORGANISATION_PARENT_CYCLE");
        }
        finally
        {
            foreach (var response in responses)
            {
                response.Dispose();
            }
        }

        await using var scope = ScopeFor(App.Services, kmsa, out var context);
        (await context.Set<OrganisationParentLink>().CountAsync(Ct)).ShouldBe(1);
    }

    [Theory]
    [InlineData(OrganisationStatus.Dissolved)]
    [InlineData(OrganisationStatus.Archived)]
    public async Task A_dissolved_or_archived_parent_is_unavailable(OrganisationStatus status)
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var association = await SaveOrganisationAsync(App.Services, kmsa, "Kampala Metro Sports Association", OrganisationType.Association, status: status);
        var club = await SaveOrganisationAsync(App.Services, kmsa, "Ntinda Swifts SC");

        using var response = await registrar.PostAsJsonAsync(OrganisationParentPath(club.Id), new { parentId = association.Id, reason = _reason }, Ct);

        var problem = await response.ShouldBeProblemAsync(HttpStatusCode.UnprocessableEntity, "ORGANISATION_PARENT_UNAVAILABLE");
        Fields(problem).ShouldBe([("parentId", "ORGANISATION_PARENT_UNAVAILABLE")]);
    }

    // An inactive or suspended organisation can still be a parent; a child's status never follows its parent's.
    [Theory]
    [InlineData(OrganisationStatus.Inactive)]
    [InlineData(OrganisationStatus.Suspended)]
    public async Task An_inactive_or_suspended_parent_is_accepted_and_the_child_keeps_its_status(OrganisationStatus status)
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var association = await SaveOrganisationAsync(App.Services, kmsa, "Kampala Metro Sports Association", OrganisationType.Association, status: status);
        var club = await SaveOrganisationAsync(App.Services, kmsa, "Ntinda Swifts SC");

        (await SetParentAsync(registrar, club.Id, association.Id, _reason)).GetProperty("status").GetString().ShouldBe("ACTIVE");
    }

    [Fact]
    public async Task An_unknown_parent_or_another_tenants_is_unavailable()
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var (elssc, _) = await TenantAsync("elssc", IdentityRoles.TenantOwner);
        var club = await SaveOrganisationAsync(App.Services, kmsa, "Ntinda Swifts SC");
        var elsscClub = await SaveOrganisationAsync(App.Services, elssc, "Lakeshore High School", OrganisationType.School);

        foreach (var parentId in new[] { Guid.NewGuid(), elsscClub.Id })
        {
            using var response = await registrar.PostAsJsonAsync(OrganisationParentPath(club.Id), new { parentId, reason = _reason }, Ct);
            await response.ShouldBeProblemAsync(HttpStatusCode.UnprocessableEntity, "ORGANISATION_PARENT_UNAVAILABLE");
        }
    }

    // Both ends of a link are the tenant's own: each composite foreign key refuses another tenant's organisation,
    // whichever end it is at (the isolation sample can only build one end).
    [Fact]
    public async Task A_link_to_or_from_another_tenants_organisation_is_refused_by_the_database()
    {
        var (kmsa, _) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var (elssc, _) = await TenantAsync("elssc", IdentityRoles.TenantOwner);
        var club = await SaveOrganisationAsync(App.Services, kmsa, "Ntinda Swifts SC");
        var school = await SaveOrganisationAsync(App.Services, elssc, "Lakeshore High School", OrganisationType.School);

        foreach (var (child, parent) in new[] { (school.Id, club.Id), (club.Id, school.Id) })
        {
            await using var scope = ScopeFor(App.Services, elssc, out var context);
            context.Add(new OrganisationParentLink(
                child, parent, new DateOnly(2026, 11, 16), _reason, BuildingBlocks.Actors.SystemActor.Instance, Now));
            var refused = await Should.ThrowAsync<DbUpdateException>(() => context.SaveChangesAsync(Ct));
            refused.InnerException.ShouldBeOfType<Npgsql.PostgresException>().SqlState.ShouldBe(Npgsql.PostgresErrorCodes.ForeignKeyViolation);
        }
    }

    // D10: a dissolved or archived organisation takes no new parent, but its parent can still be cleared.
    [Theory]
    [InlineData(OrganisationStatus.Dissolved, "ORGANISATION_DISSOLVED")]
    [InlineData(OrganisationStatus.Archived, "ORGANISATION_ARCHIVED")]
    public async Task A_dissolved_or_archived_organisation_takes_no_new_parent_but_can_clear_it(OrganisationStatus status, string code)
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var association = await SaveOrganisationAsync(App.Services, kmsa, "Kampala Metro Sports Association", OrganisationType.Association);
        var kestrels = await SaveOrganisationAsync(App.Services, kmsa, "Kisaasi Kestrels SC");
        var club = await SaveOrganisationAsync(App.Services, kmsa, "Ntinda Swifts SC");
        await SetParentAsync(registrar, club.Id, association.Id, _reason);
        await MoveAsync(club.Id, kmsa, status);

        using var refused = await registrar.PostAsJsonAsync(OrganisationParentPath(club.Id), new { parentId = kestrels.Id, reason = _reason }, Ct);
        await refused.ShouldBeProblemAsync(HttpStatusCode.Conflict, code);

        await SetParentAsync(registrar, club.Id, null, "The club no longer plays");
        (await LinksAsync(registrar, club.Id)).ShouldHaveSingleItem().GetProperty("validTo").GetString().ShouldNotBeNull();
    }

    // Owner, plan: a new link moves the child's version and the new parent's (so a parent dissolved at the same moment
    // conflicts), not the earlier parent's.
    [Fact]
    public async Task A_new_parent_moves_the_versions_of_the_organisation_and_the_new_parent_only()
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var association = await SaveOrganisationAsync(App.Services, kmsa, "Kampala Metro Sports Association", OrganisationType.Association);
        var kestrels = await SaveOrganisationAsync(App.Services, kmsa, "Kisaasi Kestrels SC");
        var club = await SaveOrganisationAsync(App.Services, kmsa, "Ntinda Swifts SC");
        await SetParentAsync(registrar, club.Id, association.Id, _reason);
        var associationVersion = await VersionAsync(registrar, association.Id);
        var kestrelsVersion = await VersionAsync(registrar, kestrels.Id);

        await SetParentAsync(registrar, club.Id, kestrels.Id, "Moved under Kisaasi Kestrels SC");

        (await VersionAsync(registrar, association.Id)).ShouldBe(associationVersion);
        (await VersionAsync(registrar, kestrels.Id)).ShouldNotBe(kestrelsVersion);
    }

    [Fact]
    public async Task A_change_from_a_stale_version_is_an_organisation_conflict()
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var association = await SaveOrganisationAsync(App.Services, kmsa, "Kampala Metro Sports Association", OrganisationType.Association);
        var club = await SaveOrganisationAsync(App.Services, kmsa, "Ntinda Swifts SC");
        var version = await VersionAsync(registrar, club.Id);
        var set = await SetParentAsync(registrar, club.Id, association.Id, _reason, $"\"{version}\"");
        set.GetProperty("version").GetUInt32().ShouldNotBe(version);

        using var request = new HttpRequestMessage(HttpMethod.Post, OrganisationParentPath(club.Id))
        {
            Content = JsonContent.Create(new { parentId = (Guid?)null, reason = "Left the association" }),
        };
        request.Headers.TryAddWithoutValidation("If-Match", $"\"{version}\"");
        using var stale = await registrar.SendAsync(request, Ct);
        await stale.ShouldBeProblemAsync(HttpStatusCode.Conflict, "ORGANISATION_CONCURRENCY_CONFLICT");

        using var staleBody = await registrar.PostAsJsonAsync(
            OrganisationParentPath(club.Id), new { parentId = (Guid?)null, reason = "Left the association", version }, Ct);
        await staleBody.ShouldBeProblemAsync(HttpStatusCode.Conflict, "ORGANISATION_CONCURRENCY_CONFLICT");

        using var unknown = await registrar.PostAsJsonAsync(
            OrganisationParentPath(Guid.NewGuid()), new { parentId = association.Id, reason = _reason }, Ct);
        await unknown.ShouldBeProblemAsync(HttpStatusCode.NotFound, "ORGANISATION_NOT_FOUND");
    }

    [Theory]
    [InlineData("""{"parentId":null}""", "reason", "VALIDATION_REASON_REQUIRED")]
    [InlineData("""{"parentId":null,"reason":"  "}""", "reason", "VALIDATION_REASON_REQUIRED")]
    [InlineData("""{"parentId":"00000000-0000-0000-0000-000000000000","reason":"Affiliated"}""", "parentId", "VALIDATION_INVALID_REQUEST")]
    public async Task A_change_without_a_reason_or_with_an_empty_parent_is_refused(string body, string field, string fieldCode)
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var club = await SaveOrganisationAsync(App.Services, kmsa, "Ntinda Swifts SC");

        using var content = new StringContent(body, System.Text.Encoding.UTF8, "application/json");
        using var response = await registrar.PostAsync(OrganisationParentPath(club.Id), content, Ct);

        var problem = await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_INVALID_REQUEST");
        Fields(problem).ShouldBe([(field, fieldCode)]);
    }

    [Fact]
    public async Task A_reason_longer_than_500_characters_is_refused()
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var association = await SaveOrganisationAsync(App.Services, kmsa, "Kampala Metro Sports Association", OrganisationType.Association);
        var club = await SaveOrganisationAsync(App.Services, kmsa, "Ntinda Swifts SC");

        using var tooLong = await registrar.PostAsJsonAsync(
            OrganisationParentPath(club.Id), new { parentId = association.Id, reason = new string('r', 501) }, Ct);
        var problem = await tooLong.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_INVALID_REQUEST");
        Fields(problem).ShouldBe([("reason", "VALIDATION_REASON_REQUIRED")]);

        await SetParentAsync(registrar, club.Id, association.Id, new string('r', 500));
    }

    [Fact]
    public async Task Another_tenants_organisation_has_no_parent_to_set_or_read()
    {
        var (kmsa, _) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var (elssc, elsscOwner) = await TenantAsync("elssc", IdentityRoles.TenantOwner);
        var club = await SaveOrganisationAsync(App.Services, kmsa, "Ntinda Swifts SC");
        var elsscClub = await SaveOrganisationAsync(App.Services, elssc, "Lakeshore High School", OrganisationType.School);

        using var set = await elsscOwner.PostAsJsonAsync(OrganisationParentPath(club.Id), new { parentId = elsscClub.Id, reason = _reason }, Ct);
        using var read = await elsscOwner.GetAsync(OrganisationParentLinksPath(club.Id), Ct);
        using var unknown = await elsscOwner.GetAsync(OrganisationParentLinksPath(Guid.NewGuid()), Ct);

        await set.ShouldBeProblemAsync(HttpStatusCode.NotFound, "ORGANISATION_NOT_FOUND");
        await read.ShouldBeProblemAsync(HttpStatusCode.NotFound, "ORGANISATION_NOT_FOUND");
        await unknown.ShouldBeProblemAsync(HttpStatusCode.NotFound, "ORGANISATION_NOT_FOUND");
    }

    // D20: setting the parent is the owner's, the admin's and the registrar's; the history is read with the record.
    [Theory]
    [InlineData(IdentityRoles.TenantOwner, true, true)]
    [InlineData(IdentityRoles.TenantAdmin, true, true)]
    [InlineData(IdentityRoles.Registrar, true, true)]
    [InlineData(IdentityRoles.CompetitionManager, false, true)]
    [InlineData(IdentityRoles.ResultsOfficer, false, true)]
    [InlineData(IdentityRoles.DataEntry, false, true)]
    [InlineData(IdentityRoles.Viewer, false, true)]
    [InlineData(IdentityRoles.Scorer, false, false)]
    public async Task Only_those_who_change_organisations_may_set_a_parent(string role, bool changes, bool reads)
    {
        var (kmsa, client) = await TenantAsync("kmsa", role);
        var association = await SaveOrganisationAsync(App.Services, kmsa, "Kampala Metro Sports Association", OrganisationType.Association);
        var club = await SaveOrganisationAsync(App.Services, kmsa, "Ntinda Swifts SC");

        using var set = await client.PostAsJsonAsync(OrganisationParentPath(club.Id), new { parentId = association.Id, reason = _reason }, Ct);
        using var history = await client.GetAsync(OrganisationParentLinksPath(club.Id), Ct);

        if (changes)
        {
            set.StatusCode.ShouldBe(HttpStatusCode.OK);
        }
        else
        {
            await set.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "AUTH_FORBIDDEN");
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
    public async Task A_read_only_tenant_reads_the_history_but_sets_no_parent()
    {
        App.Authorization.UseIdentityRoles();
        var kmsa = await TenantTestData.SaveTenantAsync(
            App.Services, "kmsa", TenantStatus.Active, publicEnabled: false, withDomain: true, withSubscription: false, Ct);
        await TenantTestData.SavePeriodAsync(App.Services, kmsa, new DateOnly(2025, 1, 1), new DateOnly(2025, 12, 31), Ct, cancelReason: "Pilot paused");
        var member = await CreateMemberAsync(App.Services, kmsa, "brian.ssemakula@kmsa.example", Ct, TenantWide(IdentityRoles.Registrar));
        var registrar = App.ClientFor(member.UserId, kmsa);
        var association = await SaveOrganisationAsync(App.Services, kmsa, "Kampala Metro Sports Association", OrganisationType.Association);
        var club = await SaveOrganisationAsync(App.Services, kmsa, "Ntinda Swifts SC");

        using var set = await registrar.PostAsJsonAsync(OrganisationParentPath(club.Id), new { parentId = association.Id, reason = _reason }, Ct);
        using var history = await registrar.GetAsync(OrganisationParentLinksPath(club.Id), Ct);

        await set.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "TENANT_READ_ONLY");
        history.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private static string Day(DateOnly date) => date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

    // The tenants' time zone (TenantTestData): Africa/Kampala, three hours ahead of UTC all year.
    private DateOnly KampalaToday() => DateOnly.FromDateTime(App.Clock.GetUtcNow().ToOffset(TimeSpan.FromHours(3)).DateTime);

    private static async Task<JsonElement> SetParentAsync(
        HttpClient client, Guid organisationId, Guid? parentId, string reason, string? ifMatch = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, OrganisationParentPath(organisationId))
        {
            Content = JsonContent.Create(new { parentId, reason }),
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

    private static async Task<List<JsonElement>> LinksAsync(HttpClient client, Guid organisationId) =>
        [.. (await client.ReadJsonAsync(OrganisationParentLinksPath(organisationId), Ct)).GetProperty("items").EnumerateArray()];

    private static async Task<uint> VersionAsync(HttpClient client, Guid organisationId) =>
        (await client.ReadJsonAsync(OrganisationPath(organisationId), Ct)).GetProperty("version").GetUInt32();

    // Moved directly, so the test's audit trail holds only what it asserts.
    private async Task MoveAsync(Guid organisationId, Guid tenantId, OrganisationStatus status)
    {
        await using var scope = ScopeFor(App.Services, tenantId, out var context);
        var organisation = await context.Set<Organisation>().Include(o => o.StatusHistory).SingleAsync(o => o.Id == organisationId, Ct);
        organisation.ChangeStatus(status, "Set up by the test", BuildingBlocks.Actors.SystemActor.Instance, App.Clock.GetUtcNow())
            .IsSuccess.ShouldBeTrue();
        await context.SaveChangesAsync(Ct);
    }

    /// <summary>An active tenant and a client of a member with <paramref name="role"/> tenant-wide.</summary>
    private async Task<(Guid Tenant, HttpClient Client)> TenantAsync(string slug, string role)
    {
        App.Authorization.UseIdentityRoles();
        var tenant = await TenantTestData.SaveTenantAsync(App.Services, slug, TenantStatus.Active, Ct);
        var member = await CreateMemberAsync(App.Services, tenant, MailboxOf(role, $"{slug}.example"), Ct, TenantWide(role));
        return (tenant, App.ClientFor(member.UserId, tenant));
    }
}

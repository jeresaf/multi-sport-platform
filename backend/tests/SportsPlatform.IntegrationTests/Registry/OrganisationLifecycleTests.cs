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
/// The organisation lifecycle over HTTP (S07-T2b-1; D12, Q55): every move from every status that isn't archived, with
/// its history row and audit entry; unarchiving only to the status before archiving, so a dissolved organisation stays
/// dissolved; the reason, the version, the history read, the roles and the read-only tenant.
/// </summary>
public sealed class OrganisationLifecycleTests(PostgresFixture fixture) : DatabaseTest(fixture)
{
    private const string _reason = "Membership fees unpaid for the season";

    private static readonly Dictionary<OrganisationStatus, string> _codes = new()
    {
        [OrganisationStatus.Active] = "ACTIVE",
        [OrganisationStatus.Inactive] = "INACTIVE",
        [OrganisationStatus.Suspended] = "SUSPENDED",
        [OrganisationStatus.Dissolved] = "DISSOLVED",
        [OrganisationStatus.Archived] = "ARCHIVED",
    };

    // D12: the moves from each status that isn't archived; every other pair is refused.
    private static readonly HashSet<(OrganisationStatus, OrganisationStatus)> _allowed =
    [
        (OrganisationStatus.Active, OrganisationStatus.Inactive),
        (OrganisationStatus.Active, OrganisationStatus.Suspended),
        (OrganisationStatus.Active, OrganisationStatus.Dissolved),
        (OrganisationStatus.Active, OrganisationStatus.Archived),
        (OrganisationStatus.Inactive, OrganisationStatus.Active),
        (OrganisationStatus.Inactive, OrganisationStatus.Suspended),
        (OrganisationStatus.Inactive, OrganisationStatus.Dissolved),
        (OrganisationStatus.Inactive, OrganisationStatus.Archived),
        (OrganisationStatus.Suspended, OrganisationStatus.Active),
        (OrganisationStatus.Suspended, OrganisationStatus.Inactive),
        (OrganisationStatus.Suspended, OrganisationStatus.Dissolved),
        (OrganisationStatus.Suspended, OrganisationStatus.Archived),
        (OrganisationStatus.Dissolved, OrganisationStatus.Archived),
    ];

    public static TheoryData<OrganisationStatus, OrganisationStatus> EveryPair()
    {
        var data = new TheoryData<OrganisationStatus, OrganisationStatus>();
        foreach (var from in Enum.GetValues<OrganisationStatus>().Where(status => status != OrganisationStatus.Archived))
        {
            foreach (var to in Enum.GetValues<OrganisationStatus>())
            {
                data.Add(from, to);
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(EveryPair))]
    public async Task Each_status_allows_only_its_moves(OrganisationStatus from, OrganisationStatus to)
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var organisation = await SaveOrganisationAsync(App.Services, kmsa, "Ntinda Swifts SC", status: from);

        using var response = await registrar.PostAsJsonAsync(
            OrganisationStatusPath(organisation.Id), new { status = _codes[to], reason = _reason }, Ct);

        await AccountTestData.DeliverOutboxAsync(App.Services, Ct);
        var entry = (await AuditTestData.EntriesAsync(App.Services, Ct)).ShouldHaveSingleItem();
        (entry.ActionCode, entry.EntityType, entry.EntityId, entry.Reason)
            .ShouldBe(("organisation.status.change", "Organisation", organisation.Id, _reason));
        await using var scope = ScopeFor(App.Services, kmsa, out var context);
        var saved = await context.Set<Organisation>().AsNoTracking().Include(o => o.StatusHistory).SingleAsync(Ct);
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
            await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, "ORGANISATION_STATUS_INVALID");
            saved.Status.ShouldBe(from);

            // No row for a refused move: the creation row, and the test's setup move for a status other than ACTIVE.
            saved.StatusHistory.Count.ShouldBe(from == OrganisationStatus.Active ? 1 : 2);
            entry.ErrorCode.ShouldBe("ORGANISATION_STATUS_INVALID");
        }
    }

    // Q55 and T2 acceptance 3: archiving a dissolved organisation and unarchiving it never undoes the dissolution.
    [Theory]
    [InlineData(OrganisationStatus.Active)]
    [InlineData(OrganisationStatus.Inactive)]
    [InlineData(OrganisationStatus.Suspended)]
    [InlineData(OrganisationStatus.Dissolved)]
    public async Task Unarchiving_goes_back_only_to_the_status_before_archiving(OrganisationStatus before)
    {
        var (_, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var id = (await CreateOrganisationAsync(registrar, NtindaSwifts())).GetProperty("id").GetGuid();
        if (before != OrganisationStatus.Active)
        {
            App.Clock.Advance(TimeSpan.FromMinutes(1));
            await MoveAsync(registrar, id, before, _reason);
        }

        App.Clock.Advance(TimeSpan.FromMinutes(1));
        await MoveAsync(registrar, id, OrganisationStatus.Archived, "No longer affiliated to the association");
        App.Clock.Advance(TimeSpan.FromMinutes(1));

        foreach (var other in _codes.Keys.Where(status => status != before))
        {
            using var refused = await registrar.PostAsJsonAsync(
                OrganisationStatusPath(id), new { status = _codes[other], reason = "Archived by mistake" }, Ct);
            await refused.ShouldBeProblemAsync(HttpStatusCode.Conflict, "ORGANISATION_STATUS_INVALID");
        }

        (await MoveAsync(registrar, id, before, "Archived by mistake")).GetProperty("status").GetString().ShouldBe(_codes[before]);
    }

    [Fact]
    public async Task A_second_archive_is_undone_to_the_status_it_archived()
    {
        var (_, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var id = (await CreateOrganisationAsync(registrar, NtindaSwifts())).GetProperty("id").GetGuid();

        foreach (var (to, reason) in new[]
        {
            (OrganisationStatus.Archived, "Recorded twice"),
            (OrganisationStatus.Active, "Recorded once after all"),
            (OrganisationStatus.Suspended, "Membership fees unpaid"),
            (OrganisationStatus.Archived, "No longer affiliated"),
        })
        {
            App.Clock.Advance(TimeSpan.FromMinutes(1));
            await MoveAsync(registrar, id, to, reason);
        }

        using var active = await registrar.PostAsJsonAsync(OrganisationStatusPath(id), new { status = "ACTIVE", reason = "Fees paid" }, Ct);
        await active.ShouldBeProblemAsync(HttpStatusCode.Conflict, "ORGANISATION_STATUS_INVALID");
        (await MoveAsync(registrar, id, OrganisationStatus.Suspended, "Archived by mistake"))
            .GetProperty("status").GetString().ShouldBe("SUSPENDED");
    }

    [Fact]
    public async Task The_history_lists_every_move_newest_first_with_who_and_why()
    {
        var (_, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var (_, elsscOwner) = await TenantAsync("elssc", IdentityRoles.TenantOwner);
        var id = (await CreateOrganisationAsync(registrar, NtindaSwifts())).GetProperty("id").GetGuid();
        var created = App.Clock.GetUtcNow();
        App.Clock.Advance(TimeSpan.FromMinutes(2));
        await MoveAsync(registrar, id, OrganisationStatus.Suspended, $" {_reason}  ");
        App.Clock.Advance(TimeSpan.FromMinutes(40));
        await MoveAsync(registrar, id, OrganisationStatus.Active, "Fees paid");

        var history = await registrar.ReadJsonAsync(OrganisationStatusChangesPath(id), Ct);

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

        using (var unknown = await registrar.GetAsync(OrganisationStatusChangesPath(Guid.NewGuid()), Ct))
        {
            await unknown.ShouldBeProblemAsync(HttpStatusCode.NotFound, "ORGANISATION_NOT_FOUND");
        }

        using var otherTenants = await elsscOwner.GetAsync(OrganisationStatusChangesPath(id), Ct);
        await otherTenants.ShouldBeProblemAsync(HttpStatusCode.NotFound, "ORGANISATION_NOT_FOUND");
    }

    [Theory]
    [InlineData("""{"status":"SUSPENDED"}""", "VALIDATION_INVALID_REQUEST", "reason", "VALIDATION_REASON_REQUIRED")]
    [InlineData("""{"status":"SUSPENDED","reason":"   "}""", "VALIDATION_INVALID_REQUEST", "reason", "VALIDATION_REASON_REQUIRED")]
    [InlineData("""{"reason":"Membership fees unpaid"}""", "VALIDATION_INVALID_REQUEST", "status", "VALIDATION_INVALID_REQUEST")]
    [InlineData("""{"status":"MERGED","reason":"Membership fees unpaid"}""", "VALIDATION_MALFORMED_REQUEST", null, null)]
    public async Task A_move_without_a_status_or_a_reason_is_refused(string body, string code, string? field, string? fieldCode)
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var organisation = await SaveOrganisationAsync(App.Services, kmsa, "Ntinda Swifts SC");

        using var content = new StringContent(body, System.Text.Encoding.UTF8, "application/json");
        using var response = await registrar.PostAsync(OrganisationStatusPath(organisation.Id), content, Ct);

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
        var organisation = await SaveOrganisationAsync(App.Services, kmsa, "Ntinda Swifts SC");

        using var longest = await registrar.PostAsJsonAsync(
            OrganisationStatusPath(organisation.Id), new { status = "SUSPENDED", reason = new string('r', 500) }, Ct);
        using var tooLong = await registrar.PostAsJsonAsync(
            OrganisationStatusPath(organisation.Id), new { status = "ACTIVE", reason = new string('r', 501) }, Ct);

        longest.StatusCode.ShouldBe(HttpStatusCode.OK);
        var problem = await tooLong.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_INVALID_REQUEST");
        Fields(problem).ShouldBe([("reason", "VALIDATION_REASON_REQUIRED")]);
    }

    [Fact]
    public async Task A_move_from_a_stale_version_is_an_organisation_conflict_and_a_move_changes_the_version()
    {
        var (_, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var created = await CreateOrganisationAsync(registrar, NtindaSwifts());
        var id = created.GetProperty("id").GetGuid();
        var version = created.GetProperty("version").GetUInt32();

        var moved = await MoveAsync(registrar, id, OrganisationStatus.Suspended, _reason, $"\"{version}\"");
        moved.GetProperty("version").GetUInt32().ShouldNotBe(version);

        using var request = new HttpRequestMessage(HttpMethod.Post, OrganisationStatusPath(id))
        {
            Content = JsonContent.Create(new { status = "ACTIVE", reason = "Fees paid" }),
        };
        request.Headers.TryAddWithoutValidation("If-Match", $"\"{version}\"");
        using var stale = await registrar.SendAsync(request, Ct);
        await stale.ShouldBeProblemAsync(HttpStatusCode.Conflict, "ORGANISATION_CONCURRENCY_CONFLICT");

        using var staleBody = await registrar.PostAsJsonAsync(
            OrganisationStatusPath(id), new { status = "ACTIVE", reason = "Fees paid", version }, Ct);
        await staleBody.ShouldBeProblemAsync(HttpStatusCode.Conflict, "ORGANISATION_CONCURRENCY_CONFLICT");

        using var unknown = await registrar.PostAsJsonAsync(
            OrganisationStatusPath(Guid.NewGuid()), new { status = "SUSPENDED", reason = _reason }, Ct);
        await unknown.ShouldBeProblemAsync(HttpStatusCode.NotFound, "ORGANISATION_NOT_FOUND");
    }

    [Fact]
    public async Task Another_tenants_organisation_cant_be_moved()
    {
        var (kmsa, _) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var (_, elsscOwner) = await TenantAsync("elssc", IdentityRoles.TenantOwner);
        var organisation = await SaveOrganisationAsync(App.Services, kmsa, "Ntinda Swifts SC");

        using var response = await elsscOwner.PostAsJsonAsync(
            OrganisationStatusPath(organisation.Id), new { status = "SUSPENDED", reason = _reason }, Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.NotFound, "ORGANISATION_NOT_FOUND");
        await using var scope = ScopeFor(App.Services, kmsa, out var context);
        var saved = await context.Set<Organisation>().AsNoTracking().Include(o => o.StatusHistory).SingleAsync(Ct);
        (saved.Status, saved.StatusHistory.Count).ShouldBe((OrganisationStatus.Active, 1));
    }

    // D20: moving organisations is the owner's, the admin's and the registrar's; the history is read with the record.
    [Theory]
    [InlineData(IdentityRoles.TenantOwner, true, true)]
    [InlineData(IdentityRoles.TenantAdmin, true, true)]
    [InlineData(IdentityRoles.Registrar, true, true)]
    [InlineData(IdentityRoles.CompetitionManager, false, true)]
    [InlineData(IdentityRoles.ResultsOfficer, false, true)]
    [InlineData(IdentityRoles.DataEntry, false, true)]
    [InlineData(IdentityRoles.Viewer, false, true)]
    [InlineData(IdentityRoles.Scorer, false, false)]
    public async Task Only_those_who_change_organisations_may_move_them(string role, bool changes, bool reads)
    {
        var (kmsa, client) = await TenantAsync("kmsa", role);
        var organisation = await SaveOrganisationAsync(App.Services, kmsa, "Ntinda Swifts SC");

        using var move = await client.PostAsJsonAsync(
            OrganisationStatusPath(organisation.Id), new { status = "SUSPENDED", reason = _reason }, Ct);
        using var history = await client.GetAsync(OrganisationStatusChangesPath(organisation.Id), Ct);

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
    public async Task A_read_only_tenant_reads_the_history_but_moves_no_organisation()
    {
        App.Authorization.UseIdentityRoles();
        var kmsa = await TenantTestData.SaveTenantAsync(
            App.Services, "kmsa", TenantStatus.Active, publicEnabled: false, withDomain: true, withSubscription: false, Ct);
        await TenantTestData.SavePeriodAsync(App.Services, kmsa, new DateOnly(2025, 1, 1), new DateOnly(2025, 12, 31), Ct, cancelReason: "Pilot paused");
        var member = await CreateMemberAsync(App.Services, kmsa, "brian.ssemakula@kmsa.example", Ct, TenantWide(IdentityRoles.Registrar));
        var registrar = App.ClientFor(member.UserId, kmsa);
        var organisation = await SaveOrganisationAsync(App.Services, kmsa, "Ntinda Swifts SC");

        using var move = await registrar.PostAsJsonAsync(
            OrganisationStatusPath(organisation.Id), new { status = "SUSPENDED", reason = _reason }, Ct);
        using var history = await registrar.GetAsync(OrganisationStatusChangesPath(organisation.Id), Ct);

        await move.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "TENANT_READ_ONLY");
        history.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private static async Task<JsonElement> MoveAsync(
        HttpClient client, Guid organisationId, OrganisationStatus to, string reason, string? ifMatch = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, OrganisationStatusPath(organisationId))
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

    /// <summary>An active tenant and a client of a member with <paramref name="role"/> tenant-wide.</summary>
    private async Task<(Guid Tenant, HttpClient Client)> TenantAsync(string slug, string role)
    {
        App.Authorization.UseIdentityRoles();
        var tenant = await TenantTestData.SaveTenantAsync(App.Services, slug, TenantStatus.Active, Ct);
        var member = await CreateMemberAsync(App.Services, tenant, MailboxOf(role, $"{slug}.example"), Ct, TenantWide(role));
        return (tenant, App.ClientFor(member.UserId, tenant));
    }
}

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
/// The venue lifecycle over HTTP (S07-T1b-1; D12, Q55): every move from every status that isn't archived, with its
/// history row and audit entry; unarchiving only to the status before archiving; the reason, the version, the history
/// read, the roles and the read-only tenant.
/// </summary>
public sealed class VenueLifecycleTests(PostgresFixture fixture) : DatabaseTest(fixture)
{
    private const string _reason = "Pitch reseeded for the season";

    private static readonly Dictionary<VenueStatus, string> _codes = new()
    {
        [VenueStatus.Active] = "ACTIVE",
        [VenueStatus.TemporarilyUnavailable] = "TEMPORARILY_UNAVAILABLE",
        [VenueStatus.Closed] = "CLOSED",
        [VenueStatus.Archived] = "ARCHIVED",
    };

    // D12: the moves from each status that isn't archived; every other pair is refused.
    private static readonly HashSet<(VenueStatus, VenueStatus)> _allowed =
    [
        (VenueStatus.Active, VenueStatus.TemporarilyUnavailable),
        (VenueStatus.Active, VenueStatus.Closed),
        (VenueStatus.Active, VenueStatus.Archived),
        (VenueStatus.TemporarilyUnavailable, VenueStatus.Active),
        (VenueStatus.TemporarilyUnavailable, VenueStatus.Closed),
        (VenueStatus.TemporarilyUnavailable, VenueStatus.Archived),
        (VenueStatus.Closed, VenueStatus.Active),
        (VenueStatus.Closed, VenueStatus.Archived),
    ];

    public static TheoryData<VenueStatus, VenueStatus> EveryPair()
    {
        var data = new TheoryData<VenueStatus, VenueStatus>();
        foreach (var from in Enum.GetValues<VenueStatus>().Where(status => status != VenueStatus.Archived))
        {
            foreach (var to in Enum.GetValues<VenueStatus>())
            {
                data.Add(from, to);
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(EveryPair))]
    public async Task Each_status_allows_only_its_moves(VenueStatus from, VenueStatus to)
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var venue = await SaveVenueAsync(App.Services, kmsa, "Kiwatule Meadow Ground", status: from);

        using var response = await registrar.PostAsJsonAsync(VenueStatusPath(venue.Id), new { status = _codes[to], reason = _reason }, Ct);

        await AccountTestData.DeliverOutboxAsync(App.Services, Ct);
        var entry = (await AuditTestData.EntriesAsync(App.Services, Ct)).ShouldHaveSingleItem();
        (entry.ActionCode, entry.EntityType, entry.EntityId, entry.Reason).ShouldBe(("venue.status.change", "Venue", venue.Id, _reason));
        await using var scope = ScopeFor(App.Services, kmsa, out var context);
        var saved = await context.Set<Venue>().AsNoTracking().Include(v => v.StatusHistory).SingleAsync(Ct);
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
            await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, "VENUE_STATUS_INVALID");
            saved.Status.ShouldBe(from);
            entry.ErrorCode.ShouldBe("VENUE_STATUS_INVALID");
        }
    }

    // Q55: archiving a closed venue and unarchiving it never reopens it.
    [Theory]
    [InlineData(VenueStatus.Active)]
    [InlineData(VenueStatus.TemporarilyUnavailable)]
    [InlineData(VenueStatus.Closed)]
    public async Task Unarchiving_goes_back_only_to_the_status_before_archiving(VenueStatus before)
    {
        var (_, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var id = (await CreateVenueAsync(registrar, KiwatuleMeadow())).GetProperty("id").GetGuid();
        if (before != VenueStatus.Active)
        {
            App.Clock.Advance(TimeSpan.FromMinutes(1));
            await MoveAsync(registrar, id, before, "Pitch reseeded for the season");
        }

        App.Clock.Advance(TimeSpan.FromMinutes(1));
        await MoveAsync(registrar, id, VenueStatus.Archived, "No longer used by the association");
        App.Clock.Advance(TimeSpan.FromMinutes(1));

        foreach (var other in _codes.Keys.Where(status => status != before))
        {
            using var refused = await registrar.PostAsJsonAsync(VenueStatusPath(id), new { status = _codes[other], reason = "Archived by mistake" }, Ct);
            await refused.ShouldBeProblemAsync(HttpStatusCode.Conflict, "VENUE_STATUS_INVALID");
        }

        (await MoveAsync(registrar, id, before, "Archived by mistake")).GetProperty("status").GetString().ShouldBe(_codes[before]);
    }

    [Fact]
    public async Task A_second_archive_is_undone_to_the_status_it_archived()
    {
        var (_, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var id = (await CreateVenueAsync(registrar, KiwatuleMeadow())).GetProperty("id").GetGuid();

        foreach (var (to, reason) in new[]
        {
            (VenueStatus.Archived, "Recorded twice"),
            (VenueStatus.Active, "Recorded once after all"),
            (VenueStatus.Closed, "The ground was sold"),
            (VenueStatus.Archived, "No longer used"),
        })
        {
            App.Clock.Advance(TimeSpan.FromMinutes(1));
            await MoveAsync(registrar, id, to, reason);
        }

        using var active = await registrar.PostAsJsonAsync(VenueStatusPath(id), new { status = "ACTIVE", reason = "Back in use" }, Ct);
        await active.ShouldBeProblemAsync(HttpStatusCode.Conflict, "VENUE_STATUS_INVALID");
        (await MoveAsync(registrar, id, VenueStatus.Closed, "Archived by mistake")).GetProperty("status").GetString().ShouldBe("CLOSED");
    }

    [Fact]
    public async Task The_history_lists_every_move_newest_first_with_who_and_why()
    {
        var (_, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var (_, elsscOwner) = await TenantAsync("elssc", IdentityRoles.TenantOwner);
        var id = (await CreateVenueAsync(registrar, KiwatuleMeadow())).GetProperty("id").GetGuid();
        var created = App.Clock.GetUtcNow();
        App.Clock.Advance(TimeSpan.FromMinutes(2));
        await MoveAsync(registrar, id, VenueStatus.TemporarilyUnavailable, " Pitch reseeded for the season  ");
        App.Clock.Advance(TimeSpan.FromMinutes(40));
        await MoveAsync(registrar, id, VenueStatus.Active, "Reseeding finished");

        var history = await registrar.ReadJsonAsync(VenueStatusChangesPath(id), Ct);

        var items = history.GetProperty("items").EnumerateArray().ToList();
        items.Select(i => (i.GetProperty("fromStatus").GetString(), i.GetProperty("toStatus").GetString(), i.GetProperty("reason").GetString()))
            .ShouldBe(
            [
                ("TEMPORARILY_UNAVAILABLE", "ACTIVE", "Reseeding finished"),
                ("ACTIVE", "TEMPORARILY_UNAVAILABLE", _reason),
                (null, "ACTIVE", null),
            ]);
        items[^1].GetProperty("occurredAt").GetDateTimeOffset().ShouldBe(created);
        items.ShouldAllBe(i => i.GetProperty("actorKind").GetString() == "USER");

        using (var unknown = await registrar.GetAsync(VenueStatusChangesPath(Guid.NewGuid()), Ct))
        {
            await unknown.ShouldBeProblemAsync(HttpStatusCode.NotFound, "VENUE_NOT_FOUND");
        }

        using var otherTenants = await elsscOwner.GetAsync(VenueStatusChangesPath(id), Ct);
        await otherTenants.ShouldBeProblemAsync(HttpStatusCode.NotFound, "VENUE_NOT_FOUND");
    }

    // As the profiles' moves: the request is VALIDATION_INVALID_REQUEST, and a missing reason is coded on its field.
    [Theory]
    [InlineData("""{"status":"CLOSED"}""", "VALIDATION_INVALID_REQUEST", "reason", "VALIDATION_REASON_REQUIRED")]
    [InlineData("""{"status":"CLOSED","reason":"   "}""", "VALIDATION_INVALID_REQUEST", "reason", "VALIDATION_REASON_REQUIRED")]
    [InlineData("""{"reason":"The ground was sold"}""", "VALIDATION_INVALID_REQUEST", "status", "VALIDATION_INVALID_REQUEST")]
    [InlineData("""{"status":"DEMOLISHED","reason":"The ground was sold"}""", "VALIDATION_MALFORMED_REQUEST", null, null)]
    public async Task A_move_without_a_status_or_a_reason_is_refused(string body, string code, string? field, string? fieldCode)
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var venue = await SaveVenueAsync(App.Services, kmsa, "Kiwatule Meadow Ground");

        using var content = new StringContent(body, System.Text.Encoding.UTF8, "application/json");
        using var response = await registrar.PostAsync(VenueStatusPath(venue.Id), content, Ct);

        var problem = await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, code);
        if (field is not null)
        {
            Fields(problem).ShouldBe([(field, fieldCode)]);
        }
    }

    [Fact]
    public async Task A_move_from_a_stale_version_is_a_venue_conflict_and_a_move_changes_the_version()
    {
        var (_, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var created = await CreateVenueAsync(registrar, KiwatuleMeadow());
        var id = created.GetProperty("id").GetGuid();
        var version = created.GetProperty("version").GetUInt32();

        var moved = await MoveAsync(registrar, id, VenueStatus.Closed, "The ground was sold", $"\"{version}\"");
        moved.GetProperty("version").GetUInt32().ShouldNotBe(version);

        using var request = new HttpRequestMessage(HttpMethod.Post, VenueStatusPath(id))
        {
            Content = JsonContent.Create(new { status = "ACTIVE", reason = "The sale fell through" }),
        };
        request.Headers.TryAddWithoutValidation("If-Match", $"\"{version}\"");
        using var stale = await registrar.SendAsync(request, Ct);
        await stale.ShouldBeProblemAsync(HttpStatusCode.Conflict, "VENUE_CONCURRENCY_CONFLICT");

        using var staleBody = await registrar.PostAsJsonAsync(
            VenueStatusPath(id), new { status = "ACTIVE", reason = "The sale fell through", version }, Ct);
        await staleBody.ShouldBeProblemAsync(HttpStatusCode.Conflict, "VENUE_CONCURRENCY_CONFLICT");

        using var unknown = await registrar.PostAsJsonAsync(VenueStatusPath(Guid.NewGuid()), new { status = "CLOSED", reason = _reason }, Ct);
        await unknown.ShouldBeProblemAsync(HttpStatusCode.NotFound, "VENUE_NOT_FOUND");
    }

    [Fact]
    public async Task Another_tenants_venue_cant_be_moved()
    {
        var (kmsa, _) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var (_, elsscOwner) = await TenantAsync("elssc", IdentityRoles.TenantOwner);
        var venue = await SaveVenueAsync(App.Services, kmsa, "Kiwatule Meadow Ground");

        using var response = await elsscOwner.PostAsJsonAsync(VenueStatusPath(venue.Id), new { status = "CLOSED", reason = _reason }, Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.NotFound, "VENUE_NOT_FOUND");
        await using var scope = ScopeFor(App.Services, kmsa, out var context);
        var saved = await context.Set<Venue>().AsNoTracking().Include(v => v.StatusHistory).SingleAsync(Ct);
        (saved.Status, saved.StatusHistory.Count).ShouldBe((VenueStatus.Active, 1));
    }

    // D20: moving venues is the owner's, the admin's and the registrar's; the history is read with the venue.
    [Theory]
    [InlineData(IdentityRoles.TenantOwner, true, true)]
    [InlineData(IdentityRoles.TenantAdmin, true, true)]
    [InlineData(IdentityRoles.Registrar, true, true)]
    [InlineData(IdentityRoles.CompetitionManager, false, true)]
    [InlineData(IdentityRoles.ResultsOfficer, false, true)]
    [InlineData(IdentityRoles.DataEntry, false, true)]
    [InlineData(IdentityRoles.Viewer, false, true)]
    [InlineData(IdentityRoles.Scorer, false, false)]
    public async Task Only_those_who_change_venues_may_move_them(string role, bool changes, bool reads)
    {
        var (kmsa, client) = await TenantAsync("kmsa", role);
        var venue = await SaveVenueAsync(App.Services, kmsa, "Kiwatule Meadow Ground");

        using var move = await client.PostAsJsonAsync(VenueStatusPath(venue.Id), new { status = "CLOSED", reason = _reason }, Ct);
        using var history = await client.GetAsync(VenueStatusChangesPath(venue.Id), Ct);

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
    public async Task A_read_only_tenant_reads_the_history_but_moves_no_venue()
    {
        App.Authorization.UseIdentityRoles();
        var kmsa = await TenantTestData.SaveTenantAsync(
            App.Services, "kmsa", TenantStatus.Active, publicEnabled: false, withDomain: true, withSubscription: false, Ct);
        await TenantTestData.SavePeriodAsync(App.Services, kmsa, new DateOnly(2025, 1, 1), new DateOnly(2025, 12, 31), Ct, cancelReason: "Pilot paused");
        var member = await CreateMemberAsync(App.Services, kmsa, "brian.ssemakula@kmsa.example", Ct, TenantWide(IdentityRoles.Registrar));
        var registrar = App.ClientFor(member.UserId, kmsa);
        var venue = await SaveVenueAsync(App.Services, kmsa, "Kiwatule Meadow Ground");

        using var move = await registrar.PostAsJsonAsync(VenueStatusPath(venue.Id), new { status = "CLOSED", reason = _reason }, Ct);
        using var history = await registrar.GetAsync(VenueStatusChangesPath(venue.Id), Ct);

        await move.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "TENANT_READ_ONLY");
        history.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private static async Task<JsonElement> MoveAsync(HttpClient client, Guid venueId, VenueStatus to, string reason, string? ifMatch = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, VenueStatusPath(venueId))
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

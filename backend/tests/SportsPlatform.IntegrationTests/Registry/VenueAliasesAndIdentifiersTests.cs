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
using SportsPlatform.Modules.Registry.Domain.Venues;
using SportsPlatform.Modules.Tenancy.Contracts.Tenants;
using static SportsPlatform.IntegrationTests.Identity.MembershipTestData;
using static SportsPlatform.IntegrationTests.Registry.PeopleRequests;
using static SportsPlatform.IntegrationTests.Registry.RegistryTestData;
using static SportsPlatform.IntegrationTests.Registry.VenueRequests;

namespace SportsPlatform.IntegrationTests.Registry;

/// <summary>
/// A venue's aliases and external identifiers over HTTP (S07-T1c; D10, D11, D20, D21): adding, removing and ending them,
/// the exact identifier check, the archived and closed guards, the version, the audit, the roles and the read-only tenant.
/// </summary>
public sealed class VenueAliasesAndIdentifiersTests(PostgresFixture fixture) : DatabaseTest(fixture)
{
    private const string _facilityNo = "KCCA_FACILITY_NO";

    [Fact]
    public async Task An_added_alias_is_shown_and_searched_until_it_is_removed()
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var venue = await SaveVenueAsync(App.Services, kmsa, "Bukoto Community Stadium");
        var before = (await registrar.ReadJsonAsync(VenuePath(venue.Id), Ct)).GetProperty("version").GetUInt32();

        var added = await OkAsync(await registrar.PostAsJsonAsync(
            VenueAliasesPath(venue.Id), new { name = "  Bukoto  Arena ", kind = "FORMER_NAME", version = before }, Ct));

        added.GetProperty("version").GetUInt32().ShouldNotBe(before);
        var alias = added.GetProperty("aliases").EnumerateArray().ShouldHaveSingleItem();
        (alias.GetProperty("name").GetString(), alias.GetProperty("kind").GetString()).ShouldBe(("Bukoto Arena", "FORMER_NAME"));
        (await registrar.ReadJsonAsync($"{VenuesPath}?q=arena", Ct)).VenueNames().ShouldBe(["Bukoto Community Stadium"]);
        (await registrar.ReadJsonAsync($"{VenuesPath}?q=Bukotto%20Arina", Ct)).VenueNames().ShouldBe(["Bukoto Community Stadium"]);

        var aliasId = alias.GetProperty("id").GetGuid();
        var removed = await OkAsync(await registrar.PostAsJsonAsync(VenueAliasPath(venue.Id, aliasId, "remove"), new { reason = " Never its name " }, Ct));

        removed.GetProperty("aliases").GetArrayLength().ShouldBe(0);
        (await registrar.ReadJsonAsync($"{VenuesPath}?q=arena", Ct)).VenueNames().ShouldBeEmpty();
        await using (var scope = ScopeFor(App.Services, kmsa, out var context))
        {
            var kept = await context.Set<VenueAlias>().AsNoTracking().SingleAsync(Ct);
            (kept.RemoveReason, kept.RemovedAt).ShouldBe(("Never its name", App.Clock.GetUtcNow()));
        }

        await AccountTestData.DeliverOutboxAsync(App.Services, Ct);
        var entries = (await AuditTestData.EntriesAsync(App.Services, Ct)).Where(e => e.Outcome == AuditOutcome.Success).ToList();
        entries.Select(e => (e.ActionCode, e.EntityType, e.EntityId, e.Reason))
            .ShouldBe([("venue.alias.add", "Venue", venue.Id, null), ("venue.alias.remove", "Venue", venue.Id, "Never its name")], ignoreOrder: true);
        entries.ShouldAllBe(e => JsonDocument.Parse(e.Metadata!).RootElement.GetProperty("aliasId").GetGuid() == aliasId);
    }

    [Fact]
    public async Task An_alias_the_venue_is_already_known_by_or_without_a_letter_is_refused()
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var venue = await SaveVenueAsync(App.Services, kmsa, "Kiwatule Meadow Ground", alias: ("The Meadow", AliasKind.Nickname));

        foreach (var name in new[] { "Meadow Ground, Kiwatule", "the MEADOW" })
        {
            using var known = await registrar.PostAsJsonAsync(VenueAliasesPath(venue.Id), new { name, kind = "SPELLING" }, Ct);
            var problem = await known.ShouldBeProblemAsync(HttpStatusCode.Conflict, "VENUE_ALIAS_EXISTS");
            Fields(problem).ShouldBe([("name", "VENUE_ALIAS_EXISTS")]);
        }

        using (var blank = await registrar.PostAsJsonAsync(VenueAliasesPath(venue.Id), new { name = "--", kind = "SPELLING" }, Ct))
        {
            var problem = await blank.ShouldBeProblemAsync(HttpStatusCode.UnprocessableEntity, "VENUE_NAME_INVALID");
            Fields(problem).ShouldBe([("name", "VENUE_NAME_INVALID")]);
        }

        using var malformed = await registrar.PostAsJsonAsync(VenueAliasesPath(venue.Id), new { name = new string('a', 121) }, Ct);
        var invalid = await malformed.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_INVALID_REQUEST");
        Fields(invalid).Select(field => field.Item1).ShouldBe(["name", "kind"], ignoreOrder: true);
    }

    [Fact]
    public async Task Removing_needs_a_reason_and_an_alias_of_this_venue_not_yet_removed()
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var venue = await SaveVenueAsync(App.Services, kmsa, "Kiwatule Meadow Ground", alias: ("The Meadow", AliasKind.Nickname));
        var other = await SaveVenueAsync(App.Services, kmsa, "Kisaasi Recreation Ground");
        var read = await registrar.ReadJsonAsync(VenuePath(venue.Id), Ct);
        var aliasId = read.GetProperty("aliases")[0].GetProperty("id").GetGuid();

        using (var noReason = await registrar.PostAsJsonAsync(VenueAliasPath(venue.Id, aliasId, "remove"), new { reason = " " }, Ct))
        {
            await noReason.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_REASON_REQUIRED");
        }

        foreach (var (owner, id) in new[] { (other.Id, aliasId), (venue.Id, Guid.CreateVersion7()) })
        {
            using var missing = await registrar.PostAsJsonAsync(VenueAliasPath(owner, id, "remove"), new { reason = "Misspelt" }, Ct);
            await missing.ShouldBeProblemAsync(HttpStatusCode.NotFound, "VENUE_ALIAS_NOT_FOUND");
        }

        var removed = await OkAsync(await registrar.PostAsJsonAsync(VenueAliasPath(venue.Id, aliasId, "remove"), new { reason = "Misspelt" }, Ct));
        removed.GetProperty("version").GetUInt32().ShouldNotBe(read.GetProperty("version").GetUInt32());
        using var again = await registrar.PostAsJsonAsync(VenueAliasPath(venue.Id, aliasId, "remove"), new { reason = "Misspelt" }, Ct);
        await again.ShouldBeProblemAsync(HttpStatusCode.Conflict, "VENUE_ALIAS_REMOVED");
    }

    [Fact]
    public async Task An_identifier_any_venue_has_or_had_is_refused_but_a_persons_is_not()
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var bukoto = await SaveVenueAsync(App.Services, kmsa, "Bukoto Community Stadium");
        var kisaasi = await SaveVenueAsync(App.Services, kmsa, "Kisaasi Recreation Ground");
        var identifierId = await AddIdentifierAsync(registrar, bukoto.Id, "KCCA-0147");
        await OkAsync(await registrar.PostAsJsonAsync(VenueIdentifierPath(bukoto.Id, identifierId, "end"), new { }, Ct));

        foreach (var (venue, value) in new[] { (kisaasi.Id, "KCCA-0147"), (kisaasi.Id, " KCCA-0147 "), (bukoto.Id, "KCCA-0147") })
        {
            using var taken = await registrar.PostAsJsonAsync(VenueIdentifiersPath(venue), new { authority = _facilityNo, value }, Ct);
            var problem = await taken.ShouldBeProblemAsync(HttpStatusCode.Conflict, "VENUE_EXTERNAL_ID_EXISTS");
            Fields(problem).ShouldBe([("value", "VENUE_EXTERNAL_ID_EXISTS")]);
        }

        // Another entity type: a person may hold the value a venue had, and a venue a person's.
        var person = await SavePersonAsync(App.Services, kmsa, IvanMukasa, Ct);
        using (var personal = await registrar.PostAsJsonAsync(
                   $"{PersonPath(person.Id)}/external-identifiers", new { authority = _facilityNo, value = "KCCA-0147" }, Ct))
        {
            personal.StatusCode.ShouldBe(HttpStatusCode.OK, await personal.Content.ReadAsStringAsync(Ct));
        }

        await OkAsync(await registrar.PostAsJsonAsync(VenueIdentifiersPath(kisaasi.Id), new { authority = "UFA_GROUND_NO", value = "KCCA-0147" }, Ct));
    }

    [Fact]
    public async Task Adding_an_identifier_has_no_reserved_authority_and_moves_the_version()
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var venue = await SaveVenueAsync(App.Services, kmsa, "Kiwatule Meadow Ground");
        var before = (await registrar.ReadJsonAsync(VenuePath(venue.Id), Ct)).GetProperty("version").GetUInt32();

        using (var malformed = await registrar.PostAsJsonAsync(VenueIdentifiersPath(venue.Id), new { authority = "kcca no", value = " " }, Ct))
        {
            var problem = await malformed.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_INVALID_REQUEST");
            Fields(problem).Select(field => field.Item1).ShouldBe(["authority", "value"], ignoreOrder: true);
        }

        var added = await OkAsync(await registrar.PostAsJsonAsync(
            VenueIdentifiersPath(venue.Id), new { authority = _facilityNo, value = " KCCA-0201 ", validFrom = "2026-01-10", version = before }, Ct));

        added.GetProperty("version").GetUInt32().ShouldNotBe(before);
        var identifier = added.GetProperty("externalIdentifiers").EnumerateArray().ShouldHaveSingleItem();
        (identifier.GetProperty("authority").GetString(), identifier.GetProperty("value").GetString(), identifier.GetProperty("validFrom").GetString())
            .ShouldBe((_facilityNo, "KCCA-0201", "2026-01-10"));
        identifier.GetProperty("validTo").ValueKind.ShouldBe(JsonValueKind.Null);

        // The national-document authorities are a person rule only (D10).
        await OkAsync(await registrar.PostAsJsonAsync(VenueIdentifiersPath(venue.Id), new { authority = "NIN", value = "LAND-77" }, Ct));
        using (var stale = await registrar.PostAsJsonAsync(VenueIdentifiersPath(venue.Id), new { authority = "UFA_GROUND_NO", value = "UFA-9", version = before }, Ct))
        {
            await stale.ShouldBeProblemAsync(HttpStatusCode.Conflict, "VENUE_CONCURRENCY_CONFLICT");
        }

        await AccountTestData.DeliverOutboxAsync(App.Services, Ct);
        var entry = (await AuditTestData.EntriesAsync(App.Services, Ct))
            .First(e => e.ActionCode == "venue.external_id.add" && e.Outcome == AuditOutcome.Success && e.Metadata!.Contains(_facilityNo, StringComparison.Ordinal));
        var metadata = JsonDocument.Parse(entry.Metadata!).RootElement;
        (metadata.GetProperty("identifierId").GetGuid(), metadata.GetProperty("authority").GetString())
            .ShouldBe((identifier.GetProperty("id").GetGuid(), _facilityNo));
        (entry.EntityType, entry.EntityId).ShouldBe(("Venue", venue.Id));
    }

    [Fact]
    public async Task A_stale_If_Match_is_a_venue_conflict_for_every_child_change()
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var venue = await SaveVenueAsync(App.Services, kmsa, "Kiwatule Meadow Ground", alias: ("The Meadow", AliasKind.Nickname), identifier: (_facilityNo, "KCCA-0147"));
        var read = await registrar.ReadJsonAsync(VenuePath(venue.Id), Ct);
        var aliasId = read.GetProperty("aliases")[0].GetProperty("id").GetGuid();
        var identifierId = read.GetProperty("externalIdentifiers")[0].GetProperty("id").GetGuid();
        var stale = $"\"{read.GetProperty("version").GetUInt32() + 1}\"";

        foreach (var (path, body) in Requests(venue.Id, aliasId, identifierId))
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
            request.Headers.TryAddWithoutValidation("If-Match", stale);
            using var response = await registrar.SendAsync(request, Ct);
            await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, "VENUE_CONCURRENCY_CONFLICT");
        }
    }

    // The outcome only: the venues lock refuses the second add before its insert, and without the lock the unique index
    // would refuse it on save with the same answer, so this doesn't prove the lock is taken.
    [Fact]
    public async Task Two_venues_given_one_value_at_once_leave_one_refused()
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var bukoto = await SaveVenueAsync(App.Services, kmsa, "Bukoto Community Stadium");
        var kisaasi = await SaveVenueAsync(App.Services, kmsa, "Kisaasi Recreation Ground");

        var responses = await Task.WhenAll(
            registrar.PostAsJsonAsync(VenueIdentifiersPath(bukoto.Id), new { authority = _facilityNo, value = "KCCA-0388" }, Ct),
            registrar.PostAsJsonAsync(VenueIdentifiersPath(kisaasi.Id), new { authority = _facilityNo, value = "KCCA-0388" }, Ct));

        responses.Select(response => response.StatusCode).ShouldBe([HttpStatusCode.OK, HttpStatusCode.Conflict], ignoreOrder: true);
        await responses.Single(response => response.StatusCode == HttpStatusCode.Conflict)
            .ShouldBeProblemAsync(HttpStatusCode.Conflict, "VENUE_EXTERNAL_ID_EXISTS");
        foreach (var response in responses)
        {
            response.Dispose();
        }
    }

    [Fact]
    public async Task Ending_an_identifier_sets_a_date_from_its_start_to_today_once()
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var venue = await SaveVenueAsync(App.Services, kmsa, "Kiwatule Meadow Ground");
        var other = await SaveVenueAsync(App.Services, kmsa, "Kisaasi Recreation Ground");
        var today = TenantToday();
        var facilityNo = await AddIdentifierAsync(registrar, venue.Id, "KCCA-0147", validFrom: today.AddDays(-30));
        var groundNo = await AddIdentifierAsync(registrar, venue.Id, "UFA-9", authority: "UFA_GROUND_NO");

        foreach (var validTo in new[] { today.AddDays(1), today.AddDays(-31) })
        {
            using var invalid = await registrar.PostAsJsonAsync(VenueIdentifierPath(venue.Id, facilityNo, "end"), new { validTo }, Ct);
            var problem = await invalid.ShouldBeProblemAsync(HttpStatusCode.UnprocessableEntity, "VENUE_EXTERNAL_ID_VALIDITY_INVALID");
            Fields(problem).ShouldBe([("validTo", "VENUE_EXTERNAL_ID_VALIDITY_INVALID")]);
        }

        foreach (var (owner, id) in new[] { (other.Id, facilityNo), (venue.Id, Guid.CreateVersion7()) })
        {
            using var missing = await registrar.PostAsync(VenueIdentifierPath(owner, id, "end"), null, Ct);
            await missing.ShouldBeProblemAsync(HttpStatusCode.NotFound, "VENUE_EXTERNAL_ID_NOT_FOUND");
        }

        var before = (await registrar.ReadJsonAsync(VenuePath(venue.Id), Ct)).GetProperty("version").GetUInt32();
        var first = await OkAsync(await registrar.PostAsJsonAsync(
            VenueIdentifierPath(venue.Id, facilityNo, "end"), new { validTo = today.AddDays(-30), reason = "Renumbered by the council" }, Ct));
        first.GetProperty("version").GetUInt32().ShouldNotBe(before);
        var ended = await OkAsync(await registrar.PostAsync(VenueIdentifierPath(venue.Id, groundNo, "end"), null, Ct));

        ended.GetProperty("externalIdentifiers").EnumerateArray()
            .Select(i => (i.GetProperty("id").GetGuid(), i.GetProperty("validTo").GetString()))
            .ShouldBe([
                (facilityNo, today.AddDays(-30).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
                (groundNo, today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
            ]);
        using var again = await registrar.PostAsync(VenueIdentifierPath(venue.Id, groundNo, "end"), null, Ct);
        await again.ShouldBeProblemAsync(HttpStatusCode.Conflict, "VENUE_EXTERNAL_ID_ENDED");
        await AccountTestData.DeliverOutboxAsync(App.Services, Ct);
        (await AuditTestData.EntriesAsync(App.Services, Ct))
            .Where(e => e.ActionCode == "venue.external_id.end" && e.Outcome == AuditOutcome.Success)
            .Select(e => e.Reason).ShouldBe(["Renumbered by the council", null], ignoreOrder: true);
    }

    // D10: an archived or closed venue takes no new alias or identifier; removing and ending stay allowed.
    [Theory]
    [InlineData(VenueStatus.Active, null)]
    [InlineData(VenueStatus.TemporarilyUnavailable, null)]
    [InlineData(VenueStatus.Closed, "VENUE_CLOSED")]
    [InlineData(VenueStatus.Archived, "VENUE_ARCHIVED")]
    public async Task Only_a_venue_that_isnt_closed_or_archived_takes_new_aliases_and_identifiers(VenueStatus status, string? refusal)
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var venue = await SaveVenueAsync(
            App.Services, kmsa, "Kiwatule Meadow Ground", status: status, alias: ("The Meadow", AliasKind.Nickname), identifier: (_facilityNo, "KCCA-0147"));
        var read = await registrar.ReadJsonAsync(VenuePath(venue.Id), Ct);
        var aliasId = read.GetProperty("aliases")[0].GetProperty("id").GetGuid();
        var identifierId = read.GetProperty("externalIdentifiers")[0].GetProperty("id").GetGuid();

        foreach (var (path, body) in new (string, object)[]
        {
            (VenueAliasesPath(venue.Id), new { name = "Kiwatule Grounds", kind = "SPELLING" }),
            (VenueIdentifiersPath(venue.Id), new { authority = "UFA_GROUND_NO", value = "UFA-9" }),
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

        await OkAsync(await registrar.PostAsJsonAsync(VenueAliasPath(venue.Id, aliasId, "remove"), new { reason = "Misspelt" }, Ct));
        await OkAsync(await registrar.PostAsync(VenueIdentifierPath(venue.Id, identifierId, "end"), null, Ct));
    }

    [Fact]
    public async Task Another_tenants_venue_is_not_found()
    {
        var (kmsa, _) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var (elssc, elsscOwner) = await TenantAsync("elssc", IdentityRoles.TenantOwner);
        var venue = await SaveVenueAsync(App.Services, kmsa, "Kiwatule Meadow Ground", alias: ("The Meadow", AliasKind.Nickname), identifier: (_facilityNo, "KCCA-0147"));

        foreach (var (path, body) in Requests(venue.Id, Guid.CreateVersion7(), Guid.CreateVersion7()))
        {
            using var response = await elsscOwner.PostAsJsonAsync(path, body, Ct);
            await response.ShouldBeProblemAsync(HttpStatusCode.NotFound, "VENUE_NOT_FOUND");
        }

        // Another tenant's identifier value is not taken here.
        var own = await SaveVenueAsync(App.Services, elssc, "Seeta Hill Sports Field");
        await OkAsync(await elsscOwner.PostAsJsonAsync(VenueIdentifiersPath(own.Id), new { authority = _facilityNo, value = "KCCA-0147" }, Ct));
    }

    [Theory]
    [InlineData(IdentityRoles.TenantOwner, true)]
    [InlineData(IdentityRoles.TenantAdmin, true)]
    [InlineData(IdentityRoles.Registrar, true)]
    [InlineData(IdentityRoles.CompetitionManager, false)]
    [InlineData(IdentityRoles.ResultsOfficer, false)]
    [InlineData(IdentityRoles.DataEntry, false)]
    [InlineData(IdentityRoles.Viewer, false)]
    [InlineData(IdentityRoles.Scorer, false)]
    public async Task Only_those_who_change_venues_may_change_aliases_and_identifiers(string role, bool allowed)
    {
        var (kmsa, client) = await TenantAsync("kmsa", role);
        var venue = await SaveVenueAsync(App.Services, kmsa, "Kiwatule Meadow Ground", alias: ("The Meadow", AliasKind.Nickname), identifier: (_facilityNo, "KCCA-0147"));
        Guid aliasId, identifierId;
        await using (var scope = ScopeFor(App.Services, kmsa, out var context))
        {
            aliasId = await context.Set<VenueAlias>().Select(a => a.Id).SingleAsync(Ct);
            identifierId = await context.Set<ExternalIdentifier>().Select(i => i.Id).SingleAsync(Ct);
        }

        foreach (var (path, body) in Requests(venue.Id, aliasId, identifierId))
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
    public async Task A_read_only_tenant_changes_no_alias_or_identifier()
    {
        App.Authorization.UseIdentityRoles();
        var kmsa = await TenantTestData.SaveTenantAsync(
            App.Services, "kmsa", TenantStatus.Active, publicEnabled: false, withDomain: true, withSubscription: false, Ct);
        await TenantTestData.SavePeriodAsync(App.Services, kmsa, new DateOnly(2025, 1, 1), new DateOnly(2025, 12, 31), Ct, cancelReason: "Pilot paused");
        var venue = await SaveVenueAsync(App.Services, kmsa, "Kiwatule Meadow Ground");
        var member = await CreateMemberAsync(App.Services, kmsa, "brian.ssemakula@kmsa.example", Ct, TenantWide(IdentityRoles.Registrar));
        var registrar = App.ClientFor(member.UserId, kmsa);

        foreach (var (path, body) in Requests(venue.Id, Guid.CreateVersion7(), Guid.CreateVersion7()))
        {
            using var response = await registrar.PostAsJsonAsync(path, body, Ct);
            await response.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "TENANT_READ_ONLY");
        }
    }

    [Theory]
    [InlineData("KCCA-0147")]
    [InlineData("0147")]
    public async Task The_directory_finds_a_venue_by_an_identifiers_exact_value_ended_ones_included(string q)
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var venue = await SaveVenueAsync(App.Services, kmsa, "Kiwatule Meadow Ground", identifier: (_facilityNo, q));
        await SaveVenueAsync(App.Services, kmsa, "Kisaasi Recreation Ground", identifier: (_facilityNo, $"{q}-B"));
        var identifierId = (await registrar.ReadJsonAsync(VenuePath(venue.Id), Ct)).GetProperty("externalIdentifiers")[0].GetProperty("id").GetGuid();
        await OkAsync(await registrar.PostAsync(VenueIdentifierPath(venue.Id, identifierId, "end"), null, Ct));

        (await registrar.ReadJsonAsync($"{VenuesPath}?q={Uri.EscapeDataString(q)}", Ct)).VenueNames().ShouldBe(["Kiwatule Meadow Ground"]);

        // A person's identifier with the same value finds no venue.
        var person = await SavePersonAsync(App.Services, kmsa, IvanMukasa, Ct);
        await using (var scope = ScopeFor(App.Services, kmsa, out var context))
        {
            context.Add(ExternalIdentifier.ForPerson(person.Id, "UFA_PLAYER_NO", "UFA-PL-88"));
            await context.SaveChangesAsync(Ct);
        }

        (await registrar.ReadJsonAsync($"{VenuesPath}?q=UFA-PL-88", Ct)).VenueNames().ShouldBeEmpty();
    }

    [Fact]
    public async Task Text_without_a_letter_or_digit_finds_only_an_identifier()
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        await SaveVenueAsync(App.Services, kmsa, "Kiwatule Meadow Ground", identifier: (_facilityNo, "--"));
        await SaveVenueAsync(App.Services, kmsa, "Kisaasi Recreation Ground");

        (await registrar.ReadJsonAsync($"{VenuesPath}?q=--", Ct)).VenueNames().ShouldBe(["Kiwatule Meadow Ground"]);
    }

    // The four requests, in the order that succeeds on a venue with the alias and the identifier given.
    private static (string Path, object Body)[] Requests(Guid venueId, Guid aliasId, Guid identifierId) =>
    [
        (VenueAliasesPath(venueId), new { name = "Kiwatule Grounds", kind = "SPELLING" }),
        (VenueAliasPath(venueId, aliasId, "remove"), new { reason = "Misspelt" }),
        (VenueIdentifiersPath(venueId), new { authority = "UFA_GROUND_NO", value = "UFA-9" }),
        (VenueIdentifierPath(venueId, identifierId, "end"), new { }),
    ];

    private static async Task<Guid> AddIdentifierAsync(
        HttpClient client, Guid venueId, string value, string authority = _facilityNo, DateOnly? validFrom = null)
    {
        var venue = await OkAsync(await client.PostAsJsonAsync(VenueIdentifiersPath(venueId), new { authority, value, validFrom }, Ct));
        return venue.GetProperty("externalIdentifiers").EnumerateArray()
            .Single(i => i.GetProperty("authority").GetString() == authority && i.GetProperty("value").GetString() == value)
            .GetProperty("id").GetGuid();
    }

    // The venue answered, with its version as the ETag.
    private static async Task<JsonElement> OkAsync(HttpResponseMessage response)
    {
        using (response)
        {
            response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));
            var venue = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
            response.Headers.ETag.ShouldNotBeNull().Tag.ShouldBe($"\"{venue.GetProperty("version").GetUInt32()}\"");
            return venue;
        }
    }

    // The test tenants' zone is Africa/Kampala (UTC+3, no daylight saving).
    private DateOnly TenantToday() => DateOnly.FromDateTime(App.Clock.GetUtcNow().AddHours(3).UtcDateTime);

    /// <summary>An active tenant (time zone Africa/Kampala) and a client of a member with <paramref name="role"/> tenant-wide.</summary>
    private async Task<(Guid Tenant, HttpClient Client)> TenantAsync(string slug, string role)
    {
        App.Authorization.UseIdentityRoles();
        var tenant = await TenantTestData.SaveTenantAsync(App.Services, slug, TenantStatus.Active, Ct);
        var member = await CreateMemberAsync(App.Services, tenant, MailboxOf(role, $"{slug}.example"), Ct, TenantWide(role));
        return (tenant, App.ClientFor(member.UserId, tenant));
    }
}

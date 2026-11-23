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
using SportsPlatform.Modules.Registry.Domain.Organisations;
using SportsPlatform.Modules.Registry.Domain.People;
using SportsPlatform.Modules.Tenancy.Contracts.Tenants;
using static SportsPlatform.IntegrationTests.Identity.MembershipTestData;
using static SportsPlatform.IntegrationTests.Registry.OrganisationRequests;
using static SportsPlatform.IntegrationTests.Registry.PeopleRequests;
using static SportsPlatform.IntegrationTests.Registry.RegistryTestData;

namespace SportsPlatform.IntegrationTests.Registry;

/// <summary>
/// An organisation's aliases and external identifiers over HTTP (S07-T1c; D10, D11, D20, D21): adding, removing and ending them,
/// the exact identifier check, the dissolved and archived guards, the version, the audit, the roles and the read-only tenant.
/// </summary>
public sealed class OrganisationAliasesAndIdentifiersTests(PostgresFixture fixture) : DatabaseTest(fixture)
{
    private const string _clubNo = "KMSA_CLUB_NO";

    [Fact]
    public async Task An_added_alias_is_shown_and_searched_until_it_is_removed()
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var organisation = await SaveOrganisationAsync(App.Services, kmsa, "Ntinda Swifts SC");
        var before = (await registrar.ReadJsonAsync(OrganisationPath(organisation.Id), Ct)).GetProperty("version").GetUInt32();

        var added = await OkAsync(await registrar.PostAsJsonAsync(
            OrganisationAliasesPath(organisation.Id), new { name = "  Kampala  Swallows ", kind = "FORMER_NAME", version = before }, Ct));

        added.GetProperty("version").GetUInt32().ShouldNotBe(before);
        var alias = added.GetProperty("aliases").EnumerateArray().ShouldHaveSingleItem();
        (alias.GetProperty("name").GetString(), alias.GetProperty("kind").GetString()).ShouldBe(("Kampala Swallows", "FORMER_NAME"));
        (await registrar.ReadJsonAsync($"{OrganisationsPath}?q=swallows", Ct)).OrganisationNames().ShouldBe(["Ntinda Swifts SC"]);
        (await registrar.ReadJsonAsync($"{OrganisationsPath}?q=Kampla%20Swalows", Ct)).OrganisationNames().ShouldBe(["Ntinda Swifts SC"]);

        var aliasId = alias.GetProperty("id").GetGuid();
        var removed = await OkAsync(await registrar.PostAsJsonAsync(OrganisationAliasPath(organisation.Id, aliasId, "remove"), new { reason = " Never its name " }, Ct));

        removed.GetProperty("aliases").GetArrayLength().ShouldBe(0);
        (await registrar.ReadJsonAsync($"{OrganisationsPath}?q=swallows", Ct)).OrganisationNames().ShouldBeEmpty();
        await using (var scope = ScopeFor(App.Services, kmsa, out var context))
        {
            var kept = await context.Set<OrganisationAlias>().AsNoTracking().SingleAsync(Ct);
            (kept.RemoveReason, kept.RemovedAt).ShouldBe(("Never its name", App.Clock.GetUtcNow()));
        }

        await AccountTestData.DeliverOutboxAsync(App.Services, Ct);
        var entries = (await AuditTestData.EntriesAsync(App.Services, Ct)).Where(e => e.Outcome == AuditOutcome.Success).ToList();
        entries.Select(e => (e.ActionCode, e.EntityType, e.EntityId, e.Reason))
            .ShouldBe([("organisation.alias.add", "Organisation", organisation.Id, null), ("organisation.alias.remove", "Organisation", organisation.Id, "Never its name")], ignoreOrder: true);
        entries.ShouldAllBe(e => JsonDocument.Parse(e.Metadata!).RootElement.GetProperty("aliasId").GetGuid() == aliasId);
    }

    [Fact]
    public async Task An_alias_the_organisation_is_already_known_by_or_without_a_letter_is_refused()
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var organisation = await SaveOrganisationAsync(
            App.Services, kmsa, "Naguru Nightjars Club", legalName: "Naguru Nightjars Sports Club Limited", shortName: "NNC",
            alias: ("The Nightjars", AliasKind.Nickname));

        foreach (var name in new[] { "Club, Nightjars Naguru", "Limited: Naguru Nightjars Sports Club", "N.N.C.", "the NIGHTJARS" })
        {
            using var known = await registrar.PostAsJsonAsync(OrganisationAliasesPath(organisation.Id), new { name, kind = "SPELLING" }, Ct);
            var problem = await known.ShouldBeProblemAsync(HttpStatusCode.Conflict, "ORGANISATION_ALIAS_EXISTS");
            Fields(problem).ShouldBe([("name", "ORGANISATION_ALIAS_EXISTS")]);
        }

        using (var blank = await registrar.PostAsJsonAsync(OrganisationAliasesPath(organisation.Id), new { name = "--", kind = "SPELLING" }, Ct))
        {
            var problem = await blank.ShouldBeProblemAsync(HttpStatusCode.UnprocessableEntity, "ORGANISATION_NAME_INVALID");
            Fields(problem).ShouldBe([("name", "ORGANISATION_NAME_INVALID")]);
        }

        using var malformed = await registrar.PostAsJsonAsync(OrganisationAliasesPath(organisation.Id), new { name = new string('a', 121) }, Ct);
        var invalid = await malformed.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_INVALID_REQUEST");
        Fields(invalid).Select(field => field.Item1).ShouldBe(["name", "kind"], ignoreOrder: true);
    }

    [Fact]
    public async Task Removing_needs_a_reason_and_an_alias_of_this_organisation_not_yet_removed()
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var organisation = await SaveOrganisationAsync(App.Services, kmsa, "Naguru Nightjars Club", alias: ("The Nightjars", AliasKind.Nickname));
        var other = await SaveOrganisationAsync(App.Services, kmsa, "Kisaasi Kestrels SC");
        var read = await registrar.ReadJsonAsync(OrganisationPath(organisation.Id), Ct);
        var aliasId = read.GetProperty("aliases")[0].GetProperty("id").GetGuid();

        using (var noReason = await registrar.PostAsJsonAsync(OrganisationAliasPath(organisation.Id, aliasId, "remove"), new { reason = " " }, Ct))
        {
            await noReason.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_REASON_REQUIRED");
        }

        foreach (var (owner, id) in new[] { (other.Id, aliasId), (organisation.Id, Guid.CreateVersion7()) })
        {
            using var missing = await registrar.PostAsJsonAsync(OrganisationAliasPath(owner, id, "remove"), new { reason = "Misspelt" }, Ct);
            await missing.ShouldBeProblemAsync(HttpStatusCode.NotFound, "ORGANISATION_ALIAS_NOT_FOUND");
        }

        var removed = await OkAsync(await registrar.PostAsJsonAsync(OrganisationAliasPath(organisation.Id, aliasId, "remove"), new { reason = "Misspelt" }, Ct));
        removed.GetProperty("version").GetUInt32().ShouldNotBe(read.GetProperty("version").GetUInt32());
        using var again = await registrar.PostAsJsonAsync(OrganisationAliasPath(organisation.Id, aliasId, "remove"), new { reason = "Misspelt" }, Ct);
        await again.ShouldBeProblemAsync(HttpStatusCode.Conflict, "ORGANISATION_ALIAS_REMOVED");
    }

    [Fact]
    public async Task An_identifier_any_organisation_has_or_had_is_refused_but_a_persons_is_not()
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var ntinda = await SaveOrganisationAsync(App.Services, kmsa, "Ntinda Swifts SC");
        var kisaasi = await SaveOrganisationAsync(App.Services, kmsa, "Kisaasi Kestrels SC");
        var identifierId = await AddIdentifierAsync(registrar, ntinda.Id, "KMSA-0147");
        await OkAsync(await registrar.PostAsJsonAsync(OrganisationIdentifierPath(ntinda.Id, identifierId, "end"), new { }, Ct));

        foreach (var (organisation, value) in new[] { (kisaasi.Id, "KMSA-0147"), (kisaasi.Id, " KMSA-0147 "), (ntinda.Id, "KMSA-0147") })
        {
            using var taken = await registrar.PostAsJsonAsync(OrganisationIdentifiersPath(organisation), new { authority = _clubNo, value }, Ct);
            var problem = await taken.ShouldBeProblemAsync(HttpStatusCode.Conflict, "ORGANISATION_EXTERNAL_ID_EXISTS");
            Fields(problem).ShouldBe([("value", "ORGANISATION_EXTERNAL_ID_EXISTS")]);
        }

        // Another entity type: a person may hold the value an organisation had, and an organisation a person's.
        var person = await SavePersonAsync(App.Services, kmsa, IvanMukasa, Ct);
        using (var personal = await registrar.PostAsJsonAsync(
                   $"{PersonPath(person.Id)}/external-identifiers", new { authority = _clubNo, value = "KMSA-0147" }, Ct))
        {
            personal.StatusCode.ShouldBe(HttpStatusCode.OK, await personal.Content.ReadAsStringAsync(Ct));
        }

        await OkAsync(await registrar.PostAsJsonAsync(OrganisationIdentifiersPath(kisaasi.Id), new { authority = "KMSA_AFFILIATION_NO", value = "KMSA-0147" }, Ct));
    }

    [Fact]
    public async Task Adding_an_identifier_has_no_reserved_authority_and_moves_the_version()
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var organisation = await SaveOrganisationAsync(App.Services, kmsa, "Naguru Nightjars Club");
        var before = (await registrar.ReadJsonAsync(OrganisationPath(organisation.Id), Ct)).GetProperty("version").GetUInt32();

        using (var malformed = await registrar.PostAsJsonAsync(OrganisationIdentifiersPath(organisation.Id), new { authority = "kcca no", value = " " }, Ct))
        {
            var problem = await malformed.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_INVALID_REQUEST");
            Fields(problem).Select(field => field.Item1).ShouldBe(["authority", "value"], ignoreOrder: true);
        }

        var added = await OkAsync(await registrar.PostAsJsonAsync(
            OrganisationIdentifiersPath(organisation.Id), new { authority = _clubNo, value = " KMSA-0201 ", validFrom = "2026-01-10", version = before }, Ct));

        added.GetProperty("version").GetUInt32().ShouldNotBe(before);
        var identifier = added.GetProperty("externalIdentifiers").EnumerateArray().ShouldHaveSingleItem();
        (identifier.GetProperty("authority").GetString(), identifier.GetProperty("value").GetString(), identifier.GetProperty("validFrom").GetString())
            .ShouldBe((_clubNo, "KMSA-0201", "2026-01-10"));
        identifier.GetProperty("validTo").ValueKind.ShouldBe(JsonValueKind.Null);

        // The national-document authorities are a person rule only (D10).
        await OkAsync(await registrar.PostAsJsonAsync(OrganisationIdentifiersPath(organisation.Id), new { authority = "NIN", value = "LAND-77" }, Ct));
        using (var stale = await registrar.PostAsJsonAsync(OrganisationIdentifiersPath(organisation.Id), new { authority = "KMSA_AFFILIATION_NO", value = "AFF-9", version = before }, Ct))
        {
            await stale.ShouldBeProblemAsync(HttpStatusCode.Conflict, "ORGANISATION_CONCURRENCY_CONFLICT");
        }

        await AccountTestData.DeliverOutboxAsync(App.Services, Ct);
        var entry = (await AuditTestData.EntriesAsync(App.Services, Ct))
            .First(e => e.ActionCode == "organisation.external_id.add" && e.Outcome == AuditOutcome.Success && e.Metadata!.Contains(_clubNo, StringComparison.Ordinal));
        var metadata = JsonDocument.Parse(entry.Metadata!).RootElement;
        (metadata.GetProperty("identifierId").GetGuid(), metadata.GetProperty("authority").GetString())
            .ShouldBe((identifier.GetProperty("id").GetGuid(), _clubNo));
        (entry.EntityType, entry.EntityId).ShouldBe(("Organisation", organisation.Id));
    }

    [Fact]
    public async Task A_stale_If_Match_is_an_organisation_conflict_for_every_child_change()
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var organisation = await SaveOrganisationAsync(App.Services, kmsa, "Naguru Nightjars Club", alias: ("The Nightjars", AliasKind.Nickname), identifier: (_clubNo, "KMSA-0147"));
        var read = await registrar.ReadJsonAsync(OrganisationPath(organisation.Id), Ct);
        var aliasId = read.GetProperty("aliases")[0].GetProperty("id").GetGuid();
        var identifierId = read.GetProperty("externalIdentifiers")[0].GetProperty("id").GetGuid();
        var stale = $"\"{read.GetProperty("version").GetUInt32() + 1}\"";

        foreach (var (path, body) in Requests(organisation.Id, aliasId, identifierId))
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
            request.Headers.TryAddWithoutValidation("If-Match", stale);
            using var response = await registrar.SendAsync(request, Ct);
            await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, "ORGANISATION_CONCURRENCY_CONFLICT");
        }
    }

    // The outcome only: the organisations lock refuses the second add before its insert, and without the lock the unique index
    // would refuse it on save with the same answer, so this doesn't prove the lock is taken.
    [Fact]
    public async Task Two_organisations_given_one_value_at_once_leave_one_refused()
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var ntinda = await SaveOrganisationAsync(App.Services, kmsa, "Ntinda Swifts SC");
        var kisaasi = await SaveOrganisationAsync(App.Services, kmsa, "Kisaasi Kestrels SC");

        var responses = await Task.WhenAll(
            registrar.PostAsJsonAsync(OrganisationIdentifiersPath(ntinda.Id), new { authority = _clubNo, value = "KMSA-0388" }, Ct),
            registrar.PostAsJsonAsync(OrganisationIdentifiersPath(kisaasi.Id), new { authority = _clubNo, value = "KMSA-0388" }, Ct));

        responses.Select(response => response.StatusCode).ShouldBe([HttpStatusCode.OK, HttpStatusCode.Conflict], ignoreOrder: true);
        await responses.Single(response => response.StatusCode == HttpStatusCode.Conflict)
            .ShouldBeProblemAsync(HttpStatusCode.Conflict, "ORGANISATION_EXTERNAL_ID_EXISTS");
        foreach (var response in responses)
        {
            response.Dispose();
        }
    }

    [Fact]
    public async Task Ending_an_identifier_sets_a_date_from_its_start_to_today_once()
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var organisation = await SaveOrganisationAsync(App.Services, kmsa, "Naguru Nightjars Club");
        var other = await SaveOrganisationAsync(App.Services, kmsa, "Kisaasi Kestrels SC");
        var today = TenantToday();
        var clubNo = await AddIdentifierAsync(registrar, organisation.Id, "KMSA-0147", validFrom: today.AddDays(-30));
        var affiliationNo = await AddIdentifierAsync(registrar, organisation.Id, "AFF-9", authority: "KMSA_AFFILIATION_NO");

        foreach (var validTo in new[] { today.AddDays(1), today.AddDays(-31) })
        {
            using var invalid = await registrar.PostAsJsonAsync(OrganisationIdentifierPath(organisation.Id, clubNo, "end"), new { validTo }, Ct);
            var problem = await invalid.ShouldBeProblemAsync(HttpStatusCode.UnprocessableEntity, "ORGANISATION_EXTERNAL_ID_VALIDITY_INVALID");
            Fields(problem).ShouldBe([("validTo", "ORGANISATION_EXTERNAL_ID_VALIDITY_INVALID")]);
        }

        foreach (var (owner, id) in new[] { (other.Id, clubNo), (organisation.Id, Guid.CreateVersion7()) })
        {
            using var missing = await registrar.PostAsync(OrganisationIdentifierPath(owner, id, "end"), null, Ct);
            await missing.ShouldBeProblemAsync(HttpStatusCode.NotFound, "ORGANISATION_EXTERNAL_ID_NOT_FOUND");
        }

        var before = (await registrar.ReadJsonAsync(OrganisationPath(organisation.Id), Ct)).GetProperty("version").GetUInt32();
        var first = await OkAsync(await registrar.PostAsJsonAsync(
            OrganisationIdentifierPath(organisation.Id, clubNo, "end"), new { validTo = today.AddDays(-30), reason = "Renumbered by the association" }, Ct));
        first.GetProperty("version").GetUInt32().ShouldNotBe(before);
        var ended = await OkAsync(await registrar.PostAsync(OrganisationIdentifierPath(organisation.Id, affiliationNo, "end"), null, Ct));

        ended.GetProperty("externalIdentifiers").EnumerateArray()
            .Select(i => (i.GetProperty("id").GetGuid(), i.GetProperty("validTo").GetString()))
            .ShouldBe([
                (affiliationNo, today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
                (clubNo, today.AddDays(-30).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
            ]);
        using var again = await registrar.PostAsync(OrganisationIdentifierPath(organisation.Id, affiliationNo, "end"), null, Ct);
        await again.ShouldBeProblemAsync(HttpStatusCode.Conflict, "ORGANISATION_EXTERNAL_ID_ENDED");
        await AccountTestData.DeliverOutboxAsync(App.Services, Ct);
        (await AuditTestData.EntriesAsync(App.Services, Ct))
            .Where(e => e.ActionCode == "organisation.external_id.end" && e.Outcome == AuditOutcome.Success)
            .Select(e => e.Reason).ShouldBe(["Renumbered by the association", null], ignoreOrder: true);
    }

    // D10: a dissolved or archived organisation takes no new alias or identifier; removing and ending stay allowed.
    [Theory]
    [InlineData(OrganisationStatus.Active, null)]
    [InlineData(OrganisationStatus.Inactive, null)]
    [InlineData(OrganisationStatus.Suspended, null)]
    [InlineData(OrganisationStatus.Dissolved, "ORGANISATION_DISSOLVED")]
    [InlineData(OrganisationStatus.Archived, "ORGANISATION_ARCHIVED")]
    public async Task Only_an_organisation_that_isnt_dissolved_or_archived_takes_new_aliases_and_identifiers(OrganisationStatus status, string? refusal)
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var organisation = await SaveOrganisationAsync(
            App.Services, kmsa, "Naguru Nightjars Club", status: status, alias: ("The Nightjars", AliasKind.Nickname), identifier: (_clubNo, "KMSA-0147"));
        var read = await registrar.ReadJsonAsync(OrganisationPath(organisation.Id), Ct);
        var aliasId = read.GetProperty("aliases")[0].GetProperty("id").GetGuid();
        var identifierId = read.GetProperty("externalIdentifiers")[0].GetProperty("id").GetGuid();

        foreach (var (path, body) in new (string, object)[]
        {
            (OrganisationAliasesPath(organisation.Id), new { name = "Naguru Night Jars", kind = "SPELLING" }),
            (OrganisationIdentifiersPath(organisation.Id), new { authority = "KMSA_AFFILIATION_NO", value = "AFF-9" }),
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

        await OkAsync(await registrar.PostAsJsonAsync(OrganisationAliasPath(organisation.Id, aliasId, "remove"), new { reason = "Misspelt" }, Ct));
        await OkAsync(await registrar.PostAsync(OrganisationIdentifierPath(organisation.Id, identifierId, "end"), null, Ct));
    }

    [Fact]
    public async Task Another_tenants_organisation_is_not_found()
    {
        var (kmsa, _) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var (elssc, elsscOwner) = await TenantAsync("elssc", IdentityRoles.TenantOwner);
        var organisation = await SaveOrganisationAsync(App.Services, kmsa, "Naguru Nightjars Club", alias: ("The Nightjars", AliasKind.Nickname), identifier: (_clubNo, "KMSA-0147"));

        foreach (var (path, body) in Requests(organisation.Id, Guid.CreateVersion7(), Guid.CreateVersion7()))
        {
            using var response = await elsscOwner.PostAsJsonAsync(path, body, Ct);
            await response.ShouldBeProblemAsync(HttpStatusCode.NotFound, "ORGANISATION_NOT_FOUND");
        }

        // Another tenant's identifier value is not taken here.
        var own = await SaveOrganisationAsync(App.Services, elssc, "Lakeshore High School");
        await OkAsync(await elsscOwner.PostAsJsonAsync(OrganisationIdentifiersPath(own.Id), new { authority = _clubNo, value = "KMSA-0147" }, Ct));
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
    public async Task Only_those_who_change_organisations_may_change_aliases_and_identifiers(string role, bool allowed)
    {
        var (kmsa, client) = await TenantAsync("kmsa", role);
        var organisation = await SaveOrganisationAsync(App.Services, kmsa, "Naguru Nightjars Club", alias: ("The Nightjars", AliasKind.Nickname), identifier: (_clubNo, "KMSA-0147"));
        Guid aliasId, identifierId;
        await using (var scope = ScopeFor(App.Services, kmsa, out var context))
        {
            aliasId = await context.Set<OrganisationAlias>().Select(a => a.Id).SingleAsync(Ct);
            identifierId = await context.Set<ExternalIdentifier>().Select(i => i.Id).SingleAsync(Ct);
        }

        foreach (var (path, body) in Requests(organisation.Id, aliasId, identifierId))
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
        var organisation = await SaveOrganisationAsync(App.Services, kmsa, "Naguru Nightjars Club");
        var member = await CreateMemberAsync(App.Services, kmsa, "brian.ssemakula@kmsa.example", Ct, TenantWide(IdentityRoles.Registrar));
        var registrar = App.ClientFor(member.UserId, kmsa);

        foreach (var (path, body) in Requests(organisation.Id, Guid.CreateVersion7(), Guid.CreateVersion7()))
        {
            using var response = await registrar.PostAsJsonAsync(path, body, Ct);
            await response.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "TENANT_READ_ONLY");
        }
    }

    [Theory]
    [InlineData("KMSA-0147")]
    [InlineData("0147")]
    public async Task The_directory_finds_an_organisation_by_an_identifiers_exact_value_ended_ones_included(string q)
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var organisation = await SaveOrganisationAsync(App.Services, kmsa, "Naguru Nightjars Club", identifier: (_clubNo, q));
        await SaveOrganisationAsync(App.Services, kmsa, "Kisaasi Kestrels SC", identifier: (_clubNo, $"{q}-B"));
        var identifierId = (await registrar.ReadJsonAsync(OrganisationPath(organisation.Id), Ct)).GetProperty("externalIdentifiers")[0].GetProperty("id").GetGuid();
        await OkAsync(await registrar.PostAsync(OrganisationIdentifierPath(organisation.Id, identifierId, "end"), null, Ct));

        (await registrar.ReadJsonAsync($"{OrganisationsPath}?q={Uri.EscapeDataString(q)}", Ct)).OrganisationNames().ShouldBe(["Naguru Nightjars Club"]);

        // A person's identifier with the same value finds no organisation.
        var person = await SavePersonAsync(App.Services, kmsa, IvanMukasa, Ct);
        await using (var scope = ScopeFor(App.Services, kmsa, out var context))
        {
            context.Add(ExternalIdentifier.ForPerson(person.Id, "UFA_PLAYER_NO", "UFA-PL-88"));
            await context.SaveChangesAsync(Ct);
        }

        (await registrar.ReadJsonAsync($"{OrganisationsPath}?q=UFA-PL-88", Ct)).OrganisationNames().ShouldBeEmpty();
    }

    [Fact]
    public async Task Text_without_a_letter_or_digit_finds_only_an_identifier()
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        await SaveOrganisationAsync(App.Services, kmsa, "Naguru Nightjars Club", identifier: (_clubNo, "--"));
        await SaveOrganisationAsync(App.Services, kmsa, "Kisaasi Kestrels SC");

        (await registrar.ReadJsonAsync($"{OrganisationsPath}?q=--", Ct)).OrganisationNames().ShouldBe(["Naguru Nightjars Club"]);
    }

    // The four requests, in the order that succeeds on an organisation with the alias and the identifier given.
    private static (string Path, object Body)[] Requests(Guid organisationId, Guid aliasId, Guid identifierId) =>
    [
        (OrganisationAliasesPath(organisationId), new { name = "Naguru Night Jars", kind = "SPELLING" }),
        (OrganisationAliasPath(organisationId, aliasId, "remove"), new { reason = "Misspelt" }),
        (OrganisationIdentifiersPath(organisationId), new { authority = "KMSA_AFFILIATION_NO", value = "AFF-9" }),
        (OrganisationIdentifierPath(organisationId, identifierId, "end"), new { }),
    ];

    private static async Task<Guid> AddIdentifierAsync(
        HttpClient client, Guid organisationId, string value, string authority = _clubNo, DateOnly? validFrom = null)
    {
        var organisation = await OkAsync(await client.PostAsJsonAsync(OrganisationIdentifiersPath(organisationId), new { authority, value, validFrom }, Ct));
        return organisation.GetProperty("externalIdentifiers").EnumerateArray()
            .Single(i => i.GetProperty("authority").GetString() == authority && i.GetProperty("value").GetString() == value)
            .GetProperty("id").GetGuid();
    }

    // The organisation answered, with its version as the ETag.
    private static async Task<JsonElement> OkAsync(HttpResponseMessage response)
    {
        using (response)
        {
            response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));
            var organisation = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
            response.Headers.ETag.ShouldNotBeNull().Tag.ShouldBe($"\"{organisation.GetProperty("version").GetUInt32()}\"");
            return organisation;
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

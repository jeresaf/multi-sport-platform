using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Auditing;
using SportsPlatform.IntegrationTests.Api;
using SportsPlatform.IntegrationTests.Audit;
using SportsPlatform.IntegrationTests.Identity;
using SportsPlatform.IntegrationTests.Infrastructure;
using SportsPlatform.IntegrationTests.Tenancy;
using SportsPlatform.Modules.Identity.Domain.Roles;
using SportsPlatform.Modules.Registry.Domain.People;
using SportsPlatform.Modules.Tenancy.Contracts.Tenants;
using static SportsPlatform.IntegrationTests.Identity.MembershipTestData;
using static SportsPlatform.IntegrationTests.Registry.PeopleRequests;
using static SportsPlatform.IntegrationTests.Registry.RegistryTestData;

namespace SportsPlatform.IntegrationTests.Registry;

/// <summary>
/// Aliases and external identifiers over HTTP (S06-T3b; D9, D10, D11): adding, removing and ending them, the exact
/// identifier check, the archived and deceased guards, the version, the audit, the roles and the read-only tenant.
/// </summary>
public sealed class PersonAliasesAndIdentifiersTests(PostgresFixture fixture) : DatabaseTest(fixture)
{
    private const string _regNo = "KMSA_REG_NO";

    [Fact]
    public async Task An_added_alias_is_shown_searched_and_matched_until_it_is_removed()
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var person = await CreatePersonAsync(App.Services, kmsa, SystemActor.Instance, DenisOpoka, overrideReason: null, Ct);
        var before = (await registrar.ReadJsonAsync(PersonPath(person), Ct)).GetProperty("version").GetUInt32();

        var added = await OkAsync(await registrar.PostAsJsonAsync(AliasesPath(person), new { name = " Deno  Opoka ", kind = "FORMER_NAME", version = before }, Ct));

        var alias = added.GetProperty("aliases").EnumerateArray().ShouldHaveSingleItem();
        alias.GetProperty("name").GetString().ShouldBe("Deno Opoka");
        alias.GetProperty("kind").GetString().ShouldBe("FORMER_NAME");
        added.GetProperty("version").GetUInt32().ShouldNotBe(before);
        var aliasId = alias.GetProperty("id").GetGuid();
        (await registrar.ReadJsonAsync($"{PeoplePath}?q=Deno", Ct)).Names().ShouldBe(["Denis Opoka"]);
        var check = await CheckAsync(registrar, new { firstName = "Deno", lastName = "Opoka" });
        check.GetProperty("candidates").EnumerateArray().ShouldHaveSingleItem().GetProperty("evidence").EnumerateArray()
            .Select(evidence => evidence.GetProperty("code").GetString()).ShouldContain("ALIAS_MATCH");

        var removed = await OkAsync(await registrar.PostAsJsonAsync(AliasPath(person, aliasId, "remove"), new { reason = " Entered twice " }, Ct));

        removed.GetProperty("aliases").GetArrayLength().ShouldBe(0);
        removed.GetProperty("version").GetUInt32().ShouldNotBe(added.GetProperty("version").GetUInt32());
        (await registrar.ReadJsonAsync($"{PeoplePath}?q=Deno", Ct)).Names().ShouldBeEmpty();
        (await CheckAsync(registrar, new { firstName = "Deno", lastName = "Opoka" })).GetProperty("candidates").GetArrayLength().ShouldBe(0);
        await using (var scope = ScopeFor(App.Services, kmsa, out var context))
        {
            var row = await context.Set<PersonAlias>().SingleAsync(a => a.Id == aliasId, Ct);
            (row.IsRemoved, row.RemoveReason, row.RemovedAt).ShouldBe((true, "Entered twice", App.Clock.GetUtcNow()));
            row.RemovedBy.ShouldNotBeNull().ShouldNotBe(Guid.Empty);
        }

        await AccountTestData.DeliverOutboxAsync(App.Services, Ct);
        var entries = await AuditTestData.EntriesAsync(App.Services, Ct);
        var add = entries.Single(e => e.ActionCode == "person.alias.add");
        (add.Outcome, add.EntityType, add.EntityId).ShouldBe((AuditOutcome.Success, "Person", (Guid?)person));
        JsonDocument.Parse(add.Metadata!).RootElement.GetProperty("aliasId").GetGuid().ShouldBe(aliasId);
        add.Metadata!.ShouldNotContain("Deno");
        var remove = entries.Single(e => e.ActionCode == "person.alias.remove");
        (remove.Outcome, remove.Reason).ShouldBe((AuditOutcome.Success, "Entered twice"));
    }

    [Fact]
    public async Task An_alias_the_person_is_already_known_by_or_without_a_letter_is_refused()
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var person = await CreatePersonAsync(App.Services, kmsa, SystemActor.Instance, IvanMukasa, overrideReason: null, Ct);
        var aliasId = (await OkAsync(await registrar.PostAsJsonAsync(AliasesPath(person), new { name = "Kasa", kind = "NICKNAME" }, Ct)))
            .GetProperty("aliases").EnumerateArray().Single().GetProperty("id").GetGuid();

        foreach (var name in new[] { "Mukasa Ivan", "IVAN mukasa.", "kása" })
        {
            using var exists = await registrar.PostAsJsonAsync(AliasesPath(person), new { name, kind = "SPELLING" }, Ct);
            var problem = await exists.ShouldBeProblemAsync(HttpStatusCode.Conflict, "PERSON_ALIAS_EXISTS");
            Fields(problem).ShouldBe([("name", "PERSON_ALIAS_EXISTS")]);
        }

        using (var noLetter = await registrar.PostAsJsonAsync(AliasesPath(person), new { name = "--", kind = "NICKNAME" }, Ct))
        {
            var problem = await noLetter.ShouldBeProblemAsync(HttpStatusCode.UnprocessableEntity, "PERSON_NAME_INVALID");
            Fields(problem).ShouldBe([("name", "PERSON_NAME_INVALID")]);
        }

        using (var noKind = await registrar.PostAsJsonAsync(AliasesPath(person), new { name = "Ivo" }, Ct))
        {
            var problem = await noKind.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_INVALID_REQUEST");
            Fields(problem).Select(field => field.Item1).ShouldBe(["kind"]);
        }

        // Once removed, the name may come back.
        await OkAsync(await registrar.PostAsJsonAsync(AliasPath(person, aliasId, "remove"), new { reason = "Misspelt" }, Ct));
        await OkAsync(await registrar.PostAsJsonAsync(AliasesPath(person), new { name = "Kasa", kind = "NICKNAME" }, Ct));
    }

    [Fact]
    public async Task Removing_needs_a_reason_and_an_alias_of_this_person_not_yet_removed()
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var person = await CreatePersonAsync(App.Services, kmsa, SystemActor.Instance, IvanMukasa, overrideReason: null, Ct);
        var other = await CreatePersonAsync(App.Services, kmsa, SystemActor.Instance, DenisOpoka, overrideReason: null, Ct);
        var aliasId = (await OkAsync(await registrar.PostAsJsonAsync(AliasesPath(person), new { name = "Kasa", kind = "NICKNAME" }, Ct)))
            .GetProperty("aliases").EnumerateArray().Single().GetProperty("id").GetGuid();

        using (var blank = await registrar.PostAsJsonAsync(AliasPath(person, aliasId, "remove"), new { reason = " " }, Ct))
        {
            await blank.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_REASON_REQUIRED");
        }

        using (var noBody = await registrar.PostAsync(AliasPath(person, aliasId, "remove"), null, Ct))
        {
            await noBody.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_MALFORMED_REQUEST");
        }

        foreach (var (owner, id) in new[] { (other, aliasId), (person, Guid.CreateVersion7()) })
        {
            using var missing = await registrar.PostAsJsonAsync(AliasPath(owner, id, "remove"), new { reason = "Misspelt" }, Ct);
            await missing.ShouldBeProblemAsync(HttpStatusCode.NotFound, "PERSON_ALIAS_NOT_FOUND");
        }

        await OkAsync(await registrar.PostAsJsonAsync(AliasPath(person, aliasId, "remove"), new { reason = "Misspelt" }, Ct));
        using var again = await registrar.PostAsJsonAsync(AliasPath(person, aliasId, "remove"), new { reason = "Misspelt" }, Ct);
        await again.ShouldBeProblemAsync(HttpStatusCode.Conflict, "PERSON_ALIAS_REMOVED");
    }

    // T3 acceptance: an identifier already used by another person, even ended, is refused with PERSON_EXTERNAL_ID_EXISTS.
    [Fact]
    public async Task An_identifier_any_person_has_or_had_is_refused()
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var mariam = await CreatePersonAsync(App.Services, kmsa, SystemActor.Instance, MariamNankya, overrideReason: null, Ct);
        var shamim = await CreatePersonAsync(App.Services, kmsa, SystemActor.Instance, ShamimNankya, overrideReason: "Twin sister, different first name", Ct);
        var identifierId = await AddIdentifierAsync(registrar, mariam, "KM-2026-0147");
        await OkAsync(await registrar.PostAsJsonAsync(IdentifierPath(mariam, identifierId, "end"), new { }, Ct));

        foreach (var (person, value) in new[] { (shamim, "KM-2026-0147"), (shamim, " KM-2026-0147 "), (mariam, "KM-2026-0147") })
        {
            using var taken = await registrar.PostAsJsonAsync(IdentifiersPath(person), new { authority = _regNo, value }, Ct);
            var problem = await taken.ShouldBeProblemAsync(HttpStatusCode.Conflict, "PERSON_EXTERNAL_ID_EXISTS");
            Fields(problem).ShouldBe([("value", "PERSON_EXTERNAL_ID_EXISTS")]);
        }

        // The same value from another authority is another identifier.
        await OkAsync(await registrar.PostAsJsonAsync(IdentifiersPath(shamim), new { authority = "SCHOOL_ADMISSION_NO", value = "KM-2026-0147" }, Ct));
        await AccountTestData.DeliverOutboxAsync(App.Services, Ct);
        var refused = (await AuditTestData.EntriesAsync(App.Services, Ct))
            .Where(e => e.ActionCode == "person.external_id.add" && e.Outcome != AuditOutcome.Success).ToList();
        refused.Count.ShouldBe(3);
        refused.ShouldAllBe(e => e.ErrorCode == "PERSON_EXTERNAL_ID_EXISTS");
    }

    [Fact]
    public async Task Adding_an_identifier_checks_its_authority_and_moves_the_version()
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var person = await CreatePersonAsync(App.Services, kmsa, SystemActor.Instance, IvanMukasa, overrideReason: null, Ct);
        var before = (await registrar.ReadJsonAsync(PersonPath(person), Ct)).GetProperty("version").GetUInt32();

        using (var reserved = await registrar.PostAsJsonAsync(IdentifiersPath(person), new { authority = "NIN", value = "CM9001" }, Ct))
        {
            var problem = await reserved.ShouldBeProblemAsync(HttpStatusCode.UnprocessableEntity, "PERSON_EXTERNAL_ID_AUTHORITY_RESERVED");
            Fields(problem).ShouldBe([("authority", "PERSON_EXTERNAL_ID_AUTHORITY_RESERVED")]);
        }

        using (var malformed = await registrar.PostAsJsonAsync(IdentifiersPath(person), new { authority = "kmsa reg", value = " " }, Ct))
        {
            var problem = await malformed.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_INVALID_REQUEST");
            Fields(problem).Select(field => field.Item1).ShouldBe(["authority", "value"], ignoreOrder: true);
        }

        var added = await OkAsync(await registrar.PostAsJsonAsync(
            IdentifiersPath(person), new { authority = _regNo, value = " KM-2026-0201 ", validFrom = "2026-01-10", version = before }, Ct));

        added.GetProperty("version").GetUInt32().ShouldNotBe(before);
        var identifier = added.GetProperty("externalIdentifiers").EnumerateArray().ShouldHaveSingleItem();
        (identifier.GetProperty("authority").GetString(), identifier.GetProperty("value").GetString(), identifier.GetProperty("validFrom").GetString())
            .ShouldBe((_regNo, "KM-2026-0201", "2026-01-10"));
        identifier.GetProperty("validTo").ValueKind.ShouldBe(JsonValueKind.Null);
        using (var stale = await registrar.PostAsJsonAsync(IdentifiersPath(person), new { authority = "SCHOOL_ADMISSION_NO", value = "NHS/0932", version = before }, Ct))
        {
            await stale.ShouldBeProblemAsync(HttpStatusCode.Conflict, "PERSON_CONCURRENCY_CONFLICT");
        }

        await AccountTestData.DeliverOutboxAsync(App.Services, Ct);
        var entry = (await AuditTestData.EntriesAsync(App.Services, Ct)).Single(e => e.ActionCode == "person.external_id.add" && e.Outcome == AuditOutcome.Success);
        var metadata = JsonDocument.Parse(entry.Metadata!).RootElement;
        (metadata.GetProperty("identifierId").GetGuid(), metadata.GetProperty("authority").GetString())
            .ShouldBe((identifier.GetProperty("id").GetGuid(), _regNo));
        entry.Metadata!.ShouldNotContain("KM-2026-0201");
    }

    // The outcome only: the people lock refuses the second add before its insert, and without the lock the unique index
    // would refuse it on save with the same answer (review #100 N1), so this doesn't prove the lock is taken.
    [Fact]
    public async Task Two_people_given_one_value_at_once_leave_one_refused()
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var mariam = await CreatePersonAsync(App.Services, kmsa, SystemActor.Instance, MariamNankya, overrideReason: null, Ct);
        var shamim = await CreatePersonAsync(App.Services, kmsa, SystemActor.Instance, ShamimNankya, overrideReason: "Twin sister, different first name", Ct);

        var responses = await Task.WhenAll(
            registrar.PostAsJsonAsync(IdentifiersPath(mariam), new { authority = _regNo, value = "KM-2026-0388" }, Ct),
            registrar.PostAsJsonAsync(IdentifiersPath(shamim), new { authority = _regNo, value = "KM-2026-0388" }, Ct));

        responses.Select(response => response.StatusCode).ShouldBe([HttpStatusCode.OK, HttpStatusCode.Conflict], ignoreOrder: true);
        await responses.Single(response => response.StatusCode == HttpStatusCode.Conflict)
            .ShouldBeProblemAsync(HttpStatusCode.Conflict, "PERSON_EXTERNAL_ID_EXISTS");
        foreach (var response in responses)
        {
            response.Dispose();
        }
    }

    [Fact]
    public async Task Ending_an_identifier_sets_a_date_from_its_start_to_today_once()
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var person = await CreatePersonAsync(App.Services, kmsa, SystemActor.Instance, IvanMukasa, overrideReason: null, Ct);
        var other = await CreatePersonAsync(App.Services, kmsa, SystemActor.Instance, DenisOpoka, overrideReason: null, Ct);
        var today = TenantToday();
        var regNo = await AddIdentifierAsync(registrar, person, "KM-2026-0147", validFrom: today.AddDays(-30));
        var admission = await AddIdentifierAsync(registrar, person, "NHS/0932", authority: "SCHOOL_ADMISSION_NO");

        foreach (var validTo in new[] { today.AddDays(1), today.AddDays(-31) })
        {
            using var invalid = await registrar.PostAsJsonAsync(IdentifierPath(person, regNo, "end"), new { validTo }, Ct);
            var problem = await invalid.ShouldBeProblemAsync(HttpStatusCode.UnprocessableEntity, "PERSON_EXTERNAL_ID_VALIDITY_INVALID");
            Fields(problem).ShouldBe([("validTo", "PERSON_EXTERNAL_ID_VALIDITY_INVALID")]);
        }

        foreach (var (owner, id) in new[] { (other, regNo), (person, Guid.CreateVersion7()) })
        {
            using var missing = await registrar.PostAsync(IdentifierPath(owner, id, "end"), null, Ct);
            await missing.ShouldBeProblemAsync(HttpStatusCode.NotFound, "PERSON_EXTERNAL_ID_NOT_FOUND");
        }

        await OkAsync(await registrar.PostAsJsonAsync(IdentifierPath(person, regNo, "end"), new { validTo = today.AddDays(-30), reason = "Replaced by a new number" }, Ct));
        var ended = await OkAsync(await registrar.PostAsync(IdentifierPath(person, admission, "end"), null, Ct));

        ended.GetProperty("externalIdentifiers").EnumerateArray()
            .Select(i => (i.GetProperty("id").GetGuid(), i.GetProperty("validTo").GetString()))
            .ShouldBe([(regNo, today.AddDays(-30).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)), (admission, today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))], ignoreOrder: true);
        using var again = await registrar.PostAsync(IdentifierPath(person, admission, "end"), null, Ct);
        await again.ShouldBeProblemAsync(HttpStatusCode.Conflict, "PERSON_EXTERNAL_ID_ENDED");
        await AccountTestData.DeliverOutboxAsync(App.Services, Ct);
        (await AuditTestData.EntriesAsync(App.Services, Ct))
            .Where(e => e.ActionCode == "person.external_id.end" && e.Outcome == AuditOutcome.Success)
            .Select(e => e.Reason).ShouldBe(["Replaced by a new number", null], ignoreOrder: true);
    }

    // D9: an archived or deceased person takes no new alias or identifier; corrections stay allowed (owner, Q45).
    [Theory]
    [InlineData(PersonStatus.Active, null)]
    [InlineData(PersonStatus.Inactive, null)]
    [InlineData(PersonStatus.Archived, "PERSON_ARCHIVED")]
    [InlineData(PersonStatus.Deceased, "PERSON_DECEASED")]
    public async Task Only_a_living_unarchived_person_takes_new_aliases_and_identifiers(PersonStatus status, string? refusal)
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var person = await CreatePersonAsync(App.Services, kmsa, SystemActor.Instance, IvanMukasa, overrideReason: null, Ct);
        var aliasId = (await OkAsync(await registrar.PostAsJsonAsync(AliasesPath(person), new { name = "Kasa", kind = "NICKNAME" }, Ct)))
            .GetProperty("aliases").EnumerateArray().Single().GetProperty("id").GetGuid();
        var identifierId = await AddIdentifierAsync(registrar, person, "KM-2026-0147");
        PersonTransition? transition = status switch
        {
            PersonStatus.Inactive => PersonTransition.Deactivate,
            PersonStatus.Archived => PersonTransition.Archive,
            PersonStatus.Deceased => PersonTransition.MarkDeceased,
            _ => null,
        };
        if (transition is { } t)
        {
            await ChangeStatusAsync(App.Services, kmsa, person, t, Ct);
        }

        using var alias = await registrar.PostAsJsonAsync(AliasesPath(person), new { name = "Ivo Mukasa", kind = "SPELLING" }, Ct);
        using var identifier = await registrar.PostAsJsonAsync(IdentifiersPath(person), new { authority = "SCHOOL_ADMISSION_NO", value = "NHS/0932" }, Ct);

        if (refusal is null)
        {
            alias.StatusCode.ShouldBe(HttpStatusCode.OK, await alias.Content.ReadAsStringAsync(Ct));
            identifier.StatusCode.ShouldBe(HttpStatusCode.OK, await identifier.Content.ReadAsStringAsync(Ct));
        }
        else
        {
            await alias.ShouldBeProblemAsync(HttpStatusCode.Conflict, refusal);
            await identifier.ShouldBeProblemAsync(HttpStatusCode.Conflict, refusal);
        }

        await OkAsync(await registrar.PostAsJsonAsync(AliasPath(person, aliasId, "remove"), new { reason = "Misspelt" }, Ct));
        await OkAsync(await registrar.PostAsync(IdentifierPath(person, identifierId, "end"), null, Ct));
    }

    [Fact]
    public async Task Another_tenants_person_is_not_found()
    {
        var (_, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var (elssc, theirRegistrar) = await TenantAsync("elssc", IdentityRoles.Registrar);
        var theirs = await CreatePersonAsync(App.Services, elssc, SystemActor.Instance, IvanMukasa, overrideReason: null, Ct);
        var identifierId = await AddIdentifierAsync(theirRegistrar, theirs, "EL/2019/044", authority: "SCHOOL_ADMISSION_NO");

        foreach (var (path, body) in Requests(theirs, Guid.CreateVersion7(), identifierId))
        {
            using var response = await registrar.PostAsJsonAsync(path, body, Ct);
            await response.ShouldBeProblemAsync(HttpStatusCode.NotFound, "PERSON_NOT_FOUND");
        }
    }

    // D21: aliases and identifiers are changed by the owner, the admin and the registrar.
    [Theory]
    [InlineData(IdentityRoles.TenantOwner, true)]
    [InlineData(IdentityRoles.TenantAdmin, true)]
    [InlineData(IdentityRoles.Registrar, true)]
    [InlineData(IdentityRoles.CompetitionManager, false)]
    [InlineData(IdentityRoles.ResultsOfficer, false)]
    [InlineData(IdentityRoles.DataEntry, false)]
    [InlineData(IdentityRoles.Viewer, false)]
    [InlineData(IdentityRoles.Scorer, false)]
    public async Task Only_those_who_change_people_may_change_aliases_and_identifiers(string role, bool allowed)
    {
        var (kmsa, client) = await TenantAsync("kmsa", role);
        var person = await SavePersonAsync(App.Services, kmsa, IvanMukasa, ("Kasa", AliasKind.Nickname), (_regNo, "KM-2026-0147"), Ct);
        Guid aliasId, identifierId;
        await using (var scope = ScopeFor(App.Services, kmsa, out var context))
        {
            aliasId = await context.Set<PersonAlias>().Select(a => a.Id).SingleAsync(Ct);
            identifierId = await context.Set<ExternalIdentifier>().Select(i => i.Id).SingleAsync(Ct);
        }

        foreach (var (path, body) in Requests(person.Id, aliasId, identifierId))
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
        var person = await SavePersonAsync(App.Services, kmsa, IvanMukasa, Ct);
        var member = await CreateMemberAsync(App.Services, kmsa, "brian.ssemakula@kmsa.example", Ct, TenantWide(IdentityRoles.Registrar));
        var registrar = App.ClientFor(member.UserId, kmsa);

        foreach (var (path, body) in Requests(person.Id, Guid.CreateVersion7(), Guid.CreateVersion7()))
        {
            using var response = await registrar.PostAsJsonAsync(path, body, Ct);
            await response.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "TENANT_READ_ONLY");
        }
    }

    // The four requests, in the order that succeeds on a person with the alias and the identifier given.
    private static (string Path, object Body)[] Requests(Guid personId, Guid aliasId, Guid identifierId) =>
    [
        (AliasesPath(personId), new { name = "Ivo Mukasa", kind = "SPELLING" }),
        (AliasPath(personId, aliasId, "remove"), new { reason = "Misspelt" }),
        (IdentifiersPath(personId), new { authority = "SCHOOL_ADMISSION_NO", value = "NHS/0932" }),
        (IdentifierPath(personId, identifierId, "end"), new { }),
    ];

    private static string AliasesPath(Guid personId) => $"{PersonPath(personId)}/aliases";

    private static string AliasPath(Guid personId, Guid aliasId, string command) => $"{PersonPath(personId)}/aliases/{aliasId}/{command}";

    private static string IdentifiersPath(Guid personId) => $"{PersonPath(personId)}/external-identifiers";

    private static string IdentifierPath(Guid personId, Guid identifierId, string command) =>
        $"{PersonPath(personId)}/external-identifiers/{identifierId}/{command}";

    private static async Task<Guid> AddIdentifierAsync(
        HttpClient client, Guid personId, string value, string authority = _regNo, DateOnly? validFrom = null)
    {
        var person = await OkAsync(await client.PostAsJsonAsync(IdentifiersPath(personId), new { authority, value, validFrom }, Ct));
        return person.GetProperty("externalIdentifiers").EnumerateArray()
            .Single(i => i.GetProperty("authority").GetString() == authority && i.GetProperty("value").GetString() == value)
            .GetProperty("id").GetGuid();
    }

    // The person answered, with its version as the ETag.
    private static async Task<JsonElement> OkAsync(HttpResponseMessage response)
    {
        using (response)
        {
            response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));
            var person = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
            response.Headers.ETag.ShouldNotBeNull().Tag.ShouldBe($"\"{person.GetProperty("version").GetUInt32()}\"");
            return person;
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

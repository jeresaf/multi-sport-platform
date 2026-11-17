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
using SportsPlatform.Modules.Registry.Domain.People.Duplicates;
using SportsPlatform.Modules.Tenancy.Contracts.Tenants;
using static SportsPlatform.IntegrationTests.Identity.MembershipTestData;
using static SportsPlatform.IntegrationTests.Registry.PeopleRequests;
using static SportsPlatform.IntegrationTests.Registry.RegistryTestData;

namespace SportsPlatform.IntegrationTests.Registry;

/// <summary>
/// Changing a person's biodata (S06-T3a; D8): the duplicate check on a change of identity, decided pairs (Q45), the
/// version, an archived person's correction, the request's refusals and the roles.
/// </summary>
public sealed class UpdatePersonTests(PostgresFixture fixture) : DatabaseTest(fixture)
{
    // Born the same day as John Ssekandi; nobody else's name.
    private static readonly PersonBiodata _moses = new("Moses", "Kato", Gender: Gender.Male, DateOfBirth: new DateOnly(2001, 4, 12));

    private static readonly object _mosesBody = new
    {
        firstName = "Moses",
        lastName = "Kato",
        gender = "MALE",
        dateOfBirth = "2001-04-12",
    };

    // T3 acceptance: a name change that creates a LIKELY match needs a check and a reason.
    [Fact]
    public async Task A_name_change_that_makes_a_likely_match_needs_a_check_and_a_reason_and_the_pair_is_remembered()
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var john = await CreatePersonAsync(App.Services, kmsa, SystemActor.Instance, JohnSsekandi, overrideReason: null, Ct);
        var moses = await CreatePersonAsync(App.Services, kmsa, SystemActor.Instance, _moses, overrideReason: null, Ct);
        // The same names in the other order: John Ssekandi's name key and date of birth.
        var renamed = With(With(_mosesBody, "firstName", "Ssekandi"), "lastName", "John");

        using (var withoutCheck = await registrar.PutAsJsonAsync(PersonPath(moses), renamed, Ct))
        {
            var problem = await withoutCheck.ShouldBeProblemAsync(HttpStatusCode.UnprocessableEntity, "PERSON_DUPLICATE_CHECK_REQUIRED");
            Fields(problem).ShouldBe([("checkToken", "PERSON_DUPLICATE_CHECK_REQUIRED")]);
        }

        // A check for a new person (no personId) is for other details.
        var newPersonsToken = await CheckTokenAsync(registrar, new { firstName = "Ssekandi", lastName = "John", dateOfBirth = "2001-04-12" });
        using (var otherCheck = await registrar.PutAsJsonAsync(PersonPath(moses), With(renamed, "checkToken", newPersonsToken), Ct))
        {
            await otherCheck.ShouldBeProblemAsync(HttpStatusCode.UnprocessableEntity, "PERSON_DUPLICATE_CHECK_REQUIRED");
        }

        var check = await CheckAsync(registrar, new { firstName = "Ssekandi", lastName = "John", dateOfBirth = "2001-04-12", personId = moses });
        var candidate = check.GetProperty("candidates").EnumerateArray().ShouldHaveSingleItem();
        (candidate.GetProperty("personId").GetGuid(), candidate.GetProperty("confidence").GetString()).ShouldBe((john, "LIKELY"));
        var token = check.GetProperty("checkToken").GetString();

        using (var withoutReason = await registrar.PutAsJsonAsync(PersonPath(moses), With(renamed, "checkToken", token), Ct))
        {
            var problem = await withoutReason.ShouldBeProblemAsync(HttpStatusCode.UnprocessableEntity, "PERSON_DUPLICATE_OVERRIDE_REQUIRED");
            Fields(problem).ShouldBe([("overrideReason", "PERSON_DUPLICATE_OVERRIDE_REQUIRED")]);
        }

        var body = With(With(renamed, "checkToken", token), "overrideReason", " Different player, same birthday ");
        using var updated = await registrar.PutAsJsonAsync(PersonPath(moses), body, Ct);

        updated.StatusCode.ShouldBe(HttpStatusCode.OK, await updated.Content.ReadAsStringAsync(Ct));
        updated.Headers.CacheControl.ShouldNotBeNull().NoStore.ShouldBeTrue();
        updated.Headers.ETag.ShouldNotBeNull();
        var person = await updated.Content.ReadFromJsonAsync<JsonElement>(Ct);
        (person.GetProperty("firstName").GetString(), person.GetProperty("lastName").GetString(), person.GetProperty("displayName").GetString())
            .ShouldBe(("Ssekandi", "John", "Ssekandi John"));
        await using (var scope = ScopeFor(App.Services, kmsa, out var context))
        {
            var decision = await context.Set<DuplicateDecision>().SingleAsync(Ct);
            (decision.OtherThan(moses), decision.Reason).ShouldBe((john, "Different player, same birthday"));
        }

        await AccountTestData.DeliverOutboxAsync(App.Services, Ct);
        var entries = (await AuditTestData.EntriesAsync(App.Services, Ct)).Where(e => e.ActionCode == "person.update").ToList();
        entries.Select(e => e.ErrorCode).ShouldBe(
            ["PERSON_DUPLICATE_CHECK_REQUIRED", "PERSON_DUPLICATE_CHECK_REQUIRED", "PERSON_DUPLICATE_OVERRIDE_REQUIRED", null],
            ignoreOrder: true);
        entries.ShouldAllBe(e => e.EntityType == "Person" && e.EntityId == moses);
        var success = entries.Single(e => e.Outcome == AuditOutcome.Success);
        success.Reason.ShouldBe("Different player, same birthday");
        success.Metadata.ShouldNotBeNull().ShouldNotContain("2001-04-12");
        var audited = JsonDocument.Parse(success.Metadata!).RootElement.GetProperty("candidates").EnumerateArray().ShouldHaveSingleItem();
        (audited.GetProperty("personId").GetGuid(), audited.GetProperty("confidence").GetString()).ShouldBe((john, "LIKELY"));

        // The pair isn't shown again, either way round.
        (await CheckAsync(registrar, new { firstName = "Ssekandi", lastName = "John", dateOfBirth = "2001-04-12", personId = moses }))
            .GetProperty("candidates").GetArrayLength().ShouldBe(0);
        (await CheckAsync(registrar, new { firstName = "John", lastName = "Ssekandi", dateOfBirth = "2001-04-12", personId = john }))
            .GetProperty("candidates").GetArrayLength().ShouldBe(0);
    }

    // Q45 (owner): a decided pair stays hidden even when a later edit would make it LIKELY.
    [Fact]
    public async Task A_pair_decided_not_the_same_stays_hidden_when_an_edit_makes_it_likely()
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var mariam = await CreatePersonAsync(App.Services, kmsa, SystemActor.Instance, MariamNankya, overrideReason: null, Ct);
        var shamim = await CreatePersonAsync(App.Services, kmsa, SystemActor.Instance, ShamimNankya, "Twin sister, different first name", Ct);
        // Shamim's first name corrected to her sister's: the same name key and date of birth.
        var details = new { firstName = "Mariam", lastName = "Nankya", dateOfBirth = "2004-09-21", personId = shamim };

        var check = await CheckAsync(registrar, details);
        check.GetProperty("candidates").GetArrayLength().ShouldBe(0);
        var body = new { firstName = "Mariam", lastName = "Nankya", dateOfBirth = "2004-09-21", checkToken = check.GetProperty("checkToken").GetString() };
        using var updated = await registrar.PutAsJsonAsync(PersonPath(shamim), body, Ct);

        updated.StatusCode.ShouldBe(HttpStatusCode.OK, await updated.Content.ReadAsStringAsync(Ct));
        await using var scope = ScopeFor(App.Services, kmsa, out var context);
        (await context.Set<DuplicateDecision>().SingleAsync(Ct)).OtherThan(shamim).ShouldBe(mariam);
    }

    // D8: the display name, gender, height and weight need no check, and neither does a name written differently.
    [Fact]
    public async Task A_change_the_check_doesnt_compare_needs_no_token_and_a_cleared_field_is_cleared()
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var john = await CreatePersonAsync(App.Services, kmsa, SystemActor.Instance, JohnSsekandi, overrideReason: null, Ct);
        var before = await registrar.ReadJsonAsync(PersonPath(john), Ct);

        using var updated = await registrar.PutAsJsonAsync(PersonPath(john), new
        {
            firstName = "JOHN",
            lastName = "Ssékandi",
            displayName = "J. Ssekandi",
            gender = "MALE",
            dateOfBirth = "2001-04-12",
            nationality = "UG",
            weightKg = 74.5,
        }, Ct);

        updated.StatusCode.ShouldBe(HttpStatusCode.OK, await updated.Content.ReadAsStringAsync(Ct));
        var person = await updated.Content.ReadFromJsonAsync<JsonElement>(Ct);
        (person.GetProperty("firstName").GetString(), person.GetProperty("lastName").GetString(), person.GetProperty("displayName").GetString())
            .ShouldBe(("JOHN", "Ssékandi", "J. Ssekandi"));
        person.GetProperty("heightCm").ValueKind.ShouldBe(JsonValueKind.Null);
        person.GetProperty("weightKg").GetDecimal().ShouldBe(74.5m);
        person.GetProperty("version").GetUInt32().ShouldNotBe(before.GetProperty("version").GetUInt32());
        updated.Headers.ETag.ShouldNotBeNull().Tag.ShouldBe($"\"{person.GetProperty("version").GetUInt32()}\"");

        await AccountTestData.DeliverOutboxAsync(App.Services, Ct);
        var entry = (await AuditTestData.EntriesAsync(App.Services, Ct)).Single(e => e.ActionCode == "person.update");
        (entry.Outcome, entry.EntityId, entry.Reason, entry.Metadata).ShouldBe((AuditOutcome.Success, john, null, null));
    }

    // D6 as D8 runs it: someone added a match since the check.
    [Fact]
    public async Task A_person_added_since_the_check_makes_the_update_stale()
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var moses = await CreatePersonAsync(App.Services, kmsa, SystemActor.Instance, _moses, overrideReason: null, Ct);
        var token = await CheckTokenAsync(registrar, new { firstName = "John", lastName = "Ssekandi", dateOfBirth = "2001-04-12", personId = moses });
        await SavePersonAsync(App.Services, kmsa, JohnSsekandi, Ct);

        var body = With(With(With(_mosesBody, "firstName", "John"), "lastName", "Ssekandi"), "checkToken", token);
        using var stale = await registrar.PutAsJsonAsync(PersonPath(moses), With(body, "overrideReason", "Checked by phone"), Ct);

        await stale.ShouldBeProblemAsync(HttpStatusCode.Conflict, "PERSON_DUPLICATE_CHECK_STALE");
        (await registrar.ReadJsonAsync(PersonPath(moses), Ct)).GetProperty("firstName").GetString().ShouldBe("Moses");
    }

    // Review #99 R1: the update's check runs again holding the people lock, so an update and a create that would make
    // each other a duplicate can't both pass (D6 as D8 runs it).
    [Fact]
    public async Task An_update_and_a_create_that_match_each_other_at_once_leave_one_of_them_stale()
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var colleague = await MemberAsync(kmsa, "brian.ssemakula@kmsa.example", IdentityRoles.Registrar);
        var moses = await CreatePersonAsync(App.Services, kmsa, SystemActor.Instance, _moses, overrideReason: null, Ct);
        var john = new { firstName = "John", lastName = "Ssekandi", dateOfBirth = "2001-04-12" };
        // Moses renamed John Ssekandi, and a new John Ssekandi born the same day: each check finds nobody.
        var update = With(With(With(_mosesBody, "firstName", "John"), "lastName", "Ssekandi"), "checkToken",
            await CheckTokenAsync(registrar, With(john, "personId", moses)));
        var create = With(john, "checkToken", await CheckTokenAsync(colleague, john));

        HttpResponseMessage[] responses;
        await using (var holder = ScopeFor(App.Services, kmsa, out var holding))
        {
            await using var held = await holding.Database.BeginTransactionAsync(Ct);
            var key = $"registry.people:{kmsa}";
            await holding.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock(hashtextextended({key}, 0))", Ct);
            var requests = new[] { registrar.PutAsJsonAsync(PersonPath(moses), update, Ct), colleague.PostAsJsonAsync(PeoplePath, create, Ct) };

            var deadline = DateTimeOffset.UtcNow.AddSeconds(30);
            while (await WaitingOnAdvisoryLocksAsync(holding) < 2)
            {
                requests.ShouldAllBe(request => !request.IsCompleted, "A request finished without waiting for the people lock.");
                DateTimeOffset.UtcNow.ShouldBeLessThan(deadline, "The update and the create never both waited on the people lock.");
                await Task.Delay(TimeSpan.FromMilliseconds(20), Ct);
            }

            await held.CommitAsync(Ct);
            responses = await Task.WhenAll(requests);
        }

        try
        {
            responses.Count(response => response.IsSuccessStatusCode).ShouldBe(1);
            await responses.Single(response => !response.IsSuccessStatusCode)
                .ShouldBeProblemAsync(HttpStatusCode.Conflict, "PERSON_DUPLICATE_CHECK_STALE");
        }
        finally
        {
            foreach (var response in responses)
            {
                response.Dispose();
            }
        }

        // Never two John Ssekandis born the same day without a decision.
        await using var scope = ScopeFor(App.Services, kmsa, out var context);
        (await context.Set<Person>().CountAsync(p => p.NameKey == "john ssekandi", Ct)).ShouldBe(1);
    }

    [Fact]
    public async Task A_stale_version_conflicts_and_the_current_one_is_accepted()
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var moses = await CreatePersonAsync(App.Services, kmsa, SystemActor.Instance, _moses, overrideReason: null, Ct);
        var version = (await registrar.ReadJsonAsync(PersonPath(moses), Ct)).GetProperty("version").GetUInt32();
        var tall = With(_mosesBody, "heightCm", 181);

        using (var stale = await PutAsync(registrar, PersonPath(moses), tall, $"\"{version + 1}\""))
        {
            await stale.ShouldBeProblemAsync(HttpStatusCode.Conflict, "PERSON_CONCURRENCY_CONFLICT");
        }

        using (var staleBody = await registrar.PutAsJsonAsync(PersonPath(moses), With(tall, "version", version + 1), Ct))
        {
            await staleBody.ShouldBeProblemAsync(HttpStatusCode.Conflict, "PERSON_CONCURRENCY_CONFLICT");
        }

        using (var malformed = await PutAsync(registrar, PersonPath(moses), tall, "not-a-tag"))
        {
            await malformed.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_MALFORMED_REQUEST");
        }

        using var current = await PutAsync(registrar, PersonPath(moses), With(tall, "version", version), $"\"{version}\"");
        current.StatusCode.ShouldBe(HttpStatusCode.OK, await current.Content.ReadAsStringAsync(Ct));
        (await current.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("heightCm").GetInt32().ShouldBe(181);
    }

    // Owner (S06-T3 plan): an archived or deceased person's record can still be corrected.
    [Theory]
    [InlineData(PersonTransition.Archive)]
    [InlineData(PersonTransition.MarkDeceased)]
    public async Task An_archived_or_deceased_person_can_be_corrected(PersonTransition transition)
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var moses = await CreatePersonAsync(App.Services, kmsa, SystemActor.Instance, _moses, overrideReason: null, Ct);
        await ChangeStatusAsync(App.Services, kmsa, moses, transition, Ct);

        using var updated = await registrar.PutAsJsonAsync(PersonPath(moses), With(_mosesBody, "displayName", "Moses K."), Ct);

        updated.StatusCode.ShouldBe(HttpStatusCode.OK, await updated.Content.ReadAsStringAsync(Ct));
        (await updated.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("displayName").GetString().ShouldBe("Moses K.");
    }

    [Fact]
    public async Task Another_tenants_person_and_an_unknown_one_are_not_found()
    {
        var (_, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var (elssc, _) = await TenantAsync("elssc", IdentityRoles.Registrar);
        var theirs = await CreatePersonAsync(App.Services, elssc, SystemActor.Instance, _moses, overrideReason: null, Ct);

        foreach (var id in new[] { theirs, Guid.CreateVersion7() })
        {
            using var response = await registrar.PutAsJsonAsync(PersonPath(id), _mosesBody, Ct);
            await response.ShouldBeProblemAsync(HttpStatusCode.NotFound, "PERSON_NOT_FOUND");
        }
    }

    public static TheoryData<string, HttpStatusCode, string, string?> BadRequests => new()
    {
        { """{"lastName":"Kato"}""", HttpStatusCode.BadRequest, "VALIDATION_INVALID_REQUEST", "firstName" },
        { """{"firstName":"Moses","lastName":"--"}""", HttpStatusCode.UnprocessableEntity, "PERSON_NAME_INVALID", "lastName" },
        { """{"firstName":"Moses","lastName":"Kato","dateOfBirth":"2030-01-01"}""", HttpStatusCode.UnprocessableEntity, "PERSON_DATE_OF_BIRTH_IN_FUTURE", "dateOfBirth" },
        { """{"firstName":"Moses","lastName":"Kato","heightCm":261}""", HttpStatusCode.BadRequest, "VALIDATION_INVALID_REQUEST", "heightCm" },
        { """{"firstName":"Moses","lastName":"Kato","nationality":"XX"}""", HttpStatusCode.BadRequest, "VALIDATION_INVALID_REQUEST", "nationality" },
        { """{"firstName":"Moses","lastName":"Kato","overrideReason":" "}""", HttpStatusCode.BadRequest, "VALIDATION_INVALID_REQUEST", "overrideReason" },
        // Identifiers and the status have their own actions.
        { """{"firstName":"Moses","lastName":"Kato","externalIdentifiers":[]}""", HttpStatusCode.BadRequest, "VALIDATION_MALFORMED_REQUEST", null },
        { """{"firstName":"Moses","lastName":"Kato","status":"ARCHIVED"}""", HttpStatusCode.BadRequest, "VALIDATION_MALFORMED_REQUEST", null },
    };

    [Theory]
    [MemberData(nameof(BadRequests))]
    public async Task A_request_that_cant_be_saved_is_refused_on_its_field(string body, HttpStatusCode status, string code, string? field)
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var moses = await CreatePersonAsync(App.Services, kmsa, SystemActor.Instance, _moses, overrideReason: null, Ct);

        using var content = new StringContent(body, System.Text.Encoding.UTF8, "application/json");
        using var response = await registrar.PutAsync(PersonPath(moses), content, Ct);

        var problem = await response.ShouldBeProblemAsync(status, code);
        if (field is not null)
        {
            Fields(problem).Select(error => error.Item1).ShouldContain(field);
        }
    }

    // D21: changing people is the owner's, the admin's and the registrar's.
    [Theory]
    [InlineData(IdentityRoles.TenantOwner, true)]
    [InlineData(IdentityRoles.TenantAdmin, true)]
    [InlineData(IdentityRoles.Registrar, true)]
    [InlineData(IdentityRoles.CompetitionManager, false)]
    [InlineData(IdentityRoles.ResultsOfficer, false)]
    [InlineData(IdentityRoles.DataEntry, false)]
    [InlineData(IdentityRoles.Viewer, false)]
    [InlineData(IdentityRoles.Scorer, false)]
    public async Task Only_those_who_change_people_may_update_them(string role, bool allowed)
    {
        var (kmsa, client) = await TenantAsync("kmsa", role);
        var moses = await CreatePersonAsync(App.Services, kmsa, SystemActor.Instance, _moses, overrideReason: null, Ct);

        using var response = await client.PutAsJsonAsync(PersonPath(moses), With(_mosesBody, "heightCm", 181), Ct);

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
    public async Task A_read_only_tenant_changes_nobody()
    {
        App.Authorization.UseIdentityRoles();
        var kmsa = await TenantTestData.SaveTenantAsync(
            App.Services, "kmsa", TenantStatus.Active, publicEnabled: false, withDomain: true, withSubscription: false, Ct);
        await TenantTestData.SavePeriodAsync(App.Services, kmsa, new DateOnly(2025, 1, 1), new DateOnly(2025, 12, 31), Ct, cancelReason: "Pilot paused");
        var moses = await SavePersonAsync(App.Services, kmsa, _moses, Ct);
        var registrar = await MemberAsync(kmsa, "brian.ssemakula@kmsa.example", IdentityRoles.Registrar);

        using var response = await registrar.PutAsJsonAsync(PersonPath(moses.Id), _mosesBody, Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "TENANT_READ_ONLY");
    }

    private static async Task<HttpResponseMessage> PutAsync(HttpClient client, string path, object body, string ifMatch)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, path) { Content = JsonContent.Create(body) };
        request.Headers.TryAddWithoutValidation("If-Match", ifMatch);
        return await client.SendAsync(request, Ct);
    }

    /// <summary>An active tenant (time zone Africa/Kampala) and a client of a member with <paramref name="role"/> tenant-wide.</summary>
    private async Task<(Guid Tenant, HttpClient Client)> TenantAsync(string slug, string role)
    {
        App.Authorization.UseIdentityRoles();
        var tenant = await TenantTestData.SaveTenantAsync(App.Services, slug, TenantStatus.Active, Ct);
        return (tenant, await MemberAsync(tenant, MailboxOf(role, $"{slug}.example"), role));
    }

    private async Task<HttpClient> MemberAsync(Guid tenant, string email, string role)
    {
        var member = await CreateMemberAsync(App.Services, tenant, email, Ct, TenantWide(role));
        return App.ClientFor(member.UserId, tenant);
    }
}

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
using SportsPlatform.Modules.Registry.Infrastructure;
using SportsPlatform.Modules.Tenancy.Contracts.Tenants;
using static SportsPlatform.IntegrationTests.Identity.MembershipTestData;
using static SportsPlatform.IntegrationTests.Registry.PeopleRequests;
using static SportsPlatform.IntegrationTests.Registry.RegistryTestData;

namespace SportsPlatform.IntegrationTests.Registry;

/// <summary>
/// Creating a person (S06-T2b; D6, D7): the check token, the four refusals, the decisions remembered, the audit entry,
/// two creates at once, the request's refusals, the roles and the read-only tenant.
/// </summary>
public sealed class CreatePersonTests(PostgresFixture fixture) : DatabaseTest(fixture)
{
    private const string _regNo = "KMSA_REG_NO";

    private static readonly object _shamim = new { firstName = "Shamim", lastName = "Nankya", dateOfBirth = "2004-09-21" };

    // T2 acceptance: create without a check is refused, and so is a token that doesn't prove this user checked these
    // details in this tenant within 30 minutes.
    [Fact]
    public async Task A_create_without_a_valid_check_token_is_refused_and_saves_nothing()
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var otherRegistrar = await MemberAsync(kmsa, "brian.ssemakula@kmsa.example", IdentityRoles.Registrar);
        var (_, elsscRegistrar) = await TenantAsync("elssc", IdentityRoles.Registrar);
        var mariam = await SavePersonAsync(App.Services, kmsa, MariamNankya, Ct);
        var moses = new { firstName = "Moses", lastName = "Kato", dateOfBirth = "2003-05-17", nationality = "UG" };

        var othersToken = await CheckTokenAsync(otherRegistrar, moses);
        var otherTenantsToken = await CheckTokenAsync(elsscRegistrar, moses);
        var otherDetailsToken = await CheckTokenAsync(registrar, With(moses, "dateOfBirth", "2003-05-18"));
        var changeToken = await CheckTokenAsync(registrar, With(moses, "personId", mariam.Id));
        var expiringToken = await CheckTokenAsync(registrar, moses);

        foreach (var token in new[] { null, "not-a-token", othersToken, otherTenantsToken, otherDetailsToken, changeToken })
        {
            await ShouldNeedACheckAsync(registrar, With(moses, "checkToken", token));
        }

        // Valid for 30 minutes (the next test creates with one 29 minutes old).
        App.Clock.Advance(TimeSpan.FromMinutes(30));
        await ShouldNeedACheckAsync(registrar, With(moses, "checkToken", expiringToken));

        // Every refusal is audited, with no person.
        await AccountTestData.DeliverOutboxAsync(App.Services, Ct);
        var entries = await AuditTestData.EntriesAsync(App.Services, Ct);
        entries.Count.ShouldBe(7);
        entries.ShouldAllBe(entry => entry.ActionCode == "person.create" && entry.ErrorCode == "PERSON_DUPLICATE_CHECK_REQUIRED"
            && entry.EntityType == "Person" && entry.EntityId == null);
        await using var scope = ScopeFor(App.Services, kmsa, out var context);
        (await context.Set<Person>().CountAsync(Ct)).ShouldBe(1);
    }

    [Fact]
    public async Task A_person_nobody_matches_is_created_with_their_identifiers()
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var moses = new Dictionary<string, object?>
        {
            ["firstName"] = "Moses",
            ["lastName"] = "Kato",
            ["gender"] = "MALE",
            ["dateOfBirth"] = "2003-05-17",
            ["nationality"] = "UG",
            ["secondaryNationality"] = "KE",
            ["heightCm"] = 181,
            ["weightKg"] = 74.5,
            ["externalIdentifiers"] = new[] { new { authority = _regNo, value = " KMSA-00512 ", validFrom = "2024-02-01" } },

            // Nobody matched: the reason is audited and nothing else is written (review #98 R3).
            ["overrideReason"] = "Registered at the Ntinda trial day",
        };
        // The check takes only what it compares: no gender, height, weight or validFrom.
        var token = await CheckTokenAsync(registrar, new
        {
            firstName = "Moses",
            lastName = "Kato",
            dateOfBirth = "2003-05-17",
            nationality = "UG",
            secondaryNationality = "KE",
            externalIdentifiers = new[] { new { authority = _regNo, value = "KMSA-00512" } },
        });
        App.Clock.Advance(TimeSpan.FromMinutes(29));
        App.Telemetry.Clear();

        using var response = await registrar.PostAsJsonAsync(PeoplePath, With(moses, "checkToken", token), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync(Ct));
        response.Headers.CacheControl.ShouldNotBeNull().NoStore.ShouldBeTrue();
        var text = await response.Content.ReadAsStringAsync(Ct);
        text.ShouldNotContain("2003-05-17");
        var person = JsonDocument.Parse(text).RootElement;
        var id = person.GetProperty("id").GetGuid();
        response.Headers.Location.ShouldNotBeNull().ToString().ShouldBe(PersonPath(id));
        response.Headers.ETag.ShouldNotBeNull();
        (person.GetProperty("displayName").GetString(), person.GetProperty("birthYear").GetInt32(), person.GetProperty("status").GetString(),
            person.GetProperty("source").GetString(), person.GetProperty("ageStatus").GetString())
            .ShouldBe(("Moses Kato", 2003, "ACTIVE", "MANUAL", "ADULT"));
        var identifier = person.GetProperty("externalIdentifiers").EnumerateArray().ShouldHaveSingleItem();
        (identifier.GetProperty("authority").GetString(), identifier.GetProperty("value").GetString(), identifier.GetProperty("validFrom").GetString())
            .ShouldBe((_regNo, "KMSA-00512", "2024-02-01"));
        (await registrar.ReadJsonAsync(PrivatePath(id), Ct)).GetProperty("dateOfBirth").GetString().ShouldBe("2003-05-17");

        await AccountTestData.DeliverOutboxAsync(App.Services, Ct);
        var entry = (await AuditTestData.EntriesAsync(App.Services, Ct)).Single(e => e.ActionCode == "person.create");
        (entry.Outcome, entry.EntityType, entry.EntityId, entry.Reason)
            .ShouldBe((AuditOutcome.Success, "Person", id, "Registered at the Ntinda trial day"));
        JsonDocument.Parse(entry.Metadata!).RootElement.GetProperty("candidates").GetArrayLength().ShouldBe(0);
        entry.Metadata!.ShouldNotContain("2003-05-17");
        await using (var scope = ScopeFor(App.Services, kmsa, out var context))
        {
            (await context.Set<DuplicateDecision>().CountAsync(Ct)).ShouldBe(0);
        }

        // D3: the date of birth and the names never reach the logs (review #98 R2).
        await App.Telemetry.WaitForRequestLogAsync(PeoplePath);
        foreach (var value in new[] { "2003-05-17", "Moses", "Kato" })
        {
            App.Telemetry.Events.ShouldAllBe(e =>
                !e.RenderMessage(null).Contains(value, StringComparison.Ordinal)
                && e.Properties.Values.All(property => !property.ToString().Contains(value, StringComparison.Ordinal)));
        }
    }

    // T2 acceptance: past candidates without a reason is refused; with one it succeeds, the audit entry keeps the reason
    // and the candidates, and the pair isn't warned again (the demo's twins).
    [Fact]
    public async Task Proceeding_past_candidates_needs_a_reason_which_is_audited_and_the_pair_is_remembered()
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var mariam = await CreatePersonAsync(App.Services, kmsa, SystemActor.Instance, MariamNankya, overrideReason: null, Ct);
        var token = await CheckTokenAsync(registrar, _shamim);

        using var withoutReason = await registrar.PostAsJsonAsync(PeoplePath, With(_shamim, "checkToken", token), Ct);
        var problem = await withoutReason.ShouldBeProblemAsync(HttpStatusCode.UnprocessableEntity, "PERSON_DUPLICATE_OVERRIDE_REQUIRED");
        Fields(problem).ShouldBe([("overrideReason", "PERSON_DUPLICATE_OVERRIDE_REQUIRED")]);

        var body = With(With(_shamim, "checkToken", token), "overrideReason", "  Twin sister, different first name ");
        using var created = await registrar.PostAsJsonAsync(PeoplePath, body, Ct);

        created.StatusCode.ShouldBe(HttpStatusCode.Created, await created.Content.ReadAsStringAsync(Ct));
        var shamim = (await created.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("id").GetGuid();
        await using (var scope = ScopeFor(App.Services, kmsa, out var context))
        {
            var decision = await context.Set<DuplicateDecision>().SingleAsync(Ct);
            (decision.OtherThan(shamim), decision.Decision, decision.Reason).ShouldBe(
                (mariam, DuplicateDecisionKind.NotSame, "Twin sister, different first name"));
        }

        await AccountTestData.DeliverOutboxAsync(App.Services, Ct);
        var entries = await AuditTestData.EntriesAsync(App.Services, Ct);
        var refused = entries.Single(e => e.ErrorCode == "PERSON_DUPLICATE_OVERRIDE_REQUIRED");
        var success = entries.Single(e => e.Outcome == AuditOutcome.Success);
        (success.EntityId, success.Reason).ShouldBe((shamim, "Twin sister, different first name"));
        foreach (var entry in new[] { refused, success })
        {
            var candidate = JsonDocument.Parse(entry.Metadata!).RootElement.GetProperty("candidates").EnumerateArray().ShouldHaveSingleItem();
            candidate.EnumerateObject().Select(property => property.Name).ShouldBe(["personId", "confidence"]);
            (candidate.GetProperty("personId").GetGuid(), candidate.GetProperty("confidence").GetString()).ShouldBe((mariam, "POSSIBLE"));
        }

        // Neither sister is shown to the other again.
        (await CheckAsync(registrar, With(_shamim, "personId", shamim))).GetProperty("candidates").GetArrayLength().ShouldBe(0);
        var mariamsData = new { firstName = "Mariam", lastName = "Nankya", dateOfBirth = "2004-09-21", personId = mariam };
        (await CheckAsync(registrar, mariamsData)).GetProperty("candidates").GetArrayLength().ShouldBe(0);
    }

    // T2 acceptance: a stale check is refused: someone added a match since, or the token already made its person.
    [Fact]
    public async Task A_person_added_since_the_check_makes_it_stale_so_a_create_is_never_repeated()
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var john = new { firstName = "John", lastName = "Ssekandi", dateOfBirth = "2001-04-12" };
        var moses = new { firstName = "Moses", lastName = "Kato" };
        var johnsToken = await CheckTokenAsync(registrar, john);
        var mosesToken = await CheckTokenAsync(registrar, moses);
        await SavePersonAsync(App.Services, kmsa, JohnSsekandi, Ct);

        using var stale = await registrar.PostAsJsonAsync(
            PeoplePath, With(With(john, "checkToken", johnsToken), "overrideReason", "Checked by phone"), Ct);
        using var first = await registrar.PostAsJsonAsync(PeoplePath, With(moses, "checkToken", mosesToken), Ct);
        using var again = await registrar.PostAsJsonAsync(PeoplePath, With(moses, "checkToken", mosesToken), Ct);

        await stale.ShouldBeProblemAsync(HttpStatusCode.Conflict, "PERSON_DUPLICATE_CHECK_STALE");
        first.StatusCode.ShouldBe(HttpStatusCode.Created);
        await again.ShouldBeProblemAsync(HttpStatusCode.Conflict, "PERSON_DUPLICATE_CHECK_STALE");
        await using var scope = ScopeFor(App.Services, kmsa, out var context);
        (await context.Set<Person>().CountAsync(Ct)).ShouldBe(2);
        (await context.Set<DuplicateDecision>().CountAsync(Ct)).ShouldBe(0);
    }

    [Fact]
    public async Task An_external_identifier_another_person_holds_blocks_the_create_even_with_a_reason()
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        await SavePersonAsync(App.Services, kmsa, JohnSsekandi, ("Johnny", AliasKind.Nickname), (_regNo, "KMSA-00417"), Ct);
        var moses = new
        {
            firstName = "Moses",
            lastName = "Kato",
            externalIdentifiers = new[]
            {
                new { authority = "SCHOOL_ADMISSION_NO", value = "EL/2021/310" },
                new { authority = _regNo, value = "KMSA-00417" },
            },
        };
        var token = await CheckTokenAsync(registrar, moses);

        using var response = await registrar.PostAsJsonAsync(
            PeoplePath, With(With(moses, "checkToken", token), "overrideReason", "Different person"), Ct);

        var problem = await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, "PERSON_EXTERNAL_ID_EXISTS");
        Fields(problem).ShouldBe([("externalIdentifiers[1].value", "PERSON_EXTERNAL_ID_EXISTS")]);
        await using var scope = ScopeFor(App.Services, kmsa, out var context);
        (await context.Set<Person>().CountAsync(Ct)).ShouldBe(1);
    }

    // T2 acceptance: two creates of the same person at once. The test holds the tenant's people lock until both creates
    // wait on it, so they overlap for certain; released, the lock lets one through and the second finds the first's
    // person, which its check didn't answer. Without the lock both would see nobody and both would be created.
    [Fact]
    public async Task Two_creates_of_the_same_person_at_once_make_one_person()
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var colleague = await MemberAsync(kmsa, "brian.ssemakula@kmsa.example", IdentityRoles.Registrar);
        var brenda = new { firstName = "Brenda", lastName = "Akello", dateOfBirth = "2011-03-02" };
        var mine = With(brenda, "checkToken", await CheckTokenAsync(registrar, brenda));
        var theirs = With(brenda, "checkToken", await CheckTokenAsync(colleague, brenda));

        HttpResponseMessage[] responses;
        await using (var holder = ScopeFor(App.Services, kmsa, out var holding))
        {
            await using var held = await holding.Database.BeginTransactionAsync(Ct);
            var key = $"registry.people:{kmsa}";
            await holding.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock(hashtextextended({key}, 0))", Ct);
            var creates = new[] { registrar.PostAsJsonAsync(PeoplePath, mine, Ct), colleague.PostAsJsonAsync(PeoplePath, theirs, Ct) };

            var deadline = DateTimeOffset.UtcNow.AddSeconds(30);
            while (await WaitingOnAdvisoryLocksAsync(holding) < 2)
            {
                creates.ShouldAllBe(create => !create.IsCompleted, "A create finished without waiting for the people lock.");
                DateTimeOffset.UtcNow.ShouldBeLessThan(deadline, "The two creates never both waited on the people lock.");
                await Task.Delay(TimeSpan.FromMilliseconds(20), Ct);
            }

            await held.CommitAsync(Ct);
            responses = await Task.WhenAll(creates);
        }

        try
        {
            responses.Select(response => response.StatusCode)
                .ShouldBe([HttpStatusCode.Created, HttpStatusCode.Conflict], ignoreOrder: true);
            await responses.Single(response => response.StatusCode == HttpStatusCode.Conflict)
                .ShouldBeProblemAsync(HttpStatusCode.Conflict, "PERSON_DUPLICATE_CHECK_STALE");
        }
        finally
        {
            foreach (var response in responses)
            {
                response.Dispose();
            }
        }

        await using var scope = ScopeFor(App.Services, kmsa, out var context);
        (await context.Set<Person>().CountAsync(Ct)).ShouldBe(1);
    }

    public static TheoryData<string, string, string> BadRequests => new()
    {
        { """{"firstName":"Moses","lastName":"Kato","weightKg":74.55}""", "VALIDATION_INVALID_REQUEST", "weightKg" },
        { """{"firstName":"Moses","lastName":"Kato","weightKg":9.5}""", "VALIDATION_INVALID_REQUEST", "weightKg" },
        { """{"firstName":"Moses","lastName":"Kato","heightCm":261}""", "VALIDATION_INVALID_REQUEST", "heightCm" },
        { $$"""{"firstName":"Moses","lastName":"Kato","displayName":"{{new string('a', 201)}}"}""", "VALIDATION_INVALID_REQUEST", "displayName" },
        { """{"firstName":"Moses","lastName":"Kato","secondaryNationality":"XX"}""", "VALIDATION_INVALID_REQUEST", "secondaryNationality" },
        { """{"firstName":"Moses","lastName":"Kato","overrideReason":"  "}""", "VALIDATION_INVALID_REQUEST", "overrideReason" },
        { $$"""{"firstName":"Moses","lastName":"Kato","overrideReason":"{{new string('a', 501)}}"}""", "VALIDATION_INVALID_REQUEST", "overrideReason" },
        {
            """{"firstName":"Moses","lastName":"Kato","externalIdentifiers":[{"authority":"KMSA_REG_NO","value":"K-1"},{"authority":"KMSA_REG_NO","value":" K-1"}]}""",
            "VALIDATION_INVALID_REQUEST", "externalIdentifiers"
        },
        { """{"firstName":"--","lastName":"Kato"}""", "PERSON_NAME_INVALID", "firstName" },

        {
            """{"firstName":"Moses","lastName":"Kato","externalIdentifiers":[{"authority":"NATIONAL_ID","value":"CM0000000000XX"}]}""",
            "PERSON_EXTERNAL_ID_AUTHORITY_RESERVED", "externalIdentifiers[0].authority"
        },
    };

    [Theory]
    [MemberData(nameof(BadRequests))]
    public async Task A_request_that_cant_be_created_is_refused_on_its_field(string body, string code, string field)
    {
        var (_, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);

        using var response = await registrar.PostAsync(
            PeoplePath, new StringContent(body, System.Text.Encoding.UTF8, "application/json"), Ct);

        var status = code.StartsWith("PERSON_", StringComparison.Ordinal) ? HttpStatusCode.UnprocessableEntity : HttpStatusCode.BadRequest;
        Fields(await response.ShouldBeProblemAsync(status, code)).ShouldBe([(field, code)]);
    }

    // Q43, on the tenant's date (Kampala). The test clock is shared by the run's tests, so the dates follow it.
    [Fact]
    public async Task A_date_of_birth_of_today_is_accepted_tomorrow_is_refused_and_unknown_members_are_refused()
    {
        var (_, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var today = DateOnly.FromDateTime(App.Clock.GetUtcNow().ToOffset(TimeSpan.FromHours(3)).DateTime);
        var newborn = new { firstName = "Moses", lastName = "Kato", dateOfBirth = today };
        var unborn = new { firstName = "Moses", lastName = "Kato", dateOfBirth = today.AddDays(1) };

        using var created = await registrar.PostAsJsonAsync(PeoplePath, With(newborn, "checkToken", await CheckTokenAsync(registrar, newborn)), Ct);
        using var refused = await registrar.PostAsJsonAsync(PeoplePath, unborn, Ct);
        using var unknownMember = await registrar.PostAsync(
            PeoplePath,
            new StringContent("""{"firstName":"Moses","lastName":"Kato","status":"ARCHIVED"}""", System.Text.Encoding.UTF8, "application/json"),
            Ct);

        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        Fields(await refused.ShouldBeProblemAsync(HttpStatusCode.UnprocessableEntity, "PERSON_DATE_OF_BIRTH_IN_FUTURE"))
            .ShouldBe([("dateOfBirth", "PERSON_DATE_OF_BIRTH_IN_FUTURE")]);
        await unknownMember.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_MALFORMED_REQUEST");
    }

    // D21: creating people is the owner's, the admin's and the registrar's.
    [Theory]
    [InlineData(IdentityRoles.TenantOwner, true)]
    [InlineData(IdentityRoles.TenantAdmin, true)]
    [InlineData(IdentityRoles.Registrar, true)]
    [InlineData(IdentityRoles.CompetitionManager, false)]
    [InlineData(IdentityRoles.ResultsOfficer, false)]
    [InlineData(IdentityRoles.DataEntry, false)]
    [InlineData(IdentityRoles.Viewer, false)]
    [InlineData(IdentityRoles.Scorer, false)]
    public async Task Only_those_who_change_people_may_create_them(string role, bool allowed)
    {
        var (_, client) = await TenantAsync("kmsa", role);
        var moses = new { firstName = "Moses", lastName = "Kato" };

        if (allowed)
        {
            using var response = await client.PostAsJsonAsync(PeoplePath, With(moses, "checkToken", await CheckTokenAsync(client, moses)), Ct);
            response.StatusCode.ShouldBe(HttpStatusCode.Created);
        }
        else
        {
            using var response = await client.PostAsJsonAsync(PeoplePath, moses, Ct);
            await response.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "AUTH_FORBIDDEN");
        }
    }

    [Fact]
    public async Task A_read_only_tenant_creates_nobody()
    {
        App.Authorization.UseIdentityRoles();
        var kmsa = await TenantTestData.SaveTenantAsync(
            App.Services, "kmsa", TenantStatus.Active, publicEnabled: false, withDomain: true, withSubscription: false, Ct);
        await TenantTestData.SavePeriodAsync(App.Services, kmsa, new DateOnly(2025, 1, 1), new DateOnly(2025, 12, 31), Ct, cancelReason: "Pilot paused");
        var registrar = await MemberAsync(kmsa, "brian.ssemakula@kmsa.example", IdentityRoles.Registrar);

        using var response = await registrar.PostAsJsonAsync(PeoplePath, new { firstName = "Moses", lastName = "Kato" }, Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "TENANT_READ_ONLY");
    }

    private static Task<int> WaitingOnAdvisoryLocksAsync(RegistryDbContext context) =>
        context.Database
            .SqlQuery<int>($"SELECT count(*)::int AS \"Value\" FROM pg_locks WHERE locktype = 'advisory' AND NOT granted AND database = (SELECT oid FROM pg_database WHERE datname = current_database())")
            .SingleAsync(Ct);

    private static async Task<JsonElement> CheckAsync(HttpClient client, object body)
    {
        using var response = await client.PostAsJsonAsync(DuplicateCheckPath, body, Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));
        return await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
    }

    private static async Task<string> CheckTokenAsync(HttpClient client, object body) =>
        (await CheckAsync(client, body)).GetProperty("checkToken").GetString()!;

    private static async Task ShouldNeedACheckAsync(HttpClient client, object body)
    {
        using var response = await client.PostAsJsonAsync(PeoplePath, body, Ct);
        var problem = await response.ShouldBeProblemAsync(HttpStatusCode.UnprocessableEntity, "PERSON_DUPLICATE_CHECK_REQUIRED");
        Fields(problem).ShouldBe([("checkToken", "PERSON_DUPLICATE_CHECK_REQUIRED")]);
    }

    private static List<(string?, string?)> Fields(JsonElement problem) =>
    [
        .. problem.GetProperty("errors").EnumerateArray()
            .Select(error => (error.GetProperty("field").GetString(), error.GetProperty("code").GetString())),
    ];

    // The body with one more member, or one replaced.
    private static Dictionary<string, object?> With(object body, string name, object? value)
    {
        var merged = JsonSerializer.Deserialize<Dictionary<string, object?>>(JsonSerializer.Serialize(body))!;
        merged[name] = value;
        return merged;
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

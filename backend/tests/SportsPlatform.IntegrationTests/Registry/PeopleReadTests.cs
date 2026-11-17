using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SportsPlatform.BuildingBlocks.Auditing;
using SportsPlatform.BuildingBlocks.Tenancy;
using SportsPlatform.IntegrationTests.Api;
using SportsPlatform.IntegrationTests.Audit;
using SportsPlatform.IntegrationTests.Identity;
using SportsPlatform.IntegrationTests.Infrastructure;
using SportsPlatform.IntegrationTests.Tenancy;
using SportsPlatform.Modules.Identity.Domain.Roles;
using SportsPlatform.Modules.Platform.Domain.Audit;
using SportsPlatform.Modules.Registry.Contracts.People;
using SportsPlatform.Modules.Registry.Domain.People;
using SportsPlatform.Modules.Tenancy.Contracts.Tenants;
using static SportsPlatform.IntegrationTests.Identity.MembershipTestData;
using static SportsPlatform.IntegrationTests.Registry.PeopleRequests;
using static SportsPlatform.IntegrationTests.Registry.RegistryTestData;
using ContractStatus = SportsPlatform.Modules.Registry.Contracts.People.PersonStatus;
using PersonStatus = SportsPlatform.Modules.Registry.Domain.People.PersonStatus;

namespace SportsPlatform.IntegrationTests.Registry;

/// <summary>
/// The people reads (S06-T1b; D3, D13, D21, D22): the directory with its search, filters and paging, a person's record
/// without <c>PRIVATE</c> fields, the audited private read, the status history, the roles and
/// <see cref="IPeopleDirectory"/>. The shared test clock starts on 2026-10-17 and only moves forward, by days at most.
/// </summary>
public sealed class PeopleReadTests(PostgresFixture fixture) : DatabaseTest(fixture)
{
    [Fact]
    public async Task The_directory_is_sorted_by_last_name_then_first_name_and_leaves_archived_people_out()
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        foreach (var biodata in new[] { ShamimNankya, JohnSsekandi, BrendaAkello, MariamNankya, DenisOpoka })
        {
            await SavePersonAsync(App.Services, kmsa, biodata, Ct);
        }

        var ivan = await SavePersonAsync(App.Services, kmsa, IvanMukasa, Ct);
        await SetStatusAsync(App.Services, kmsa, ivan.Id, PersonStatus.Archived, Ct);

        var page = await registrar.ReadJsonAsync(PeoplePath, Ct);

        page.Names().ShouldBe(["Brenda Akello", "Mariam Nankya", "Shamim Nankya", "Denis Opoka", "John Ssekandi"]);
        page.GetProperty("page").GetProperty("hasMore").GetBoolean().ShouldBeFalse();
        var brenda = page.GetProperty("items")[0];
        (brenda.GetProperty("firstName").GetString(), brenda.GetProperty("lastName").GetString(), brenda.GetProperty("status").GetString(),
            brenda.GetProperty("birthYear").GetInt32(), brenda.GetProperty("isMinor").GetBoolean(), brenda.GetProperty("ageStatus").GetString(),
            brenda.GetProperty("nationality").GetString())
            .ShouldBe(("Brenda", "Akello", "ACTIVE", 2011, true, "MINOR", "UG"));
        (await registrar.ReadJsonAsync($"{PeoplePath}?status=ARCHIVED", Ct)).Names().ShouldBe(["Ivan Mukasa"]);
    }

    [Fact]
    public async Task The_directory_pages_by_cursor_through_people_who_share_a_last_name()
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        foreach (var biodata in new[] { ShamimNankya, JohnSsekandi, MariamNankya, MariamNankya, BrendaAkello })
        {
            await SavePersonAsync(App.Services, kmsa, biodata, Ct);
        }

        var names = new List<string?>();
        var ids = new List<Guid>();
        string? cursor = null;
        var pages = 0;
        do
        {
            var page = await registrar.ReadJsonAsync($"{PeoplePath}?limit=2{(cursor is null ? "" : $"&cursor={cursor}")}", Ct);
            names.AddRange(page.Names());
            ids.AddRange(page.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetGuid()));
            cursor = page.GetProperty("page").GetProperty("nextCursor").GetString();
            page.GetProperty("page").GetProperty("hasMore").GetBoolean().ShouldBe(cursor is not null);
            pages++;
        }
        while (cursor is not null);

        pages.ShouldBe(3);
        names.ShouldBe(["Brenda Akello", "Mariam Nankya", "Mariam Nankya", "Shamim Nankya", "John Ssekandi"]);
        ids.Distinct().Count().ShouldBe(5);
    }

    [Theory]
    [InlineData("Ssek", "John Ssekandi")] // a prefix of the last name
    [InlineData("joh", "John Ssekandi")] // a prefix of the first name, any case
    [InlineData("Ssekandi John", "John Ssekandi")] // the other order
    [InlineData("Jon Ssekandi", "John Ssekandi")] // a spelling close to it
    [InlineData("Deno", "Denis Opoka")] // an alias
    [InlineData("KMSA-00417", "Denis Opoka")] // an external identifier's value, exactly
    [InlineData("KMSA-0041", null)]
    [InlineData("Namaganda", null)]
    [InlineData("%", null)]
    public async Task Search_finds_people_by_name_alias_or_identifier(string q, string? expected)
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        await SavePersonAsync(App.Services, kmsa, JohnSsekandi, Ct);
        await SavePersonAsync(App.Services, kmsa, BrendaAkello, Ct);
        await SavePersonAsync(App.Services, kmsa, DenisOpoka, ("Deno", AliasKind.Nickname), ("KMSA_REG_NO", "KMSA-00417"), Ct);

        var page = await registrar.ReadJsonAsync($"{PeoplePath}?q={Uri.EscapeDataString(q)}", Ct);

        page.Names().ShouldBe(expected is null ? [] : [expected]);
    }

    [Fact]
    public async Task Search_and_filters_page_by_cursor_too()
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        foreach (var biodata in new[] { ShamimNankya, MariamNankya, JohnSsekandi, BrendaAkello })
        {
            await SavePersonAsync(App.Services, kmsa, biodata, Ct);
        }

        var first = await registrar.ReadJsonAsync($"{PeoplePath}?q=Nankya&status=ACTIVE&limit=1", Ct);
        var cursor = first.GetProperty("page").GetProperty("nextCursor").GetString();
        var second = await registrar.ReadJsonAsync($"{PeoplePath}?q=Nankya&status=ACTIVE&limit=1&cursor={cursor}", Ct);

        first.Names().ShouldBe(["Mariam Nankya"]);
        second.Names().ShouldBe(["Shamim Nankya"]);
        second.GetProperty("page").GetProperty("hasMore").GetBoolean().ShouldBeFalse();
        (await registrar.ReadJsonAsync($"{PeoplePath}?status=INACTIVE", Ct)).Names().ShouldBeEmpty();
    }

    [Theory]
    [InlineData("ADULT", "John Ssekandi")]
    [InlineData("MINOR", "Brenda Akello")]
    [InlineData("UNKNOWN", "Ivan Mukasa")]
    public async Task The_age_filter_follows_the_tenants_date(string ageStatus, string expected)
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        foreach (var biodata in new[] { JohnSsekandi, BrendaAkello, IvanMukasa })
        {
            await SavePersonAsync(App.Services, kmsa, biodata, Ct);
        }

        (await registrar.ReadJsonAsync($"{PeoplePath}?ageStatus={ageStatus}", Ct)).Names().ShouldBe([expected]);
    }

    // D13: "today" is the tenant's. Kampala is three hours ahead of UTC, so the birthday starts at 21:00 UTC the day before.
    [Fact]
    public async Task A_person_turns_18_at_midnight_in_the_tenants_time_zone()
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var kampala = TimeSpan.FromHours(3);
        var tomorrow = DateOnly.FromDateTime(App.Clock.GetUtcNow().ToOffset(kampala).DateTime).AddDays(1);
        var person = await SavePersonAsync(App.Services, kmsa, BrendaAkello with { DateOfBirth = tomorrow.AddYears(-18) }, Ct);
        var midnight = new DateTimeOffset(tomorrow.ToDateTime(TimeOnly.MinValue), kampala);

        App.Clock.Advance(midnight - App.Clock.GetUtcNow() - TimeSpan.FromMinutes(1));
        var before = await registrar.ReadJsonAsync(PersonPath(person.Id), Ct);
        App.Clock.Advance(TimeSpan.FromMinutes(2));
        var after = await registrar.ReadJsonAsync(PersonPath(person.Id), Ct);

        (before.GetProperty("ageStatus").GetString(), before.GetProperty("isMinor").GetBoolean()).ShouldBe(("MINOR", true));
        (after.GetProperty("ageStatus").GetString(), after.GetProperty("isMinor").GetBoolean()).ShouldBe(("ADULT", false));
        (await registrar.ReadJsonAsync($"{PeoplePath}?ageStatus=ADULT", Ct)).Names().ShouldBe(["Brenda Akello"]);
    }

    [Theory]
    [InlineData("status=RETIRED")]
    [InlineData("status=active")]
    [InlineData("ageStatus=CHILD")]
    [InlineData("limit=0")]
    [InlineData("limit=101")]
    [InlineData("limit=ten")]
    [InlineData("cursor=bm90LWEtY3Vyc29y")]
    [InlineData("q=%00")]
    [InlineData("q=John%00")]
    [InlineData("q=John%09Ssekandi")]
    [InlineData("q=aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    public async Task A_bad_query_is_refused(string query)
    {
        var (_, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);

        using var response = await registrar.GetAsync($"{PeoplePath}?{query}", Ct);

        var problem = await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_INVALID_QUERY");
        problem.GetProperty("errors").GetArrayLength().ShouldBe(1);
    }

    // T1 acceptance: person.read answers no PRIVATE field.
    [Fact]
    public async Task A_persons_record_carries_the_birth_year_and_never_the_date_of_birth()
    {
        var (kmsa, viewer) = await TenantAsync("kmsa", IdentityRoles.Viewer);
        var denis = await SavePersonAsync(App.Services, kmsa, DenisOpoka, ("Deno", AliasKind.Nickname), ("KMSA_REG_NO", "KMSA-00417"), Ct);

        using var response = await viewer.GetAsync(PersonPath(denis.Id), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var text = await response.Content.ReadAsStringAsync(Ct);
        var person = JsonDocument.Parse(text).RootElement;
        (person.GetProperty("firstName").GetString(), person.GetProperty("middleName").GetString(), person.GetProperty("lastName").GetString(),
            person.GetProperty("displayName").GetString(), person.GetProperty("gender").GetString(), person.GetProperty("birthYear").GetInt32(),
            person.GetProperty("ageStatus").GetString(), person.GetProperty("isMinor").GetBoolean())
            .ShouldBe(("Denis", "Okot", "Opoka", "Denis Opoka", "MALE", 1990, "ADULT", false));
        (person.GetProperty("nationality").GetString(), person.GetProperty("secondaryNationality").GetString(),
            person.GetProperty("status").GetString(), person.GetProperty("source").GetString(), person.GetProperty("publicProfileEnabled").GetBoolean())
            .ShouldBe(("UG", "KE", "ACTIVE", "MANUAL", false));
        var alias = person.GetProperty("aliases").EnumerateArray().Single();
        (alias.GetProperty("name").GetString(), alias.GetProperty("kind").GetString()).ShouldBe(("Deno", "NICKNAME"));
        var identifier = person.GetProperty("externalIdentifiers").EnumerateArray().Single();
        (identifier.GetProperty("authority").GetString(), identifier.GetProperty("value").GetString()).ShouldBe(("KMSA_REG_NO", "KMSA-00417"));
        response.Headers.ETag.ShouldNotBeNull().Tag.ShouldBe($"\"{person.GetProperty("version").GetUInt32()}\"");

        person.TryGetProperty("dateOfBirth", out _).ShouldBeFalse();
        text.ShouldNotContain("1990-07-05");
        var list = await viewer.GetAsync(PeoplePath, Ct);
        (await list.Content.ReadAsStringAsync(Ct)).ShouldNotContain("1990-07-05");
    }

    [Fact]
    public async Task A_person_without_a_date_of_birth_has_no_birth_year_and_is_treated_as_a_minor()
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var ivan = await SavePersonAsync(App.Services, kmsa, IvanMukasa, Ct);

        var person = await registrar.ReadJsonAsync(PersonPath(ivan.Id), Ct);

        person.GetProperty("birthYear").ValueKind.ShouldBe(JsonValueKind.Null);
        (person.GetProperty("ageStatus").GetString(), person.GetProperty("isMinor").GetBoolean()).ShouldBe(("UNKNOWN", true));
        (await registrar.ReadJsonAsync(PrivatePath(ivan.Id), Ct)).GetProperty("dateOfBirth").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    // T1 acceptance: person.read_private is audited per read.
    [Fact]
    public async Task The_private_read_answers_the_date_of_birth_and_is_audited_every_time()
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var john = await SavePersonAsync(App.Services, kmsa, JohnSsekandi, Ct);
        await registrar.ReadJsonAsync(PersonPath(john.Id), Ct);
        await registrar.ReadJsonAsync(PeoplePath, Ct);

        using var first = await registrar.GetAsync(PrivatePath(john.Id), Ct);
        var second = await registrar.ReadJsonAsync(PrivatePath(john.Id), Ct);

        first.StatusCode.ShouldBe(HttpStatusCode.OK);
        first.Headers.CacheControl.ShouldNotBeNull().NoStore.ShouldBeTrue();
        (second.GetProperty("id").GetGuid(), second.GetProperty("dateOfBirth").GetString()).ShouldBe((john.Id, "2001-04-12"));
        var entries = await AuditEntriesAsync();
        entries.Select(e => (e.ActionCode, e.Outcome, e.TenantId, e.EntityType, e.EntityId, e.Metadata)).ShouldBe(
        [
            ("person.read_private", AuditOutcome.Success, kmsa, "Person", john.Id, null),
            ("person.read_private", AuditOutcome.Success, kmsa, "Person", john.Id, null),
        ]);
    }

    // S06 D3: PRIVATE values never reach logs or audit metadata.
    [Fact]
    public async Task The_date_of_birth_reaches_neither_the_logs_nor_the_audit_trail()
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var john = await SavePersonAsync(App.Services, kmsa, JohnSsekandi, Ct);
        App.Telemetry.Clear();

        await registrar.ReadJsonAsync(PrivatePath(john.Id), Ct);
        await registrar.ReadJsonAsync(PersonPath(john.Id), Ct);
        await registrar.ReadJsonAsync($"{PeoplePath}?ageStatus=ADULT&q=Ssekandi", Ct);
        using var missing = await registrar.GetAsync(PrivatePath(Guid.CreateVersion7()), Ct);

        await missing.ShouldBeProblemAsync(HttpStatusCode.NotFound, "PERSON_NOT_FOUND");
        var missingPath = missing.RequestMessage!.RequestUri!.AbsolutePath;
        await App.Telemetry.WaitForRequestLogAsync(missingPath);
        await App.Telemetry.WaitForServerSpanAsync(missingPath);
        var entries = await AuditEntriesAsync();
        entries.Count.ShouldBe(2);

        // The search text is a name: the query string is neither logged nor traced.
        foreach (var date in new[] { "2001-04-12", "04/12/2001", "12/04/2001", "2001/04/12", "Ssekandi" })
        {
            App.Telemetry.Events.ShouldAllBe(e =>
                !e.RenderMessage(null).Contains(date, StringComparison.Ordinal)
                && e.Properties.Values.All(value => !value.ToString().Contains(date, StringComparison.Ordinal))
                && (e.Exception == null || !e.Exception.ToString().Contains(date, StringComparison.Ordinal)));
            App.Telemetry.Spans.ShouldAllBe(span =>
                span.TagObjects.All(tag => !(tag.Value ?? "").ToString()!.Contains(date, StringComparison.Ordinal)));
            entries.ShouldAllBe(e => !JsonSerializer.Serialize(e, JsonSerializerOptions.Web).Contains(date, StringComparison.Ordinal));
        }
    }

    // T1 acceptance: a viewer gets 403 on the private read. D21's role table.
    [Theory]
    [InlineData(IdentityRoles.TenantOwner, true, true)]
    [InlineData(IdentityRoles.TenantAdmin, true, true)]
    [InlineData(IdentityRoles.Registrar, true, true)]
    [InlineData(IdentityRoles.CompetitionManager, true, false)]
    [InlineData(IdentityRoles.ResultsOfficer, true, false)]
    [InlineData(IdentityRoles.DataEntry, true, false)]
    [InlineData(IdentityRoles.Viewer, true, false)]
    [InlineData(IdentityRoles.Scorer, false, false)]
    public async Task Roles_read_people_and_only_some_read_private_data(string role, bool reads, bool readsPrivate)
    {
        var (kmsa, client) = await TenantAsync("kmsa", role);
        var john = await SavePersonAsync(App.Services, kmsa, JohnSsekandi, Ct);

        foreach (var path in new[] { PeoplePath, PersonPath(john.Id), StatusChangesPath(john.Id) })
        {
            using var response = await client.GetAsync(path, Ct);
            await ShouldBeAllowedAsync(response, reads);
        }

        using var privateRead = await client.GetAsync(PrivatePath(john.Id), Ct);
        await ShouldBeAllowedAsync(privateRead, readsPrivate);
        if (!readsPrivate)
        {
            var denied = (await AuditEntriesAsync()).Single(e => e.ActionCode == "person.read_private");
            (denied.Outcome, denied.ErrorCode).ShouldBe((AuditOutcome.Denied, "AUTH_FORBIDDEN"));
        }
    }

    [Fact]
    public async Task Another_tenants_person_is_not_found_and_not_listed()
    {
        var (kmsa, _) = await TenantAsync("kmsa", IdentityRoles.TenantOwner);
        var john = await SavePersonAsync(App.Services, kmsa, JohnSsekandi, Ct);
        var elssc = await TenantTestData.SaveTenantAsync(App.Services, "elssc", TenantStatus.Active, Ct);
        var agnes = await CreateMemberAsync(App.Services, elssc, "agnes.namutebi@elssc.example", Ct, TenantWide(IdentityRoles.TenantOwner));
        var client = App.ClientFor(agnes.UserId, elssc);

        (await client.ReadJsonAsync(PeoplePath, Ct)).Names().ShouldBeEmpty();
        foreach (var path in new[] { PersonPath(john.Id), PrivatePath(john.Id), StatusChangesPath(john.Id) })
        {
            using var response = await client.GetAsync(path, Ct);
            await response.ShouldBeProblemAsync(HttpStatusCode.NotFound, "PERSON_NOT_FOUND");
        }

        (await DirectoryAsync(elssc, john.Id)).ShouldBeEmpty();
    }

    [Fact]
    public async Task The_status_history_starts_with_the_persons_creation()
    {
        var (kmsa, viewer) = await TenantAsync("kmsa", IdentityRoles.Viewer);
        var john = await SavePersonAsync(App.Services, kmsa, JohnSsekandi, Ct);

        var history = await viewer.ReadJsonAsync(StatusChangesPath(john.Id), Ct);

        var created = history.GetProperty("items").EnumerateArray().Single();
        created.GetProperty("fromStatus").ValueKind.ShouldBe(JsonValueKind.Null);
        (created.GetProperty("toStatus").GetString(), created.GetProperty("actorKind").GetString(), created.GetProperty("occurredAt").GetDateTimeOffset())
            .ShouldBe(("ACTIVE", "SYSTEM", RegistryTestData.Now));
        created.GetProperty("reason").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task The_people_directory_answers_summaries_of_the_scopes_tenant()
    {
        var (kmsa, _) = await TenantAsync("kmsa", IdentityRoles.TenantOwner);
        var john = await SavePersonAsync(App.Services, kmsa, JohnSsekandi, Ct);
        var brenda = await SavePersonAsync(App.Services, kmsa, BrendaAkello, Ct);
        var ivan = await SavePersonAsync(App.Services, kmsa, IvanMukasa, Ct);
        await SetStatusAsync(App.Services, kmsa, ivan.Id, PersonStatus.Archived, Ct);

        var summaries = await DirectoryAsync(kmsa, john.Id, brenda.Id, ivan.Id, ivan.Id, Guid.CreateVersion7());

        summaries.ShouldBe(
        [
            new PersonSummary(john.Id, "John Ssekandi", ContractStatus.Active, AgeStatus.Adult),
            new PersonSummary(brenda.Id, "Brenda Akello", ContractStatus.Active, AgeStatus.Minor),
            new PersonSummary(ivan.Id, "Ivan Mukasa", ContractStatus.Archived, AgeStatus.Unknown),
        ], ignoreOrder: true);
        (await DirectoryAsync(kmsa)).ShouldBeEmpty();
    }

    private static async Task ShouldBeAllowedAsync(HttpResponseMessage response, bool allowed)
    {
        if (allowed)
        {
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
        }
        else
        {
            await response.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "AUTH_FORBIDDEN");
        }
    }

    private async Task<IReadOnlyList<PersonSummary>> DirectoryAsync(Guid tenantId, params Guid[] personIds)
    {
        await using var scope = App.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Set(tenantId);
        return await scope.ServiceProvider.GetRequiredService<IPeopleDirectory>().GetSummariesAsync(personIds, Ct);
    }

    private async Task<List<AuditEntry>> AuditEntriesAsync()
    {
        await AccountTestData.DeliverOutboxAsync(App.Services, Ct);
        return await AuditTestData.EntriesAsync(App.Services, Ct);
    }

    /// <summary>An active tenant (time zone Africa/Kampala) and a client of a member with <paramref name="role"/> tenant-wide.</summary>
    private async Task<(Guid Tenant, HttpClient Client)> TenantAsync(string slug, string role)
    {
        App.Authorization.UseIdentityRoles();
        var tenant = await TenantTestData.SaveTenantAsync(App.Services, slug, TenantStatus.Active, Ct);
        var member = await CreateMemberAsync(App.Services, tenant, MailboxOf(role, $"{slug}.example"), Ct, TenantWide(role));
        return (tenant, App.ClientFor(member.UserId, tenant));
    }
}

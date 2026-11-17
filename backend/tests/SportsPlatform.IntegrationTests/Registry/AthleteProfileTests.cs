using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
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
using SportsPlatform.Modules.Registry.Domain.People.Profiles;
using SportsPlatform.Modules.Tenancy.Contracts.Tenants;
using static SportsPlatform.IntegrationTests.Identity.MembershipTestData;
using static SportsPlatform.IntegrationTests.Registry.PeopleRequests;
using static SportsPlatform.IntegrationTests.Registry.RegistryTestData;
using static SportsPlatform.IntegrationTests.Tenancy.TestSportDefinitions;

namespace SportsPlatform.IntegrationTests.Registry;

/// <summary>
/// Athlete profiles over HTTP (S06-T4a; D9, D11, D12, D21, D22): adding, changing and moving them, the codes checked
/// against the tenant's sports and the definitions (test-only variants, <see cref="TestSportDefinitions"/>), one open
/// profile per variant, the archived and deceased guards, the version, the audit, the directory's filters, the duplicate
/// check's sports, the roles and the read-only tenant.
/// </summary>
public sealed class AthleteProfileTests(PostgresFixture fixture) : DatabaseTest(fixture)
{
    private static readonly string[] _wingAndStriker = ["TB_WING", "TB_STRIKER"];
    private static readonly string[] _anotherVariantsPosition = ["TH_CENTRE"];

    [Fact]
    public async Task A_profile_is_added_changed_and_moved_and_the_person_shows_it()
    {
        await using var app = AppWithTestSports(App);
        var (kmsa, registrar) = await TenantAsync(app, "kmsa", IdentityRoles.Registrar);
        await TenantTestData.EnableSportAsync(App.Services, kmsa, TestballFull, Ct);
        var person = await CreatePersonAsync(App.Services, kmsa, SystemActor.Instance, JohnSsekandi, overrideReason: null, Ct);
        var before = (await registrar.ReadJsonAsync(PersonPath(person), Ct)).GetProperty("version").GetUInt32();

        var added = await OkAsync(await registrar.PostAsJsonAsync(ProfilesPath(person), new
        {
            variant = TestballFull,
            primaryPosition = "TB_MID",
            otherPositions = _wingAndStriker,
            dominantSide = "RIGHT",
            publicName = " J.  Ssekandi ",
            version = before,
        }, Ct));

        added.GetProperty("version").GetUInt32().ShouldNotBe(before);
        var profile = added.GetProperty("athleteProfiles").EnumerateArray().ShouldHaveSingleItem();
        var profileId = profile.GetProperty("id").GetGuid();
        profile.GetProperty("variant").GetString().ShouldBe(TestballFull);
        profile.GetProperty("primaryPosition").GetString().ShouldBe("TB_MID");
        profile.GetProperty("otherPositions").EnumerateArray().Select(code => code.GetString()).ShouldBe(["TB_WING", "TB_STRIKER"]);
        profile.GetProperty("dominantSide").GetString().ShouldBe("RIGHT");
        profile.GetProperty("publicName").GetString().ShouldBe("J. Ssekandi");
        profile.GetProperty("status").GetString().ShouldBe("ACTIVE");

        // A whole replacement: what is left out is cleared.
        var updated = await OkAsync(await registrar.PutAsJsonAsync(ProfilePath(person, profileId), new { primaryPosition = "TB_KEEPER" }, Ct));
        updated.GetProperty("version").GetUInt32().ShouldNotBe(added.GetProperty("version").GetUInt32());
        var changed = updated.GetProperty("athleteProfiles").EnumerateArray().ShouldHaveSingleItem();
        changed.GetProperty("primaryPosition").GetString().ShouldBe("TB_KEEPER");
        changed.GetProperty("otherPositions").GetArrayLength().ShouldBe(0);
        changed.GetProperty("dominantSide").ValueKind.ShouldBe(JsonValueKind.Null);
        changed.GetProperty("publicName").ValueKind.ShouldBe(JsonValueKind.Null);

        var retired = await OkAsync(await registrar.PostAsJsonAsync(
            StatusPath(person, profileId), new { status = "RETIRED", reason = " Retired after the season " }, Ct));
        retired.GetProperty("athleteProfiles")[0].GetProperty("status").GetString().ShouldBe("RETIRED");
        retired.GetProperty("version").GetUInt32().ShouldNotBe(updated.GetProperty("version").GetUInt32());
        await OkAsync(await registrar.PostAsJsonAsync(StatusPath(person, profileId), new { status = "ACTIVE", reason = "Came back" }, Ct));

        await using (var scope = ScopeFor(App.Services, kmsa, out var context))
        {
            var rows = await context.Set<ProfileStatusChange>().Where(c => c.ProfileId == profileId).OrderBy(c => c.Id).ToListAsync(Ct);
            rows.Select(c => (c.ProfileKind, c.FromStatus, c.ToStatus, c.Reason)).ShouldBe(
            [
                (ProfileKind.Athlete, null, ProfileStatus.Active, null),
                (ProfileKind.Athlete, ProfileStatus.Active, ProfileStatus.Retired, "Retired after the season"),
                (ProfileKind.Athlete, ProfileStatus.Retired, ProfileStatus.Active, "Came back"),
            ]);
            rows.ShouldAllBe(c => c.PersonId == person && c.OccurredAt == App.Clock.GetUtcNow());
        }

        await AccountTestData.DeliverOutboxAsync(App.Services, Ct);
        var entries = await AuditTestData.EntriesAsync(App.Services, Ct);
        var create = entries.Single(e => e.ActionCode == "athlete_profile.create");
        (create.Outcome, create.EntityType, create.EntityId).ShouldBe((AuditOutcome.Success, "Person", (Guid?)person));
        var metadata = JsonDocument.Parse(create.Metadata!).RootElement;
        (metadata.GetProperty("profileId").GetGuid(), metadata.GetProperty("variant").GetString()).ShouldBe((profileId, TestballFull));
        create.Metadata!.ShouldNotContain("Ssekandi");
        entries.Single(e => e.ActionCode == "athlete_profile.update").Outcome.ShouldBe(AuditOutcome.Success);
        var moves = entries.Where(e => e.ActionCode == "athlete_profile.status.change").OrderBy(e => e.OccurredAt).ThenBy(e => e.Id).ToList();
        moves.Select(e => e.Reason).ShouldBe(["Retired after the season", "Came back"], ignoreOrder: true);
        moves.Select(e => JsonDocument.Parse(e.Metadata!).RootElement.GetProperty("toStatus").GetString()).ShouldBe(["RETIRED", "ACTIVE"], ignoreOrder: true);
    }

    // T4 acceptance: a variant the tenant hasn't enabled, or a position the definition doesn't have, is refused with its code.
    [Fact]
    public async Task A_variant_not_enabled_or_a_position_not_in_the_definition_is_refused()
    {
        await using var app = AppWithTestSports(App);
        var (kmsa, registrar) = await TenantAsync(app, "kmsa", IdentityRoles.Registrar);
        await TenantTestData.EnableSportAsync(App.Services, kmsa, TestballFull, Ct);
        var person = await CreatePersonAsync(App.Services, kmsa, SystemActor.Instance, DenisOpoka, overrideReason: null, Ct);

        foreach (var variant in new[] { Testhoop, "NOT_A_VARIANT" })
        {
            using var refused = await registrar.PostAsJsonAsync(ProfilesPath(person), new { variant }, Ct);
            var problem = await refused.ShouldBeProblemAsync(HttpStatusCode.UnprocessableEntity, "PERSON_SPORT_NOT_ENABLED");
            Fields(problem).ShouldBe([("variant", "PERSON_SPORT_NOT_ENABLED")]);
        }

        // Another variant's position is no position of this one.
        foreach (var (body, field) in new (object, string)[]
        {
            (new { variant = TestballFull, primaryPosition = "TH_GUARD" }, "primaryPosition"),
            (new { variant = TestballFull, primaryPosition = "TB_MID", otherPositions = new[] { "TB_WING", "TB_GOALIE" } }, "otherPositions[1]"),
        })
        {
            using var refused = await registrar.PostAsJsonAsync(ProfilesPath(person), body, Ct);
            var problem = await refused.ShouldBeProblemAsync(HttpStatusCode.UnprocessableEntity, "PERSON_POSITION_UNKNOWN");
            Fields(problem).ShouldBe([(field, "PERSON_POSITION_UNKNOWN")]);
        }

        var profileId = await AddProfileAsync(registrar, person, TestballFull);
        using (var update = await registrar.PutAsJsonAsync(ProfilePath(person, profileId), new { otherPositions = _anotherVariantsPosition }, Ct))
        {
            var problem = await update.ShouldBeProblemAsync(HttpStatusCode.UnprocessableEntity, "PERSON_POSITION_UNKNOWN");
            Fields(problem).ShouldBe([("otherPositions[0]", "PERSON_POSITION_UNKNOWN")]);
        }

        // The shape is checked first: codes as definitions write them, distinct, at most five, never the primary one.
        foreach (var body in new object[]
        {
            new { variant = "testball_full" },
            new { variant = TestballFull, primaryPosition = "tb_mid" },
            new { variant = TestballFull, primaryPosition = "TB_MID", otherPositions = new[] { "TB_MID" } },
            new { variant = TestballFull, otherPositions = new[] { "TB_WING", "TB_WING" } },
            new { variant = TestballFull, otherPositions = TestballPositions.Append("TB_EXTRA").ToArray() },
            new { variant = TestballFull, publicName = new string('J', 201) },
        })
        {
            using var invalid = await registrar.PostAsJsonAsync(ProfilesPath(person), body, Ct);
            await invalid.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_INVALID_REQUEST");
        }

        using var unknownMember = await registrar.PostAsJsonAsync(ProfilesPath(person), new { variant = TestballFull, status = "INACTIVE" }, Ct);
        await unknownMember.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_MALFORMED_REQUEST");
    }

    // T4 acceptance: an archived person gets no profile (and a deceased one neither).
    [Theory]
    [InlineData(PersonTransition.Archive, "PERSON_ARCHIVED")]
    [InlineData(PersonTransition.MarkDeceased, "PERSON_DECEASED")]
    public async Task An_archived_or_deceased_person_gets_no_profile_and_none_back_to_active(PersonTransition transition, string code)
    {
        await using var app = AppWithTestSports(App);
        var (kmsa, registrar) = await TenantAsync(app, "kmsa", IdentityRoles.Registrar);
        await TenantTestData.EnableSportAsync(App.Services, kmsa, TestballFull, Ct);
        await TenantTestData.EnableSportAsync(App.Services, kmsa, TestballShort, Ct);
        var person = await CreatePersonAsync(App.Services, kmsa, SystemActor.Instance, IvanMukasa, overrideReason: null, Ct);
        var profileId = await AddProfileAsync(registrar, person, TestballFull);
        await OkAsync(await registrar.PostAsJsonAsync(StatusPath(person, profileId), new { status = "INACTIVE", reason = "Injured" }, Ct));
        await ChangeStatusAsync(App.Services, kmsa, person, transition, Ct);

        using (var added = await registrar.PostAsJsonAsync(ProfilesPath(person), new { variant = TestballShort }, Ct))
        {
            await added.ShouldBeProblemAsync(HttpStatusCode.Conflict, code);
        }

        using (var reactivated = await registrar.PostAsJsonAsync(StatusPath(person, profileId), new { status = "ACTIVE", reason = "Back" }, Ct))
        {
            await reactivated.ShouldBeProblemAsync(HttpStatusCode.Conflict, code);
        }

        // Corrections and closing moves stay allowed.
        await OkAsync(await registrar.PutAsJsonAsync(ProfilePath(person, profileId), new { primaryPosition = "TB_BACK" }, Ct));
        var closed = await OkAsync(await registrar.PostAsJsonAsync(StatusPath(person, profileId), new { status = "RETIRED", reason = "Retired" }, Ct));
        closed.GetProperty("athleteProfiles").EnumerateArray().ShouldHaveSingleItem().GetProperty("status").GetString().ShouldBe("RETIRED");
    }

    [Fact]
    public async Task A_person_holds_one_open_profile_per_variant_and_profiles_of_several_variants()
    {
        await using var app = AppWithTestSports(App);
        var (kmsa, registrar) = await TenantAsync(app, "kmsa", IdentityRoles.Registrar);
        foreach (var variant in new[] { TestballFull, TestballShort, Testhoop })
        {
            await TenantTestData.EnableSportAsync(App.Services, kmsa, variant, Ct);
        }

        var person = await CreatePersonAsync(App.Services, kmsa, SystemActor.Instance, DenisOpoka, overrideReason: null, Ct);
        var full = await AddProfileAsync(registrar, person, TestballFull);
        await AddProfileAsync(registrar, person, TestballShort);
        var hoop = await OkAsync(await registrar.PostAsJsonAsync(ProfilesPath(person), new { variant = Testhoop, primaryPosition = "TH_CENTRE" }, Ct));
        hoop.GetProperty("athleteProfiles").EnumerateArray().Select(p => p.GetProperty("variant").GetString())
            .ShouldBe([TestballFull, TestballShort, Testhoop]);

        foreach (var status in new[] { "ACTIVE", "RETIRED" })
        {
            if (status == "RETIRED")
            {
                await OkAsync(await registrar.PostAsJsonAsync(StatusPath(person, full), new { status, reason = "Retired" }, Ct));
            }

            using var again = await registrar.PostAsJsonAsync(ProfilesPath(person), new { variant = TestballFull }, Ct);
            var problem = await again.ShouldBeProblemAsync(HttpStatusCode.Conflict, "PERSON_ATHLETE_PROFILE_EXISTS");
            Fields(problem).ShouldBe([("variant", "PERSON_ATHLETE_PROFILE_EXISTS")]);
        }

        // An archived profile is closed for good: a new one for the variant may start, and the old one takes no change.
        await OkAsync(await registrar.PostAsJsonAsync(StatusPath(person, full), new { status = "ARCHIVED", reason = "Recorded twice" }, Ct));
        await AddProfileAsync(registrar, person, TestballFull);
        using (var update = await registrar.PutAsJsonAsync(ProfilePath(person, full), new { primaryPosition = "TB_MID" }, Ct))
        {
            await update.ShouldBeProblemAsync(HttpStatusCode.Conflict, "PERSON_PROFILE_ARCHIVED");
        }

        using var back = await registrar.PostAsJsonAsync(StatusPath(person, full), new { status = "ACTIVE", reason = "Undo" }, Ct);
        await back.ShouldBeProblemAsync(HttpStatusCode.Conflict, "PERSON_PROFILE_STATUS_INVALID");
    }

    // S05 D12: disabling a variant keeps its profiles (they can still be corrected) but takes no new one.
    [Fact]
    public async Task A_disabled_variant_keeps_its_profiles_but_takes_no_new_one()
    {
        await using var app = AppWithTestSports(App);
        var (kmsa, registrar) = await TenantAsync(app, "kmsa", IdentityRoles.Registrar);
        await TenantTestData.EnableSportAsync(App.Services, kmsa, TestballFull, Ct);
        var denis = await CreatePersonAsync(App.Services, kmsa, SystemActor.Instance, DenisOpoka, overrideReason: null, Ct);
        var ivan = await CreatePersonAsync(App.Services, kmsa, SystemActor.Instance, IvanMukasa, overrideReason: null, Ct);
        var profileId = await AddProfileAsync(registrar, denis, TestballFull);

        await TenantTestData.DisableSportAsync(App.Services, kmsa, TestballFull, Ct);

        var corrected = await OkAsync(await registrar.PutAsJsonAsync(ProfilePath(denis, profileId), new { primaryPosition = "TB_WING" }, Ct));
        corrected.GetProperty("athleteProfiles")[0].GetProperty("primaryPosition").GetString().ShouldBe("TB_WING");
        using var refused = await registrar.PostAsJsonAsync(ProfilesPath(ivan), new { variant = TestballFull }, Ct);
        await refused.ShouldBeProblemAsync(HttpStatusCode.UnprocessableEntity, "PERSON_SPORT_NOT_ENABLED");
    }

    [Fact]
    public async Task A_move_needs_a_status_and_a_reason_and_a_profile_of_this_person()
    {
        await using var app = AppWithTestSports(App);
        var (kmsa, registrar) = await TenantAsync(app, "kmsa", IdentityRoles.Registrar);
        await TenantTestData.EnableSportAsync(App.Services, kmsa, TestballFull, Ct);
        var person = await CreatePersonAsync(App.Services, kmsa, SystemActor.Instance, DenisOpoka, overrideReason: null, Ct);
        var other = await CreatePersonAsync(App.Services, kmsa, SystemActor.Instance, IvanMukasa, overrideReason: null, Ct);
        var profileId = await AddProfileAsync(registrar, person, TestballFull);

        using (var blank = await registrar.PostAsJsonAsync(StatusPath(person, profileId), new { status = "INACTIVE", reason = " " }, Ct))
        {
            var problem = await blank.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_INVALID_REQUEST");
            Fields(problem).ShouldBe([("reason", "VALIDATION_REASON_REQUIRED")]);
        }

        using (var noStatus = await registrar.PostAsJsonAsync(StatusPath(person, profileId), new { reason = "Injured" }, Ct))
        {
            var problem = await noStatus.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_INVALID_REQUEST");
            Fields(problem).ShouldBe([("status", "VALIDATION_INVALID_REQUEST")]);
        }

        using (var same = await registrar.PostAsJsonAsync(StatusPath(person, profileId), new { status = "ACTIVE", reason = "Again" }, Ct))
        {
            await same.ShouldBeProblemAsync(HttpStatusCode.Conflict, "PERSON_PROFILE_STATUS_INVALID");
        }

        foreach (var (owner, id) in new[] { (other, profileId), (person, Guid.CreateVersion7()) })
        {
            using var moved = await registrar.PostAsJsonAsync(StatusPath(owner, id), new { status = "INACTIVE", reason = "Injured" }, Ct);
            await moved.ShouldBeProblemAsync(HttpStatusCode.NotFound, "PERSON_PROFILE_NOT_FOUND");
            using var updated = await registrar.PutAsJsonAsync(ProfilePath(owner, id), new { }, Ct);
            await updated.ShouldBeProblemAsync(HttpStatusCode.NotFound, "PERSON_PROFILE_NOT_FOUND");
        }
    }

    // D11: a profile is a child of the person, so a profile change against an older version of the person conflicts.
    [Fact]
    public async Task A_profile_change_on_an_older_version_of_the_person_conflicts()
    {
        await using var app = AppWithTestSports(App);
        var (kmsa, registrar) = await TenantAsync(app, "kmsa", IdentityRoles.Registrar);
        await TenantTestData.EnableSportAsync(App.Services, kmsa, TestballFull, Ct);
        var person = await CreatePersonAsync(App.Services, kmsa, SystemActor.Instance, DenisOpoka, overrideReason: null, Ct);
        var read = await registrar.ReadJsonAsync(PersonPath(person), Ct);
        var version = read.GetProperty("version").GetUInt32();
        await OkAsync(await registrar.PostAsJsonAsync($"{PersonPath(person)}/aliases", new { name = "Deno", kind = "NICKNAME" }, Ct));

        using (var stale = await registrar.PostAsJsonAsync(ProfilesPath(person), new { variant = TestballFull, version }, Ct))
        {
            await stale.ShouldBeProblemAsync(HttpStatusCode.Conflict, "PERSON_CONCURRENCY_CONFLICT");
        }

        var profileId = await AddProfileAsync(registrar, person, TestballFull);
        using var request = new HttpRequestMessage(HttpMethod.Post, StatusPath(person, profileId))
        {
            Content = JsonContent.Create(new { status = "INACTIVE", reason = "Injured" }),
        };
        request.Headers.IfMatch.Add(new($"\"{version}\""));
        using var ifMatch = await registrar.SendAsync(request, Ct);
        await ifMatch.ShouldBeProblemAsync(HttpStatusCode.Conflict, "PERSON_CONCURRENCY_CONFLICT");
    }

    [Fact]
    public async Task The_directory_filters_by_profile_and_variant_and_the_check_shows_the_variants()
    {
        await using var app = AppWithTestSports(App);
        var (kmsa, registrar) = await TenantAsync(app, "kmsa", IdentityRoles.Registrar);
        await TenantTestData.EnableSportAsync(App.Services, kmsa, TestballFull, Ct);
        await TenantTestData.EnableSportAsync(App.Services, kmsa, Testhoop, Ct);
        var john = await CreatePersonAsync(App.Services, kmsa, SystemActor.Instance, JohnSsekandi, overrideReason: null, Ct);
        var denis = await CreatePersonAsync(App.Services, kmsa, SystemActor.Instance, DenisOpoka, overrideReason: null, Ct);
        await CreatePersonAsync(App.Services, kmsa, SystemActor.Instance, IvanMukasa, overrideReason: null, Ct);
        await AddProfileAsync(registrar, john, TestballFull);
        await AddProfileAsync(registrar, john, Testhoop);
        var archived = await AddProfileAsync(registrar, denis, Testhoop);
        await OkAsync(await registrar.PostAsJsonAsync(StatusPath(denis, archived), new { status = "ARCHIVED", reason = "Recorded by mistake" }, Ct));

        (await registrar.ReadJsonAsync($"{PeoplePath}?profile=ATHLETE", Ct)).Names().ShouldBe(["John Ssekandi"]);
        (await registrar.ReadJsonAsync($"{PeoplePath}?variant={Testhoop}", Ct)).Names().ShouldBe(["John Ssekandi"]);
        (await registrar.ReadJsonAsync($"{PeoplePath}?profile=ATHLETE&variant={TestballShort}", Ct)).Names().ShouldBeEmpty();
        (await registrar.ReadJsonAsync(PeoplePath, Ct)).Names().ShouldBe(["Ivan Mukasa", "Denis Opoka", "John Ssekandi"]);
        foreach (var query in new[] { "profile=COACH", "profile=athlete", "variant=testhoop_full", $"variant={new string('A', 51)}" })
        {
            using var refused = await registrar.GetAsync($"{PeoplePath}?{query}", Ct);
            await refused.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_INVALID_QUERY");
        }

        var check = await CheckAsync(registrar, new { firstName = "John", lastName = "Ssekandi", dateOfBirth = JohnSsekandi.DateOfBirth });
        check.GetProperty("candidates").EnumerateArray().ShouldHaveSingleItem().GetProperty("athleteVariants").EnumerateArray()
            .Select(variant => variant.GetString()).ShouldBe([TestballFull, Testhoop]);
        var denisCheck = await CheckAsync(registrar, new { firstName = "Denis", lastName = "Opoka", middleName = "Okot" });
        denisCheck.GetProperty("candidates").EnumerateArray().ShouldHaveSingleItem().GetProperty("athleteVariants").GetArrayLength().ShouldBe(0);
    }

    [Fact]
    public async Task Another_tenants_person_is_not_found()
    {
        await using var app = AppWithTestSports(App);
        var (_, registrar) = await TenantAsync(app, "kmsa", IdentityRoles.Registrar);
        var (elssc, theirRegistrar) = await TenantAsync(app, "elssc", IdentityRoles.Registrar);
        await TenantTestData.EnableSportAsync(App.Services, elssc, TestballFull, Ct);
        var theirs = await CreatePersonAsync(App.Services, elssc, SystemActor.Instance, IvanMukasa, overrideReason: null, Ct);
        var profileId = await AddProfileAsync(theirRegistrar, theirs, TestballFull);

        foreach (var (method, path, body) in Requests(theirs, profileId))
        {
            using var response = await registrar.SendAsync(new HttpRequestMessage(method, path) { Content = JsonContent.Create(body) }, Ct);
            await response.ShouldBeProblemAsync(HttpStatusCode.NotFound, "PERSON_NOT_FOUND");
        }
    }

    // D21: profiles are changed by the owner, the admin and the registrar.
    [Theory]
    [InlineData(IdentityRoles.TenantOwner, true)]
    [InlineData(IdentityRoles.TenantAdmin, true)]
    [InlineData(IdentityRoles.Registrar, true)]
    [InlineData(IdentityRoles.CompetitionManager, false)]
    [InlineData(IdentityRoles.ResultsOfficer, false)]
    [InlineData(IdentityRoles.DataEntry, false)]
    [InlineData(IdentityRoles.Viewer, false)]
    [InlineData(IdentityRoles.Scorer, false)]
    public async Task Only_those_who_change_people_may_change_profiles(string role, bool allowed)
    {
        await using var app = AppWithTestSports(App);
        var (kmsa, client) = await TenantAsync(app, "kmsa", role);
        await TenantTestData.EnableSportAsync(App.Services, kmsa, TestballFull, Ct);
        await TenantTestData.EnableSportAsync(App.Services, kmsa, TestballShort, Ct);
        var person = await SavePersonWithProfileAsync(kmsa, TestballFull);

        foreach (var (method, path, body) in Requests(person.Id, person.AthleteProfiles.Single().Id))
        {
            using var response = await client.SendAsync(new HttpRequestMessage(method, path) { Content = JsonContent.Create(body) }, Ct);
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
    public async Task A_read_only_tenant_changes_no_profile()
    {
        await using var app = AppWithTestSports(App);
        App.Authorization.UseIdentityRoles();
        var kmsa = await TenantTestData.SaveTenantAsync(
            App.Services, "kmsa", TenantStatus.Active, publicEnabled: false, withDomain: true, withSubscription: false, Ct);
        await TenantTestData.SavePeriodAsync(App.Services, kmsa, new DateOnly(2025, 1, 1), new DateOnly(2025, 12, 31), Ct, cancelReason: "Pilot paused");
        var person = await SavePersonAsync(App.Services, kmsa, IvanMukasa, Ct);
        var member = await CreateMemberAsync(App.Services, kmsa, "brian.ssemakula@kmsa.example", Ct, TenantWide(IdentityRoles.Registrar));
        using var registrar = ClientFor(app, member.UserId, kmsa);

        foreach (var (method, path, body) in Requests(person.Id, Guid.CreateVersion7()))
        {
            using var response = await registrar.SendAsync(new HttpRequestMessage(method, path) { Content = JsonContent.Create(body) }, Ct);
            await response.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "TENANT_READ_ONLY");
        }
    }

    // The three requests, in the order that succeeds on a person with an active Testball profile and Testball sevens enabled.
    private static (HttpMethod Method, string Path, object Body)[] Requests(Guid personId, Guid profileId) =>
    [
        (HttpMethod.Post, ProfilesPath(personId), new { variant = TestballShort }),
        (HttpMethod.Put, ProfilePath(personId, profileId), new { primaryPosition = "TB_MID" }),
        (HttpMethod.Post, StatusPath(personId, profileId), new { status = "INACTIVE", reason = "Injured" }),
    ];

    private static string ProfilesPath(Guid personId) => $"{PersonPath(personId)}/athlete-profiles";

    private static string ProfilePath(Guid personId, Guid profileId) => $"{ProfilesPath(personId)}/{profileId}";

    private static string StatusPath(Guid personId, Guid profileId) => $"{ProfilePath(personId, profileId)}/status";

    private static async Task<Guid> AddProfileAsync(HttpClient client, Guid personId, string variant)
    {
        var person = await OkAsync(await client.PostAsJsonAsync(ProfilesPath(personId), new { variant }, Ct));
        return person.GetProperty("athleteProfiles").EnumerateArray()
            .Single(p => p.GetProperty("variant").GetString() == variant && p.GetProperty("status").GetString() == "ACTIVE")
            .GetProperty("id").GetGuid();
    }

    private async Task<Person> SavePersonWithProfileAsync(Guid tenantId, string variant)
    {
        await using var scope = ScopeFor(App.Services, tenantId, out var context);
        var person = NewPerson(IvanMukasa);
        person.AddAthleteProfile(variant, new AthleteProfileDetails("TB_BACK", [], null, null), SystemActor.Instance, Now).IsSuccess.ShouldBeTrue();
        context.Add(person);
        await context.SaveChangesAsync(Ct);
        return person;
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

    private static HttpClient ClientFor(WebApplicationFactory<Program> app, Guid userId, Guid tenantId)
    {
        var client = app.CreateClient().AsUser(userId);
        client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.ToString());
        return client;
    }

    /// <summary>An active tenant (time zone Africa/Kampala) and a client of a member with <paramref name="role"/> tenant-wide.</summary>
    private async Task<(Guid Tenant, HttpClient Client)> TenantAsync(WebApplicationFactory<Program> app, string slug, string role)
    {
        App.Authorization.UseIdentityRoles();
        var tenant = await TenantTestData.SaveTenantAsync(App.Services, slug, TenantStatus.Active, Ct);
        var member = await CreateMemberAsync(App.Services, tenant, MailboxOf(role, $"{slug}.example"), Ct, TenantWide(role));
        return (tenant, ClientFor(app, member.UserId, tenant));
    }
}

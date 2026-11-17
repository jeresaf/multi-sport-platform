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
/// Staff profiles over HTTP (S06-T4b1; D9, D11, D12, D21, D22): adding one, replacing its capabilities and moving it, the
/// codes checked against the tenant's sports and the definitions (test-only variants, <see cref="TestSportDefinitions"/>),
/// one open staff profile per person, the archived and deceased guards, the version, the audit, the directory's filters,
/// the roles and the read-only tenant.
/// </summary>
public sealed class StaffProfileTests(PostgresFixture fixture) : DatabaseTest(fixture)
{
    [Fact]
    public async Task A_profile_is_added_changed_and_moved_and_the_person_shows_it()
    {
        await using var app = AppWithTestSports(App);
        var (kmsa, registrar) = await TenantAsync(app, "kmsa", IdentityRoles.Registrar);
        await TenantTestData.EnableSportAsync(App.Services, kmsa, TestballFull, Ct);
        await TenantTestData.EnableSportAsync(App.Services, kmsa, Testhoop, Ct);
        var person = await CreatePersonAsync(App.Services, kmsa, SystemActor.Instance, DenisOpoka, overrideReason: null, Ct);
        var before = (await registrar.ReadJsonAsync(PersonPath(person), Ct)).GetProperty("version").GetUInt32();

        var added = await OkAsync(await registrar.PostAsJsonAsync(ProfilesPath(person), new
        {
            capabilities = new object[]
            {
                new { variant = Testhoop, role = "TH_COACH" },
                new { variant = TestballFull, role = "TB_COACH", qualification = " Level 2   coaching " },
            },
            version = before,
        }, Ct));

        added.GetProperty("version").GetUInt32().ShouldNotBe(before);
        var profile = added.GetProperty("staffProfiles").EnumerateArray().ShouldHaveSingleItem();
        var profileId = profile.GetProperty("id").GetGuid();
        profile.GetProperty("status").GetString().ShouldBe("ACTIVE");
        Capabilities(profile).ShouldBe([(TestballFull, "TB_COACH", "Level 2 coaching"), (Testhoop, "TH_COACH", null)]);

        // A whole replacement: one left out is removed, one held keeps going with the new qualification.
        var updated = await OkAsync(await registrar.PutAsJsonAsync(ProfilePath(person, profileId), new
        {
            capabilities = new object[]
            {
                new { variant = TestballFull, role = "TB_COACH" },
                new { variant = TestballFull, role = "TB_PHYSIO", qualification = "Sports physiotherapy" },
            },
        }, Ct));
        updated.GetProperty("version").GetUInt32().ShouldNotBe(added.GetProperty("version").GetUInt32());
        Capabilities(updated.GetProperty("staffProfiles")[0])
            .ShouldBe([(TestballFull, "TB_COACH", null), (TestballFull, "TB_PHYSIO", "Sports physiotherapy")]);

        var retired = await OkAsync(await registrar.PostAsJsonAsync(
            StatusPath(person, profileId), new { status = "RETIRED", reason = " Stepped down after the season " }, Ct));
        retired.GetProperty("staffProfiles")[0].GetProperty("status").GetString().ShouldBe("RETIRED");
        retired.GetProperty("version").GetUInt32().ShouldNotBe(updated.GetProperty("version").GetUInt32());
        await OkAsync(await registrar.PostAsJsonAsync(StatusPath(person, profileId), new { status = "ACTIVE", reason = "Came back" }, Ct));

        await using (var scope = ScopeFor(App.Services, kmsa, out var context))
        {
            var rows = await context.Set<ProfileStatusChange>().Where(c => c.ProfileId == profileId).OrderBy(c => c.Id).ToListAsync(Ct);
            rows.Select(c => (c.ProfileKind, c.FromStatus, c.ToStatus, c.Reason)).ShouldBe(
            [
                (ProfileKind.Staff, null, ProfileStatus.Active, null),
                (ProfileKind.Staff, ProfileStatus.Active, ProfileStatus.Retired, "Stepped down after the season"),
                (ProfileKind.Staff, ProfileStatus.Retired, ProfileStatus.Active, "Came back"),
            ]);
            (await context.Set<StaffCapability>().CountAsync(Ct)).ShouldBe(2);
        }

        await AccountTestData.DeliverOutboxAsync(App.Services, Ct);
        var entries = await AuditTestData.EntriesAsync(App.Services, Ct);
        var create = entries.Single(e => e.ActionCode == "staff_profile.create");
        (create.Outcome, create.EntityType, create.EntityId).ShouldBe((AuditOutcome.Success, "Person", (Guid?)person));
        var metadata = JsonDocument.Parse(create.Metadata!).RootElement;
        metadata.GetProperty("profileId").GetGuid().ShouldBe(profileId);
        metadata.GetProperty("variants").EnumerateArray().Select(v => v.GetString()).ShouldBe([TestballFull, Testhoop]);
        create.Metadata!.ShouldNotContain("TB_COACH");
        create.Metadata!.ShouldNotContain("coaching");
        entries.Single(e => e.ActionCode == "staff_profile.update").Outcome.ShouldBe(AuditOutcome.Success);
        var moves = entries.Where(e => e.ActionCode == "staff_profile.status.change").ToList();
        moves.Select(e => e.Reason).ShouldBe(["Stepped down after the season", "Came back"], ignoreOrder: true);
        moves.Select(e => JsonDocument.Parse(e.Metadata!).RootElement.GetProperty("toStatus").GetString())
            .ShouldBe(["RETIRED", "ACTIVE"], ignoreOrder: true);
    }

    // T4 acceptance: a variant the tenant hasn't enabled, or a role code the definition doesn't have, is refused with its code.
    [Fact]
    public async Task A_variant_not_enabled_or_a_role_not_in_the_definition_is_refused()
    {
        await using var app = AppWithTestSports(App);
        var (kmsa, registrar) = await TenantAsync(app, "kmsa", IdentityRoles.Registrar);
        await TenantTestData.EnableSportAsync(App.Services, kmsa, TestballFull, Ct);
        var person = await CreatePersonAsync(App.Services, kmsa, SystemActor.Instance, DenisOpoka, overrideReason: null, Ct);

        foreach (var variant in new[] { Testhoop, "NOT_A_VARIANT" })
        {
            using var refused = await registrar.PostAsJsonAsync(ProfilesPath(person), Body((TestballFull, "TB_COACH"), (variant, "TH_COACH")), Ct);
            var problem = await refused.ShouldBeProblemAsync(HttpStatusCode.UnprocessableEntity, "PERSON_SPORT_NOT_ENABLED");
            Fields(problem).ShouldBe([("capabilities[1].variant", "PERSON_SPORT_NOT_ENABLED")]);
        }

        // Another variant's role, or a position, is no staff role of this one.
        foreach (var role in new[] { "TH_COACH", "TB_MID" })
        {
            using var refused = await registrar.PostAsJsonAsync(ProfilesPath(person), Body((TestballFull, role)), Ct);
            var problem = await refused.ShouldBeProblemAsync(HttpStatusCode.UnprocessableEntity, "PERSON_ROLE_UNKNOWN");
            Fields(problem).ShouldBe([("capabilities[0].role", "PERSON_ROLE_UNKNOWN")]);
        }

        var profileId = await AddProfileAsync(registrar, person, (TestballFull, "TB_COACH"));
        using (var update = await registrar.PutAsJsonAsync(ProfilePath(person, profileId), Body((TestballFull, "TB_COACH"), (TestballFull, "TB_REFEREE")), Ct))
        {
            var problem = await update.ShouldBeProblemAsync(HttpStatusCode.UnprocessableEntity, "PERSON_ROLE_UNKNOWN");
            Fields(problem).ShouldBe([("capabilities[1].role", "PERSON_ROLE_UNKNOWN")]);
        }

        // The shape is checked first: 1–10 capabilities, codes as definitions write them, each pair once, a short qualification.
        foreach (var body in new object[]
        {
            new { },
            new { capabilities = Array.Empty<object>() },
            new { capabilities = new object?[] { null } },
            Body([.. Enumerable.Range(0, 11).Select(i => (TestballFull, $"TB_ROLE_{i}"))]),
            Body((TestballFull, "TB_COACH"), (TestballFull, "TB_COACH")),
            Body(("testball_full", "TB_COACH")),
            Body((TestballFull, "tb_coach")),
            new { capabilities = new[] { new { variant = TestballFull } } },
            new { capabilities = new[] { new { variant = TestballFull, role = "TB_COACH", qualification = new string('Q', 101) } } },
        })
        {
            using var invalid = await registrar.PostAsJsonAsync(ProfilesPath(person), body, Ct);
            await invalid.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_INVALID_REQUEST");
        }

        using var unknownMember = await registrar.PostAsJsonAsync(ProfilesPath(person), new
        {
            capabilities = new[] { new { variant = TestballFull, role = "TB_COACH", grade = "A" } },
        }, Ct);
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
        var ivan = await CreatePersonAsync(App.Services, kmsa, SystemActor.Instance, IvanMukasa, overrideReason: null, Ct);
        var denis = await CreatePersonAsync(App.Services, kmsa, SystemActor.Instance, DenisOpoka, overrideReason: null, Ct);
        var profileId = await AddProfileAsync(registrar, denis, (TestballFull, "TB_COACH"));
        await OkAsync(await registrar.PostAsJsonAsync(StatusPath(denis, profileId), new { status = "INACTIVE", reason = "On leave" }, Ct));
        await ChangeStatusAsync(App.Services, kmsa, ivan, transition, Ct);
        await ChangeStatusAsync(App.Services, kmsa, denis, transition, Ct);

        using (var added = await registrar.PostAsJsonAsync(ProfilesPath(ivan), Body((TestballFull, "TB_COACH")), Ct))
        {
            await added.ShouldBeProblemAsync(HttpStatusCode.Conflict, code);
        }

        using (var reactivated = await registrar.PostAsJsonAsync(StatusPath(denis, profileId), new { status = "ACTIVE", reason = "Back" }, Ct))
        {
            await reactivated.ShouldBeProblemAsync(HttpStatusCode.Conflict, code);
        }

        // Corrections and closing moves stay allowed.
        await OkAsync(await registrar.PutAsJsonAsync(ProfilePath(denis, profileId), Body((TestballFull, "TB_PHYSIO")), Ct));
        var closed = await OkAsync(await registrar.PostAsJsonAsync(StatusPath(denis, profileId), new { status = "RETIRED", reason = "Retired" }, Ct));
        closed.GetProperty("staffProfiles").EnumerateArray().ShouldHaveSingleItem().GetProperty("status").GetString().ShouldBe("RETIRED");
    }

    // T4 acceptance (staff part; T4b2 adds the official profile): one person holds athlete and staff profiles at once.
    [Fact]
    public async Task A_person_holds_one_open_staff_profile_beside_athlete_profiles()
    {
        await using var app = AppWithTestSports(App);
        var (kmsa, registrar) = await TenantAsync(app, "kmsa", IdentityRoles.Registrar);
        await TenantTestData.EnableSportAsync(App.Services, kmsa, TestballFull, Ct);
        var person = await CreatePersonAsync(App.Services, kmsa, SystemActor.Instance, DenisOpoka, overrideReason: null, Ct);
        await OkAsync(await registrar.PostAsJsonAsync($"{PersonPath(person)}/athlete-profiles", new { variant = TestballFull }, Ct));
        var first = await AddProfileAsync(registrar, person, (TestballFull, "TB_COACH"));

        var both = await registrar.ReadJsonAsync(PersonPath(person), Ct);
        both.GetProperty("athleteProfiles").GetArrayLength().ShouldBe(1);
        both.GetProperty("staffProfiles").GetArrayLength().ShouldBe(1);

        foreach (var status in new[] { "ACTIVE", "RETIRED" })
        {
            if (status == "RETIRED")
            {
                await OkAsync(await registrar.PostAsJsonAsync(StatusPath(person, first), new { status, reason = "Retired" }, Ct));
            }

            using var again = await registrar.PostAsJsonAsync(ProfilesPath(person), Body((TestballFull, "TB_PHYSIO")), Ct);
            await again.ShouldBeProblemAsync(HttpStatusCode.Conflict, "PERSON_STAFF_PROFILE_EXISTS");
        }

        // An archived profile is closed for good: a new one may start, and the old one takes no change.
        await OkAsync(await registrar.PostAsJsonAsync(StatusPath(person, first), new { status = "ARCHIVED", reason = "Recorded twice" }, Ct));
        await AddProfileAsync(registrar, person, (TestballFull, "TB_PHYSIO"));
        using (var update = await registrar.PutAsJsonAsync(ProfilePath(person, first), Body((TestballFull, "TB_COACH")), Ct))
        {
            await update.ShouldBeProblemAsync(HttpStatusCode.Conflict, "PERSON_PROFILE_ARCHIVED");
        }

        using var back = await registrar.PostAsJsonAsync(StatusPath(person, first), new { status = "ACTIVE", reason = "Undo" }, Ct);
        await back.ShouldBeProblemAsync(HttpStatusCode.Conflict, "PERSON_PROFILE_STATUS_INVALID");
    }

    // As review #101 R1: a double submit without a version answers 409 (the version or the open-profile index), never 500.
    [Fact]
    public async Task Two_creates_at_once_leave_one_refused()
    {
        await using var app = AppWithTestSports(App);
        var (kmsa, registrar) = await TenantAsync(app, "kmsa", IdentityRoles.Registrar);
        await TenantTestData.EnableSportAsync(App.Services, kmsa, TestballFull, Ct);
        var people = new List<Guid>();
        for (var i = 0; i < 10; i++)
        {
            people.Add((await SavePersonAsync(App.Services, kmsa, IvanMukasa with { FirstName = $"Ivan{(char)('a' + i)}" }, Ct)).Id);
        }

        var pairs = await Task.WhenAll(people.Select(person => Task.WhenAll(
            registrar.PostAsJsonAsync(ProfilesPath(person), Body((TestballFull, "TB_COACH")), Ct),
            registrar.PostAsJsonAsync(ProfilesPath(person), Body((TestballFull, "TB_COACH")), Ct))));

        foreach (var responses in pairs)
        {
            responses.Select(response => response.StatusCode).ShouldBe([HttpStatusCode.OK, HttpStatusCode.Conflict], ignoreOrder: true);
            var problem = await responses.Single(response => response.StatusCode == HttpStatusCode.Conflict).Content
                .ReadFromJsonAsync<JsonElement>(Ct);
            problem.GetProperty("code").GetString().ShouldBeOneOf("PERSON_STAFF_PROFILE_EXISTS", "PERSON_CONCURRENCY_CONFLICT");
            foreach (var response in responses)
            {
                response.Dispose();
            }
        }

        await using var scope = ScopeFor(App.Services, kmsa, out var context);
        (await context.Set<StaffProfile>().CountAsync(Ct)).ShouldBe(people.Count);
    }

    // S05 D12 and the owner (S06-T4b plan): a disabled variant keeps the capabilities it has but takes no new one.
    [Fact]
    public async Task A_disabled_variant_keeps_its_capabilities_but_takes_no_new_one()
    {
        await using var app = AppWithTestSports(App);
        var (kmsa, registrar) = await TenantAsync(app, "kmsa", IdentityRoles.Registrar);
        await TenantTestData.EnableSportAsync(App.Services, kmsa, TestballFull, Ct);
        await TenantTestData.EnableSportAsync(App.Services, kmsa, Testhoop, Ct);
        var denis = await CreatePersonAsync(App.Services, kmsa, SystemActor.Instance, DenisOpoka, overrideReason: null, Ct);
        var ivan = await CreatePersonAsync(App.Services, kmsa, SystemActor.Instance, IvanMukasa, overrideReason: null, Ct);
        var profileId = await AddProfileAsync(registrar, denis, (TestballFull, "TB_COACH"));

        await TenantTestData.DisableSportAsync(App.Services, kmsa, TestballFull, Ct);

        // The held capability stays (with a new qualification) beside an added one on an enabled variant.
        var corrected = await OkAsync(await registrar.PutAsJsonAsync(ProfilePath(denis, profileId), new
        {
            capabilities = new object[]
            {
                new { variant = TestballFull, role = "TB_COACH", qualification = "Level 3" },
                new { variant = Testhoop, role = "TH_COACH" },
            },
        }, Ct));
        Capabilities(corrected.GetProperty("staffProfiles")[0]).ShouldBe([(TestballFull, "TB_COACH", "Level 3"), (Testhoop, "TH_COACH", null)]);

        using (var added = await registrar.PutAsJsonAsync(ProfilePath(denis, profileId), Body((TestballFull, "TB_COACH"), (TestballFull, "TB_PHYSIO")), Ct))
        {
            var problem = await added.ShouldBeProblemAsync(HttpStatusCode.UnprocessableEntity, "PERSON_SPORT_NOT_ENABLED");
            Fields(problem).ShouldBe([("capabilities[1].variant", "PERSON_SPORT_NOT_ENABLED")]);
        }

        using var refused = await registrar.PostAsJsonAsync(ProfilesPath(ivan), Body((TestballFull, "TB_COACH")), Ct);
        await refused.ShouldBeProblemAsync(HttpStatusCode.UnprocessableEntity, "PERSON_SPORT_NOT_ENABLED");
    }

    [Fact]
    public async Task A_move_needs_a_reason_and_a_profile_of_this_person_and_kind()
    {
        await using var app = AppWithTestSports(App);
        var (kmsa, registrar) = await TenantAsync(app, "kmsa", IdentityRoles.Registrar);
        await TenantTestData.EnableSportAsync(App.Services, kmsa, TestballFull, Ct);
        var person = await CreatePersonAsync(App.Services, kmsa, SystemActor.Instance, DenisOpoka, overrideReason: null, Ct);
        var other = await CreatePersonAsync(App.Services, kmsa, SystemActor.Instance, IvanMukasa, overrideReason: null, Ct);
        var profileId = await AddProfileAsync(registrar, person, (TestballFull, "TB_COACH"));
        var athlete = await OkAsync(await registrar.PostAsJsonAsync($"{PersonPath(person)}/athlete-profiles", new { variant = TestballFull }, Ct));
        var athleteProfileId = athlete.GetProperty("athleteProfiles")[0].GetProperty("id").GetGuid();

        using (var blank = await registrar.PostAsJsonAsync(StatusPath(person, profileId), new { status = "INACTIVE", reason = " " }, Ct))
        {
            var problem = await blank.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_INVALID_REQUEST");
            Fields(problem).ShouldBe([("reason", "VALIDATION_REASON_REQUIRED")]);
        }

        using (var same = await registrar.PostAsJsonAsync(StatusPath(person, profileId), new { status = "ACTIVE", reason = "Again" }, Ct))
        {
            await same.ShouldBeProblemAsync(HttpStatusCode.Conflict, "PERSON_PROFILE_STATUS_INVALID");
        }

        foreach (var (owner, id) in new[] { (other, profileId), (person, Guid.CreateVersion7()), (person, athleteProfileId) })
        {
            using var moved = await registrar.PostAsJsonAsync(StatusPath(owner, id), new { status = "INACTIVE", reason = "On leave" }, Ct);
            await moved.ShouldBeProblemAsync(HttpStatusCode.NotFound, "PERSON_PROFILE_NOT_FOUND");
            using var updated = await registrar.PutAsJsonAsync(ProfilePath(owner, id), Body((TestballFull, "TB_COACH")), Ct);
            await updated.ShouldBeProblemAsync(HttpStatusCode.NotFound, "PERSON_PROFILE_NOT_FOUND");
        }
    }

    // D11: a profile and its capabilities are children of the person, so a change against an older version conflicts.
    [Fact]
    public async Task A_profile_change_on_an_older_version_of_the_person_conflicts()
    {
        await using var app = AppWithTestSports(App);
        var (kmsa, registrar) = await TenantAsync(app, "kmsa", IdentityRoles.Registrar);
        await TenantTestData.EnableSportAsync(App.Services, kmsa, TestballFull, Ct);
        var person = await CreatePersonAsync(App.Services, kmsa, SystemActor.Instance, DenisOpoka, overrideReason: null, Ct);
        var profileId = await AddProfileAsync(registrar, person, (TestballFull, "TB_COACH"));
        var version = (await registrar.ReadJsonAsync(PersonPath(person), Ct)).GetProperty("version").GetUInt32();

        // A change to only a capability's qualification moves the person's version.
        var qualified = await OkAsync(await registrar.PutAsJsonAsync(ProfilePath(person, profileId), new
        {
            capabilities = new[] { new { variant = TestballFull, role = "TB_COACH", qualification = "Level 2" } },
            version,
        }, Ct));
        qualified.GetProperty("version").GetUInt32().ShouldNotBe(version);

        using (var stale = await registrar.PutAsJsonAsync(ProfilePath(person, profileId), new
        {
            capabilities = new[] { new { variant = TestballFull, role = "TB_PHYSIO" } },
            version,
        }, Ct))
        {
            await stale.ShouldBeProblemAsync(HttpStatusCode.Conflict, "PERSON_CONCURRENCY_CONFLICT");
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, StatusPath(person, profileId))
        {
            Content = JsonContent.Create(new { status = "INACTIVE", reason = "On leave" }),
        };
        request.Headers.IfMatch.Add(new($"\"{version}\""));
        using var ifMatch = await registrar.SendAsync(request, Ct);
        await ifMatch.ShouldBeProblemAsync(HttpStatusCode.Conflict, "PERSON_CONCURRENCY_CONFLICT");
    }

    // Owner (S06-T4b plan): `variant` matches an open athlete profile's variant or an open staff profile's capability.
    [Fact]
    public async Task The_directory_filters_staff_by_profile_and_variant()
    {
        await using var app = AppWithTestSports(App);
        var (kmsa, registrar) = await TenantAsync(app, "kmsa", IdentityRoles.Registrar);
        await TenantTestData.EnableSportAsync(App.Services, kmsa, TestballFull, Ct);
        await TenantTestData.EnableSportAsync(App.Services, kmsa, Testhoop, Ct);
        var john = await CreatePersonAsync(App.Services, kmsa, SystemActor.Instance, JohnSsekandi, overrideReason: null, Ct);
        var denis = await CreatePersonAsync(App.Services, kmsa, SystemActor.Instance, DenisOpoka, overrideReason: null, Ct);
        var ivan = await CreatePersonAsync(App.Services, kmsa, SystemActor.Instance, IvanMukasa, overrideReason: null, Ct);
        await OkAsync(await registrar.PostAsJsonAsync($"{PersonPath(john)}/athlete-profiles", new { variant = TestballFull }, Ct));
        await AddProfileAsync(registrar, denis, (Testhoop, "TH_COACH"));
        var archived = await AddProfileAsync(registrar, ivan, (TestballFull, "TB_PHYSIO"));
        await OkAsync(await registrar.PostAsJsonAsync(StatusPath(ivan, archived), new { status = "ARCHIVED", reason = "Recorded by mistake" }, Ct));

        (await registrar.ReadJsonAsync($"{PeoplePath}?profile=STAFF", Ct)).Names().ShouldBe(["Denis Opoka"]);
        (await registrar.ReadJsonAsync($"{PeoplePath}?profile=ATHLETE", Ct)).Names().ShouldBe(["John Ssekandi"]);
        (await registrar.ReadJsonAsync($"{PeoplePath}?variant={Testhoop}", Ct)).Names().ShouldBe(["Denis Opoka"]);
        (await registrar.ReadJsonAsync($"{PeoplePath}?variant={TestballFull}", Ct)).Names().ShouldBe(["John Ssekandi"]);
        (await registrar.ReadJsonAsync($"{PeoplePath}?profile=STAFF&variant={TestballFull}", Ct)).Names().ShouldBeEmpty();
        (await registrar.ReadJsonAsync($"{PeoplePath}?profile=ATHLETE&variant={Testhoop}", Ct)).Names().ShouldBeEmpty();
        using var refused = await registrar.GetAsync($"{PeoplePath}?profile=staff", Ct);
        await refused.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_INVALID_QUERY");
    }

    [Fact]
    public async Task Another_tenants_person_is_not_found()
    {
        await using var app = AppWithTestSports(App);
        var (_, registrar) = await TenantAsync(app, "kmsa", IdentityRoles.Registrar);
        var (elssc, theirRegistrar) = await TenantAsync(app, "elssc", IdentityRoles.Registrar);
        await TenantTestData.EnableSportAsync(App.Services, elssc, TestballFull, Ct);
        var theirs = await CreatePersonAsync(App.Services, elssc, SystemActor.Instance, IvanMukasa, overrideReason: null, Ct);
        var profileId = await AddProfileAsync(theirRegistrar, theirs, (TestballFull, "TB_COACH"));

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
        var (personId, profileId) = await SavePersonWithProfileAsync(kmsa);

        foreach (var (method, path, body) in Requests(personId, profileId))
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

    // The three requests, in an order where each succeeds on a person with an active staff profile: the update, the move
    // that archives the profile, then a new profile.
    private static (HttpMethod Method, string Path, object Body)[] Requests(Guid personId, Guid profileId) =>
    [
        (HttpMethod.Put, ProfilePath(personId, profileId), Body((TestballFull, "TB_PHYSIO"))),
        (HttpMethod.Post, StatusPath(personId, profileId), new { status = "ARCHIVED", reason = "Recorded by mistake" }),
        (HttpMethod.Post, ProfilesPath(personId), Body((TestballFull, "TB_COACH"))),
    ];

    private static object Body(params (string Variant, string Role)[] capabilities) =>
        new { capabilities = capabilities.Select(c => new { variant = c.Variant, role = c.Role }).ToArray() };

    private static (string?, string?, string?)[] Capabilities(JsonElement profile) =>
    [
        .. profile.GetProperty("capabilities").EnumerateArray().Select(c => (
            c.GetProperty("variant").GetString(), c.GetProperty("role").GetString(), c.GetProperty("qualification").GetString())),
    ];

    private static string ProfilesPath(Guid personId) => $"{PersonPath(personId)}/staff-profiles";

    private static string ProfilePath(Guid personId, Guid profileId) => $"{ProfilesPath(personId)}/{profileId}";

    private static string StatusPath(Guid personId, Guid profileId) => $"{ProfilePath(personId, profileId)}/status";

    private static async Task<Guid> AddProfileAsync(HttpClient client, Guid personId, params (string Variant, string Role)[] capabilities)
    {
        var person = await OkAsync(await client.PostAsJsonAsync(ProfilesPath(personId), Body(capabilities), Ct));
        return person.GetProperty("staffProfiles").EnumerateArray()
            .Single(p => p.GetProperty("status").GetString() == "ACTIVE")
            .GetProperty("id").GetGuid();
    }

    private async Task<(Guid PersonId, Guid ProfileId)> SavePersonWithProfileAsync(Guid tenantId)
    {
        await using var scope = ScopeFor(App.Services, tenantId, out var context);
        var person = NewPerson(DenisOpoka);
        var profile = person.AddStaffProfile([new StaffCapabilityDetails(TestballFull, "TB_COACH", null)], SystemActor.Instance, Now).Value;
        context.Add(person);
        await context.SaveChangesAsync(Ct);
        return (person.Id, profile.Id);
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

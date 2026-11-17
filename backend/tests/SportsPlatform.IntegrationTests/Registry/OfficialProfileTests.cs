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
/// Official profiles over HTTP (S06-T4b2; D9, D11, D12, D21, D22): adding one, replacing its capabilities and moving it,
/// the codes checked against the tenant's sports and the definitions (test-only variants, <see cref="TestSportDefinitions"/>),
/// one open official profile per person, a licence's expiry on the tenant's date, the archived and deceased guards, the
/// version, the audit, the directory's filters, the roles and the read-only tenant. Also the T4 acceptance over all three
/// profile kinds.
/// </summary>
public sealed class OfficialProfileTests(PostgresFixture fixture) : DatabaseTest(fixture)
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
                new { variant = Testhoop, role = "TH_REFEREE" },
                new
                {
                    variant = TestballFull, role = "TB_REFEREE", grade = " Class   1 ", licenceNumber = " UG-REF  0147 ",
                    licenceValidUntil = "2027-06-30",
                },
            },
            version = before,
        }, Ct));

        added.GetProperty("version").GetUInt32().ShouldNotBe(before);
        var profile = added.GetProperty("officialProfiles").EnumerateArray().ShouldHaveSingleItem();
        var profileId = profile.GetProperty("id").GetGuid();
        profile.GetProperty("status").GetString().ShouldBe("ACTIVE");
        Capabilities(profile).ShouldBe(
        [
            (TestballFull, "TB_REFEREE", "Class 1", "UG-REF 0147", "2027-06-30", false),
            (Testhoop, "TH_REFEREE", null, null, null, false),
        ]);

        // A whole replacement: one left out is removed, one held keeps going with the new grade and licence.
        var updated = await OkAsync(await registrar.PutAsJsonAsync(ProfilePath(person, profileId), new
        {
            capabilities = new object[]
            {
                new { variant = TestballFull, role = "TB_REFEREE", licenceNumber = "UG-REF-0200", licenceValidUntil = "2028-06-30" },
                new { variant = TestballFull, role = "TB_ASSISTANT", grade = "Class 2" },
            },
        }, Ct));
        updated.GetProperty("version").GetUInt32().ShouldNotBe(added.GetProperty("version").GetUInt32());
        Capabilities(updated.GetProperty("officialProfiles")[0]).ShouldBe(
        [
            (TestballFull, "TB_ASSISTANT", "Class 2", null, null, false),
            (TestballFull, "TB_REFEREE", null, "UG-REF-0200", "2028-06-30", false),
        ]);

        var retired = await OkAsync(await registrar.PostAsJsonAsync(
            StatusPath(person, profileId), new { status = "RETIRED", reason = " Stepped down after the season " }, Ct));
        retired.GetProperty("officialProfiles")[0].GetProperty("status").GetString().ShouldBe("RETIRED");
        retired.GetProperty("version").GetUInt32().ShouldNotBe(updated.GetProperty("version").GetUInt32());
        await OkAsync(await registrar.PostAsJsonAsync(StatusPath(person, profileId), new { status = "ACTIVE", reason = "Came back" }, Ct));

        await using (var scope = ScopeFor(App.Services, kmsa, out var context))
        {
            var rows = await context.Set<ProfileStatusChange>().Where(c => c.ProfileId == profileId).OrderBy(c => c.Id).ToListAsync(Ct);
            rows.Select(c => (c.ProfileKind, c.FromStatus, c.ToStatus, c.Reason)).ShouldBe(
            [
                (ProfileKind.Official, null, ProfileStatus.Active, null),
                (ProfileKind.Official, ProfileStatus.Active, ProfileStatus.Retired, "Stepped down after the season"),
                (ProfileKind.Official, ProfileStatus.Retired, ProfileStatus.Active, "Came back"),
            ]);
            (await context.Set<OfficialCapability>().CountAsync(Ct)).ShouldBe(2);
        }

        await AccountTestData.DeliverOutboxAsync(App.Services, Ct);
        var entries = await AuditTestData.EntriesAsync(App.Services, Ct);
        var create = entries.Single(e => e.ActionCode == "official_profile.create");
        (create.Outcome, create.EntityType, create.EntityId).ShouldBe((AuditOutcome.Success, "Person", (Guid?)person));
        var metadata = JsonDocument.Parse(create.Metadata!).RootElement;
        metadata.GetProperty("profileId").GetGuid().ShouldBe(profileId);
        metadata.GetProperty("variants").EnumerateArray().Select(v => v.GetString()).ShouldBe([TestballFull, Testhoop]);
        Entries(metadata.GetProperty("added")).ShouldBe([(TestballFull, "TB_REFEREE"), (Testhoop, "TH_REFEREE")]);
        create.Metadata!.ShouldNotContain("Class");
        create.Metadata!.ShouldNotContain("UG-REF");
        create.Metadata!.ShouldNotContain("2027");
        var update = entries.Single(e => e.ActionCode == "official_profile.update");
        update.Outcome.ShouldBe(AuditOutcome.Success);
        // Review #102 R1: a removed capability is deleted, so the update's entry names what was added and removed.
        var changes = JsonDocument.Parse(update.Metadata!).RootElement;
        Entries(changes.GetProperty("added")).ShouldBe([(TestballFull, "TB_ASSISTANT")]);
        Entries(changes.GetProperty("removed")).ShouldBe([(Testhoop, "TH_REFEREE")]);
        update.Metadata!.ShouldNotContain("UG-REF");
        update.Metadata!.ShouldNotContain("Class");
        var moves = entries.Where(e => e.ActionCode == "official_profile.status.change").ToList();
        moves.Select(e => e.Reason).ShouldBe(["Stepped down after the season", "Came back"], ignoreOrder: true);
        moves.Select(e => JsonDocument.Parse(e.Metadata!).RootElement.GetProperty("toStatus").GetString())
            .ShouldBe(["RETIRED", "ACTIVE"], ignoreOrder: true);
    }

    // D12 (owner, S06-T4b2 plan): a licence is valid through its last day on the tenant's date; Kampala is three hours ahead
    // of UTC, so 30 June ends at 21:00 UTC. Nothing is refused for an expired licence.
    [Fact]
    public async Task A_licence_expires_after_its_last_day_on_the_tenants_date()
    {
        await using var app = AppWithTestSports(App);
        var (kmsa, registrar) = await TenantAsync(app, "kmsa", IdentityRoles.Registrar);
        await TenantTestData.EnableSportAsync(App.Services, kmsa, TestballFull, Ct);
        var person = await CreatePersonAsync(App.Services, kmsa, SystemActor.Instance, DenisOpoka, overrideReason: null, Ct);
        var kampala = TimeSpan.FromHours(3);
        var today = DateOnly.FromDateTime(App.Clock.GetUtcNow().ToOffset(kampala).DateTime);
        var lastDay = today.AddDays(1);
        var profileId = await AddProfileAsync(registrar, person, new
        {
            capabilities = new object[]
            {
                new { variant = TestballFull, role = "TB_REFEREE", licenceNumber = "UG-REF-0147", licenceValidUntil = lastDay },
                new { variant = TestballFull, role = "TB_ASSISTANT" },
            },
        });
        var midnight = new DateTimeOffset(lastDay.AddDays(1).ToDateTime(TimeOnly.MinValue), kampala);

        App.Clock.Advance(midnight - App.Clock.GetUtcNow() - TimeSpan.FromMinutes(1));
        var before = await registrar.ReadJsonAsync(PersonPath(person), Ct);
        App.Clock.Advance(TimeSpan.FromMinutes(2));
        var after = await registrar.ReadJsonAsync(PersonPath(person), Ct);

        Capabilities(before.GetProperty("officialProfiles")[0]).Select(c => (c.Item2, c.Item6)).ShouldBe([("TB_ASSISTANT", false), ("TB_REFEREE", false)]);
        Capabilities(after.GetProperty("officialProfiles")[0]).Select(c => (c.Item2, c.Item6)).ShouldBe([("TB_ASSISTANT", false), ("TB_REFEREE", true)]);

        // An expired licence blocks nothing: the profile is still changed and moved, and a past date is accepted.
        var renewed = await OkAsync(await registrar.PutAsJsonAsync(ProfilePath(person, profileId), new
        {
            capabilities = new[] { new { variant = TestballFull, role = "TB_REFEREE", licenceValidUntil = today } },
        }, Ct));
        Capabilities(renewed.GetProperty("officialProfiles")[0]).ShouldHaveSingleItem().Item6.ShouldBeTrue();
        await OkAsync(await registrar.PostAsJsonAsync(StatusPath(person, profileId), new { status = "INACTIVE", reason = "Licence lapsed" }, Ct));
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
            using var refused = await registrar.PostAsJsonAsync(ProfilesPath(person), Body((TestballFull, "TB_REFEREE"), (variant, "TH_REFEREE")), Ct);
            var problem = await refused.ShouldBeProblemAsync(HttpStatusCode.UnprocessableEntity, "PERSON_SPORT_NOT_ENABLED");
            Fields(problem).ShouldBe([("capabilities[1].variant", "PERSON_SPORT_NOT_ENABLED")]);
        }

        // Another variant's official role, a staff role or a position is no official role of this one.
        foreach (var role in new[] { "TH_REFEREE", "TB_COACH", "TB_MID" })
        {
            using var refused = await registrar.PostAsJsonAsync(ProfilesPath(person), Body((TestballFull, role)), Ct);
            var problem = await refused.ShouldBeProblemAsync(HttpStatusCode.UnprocessableEntity, "PERSON_ROLE_UNKNOWN");
            Fields(problem).ShouldBe([("capabilities[0].role", "PERSON_ROLE_UNKNOWN")]);
        }

        var profileId = await AddProfileAsync(registrar, person, (TestballFull, "TB_REFEREE"));
        using (var update = await registrar.PutAsJsonAsync(ProfilePath(person, profileId), Body((TestballFull, "TB_REFEREE"), (TestballFull, "TB_COACH")), Ct))
        {
            var problem = await update.ShouldBeProblemAsync(HttpStatusCode.UnprocessableEntity, "PERSON_ROLE_UNKNOWN");
            Fields(problem).ShouldBe([("capabilities[1].role", "PERSON_ROLE_UNKNOWN")]);
        }

        // The shape is checked first: 1–10 capabilities, codes as definitions write them, each pair once, a short grade and
        // licence number without control characters.
        foreach (var (body, field) in new (object, string)[]
        {
            (new { }, "capabilities"),
            (new { capabilities = Array.Empty<object>() }, "capabilities"),
            (new { capabilities = new object?[] { null } }, "capabilities"),
            (Body([.. Enumerable.Range(0, 11).Select(i => (TestballFull, $"TB_ROLE_{i}"))]), "capabilities"),
            (Body((TestballFull, "TB_REFEREE"), (TestballFull, "TB_REFEREE")), "capabilities"),
            (Body(("testball_full", "TB_REFEREE")), "capabilities[0].variant"),
            (Body((TestballFull, "TB_REFEREE"), (TestballFull, "tb_referee")), "capabilities[1].role"),
            (new { capabilities = new[] { new { variant = TestballFull } } }, "capabilities[0].role"),
            (new { capabilities = new[] { new { variant = TestballFull, role = "TB_REFEREE", grade = new string('G', 51) } } },
                "capabilities[0].grade"),
            (new { capabilities = new[] { new { variant = TestballFull, role = "TB_REFEREE", licenceNumber = new string('L', 51) } } },
                "capabilities[0].licenceNumber"),
            (new { capabilities = new[] { new { variant = TestballFull, role = "TB_REFEREE", licenceNumber = "UG-REF\n0147" } } },
                "capabilities[0].licenceNumber"),
        })
        {
            using var invalid = await registrar.PostAsJsonAsync(ProfilesPath(person), body, Ct);
            var problem = await invalid.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_INVALID_REQUEST");
            Fields(problem).ShouldBe([(field, "VALIDATION_INVALID_REQUEST")]);
        }

        using var unknownMember = await registrar.PostAsJsonAsync(ProfilesPath(person), new
        {
            capabilities = new[] { new { variant = TestballFull, role = "TB_REFEREE", qualification = "Class 1" } },
        }, Ct);
        await unknownMember.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_MALFORMED_REQUEST");

        using var notADate = await registrar.PostAsJsonAsync(ProfilesPath(person), new
        {
            capabilities = new[] { new { variant = TestballFull, role = "TB_REFEREE", licenceValidUntil = "30/06/2027" } },
        }, Ct);
        await notADate.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_MALFORMED_REQUEST");
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
        var profileId = await AddProfileAsync(registrar, denis, (TestballFull, "TB_REFEREE"), (TestballFull, "TB_FOURTH"));
        await OkAsync(await registrar.PostAsJsonAsync(StatusPath(denis, profileId), new { status = "INACTIVE", reason = "On leave" }, Ct));
        await ChangeStatusAsync(App.Services, kmsa, ivan, transition, Ct);
        await ChangeStatusAsync(App.Services, kmsa, denis, transition, Ct);

        using (var added = await registrar.PostAsJsonAsync(ProfilesPath(ivan), Body((TestballFull, "TB_REFEREE")), Ct))
        {
            await added.ShouldBeProblemAsync(HttpStatusCode.Conflict, code);
        }

        using (var reactivated = await registrar.PostAsJsonAsync(StatusPath(denis, profileId), new { status = "ACTIVE", reason = "Back" }, Ct))
        {
            await reactivated.ShouldBeProblemAsync(HttpStatusCode.Conflict, code);
        }

        // As staff (review #102 R2): a new role is refused; keeping, removing and re-licensing held ones stay allowed, as do
        // closing moves.
        using (var newRole = await registrar.PutAsJsonAsync(ProfilePath(denis, profileId), Body((TestballFull, "TB_REFEREE"), (TestballFull, "TB_ASSISTANT")), Ct))
        {
            await newRole.ShouldBeProblemAsync(HttpStatusCode.Conflict, code);
        }

        var kept = await OkAsync(await registrar.PutAsJsonAsync(ProfilePath(denis, profileId), new
        {
            capabilities = new[] { new { variant = TestballFull, role = "TB_REFEREE", grade = "Class 1" } },
        }, Ct));
        Capabilities(kept.GetProperty("officialProfiles")[0]).ShouldBe([(TestballFull, "TB_REFEREE", "Class 1", null, null, false)]);
        var closed = await OkAsync(await registrar.PostAsJsonAsync(StatusPath(denis, profileId), new { status = "RETIRED", reason = "Retired" }, Ct));
        closed.GetProperty("officialProfiles").EnumerateArray().ShouldHaveSingleItem().GetProperty("status").GetString().ShouldBe("RETIRED");
    }

    // T4 acceptance: one person holds athlete, staff and official profiles at once (Denis Opoka: player, assistant coach and
    // referee), and an archived person gets none of them.
    [Fact]
    public async Task A_person_holds_all_three_profiles_and_an_archived_person_gets_none()
    {
        await using var app = AppWithTestSports(App);
        var (kmsa, registrar) = await TenantAsync(app, "kmsa", IdentityRoles.Registrar);
        await TenantTestData.EnableSportAsync(App.Services, kmsa, TestballFull, Ct);
        var denis = await CreatePersonAsync(App.Services, kmsa, SystemActor.Instance, DenisOpoka, overrideReason: null, Ct);
        var ivan = await CreatePersonAsync(App.Services, kmsa, SystemActor.Instance, IvanMukasa, overrideReason: null, Ct);

        await OkAsync(await registrar.PostAsJsonAsync($"{PersonPath(denis)}/athlete-profiles", new { variant = TestballFull, primaryPosition = "TB_MID" }, Ct));
        await OkAsync(await registrar.PostAsJsonAsync(
            $"{PersonPath(denis)}/staff-profiles", new { capabilities = new[] { new { variant = TestballFull, role = "TB_COACH" } } }, Ct));
        await AddProfileAsync(registrar, denis, new
        {
            capabilities = new[] { new { variant = TestballFull, role = "TB_REFEREE", licenceValidUntil = "2027-06-30" } },
        });

        var all = await registrar.ReadJsonAsync(PersonPath(denis), Ct);
        all.GetProperty("athleteProfiles").EnumerateArray().ShouldHaveSingleItem().GetProperty("primaryPosition").GetString().ShouldBe("TB_MID");
        all.GetProperty("staffProfiles").EnumerateArray().ShouldHaveSingleItem().GetProperty("capabilities")[0].GetProperty("role").GetString()
            .ShouldBe("TB_COACH");
        all.GetProperty("officialProfiles").EnumerateArray().ShouldHaveSingleItem().GetProperty("capabilities")[0].GetProperty("role").GetString()
            .ShouldBe("TB_REFEREE");
        foreach (var profile in new[] { "ATHLETE", "STAFF", "OFFICIAL" })
        {
            (await registrar.ReadJsonAsync($"{PeoplePath}?profile={profile}", Ct)).Names().ShouldBe(["Denis Opoka"]);
        }

        await ChangeStatusAsync(App.Services, kmsa, ivan, PersonTransition.Archive, Ct);
        foreach (var (path, body) in new (string, object)[]
        {
            ($"{PersonPath(ivan)}/athlete-profiles", new { variant = TestballFull }),
            ($"{PersonPath(ivan)}/staff-profiles", new { capabilities = new[] { new { variant = TestballFull, role = "TB_COACH" } } }),
            (ProfilesPath(ivan), Body((TestballFull, "TB_REFEREE"))),
        })
        {
            using var refused = await registrar.PostAsJsonAsync(path, body, Ct);
            await refused.ShouldBeProblemAsync(HttpStatusCode.Conflict, "PERSON_ARCHIVED");
        }

        var archived = await registrar.ReadJsonAsync(PersonPath(ivan), Ct);
        foreach (var kind in new[] { "athleteProfiles", "staffProfiles", "officialProfiles" })
        {
            archived.GetProperty(kind).GetArrayLength().ShouldBe(0);
        }
    }

    [Fact]
    public async Task A_person_holds_one_open_official_profile()
    {
        await using var app = AppWithTestSports(App);
        var (kmsa, registrar) = await TenantAsync(app, "kmsa", IdentityRoles.Registrar);
        await TenantTestData.EnableSportAsync(App.Services, kmsa, TestballFull, Ct);
        var person = await CreatePersonAsync(App.Services, kmsa, SystemActor.Instance, DenisOpoka, overrideReason: null, Ct);
        var first = await AddProfileAsync(registrar, person, (TestballFull, "TB_REFEREE"));

        foreach (var status in new[] { "ACTIVE", "RETIRED" })
        {
            if (status == "RETIRED")
            {
                await OkAsync(await registrar.PostAsJsonAsync(StatusPath(person, first), new { status, reason = "Retired" }, Ct));
            }

            using var again = await registrar.PostAsJsonAsync(ProfilesPath(person), Body((TestballFull, "TB_ASSISTANT")), Ct);
            await again.ShouldBeProblemAsync(HttpStatusCode.Conflict, "PERSON_OFFICIAL_PROFILE_EXISTS");
        }

        // An archived profile is closed for good: a new one may start, and the old one takes no change.
        await OkAsync(await registrar.PostAsJsonAsync(StatusPath(person, first), new { status = "ARCHIVED", reason = "Recorded twice" }, Ct));
        await AddProfileAsync(registrar, person, (TestballFull, "TB_ASSISTANT"));
        using (var update = await registrar.PutAsJsonAsync(ProfilePath(person, first), Body((TestballFull, "TB_REFEREE")), Ct))
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
            registrar.PostAsJsonAsync(ProfilesPath(person), Body((TestballFull, "TB_REFEREE")), Ct),
            registrar.PostAsJsonAsync(ProfilesPath(person), Body((TestballFull, "TB_REFEREE")), Ct))));

        foreach (var responses in pairs)
        {
            responses.Select(response => response.StatusCode).ShouldBe([HttpStatusCode.OK, HttpStatusCode.Conflict], ignoreOrder: true);
            var problem = await responses.Single(response => response.StatusCode == HttpStatusCode.Conflict).Content
                .ReadFromJsonAsync<JsonElement>(Ct);
            problem.GetProperty("code").GetString().ShouldBeOneOf("PERSON_OFFICIAL_PROFILE_EXISTS", "PERSON_CONCURRENCY_CONFLICT");
            foreach (var response in responses)
            {
                response.Dispose();
            }
        }

        await using var scope = ScopeFor(App.Services, kmsa, out var context);
        (await context.Set<OfficialProfile>().CountAsync(Ct)).ShouldBe(people.Count);
    }

    // Pre-review: two updates adding the same capability at once answer 409 (the version or the capability index), never 500.
    [Fact]
    public async Task Two_updates_adding_one_capability_at_once_leave_one_refused()
    {
        await using var app = AppWithTestSports(App);
        var (kmsa, registrar) = await TenantAsync(app, "kmsa", IdentityRoles.Registrar);
        await TenantTestData.EnableSportAsync(App.Services, kmsa, TestballFull, Ct);
        var profiles = new List<(Guid Person, Guid Profile)>();
        for (var i = 0; i < 10; i++)
        {
            var person = (await SavePersonAsync(App.Services, kmsa, IvanMukasa with { FirstName = $"Ivan{(char)('a' + i)}" }, Ct)).Id;
            profiles.Add((person, await AddProfileAsync(registrar, person, (TestballFull, "TB_REFEREE"))));
        }

        var body = Body((TestballFull, "TB_REFEREE"), (TestballFull, "TB_ASSISTANT"));
        var pairs = await Task.WhenAll(profiles.Select(p => Task.WhenAll(
            registrar.PutAsJsonAsync(ProfilePath(p.Person, p.Profile), body, Ct),
            registrar.PutAsJsonAsync(ProfilePath(p.Person, p.Profile), body, Ct))));

        foreach (var responses in pairs)
        {
            var codes = responses.Select(response => response.StatusCode).ToList();
            codes.ShouldContain(HttpStatusCode.OK);
            codes.ShouldAllBe(code => code == HttpStatusCode.OK || code == HttpStatusCode.Conflict);
            foreach (var response in responses)
            {
                if (response.StatusCode == HttpStatusCode.Conflict)
                {
                    (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("code").GetString()
                        .ShouldBe("PERSON_CONCURRENCY_CONFLICT");
                }

                response.Dispose();
            }
        }

        await using var scope = ScopeFor(App.Services, kmsa, out var context);
        (await context.Set<OfficialCapability>().CountAsync(Ct)).ShouldBe(2 * profiles.Count);
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
        var profileId = await AddProfileAsync(registrar, denis, (TestballFull, "TB_REFEREE"));

        await TenantTestData.DisableSportAsync(App.Services, kmsa, TestballFull, Ct);

        // The held capability stays (with a new grade) beside an added one on an enabled variant.
        var corrected = await OkAsync(await registrar.PutAsJsonAsync(ProfilePath(denis, profileId), new
        {
            capabilities = new object[]
            {
                new { variant = TestballFull, role = "TB_REFEREE", grade = "Class 1" },
                new { variant = Testhoop, role = "TH_REFEREE" },
            },
        }, Ct));
        Capabilities(corrected.GetProperty("officialProfiles")[0])
            .ShouldBe([(TestballFull, "TB_REFEREE", "Class 1", null, null, false), (Testhoop, "TH_REFEREE", null, null, null, false)]);

        using (var added = await registrar.PutAsJsonAsync(ProfilePath(denis, profileId), Body((TestballFull, "TB_REFEREE"), (TestballFull, "TB_ASSISTANT")), Ct))
        {
            var problem = await added.ShouldBeProblemAsync(HttpStatusCode.UnprocessableEntity, "PERSON_SPORT_NOT_ENABLED");
            Fields(problem).ShouldBe([("capabilities[1].variant", "PERSON_SPORT_NOT_ENABLED")]);
        }

        using var refused = await registrar.PostAsJsonAsync(ProfilesPath(ivan), Body((TestballFull, "TB_REFEREE")), Ct);
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
        var profileId = await AddProfileAsync(registrar, person, (TestballFull, "TB_REFEREE"));
        var staff = await OkAsync(await registrar.PostAsJsonAsync(
            $"{PersonPath(person)}/staff-profiles", new { capabilities = new[] { new { variant = TestballFull, role = "TB_COACH" } } }, Ct));
        var staffProfileId = staff.GetProperty("staffProfiles")[0].GetProperty("id").GetGuid();

        using (var blank = await registrar.PostAsJsonAsync(StatusPath(person, profileId), new { status = "INACTIVE", reason = " " }, Ct))
        {
            var problem = await blank.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_INVALID_REQUEST");
            Fields(problem).ShouldBe([("reason", "VALIDATION_REASON_REQUIRED")]);
        }

        using (var same = await registrar.PostAsJsonAsync(StatusPath(person, profileId), new { status = "ACTIVE", reason = "Again" }, Ct))
        {
            await same.ShouldBeProblemAsync(HttpStatusCode.Conflict, "PERSON_PROFILE_STATUS_INVALID");
        }

        foreach (var (owner, id) in new[] { (other, profileId), (person, Guid.CreateVersion7()), (person, staffProfileId) })
        {
            using var moved = await registrar.PostAsJsonAsync(StatusPath(owner, id), new { status = "INACTIVE", reason = "On leave" }, Ct);
            await moved.ShouldBeProblemAsync(HttpStatusCode.NotFound, "PERSON_PROFILE_NOT_FOUND");
            using var updated = await registrar.PutAsJsonAsync(ProfilePath(owner, id), Body((TestballFull, "TB_REFEREE")), Ct);
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
        var profileId = await AddProfileAsync(registrar, person, (TestballFull, "TB_REFEREE"));
        var version = (await registrar.ReadJsonAsync(PersonPath(person), Ct)).GetProperty("version").GetUInt32();

        // A change to only a capability's licence moves the person's version.
        var licensed = await OkAsync(await registrar.PutAsJsonAsync(ProfilePath(person, profileId), new
        {
            capabilities = new[] { new { variant = TestballFull, role = "TB_REFEREE", licenceValidUntil = "2027-06-30" } },
            version,
        }, Ct));
        licensed.GetProperty("version").GetUInt32().ShouldNotBe(version);

        using (var stale = await registrar.PutAsJsonAsync(ProfilePath(person, profileId), new
        {
            capabilities = new[] { new { variant = TestballFull, role = "TB_ASSISTANT" } },
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

    // Owner (S06-T4b plan): `variant` matches an open athlete profile's variant or an open staff or official profile's capability.
    [Fact]
    public async Task The_directory_filters_officials_by_profile_and_variant()
    {
        await using var app = AppWithTestSports(App);
        var (kmsa, registrar) = await TenantAsync(app, "kmsa", IdentityRoles.Registrar);
        await TenantTestData.EnableSportAsync(App.Services, kmsa, TestballFull, Ct);
        await TenantTestData.EnableSportAsync(App.Services, kmsa, Testhoop, Ct);
        var john = await CreatePersonAsync(App.Services, kmsa, SystemActor.Instance, JohnSsekandi, overrideReason: null, Ct);
        var denis = await CreatePersonAsync(App.Services, kmsa, SystemActor.Instance, DenisOpoka, overrideReason: null, Ct);
        var ivan = await CreatePersonAsync(App.Services, kmsa, SystemActor.Instance, IvanMukasa, overrideReason: null, Ct);
        await OkAsync(await registrar.PostAsJsonAsync($"{PersonPath(john)}/athlete-profiles", new { variant = TestballFull }, Ct));
        await AddProfileAsync(registrar, denis, (Testhoop, "TH_REFEREE"));
        var archived = await AddProfileAsync(registrar, ivan, (TestballFull, "TB_ASSISTANT"));
        await OkAsync(await registrar.PostAsJsonAsync(StatusPath(ivan, archived), new { status = "ARCHIVED", reason = "Recorded by mistake" }, Ct));

        (await registrar.ReadJsonAsync($"{PeoplePath}?profile=OFFICIAL", Ct)).Names().ShouldBe(["Denis Opoka"]);
        (await registrar.ReadJsonAsync($"{PeoplePath}?profile=STAFF", Ct)).Names().ShouldBeEmpty();
        (await registrar.ReadJsonAsync($"{PeoplePath}?profile=ATHLETE", Ct)).Names().ShouldBe(["John Ssekandi"]);
        (await registrar.ReadJsonAsync($"{PeoplePath}?variant={Testhoop}", Ct)).Names().ShouldBe(["Denis Opoka"]);
        (await registrar.ReadJsonAsync($"{PeoplePath}?variant={TestballFull}", Ct)).Names().ShouldBe(["John Ssekandi"]);
        (await registrar.ReadJsonAsync($"{PeoplePath}?profile=OFFICIAL&variant={TestballFull}", Ct)).Names().ShouldBeEmpty();
        (await registrar.ReadJsonAsync($"{PeoplePath}?profile=ATHLETE&variant={Testhoop}", Ct)).Names().ShouldBeEmpty();
        using var refused = await registrar.GetAsync($"{PeoplePath}?profile=official", Ct);
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
        var profileId = await AddProfileAsync(theirRegistrar, theirs, (TestballFull, "TB_REFEREE"));

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

    // The three requests, in an order where each succeeds on a person with an active official profile: the update, the
    // move that archives the profile, then a new profile.
    private static (HttpMethod Method, string Path, object Body)[] Requests(Guid personId, Guid profileId) =>
    [
        (HttpMethod.Put, ProfilePath(personId, profileId), Body((TestballFull, "TB_ASSISTANT"))),
        (HttpMethod.Post, StatusPath(personId, profileId), new { status = "ARCHIVED", reason = "Recorded by mistake" }),
        (HttpMethod.Post, ProfilesPath(personId), Body((TestballFull, "TB_REFEREE"))),
    ];

    private static object Body(params (string Variant, string Role)[] capabilities) =>
        new { capabilities = capabilities.Select(c => new { variant = c.Variant, role = c.Role }).ToArray() };

    // Each capability as (variant, role, grade, licence number, licence valid until, licence expired).
    private static (string?, string?, string?, string?, string?, bool)[] Capabilities(JsonElement profile) =>
    [
        .. profile.GetProperty("capabilities").EnumerateArray().Select(c => (
            c.GetProperty("variant").GetString(), c.GetProperty("role").GetString(), c.GetProperty("grade").GetString(),
            c.GetProperty("licenceNumber").GetString(), c.GetProperty("licenceValidUntil").GetString(),
            c.GetProperty("licenceExpired").GetBoolean())),
    ];

    private static (string?, string?)[] Entries(JsonElement capabilities) =>
        [.. capabilities.EnumerateArray().Select(c => (c.GetProperty("variant").GetString(), c.GetProperty("role").GetString()))];

    private static string ProfilesPath(Guid personId) => $"{PersonPath(personId)}/official-profiles";

    private static string ProfilePath(Guid personId, Guid profileId) => $"{ProfilesPath(personId)}/{profileId}";

    private static string StatusPath(Guid personId, Guid profileId) => $"{ProfilePath(personId, profileId)}/status";

    private static Task<Guid> AddProfileAsync(HttpClient client, Guid personId, params (string Variant, string Role)[] capabilities) =>
        AddProfileAsync(client, personId, Body(capabilities));

    private static async Task<Guid> AddProfileAsync(HttpClient client, Guid personId, object body)
    {
        var person = await OkAsync(await client.PostAsJsonAsync(ProfilesPath(personId), body, Ct));
        return person.GetProperty("officialProfiles").EnumerateArray()
            .Single(p => p.GetProperty("status").GetString() == "ACTIVE")
            .GetProperty("id").GetGuid();
    }

    private async Task<(Guid PersonId, Guid ProfileId)> SavePersonWithProfileAsync(Guid tenantId)
    {
        await using var scope = ScopeFor(App.Services, tenantId, out var context);
        var person = NewPerson(DenisOpoka);
        var profile = person.AddOfficialProfile(
            [new OfficialCapabilityDetails(TestballFull, "TB_REFEREE", null, null, null)], SystemActor.Instance, Now).Value;
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

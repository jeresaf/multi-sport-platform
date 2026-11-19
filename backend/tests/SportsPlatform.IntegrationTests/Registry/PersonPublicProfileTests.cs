using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
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
using SportsPlatform.Modules.Registry.Domain.People.Consents;
using SportsPlatform.Modules.Registry.Domain.People.Profiles;
using SportsPlatform.Modules.Tenancy.Contracts.Tenants;
using SportsPlatform.UnitTests.Platform.Media;
using static SportsPlatform.IntegrationTests.Identity.MembershipTestData;
using static SportsPlatform.IntegrationTests.Registry.PeopleRequests;
using static SportsPlatform.IntegrationTests.Registry.RegistryTestData;
using AgeStatus = SportsPlatform.Modules.Registry.Contracts.People.AgeStatus;

namespace SportsPlatform.IntegrationTests.Registry;

/// <summary>
/// The public profile and the public person read (S06-T6b; D13, D15, D17, D18, ADR-0015): turning the profile on needs
/// <c>PUBLIC_PROFILE</c> consent for anyone not known to be an adult; the public read and its photo route show a minor's
/// display name only, and the photo only with <c>PHOTO</c>, checked on every request; a person turns 18 on the tenant's
/// date; nothing answers for another tenant's host. Also the roles, the audit and the version.
/// </summary>
public sealed class PersonPublicProfileTests(PostgresFixture fixture) : DatabaseTest(fixture)
{
    private const string _kmsaHost = "kmsa.localhost";

    private static readonly string[] _variants = ["THUMBNAIL", "MEDIUM", "LARGE"];

    private static readonly string[] _photo = ["PHOTO"];

    private static readonly string[] _publicProfile = ["PUBLIC_PROFILE"];

    // T6 acceptance 1: no PUBLIC_PROFILE consent, no public profile; with it, the display name only; the photo with PHOTO.
    [Fact]
    public async Task A_minors_public_profile_needs_consent_and_shows_the_display_name_and_the_photo_only_with_photo_consent()
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var brenda = await CreatePersonAsync(App.Services, kmsa, SystemActor.Instance, BrendaAkello, overrideReason: null, Ct);
        await OkAsync(await registrar.PutAsync(PhotoPath(brenda), Form(TestImages.Jpeg(800, 600)), Ct));

        using var refused = await registrar.PostAsync(EnablePath(brenda), null, Ct);
        await refused.ShouldBeProblemAsync(HttpStatusCode.UnprocessableEntity, "PERSON_CONSENT_REQUIRED");
        (await PublicAsync(PublicPath(brenda))).ShouldBeProblem(StatusCodes.Status404NotFound, "PERSON_NOT_FOUND");

        // Review #106 N1: the scope is read from the consents the handler loads.
        await SaveConsentAsync(kmsa, brenda, ConsentScope.DataProcessing, ConsentScope.PublicProfile);
        var enabled = await OkAsync(await registrar.PostAsync(EnablePath(brenda), null, Ct));
        enabled.GetProperty("publicProfileEnabled").GetBoolean().ShouldBeTrue();

        var read = await PublicAsync(PublicPath(brenda));
        read.Status.ShouldBe(StatusCodes.Status200OK, read.Body);
        read.Headers.CacheControl.ToString().ShouldBe("public, max-age=60");
        PropertiesOf(read.Json).ShouldBe(["id", "displayName", "nationality", "photo", "athleteProfiles"]);
        read.Json.GetProperty("id").GetGuid().ShouldBe(brenda);
        read.Json.GetProperty("displayName").GetString().ShouldBe("Brenda Akello");
        read.Json.GetProperty("nationality").ValueKind.ShouldBe(JsonValueKind.Null);
        read.Json.GetProperty("photo").ValueKind.ShouldBe(JsonValueKind.Null);
        read.Json.GetProperty("athleteProfiles").GetArrayLength().ShouldBe(0);
        (await PublicAsync(PhotoRoute(brenda, "MEDIUM"))).ShouldBeProblem(StatusCodes.Status404NotFound, "PERSON_PHOTO_NOT_FOUND");

        // A second guardian's PHOTO consent shows the photo.
        await SaveConsentAsync(kmsa, brenda, ConsentScope.Photo);
        var withPhoto = (await PublicAsync(PublicPath(brenda))).Json;
        withPhoto.GetProperty("nationality").ValueKind.ShouldBe(JsonValueKind.Null);
        var photo = withPhoto.GetProperty("photo");
        photo.GetProperty("mediumUrl").GetString().ShouldBe($"/api/public/v1/people/{brenda}/photo?variant=MEDIUM");
        foreach (var (property, size) in new[] { ("thumbnailUrl", (160, 120)), ("mediumUrl", (640, 480)), ("largeUrl", (800, 600)) })
        {
            var served = await PublicBytesAsync(photo.GetProperty(property).GetString()!);
            served.Status.ShouldBe(StatusCodes.Status200OK, property);
            served.ContentType.ShouldBe("image/webp");
            served.CacheControl.ShouldBe("public, max-age=300");
            TestImages.SizeOf(served.Body).ShouldBe(size);
        }
    }

    [Fact]
    public async Task An_adult_needs_no_consent_and_shows_nationality_photo_and_active_athlete_profiles()
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var john = await CreatePersonAsync(App.Services, kmsa, SystemActor.Instance, JohnSsekandi, overrideReason: null, Ct);
        await OkAsync(await registrar.PutAsync(PhotoPath(john), Form(TestImages.Jpeg(800, 600)), Ct));
        await SaveAthleteProfilesAsync(kmsa, john);

        await OkAsync(await registrar.PostAsync(EnablePath(john), null, Ct));

        var read = (await PublicAsync(PublicPath(john))).Json;
        read.GetProperty("displayName").GetString().ShouldBe("John Ssekandi");
        read.GetProperty("nationality").GetString().ShouldBe("UG");
        read.GetProperty("photo").GetProperty("thumbnailUrl").GetString().ShouldBe($"/api/public/v1/people/{john}/photo?variant=THUMBNAIL");
        var profile = read.GetProperty("athleteProfiles").EnumerateArray().ShouldHaveSingleItem();
        PropertiesOf(profile).ShouldBe(["variant", "position", "publicName"]);
        profile.GetProperty("variant").GetString().ShouldBe(TestSportDefinitions.TestballFull);
        profile.GetProperty("position").GetString().ShouldBe("TB_MID");
        profile.GetProperty("publicName").GetString().ShouldBe("Ssekandi");
        // Nothing internal: no birth year, status or names.
        read.GetRawText().ShouldNotContain("2001");
        read.GetRawText().ShouldNotContain("ACTIVE");
        (await PublicBytesAsync(PhotoRoute(john, "LARGE"))).Status.ShouldBe(StatusCodes.Status200OK);
    }

    [Theory]
    [InlineData(PersonTransition.Deactivate)]
    [InlineData(PersonTransition.Archive)]
    [InlineData(PersonTransition.MarkDeceased)]
    public async Task Only_an_active_person_is_shown(PersonTransition transition)
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var john = await CreatePersonAsync(App.Services, kmsa, SystemActor.Instance, JohnSsekandi, overrideReason: null, Ct);
        await OkAsync(await registrar.PutAsync(PhotoPath(john), Form(TestImages.Jpeg(800, 600)), Ct));
        await OkAsync(await registrar.PostAsync(EnablePath(john), null, Ct));
        (await PublicAsync(PublicPath(john))).Status.ShouldBe(StatusCodes.Status200OK);

        await ChangeStatusAsync(App.Services, kmsa, john, transition, Ct);

        (await PublicAsync(PublicPath(john))).ShouldBeProblem(StatusCodes.Status404NotFound, "PERSON_NOT_FOUND");
        (await PublicAsync(PhotoRoute(john, "MEDIUM"))).ShouldBeProblem(StatusCodes.Status404NotFound, "PERSON_NOT_FOUND");
    }

    [Fact]
    public async Task Turning_the_profile_off_hides_the_person_at_once_and_twice_changes_nothing()
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var john = await CreatePersonAsync(App.Services, kmsa, SystemActor.Instance, JohnSsekandi, overrideReason: null, Ct);
        await OkAsync(await registrar.PutAsync(PhotoPath(john), Form(TestImages.Jpeg(800, 600)), Ct));
        var on = await OkAsync(await registrar.PostAsync(EnablePath(john), null, Ct));

        // Turning on again: answered unchanged, the version stays.
        var again = await OkAsync(await registrar.PostAsync(EnablePath(john), null, Ct));
        again.GetProperty("version").GetUInt32().ShouldBe(on.GetProperty("version").GetUInt32());

        using var stale = new HttpRequestMessage(HttpMethod.Post, DisablePath(john));
        stale.Headers.IfMatch.Add(new EntityTagHeaderValue("\"1\""));
        await (await registrar.SendAsync(stale, Ct)).ShouldBeProblemAsync(HttpStatusCode.Conflict, "PERSON_CONCURRENCY_CONFLICT");

        using var current = new HttpRequestMessage(HttpMethod.Post, DisablePath(john));
        current.Headers.IfMatch.Add(new EntityTagHeaderValue($"\"{again.GetProperty("version").GetUInt32()}\""));
        var off = await OkAsync(await registrar.SendAsync(current, Ct));
        off.GetProperty("publicProfileEnabled").GetBoolean().ShouldBeFalse();
        off.GetProperty("version").GetUInt32().ShouldNotBe(on.GetProperty("version").GetUInt32());

        (await PublicAsync(PublicPath(john))).ShouldBeProblem(StatusCodes.Status404NotFound, "PERSON_NOT_FOUND");
        (await PublicAsync(PhotoRoute(john, "THUMBNAIL"))).ShouldBeProblem(StatusCodes.Status404NotFound, "PERSON_NOT_FOUND");
        var offAgain = await OkAsync(await registrar.PostAsync(DisablePath(john), null, Ct));
        offAgain.GetProperty("version").GetUInt32().ShouldBe(off.GetProperty("version").GetUInt32());
    }

    // D9 (owner, S06-T6b plan): an archived or deceased person isn't turned on; turning off works in any status.
    [Theory]
    [InlineData(PersonTransition.Archive, "PERSON_ARCHIVED")]
    [InlineData(PersonTransition.MarkDeceased, "PERSON_DECEASED")]
    public async Task An_archived_or_deceased_person_is_not_turned_on_but_is_turned_off(PersonTransition transition, string refusal)
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var john = await CreatePersonAsync(App.Services, kmsa, SystemActor.Instance, JohnSsekandi, overrideReason: null, Ct);
        await OkAsync(await registrar.PostAsync(EnablePath(john), null, Ct));
        await ChangeStatusAsync(App.Services, kmsa, john, transition, Ct);

        (await OkAsync(await registrar.PostAsync(DisablePath(john), null, Ct))).GetProperty("publicProfileEnabled").GetBoolean().ShouldBeFalse();
        using var refused = await registrar.PostAsync(EnablePath(john), null, Ct);
        await refused.ShouldBeProblemAsync(HttpStatusCode.Conflict, refusal);
    }

    // T6 acceptance 2: withdrawing PHOTO drops the photo from the read and the route answers 404 at once; withdrawing
    // PUBLIC_PROFILE hides the person without touching the flag (D15).
    [Fact]
    public async Task Withdrawing_photo_drops_the_photo_at_once_and_withdrawing_public_profile_hides_the_person()
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var brenda = await CreatePersonAsync(App.Services, kmsa, SystemActor.Instance, BrendaAkello, overrideReason: null, Ct);
        await OkAsync(await registrar.PutAsync(PhotoPath(brenda), Form(TestImages.Jpeg(800, 600)), Ct));
        var consent = await SaveConsentAsync(kmsa, brenda, ConsentScope.DataProcessing, ConsentScope.PublicProfile, ConsentScope.Photo);
        await OkAsync(await registrar.PostAsync(EnablePath(brenda), null, Ct));
        (await PublicAsync(PublicPath(brenda))).Json.GetProperty("photo").ValueKind.ShouldBe(JsonValueKind.Object);
        (await PublicBytesAsync(PhotoRoute(brenda, "MEDIUM"))).Status.ShouldBe(StatusCodes.Status200OK);

        var withdrawn = await OkAsync(await registrar.PostAsJsonAsync(
            WithdrawPath(brenda, consent), new { scopes = _photo, reason = "Guardian asked" }, Ct));

        var read = await PublicAsync(PublicPath(brenda));
        read.Status.ShouldBe(StatusCodes.Status200OK);
        read.Json.GetProperty("photo").ValueKind.ShouldBe(JsonValueKind.Null);
        foreach (var variant in _variants)
        {
            (await PublicAsync(PhotoRoute(brenda, variant))).ShouldBeProblem(StatusCodes.Status404NotFound, "PERSON_PHOTO_NOT_FOUND");
        }

        var successor = withdrawn.GetProperty("consents").EnumerateArray()
            .Single(c => c.GetProperty("status").GetString() == "ACTIVE").GetProperty("id").GetGuid();
        var hidden = await OkAsync(await registrar.PostAsJsonAsync(
            WithdrawPath(brenda, successor), new { scopes = _publicProfile, reason = "Guardian asked" }, Ct));
        hidden.GetProperty("publicProfileEnabled").GetBoolean().ShouldBeTrue();
        (await PublicAsync(PublicPath(brenda))).ShouldBeProblem(StatusCodes.Status404NotFound, "PERSON_NOT_FOUND");
    }

    // T6 acceptance 3 (D13): 18 on the tenant's date (Africa/Kampala, UTC+3), so at 21:00 UTC the day before the
    // birthday, not earlier and not at UTC midnight.
    [Fact]
    public async Task A_person_turning_18_is_an_adult_from_midnight_on_the_tenants_date()
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var kampala = TimeSpan.FromHours(3);
        var local = App.Clock.GetUtcNow().ToOffset(kampala);
        var midnight = new DateTimeOffset(local.Date.AddDays(2), kampala);
        App.Clock.SetUtcNow(midnight.AddMinutes(-1).ToUniversalTime());
        var birthday = DateOnly.FromDateTime(midnight.Date);
        var withConsent = await CreatePersonAsync(
            App.Services, kmsa, SystemActor.Instance, BrendaAkello with { DateOfBirth = birthday.AddYears(-18) }, overrideReason: null, Ct);
        var withoutConsent = await CreatePersonAsync(
            App.Services, kmsa, SystemActor.Instance, DenisOpoka with { DateOfBirth = birthday.AddYears(-18) }, overrideReason: null, Ct);
        await OkAsync(await registrar.PutAsync(PhotoPath(withConsent), Form(TestImages.Jpeg(800, 600)), Ct));
        await SaveConsentAsync(kmsa, withConsent, ConsentScope.PublicProfile);

        // 23:59 in Kampala on the eve of the 18th birthday: a minor.
        App.Clock.GetUtcNow().ShouldBe(new DateTimeOffset(midnight.Date.AddDays(-1).AddHours(20).AddMinutes(59), TimeSpan.Zero));
        await OkAsync(await registrar.PostAsync(EnablePath(withConsent), null, Ct));
        using (var minor = await registrar.PostAsync(EnablePath(withoutConsent), null, Ct))
        {
            await minor.ShouldBeProblemAsync(HttpStatusCode.UnprocessableEntity, "PERSON_CONSENT_REQUIRED");
        }

        var before = (await PublicAsync(PublicPath(withConsent))).Json;
        before.GetProperty("nationality").ValueKind.ShouldBe(JsonValueKind.Null);
        before.GetProperty("photo").ValueKind.ShouldBe(JsonValueKind.Null);
        (await registrar.ReadJsonAsync(PersonPath(withConsent), Ct)).GetProperty("ageStatus").GetString().ShouldBe("MINOR");

        // 00:00 in Kampala (21:00 UTC): an adult.
        App.Clock.Advance(TimeSpan.FromMinutes(1));
        var after = (await PublicAsync(PublicPath(withConsent))).Json;
        after.GetProperty("nationality").GetString().ShouldBe("UG");
        after.GetProperty("photo").ValueKind.ShouldBe(JsonValueKind.Object);
        (await PublicBytesAsync(PhotoRoute(withConsent, "THUMBNAIL"))).Status.ShouldBe(StatusCodes.Status200OK);
        (await registrar.ReadJsonAsync(PersonPath(withConsent), Ct)).GetProperty("ageStatus").GetString().ShouldBe("ADULT");
        await OkAsync(await registrar.PostAsync(EnablePath(withoutConsent), null, Ct));
    }

    // T6 acceptance 4: a person is read only through its own tenant's host; a site that isn't public answers nothing.
    [Fact]
    public async Task Another_tenants_host_and_a_site_that_isnt_public_answer_nothing()
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        await TenantTestData.SaveTenantAsync(App.Services, "elssc", TenantStatus.Active, publicEnabled: true, withDomain: true, Ct);
        await TenantTestData.SaveTenantAsync(App.Services, "jinja", TenantStatus.Active, publicEnabled: false, withDomain: true, Ct);
        var john = await CreatePersonAsync(App.Services, kmsa, SystemActor.Instance, JohnSsekandi, overrideReason: null, Ct);
        await OkAsync(await registrar.PutAsync(PhotoPath(john), Form(TestImages.Jpeg(800, 600)), Ct));
        await OkAsync(await registrar.PostAsync(EnablePath(john), null, Ct));
        (await PublicAsync(PublicPath(john))).Status.ShouldBe(StatusCodes.Status200OK);

        (await App.Server.GetAsync(PublicPath(john), "elssc.localhost")).ShouldBeProblem(StatusCodes.Status404NotFound, "PERSON_NOT_FOUND");
        (await App.Server.GetAsync(PhotoRoute(john, "MEDIUM"), "elssc.localhost"))
            .ShouldBeProblem(StatusCodes.Status404NotFound, "PERSON_NOT_FOUND");
        (await App.Server.GetAsync(PublicPath(john), "jinja.localhost")).ShouldBeProblem(StatusCodes.Status403Forbidden, "TENANT_UNAVAILABLE");
        (await App.Server.GetAsync(PublicPath(john), "unknown.localhost")).ShouldBeProblem(StatusCodes.Status404NotFound, "TENANT_NOT_FOUND");
    }

    [Theory]
    [InlineData("")]
    [InlineData("?variant=medium")]
    [InlineData("?variant=ORIGINAL")]
    public async Task The_photo_route_needs_a_known_variant(string query)
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var john = await CreatePersonAsync(App.Services, kmsa, SystemActor.Instance, JohnSsekandi, overrideReason: null, Ct);
        await OkAsync(await registrar.PostAsync(EnablePath(john), null, Ct));

        (await PublicAsync($"{PublicPath(john)}/photo{query}")).ShouldBeProblem(StatusCodes.Status400BadRequest, "VALIDATION_INVALID_QUERY");
    }

    [Fact]
    public async Task Turning_the_profile_on_and_off_is_audited_by_the_persons_id()
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var brenda = await CreatePersonAsync(App.Services, kmsa, SystemActor.Instance, BrendaAkello, overrideReason: null, Ct);

        using (var refused = await registrar.PostAsync(EnablePath(brenda), null, Ct))
        {
            await refused.ShouldBeProblemAsync(HttpStatusCode.UnprocessableEntity, "PERSON_CONSENT_REQUIRED");
        }

        await SaveConsentAsync(kmsa, brenda, ConsentScope.PublicProfile);
        await OkAsync(await registrar.PostAsync(EnablePath(brenda), null, Ct));
        await OkAsync(await registrar.PostAsync(DisablePath(brenda), null, Ct));

        await AccountTestData.DeliverOutboxAsync(App.Services, Ct);
        var entries = (await AuditTestData.EntriesAsync(App.Services, Ct))
            .Where(e => e.ActionCode.StartsWith("person.public_profile", StringComparison.Ordinal))
            .Select(e => (e.ActionCode, e.Outcome, e.EntityType, e.EntityId))
            .ToList();
        entries.ShouldBe(
        [
            ("person.public_profile.enable", AuditOutcome.Failure, "Person", brenda),
            ("person.public_profile.enable", AuditOutcome.Success, "Person", brenda),
            ("person.public_profile.disable", AuditOutcome.Success, "Person", brenda),
        ], ignoreOrder: true);
    }

    // D21: the public profile is changed by the owner, the admin and the registrar.
    [Theory]
    [InlineData(IdentityRoles.TenantOwner, true)]
    [InlineData(IdentityRoles.TenantAdmin, true)]
    [InlineData(IdentityRoles.Registrar, true)]
    [InlineData(IdentityRoles.CompetitionManager, false)]
    [InlineData(IdentityRoles.ResultsOfficer, false)]
    [InlineData(IdentityRoles.DataEntry, false)]
    [InlineData(IdentityRoles.Viewer, false)]
    [InlineData(IdentityRoles.Scorer, false)]
    public async Task Only_those_who_change_people_may_turn_the_public_profile_on_or_off(string role, bool allowed)
    {
        var (kmsa, client) = await TenantAsync("kmsa", role);
        var john = await SavePersonAsync(App.Services, kmsa, JohnSsekandi, Ct);

        using var enable = await client.PostAsync(EnablePath(john.Id), null, Ct);
        using var disable = await client.PostAsync(DisablePath(john.Id), null, Ct);

        foreach (var response in new[] { enable, disable })
        {
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

    private static string EnablePath(Guid personId) => $"{PersonPath(personId)}/public-profile/enable";

    private static string DisablePath(Guid personId) => $"{PersonPath(personId)}/public-profile/disable";

    private static string PhotoPath(Guid personId) => $"{PersonPath(personId)}/photo";

    private static string WithdrawPath(Guid personId, Guid consentId) => $"{PersonPath(personId)}/consents/{consentId}/withdraw";

    private static string PublicPath(Guid personId) => $"/api/public/v1/people/{personId}";

    private static string PhotoRoute(Guid personId, string variant) => $"{PublicPath(personId)}/photo?variant={variant}";

    private static List<string> PropertiesOf(JsonElement element) => [.. element.EnumerateObject().Select(property => property.Name)];

    private Task<PublicResponse> PublicAsync(string pathAndQuery) => App.Server.GetAsync(pathAndQuery, _kmsaHost);

    /// <summary>A public GET through an <see cref="HttpClient"/> on KMSA's host, kept as bytes (an image).</summary>
    private async Task<(int Status, string? ContentType, string CacheControl, byte[] Body)> PublicBytesAsync(string pathAndQuery)
    {
        using var client = App.Server.CreateClient();
        client.BaseAddress = new Uri($"https://{_kmsaHost}");
        using var response = await client.GetAsync(pathAndQuery, Ct);
        return (
            (int)response.StatusCode,
            response.Content.Headers.ContentType?.MediaType,
            response.Headers.CacheControl?.ToString() ?? string.Empty,
            await response.Content.ReadAsByteArrayAsync(Ct));
    }

    private static MultipartFormDataContent Form(byte[] bytes)
    {
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        return new() { { file, "file", "photo.jpg" } };
    }

    private static async Task<JsonElement> OkAsync(HttpResponseMessage response)
    {
        using (response)
        {
            response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));
            return await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        }
    }

    /// <summary>An active consent with <paramref name="scopes"/>, saved as the system.</summary>
    private async Task<Guid> SaveConsentAsync(Guid tenantId, Guid personId, params ConsentScope[] scopes)
    {
        await using var scope = ScopeFor(App.Services, tenantId, out var context);
        var person = await context.Set<Person>().SingleAsync(p => p.Id == personId, Ct);
        var details = new GuardianConsentDetails(
            "Grace Akello", GuardianRelationship.Mother, "+256 772 410 233", scopes, new DateOnly(2026, 10, 10));
        var consent = person.RecordConsent(details, AgeStatus.Minor, SystemActor.Instance, Now).Value;
        await context.SaveChangesAsync(Ct);
        return consent.Id;
    }

    /// <summary>An active and a retired athlete profile, saved as the system (the read shows only the active one).</summary>
    private async Task SaveAthleteProfilesAsync(Guid tenantId, Guid personId)
    {
        await using var scope = ScopeFor(App.Services, tenantId, out var context);
        var person = await context.Set<Person>().Include(p => p.AthleteProfiles).SingleAsync(p => p.Id == personId, Ct);
        person.AddAthleteProfile(
            TestSportDefinitions.TestballFull, new AthleteProfileDetails("TB_MID", [], null, "Ssekandi"), SystemActor.Instance, Now)
            .IsSuccess.ShouldBeTrue();
        var retired = person.AddAthleteProfile(
            TestSportDefinitions.Testhoop, new AthleteProfileDetails("TH_GUARD", [], null, null), SystemActor.Instance, Now).Value;
        person.ChangeAthleteProfileStatus(retired.Id, ProfileStatus.Retired, "Retired", SystemActor.Instance, Now).IsSuccess.ShouldBeTrue();
        await context.SaveChangesAsync(Ct);
    }

    /// <summary>An active tenant with a public site (time zone Africa/Kampala) and a client of a member with <paramref name="role"/>.</summary>
    private async Task<(Guid Tenant, HttpClient Client)> TenantAsync(string slug, string role)
    {
        App.Authorization.UseIdentityRoles();
        var tenant = await TenantTestData.SaveTenantAsync(App.Services, slug, TenantStatus.Active, publicEnabled: true, withDomain: true, Ct);
        var member = await CreateMemberAsync(App.Services, tenant, MailboxOf(role, $"{slug}.example"), Ct, TenantWide(role));
        return (tenant, App.ClientFor(member.UserId, tenant));
    }
}

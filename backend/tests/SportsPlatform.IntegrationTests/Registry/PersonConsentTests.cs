using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Auditing;
using SportsPlatform.BuildingBlocks.Persistence;
using SportsPlatform.BuildingBlocks.Tenancy;
using SportsPlatform.IntegrationTests.Api;
using SportsPlatform.IntegrationTests.Audit;
using SportsPlatform.IntegrationTests.Identity;
using SportsPlatform.IntegrationTests.Infrastructure;
using SportsPlatform.IntegrationTests.Tenancy;
using SportsPlatform.Modules.Identity.Domain.Roles;
using SportsPlatform.Modules.Platform.Domain.Media;
using SportsPlatform.Modules.Platform.Infrastructure;
using SportsPlatform.Modules.Registry.Domain.People;
using SportsPlatform.Modules.Registry.Domain.People.Consents;
using SportsPlatform.Modules.Tenancy.Contracts.Tenants;
using SportsPlatform.Modules.Tenancy.Features.Access;
using SportsPlatform.UnitTests.Platform.Media;
using static SportsPlatform.IntegrationTests.Identity.MembershipTestData;
using static SportsPlatform.IntegrationTests.Registry.PeopleRequests;
using static SportsPlatform.IntegrationTests.Registry.RegistryTestData;
using AgeStatus = SportsPlatform.Modules.Registry.Contracts.People.AgeStatus;

namespace SportsPlatform.IntegrationTests.Registry;

/// <summary>
/// Guardian consent over HTTP (S06-T6a; D3, D9, D14, D19, D20): recording for anyone not known to be an adult, the
/// scopes in the person read and the guardian only in the private read, withdrawing with a successor, the evidence
/// upload checked by its bytes, the version, <c>X-CSRF</c>, the audit (no <c>PRIVATE</c> value), the roles, the
/// read-only tenant and the <c>STORAGE_GB</c> quota.
/// </summary>
public sealed class PersonConsentTests(PostgresFixture fixture) : DatabaseTest(fixture)
{
    private static readonly object _mother = new
    {
        guardianName = "Grace Akello",
        relationship = "MOTHER",
        contact = "+256 772 410 233",
        scopes = new[] { "DATA_PROCESSING", "PUBLIC_PROFILE", "PHOTO" },
        givenOn = "2026-10-10",
    };

    // The day after the tenant's today on the test clock (the clock's date depends on the tests run before).
    private const string _tomorrow = "tomorrow";

    private static readonly string[] _dataProcessingOnly = ["DATA_PROCESSING"];

    [Fact]
    public async Task A_recorded_consent_shows_its_scopes_to_person_read_and_its_guardian_only_to_the_private_read()
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var brenda = await CreatePersonAsync(App.Services, kmsa, SystemActor.Instance, BrendaAkello, overrideReason: null, Ct);
        var before = await registrar.ReadJsonAsync(PersonPath(brenda), Ct);
        before.GetProperty("activeConsentScopes").GetArrayLength().ShouldBe(0);

        var recorded = await OkAsync(await registrar.PostAsJsonAsync(ConsentsPath(brenda), _mother, Ct));

        recorded.GetProperty("version").GetUInt32().ShouldNotBe(before.GetProperty("version").GetUInt32());
        Strings(recorded.GetProperty("activeConsentScopes")).ShouldBe(["DATA_PROCESSING", "PUBLIC_PROFILE", "PHOTO"]);
        var consent = recorded.GetProperty("consents").EnumerateArray().ShouldHaveSingleItem();
        Strings(consent.GetProperty("scopes")).ShouldBe(["DATA_PROCESSING", "PUBLIC_PROFILE", "PHOTO"]);
        consent.GetProperty("givenOn").GetString().ShouldBe("2026-10-10");
        consent.GetProperty("status").GetString().ShouldBe("ACTIVE");
        consent.GetProperty("hasEvidence").GetBoolean().ShouldBeFalse();
        consent.GetProperty("recordedAt").GetDateTimeOffset().ShouldBe(App.Clock.GetUtcNow());
        var person = recorded.GetRawText();
        person.ShouldNotContain("Grace Akello");
        person.ShouldNotContain("772 410 233");
        person.ShouldNotContain("MOTHER");

        var privateRead = await registrar.ReadJsonAsync(PrivatePath(brenda), Ct);
        var guardian = privateRead.GetProperty("consents").EnumerateArray().ShouldHaveSingleItem();
        guardian.GetProperty("id").GetGuid().ShouldBe(consent.GetProperty("id").GetGuid());
        guardian.GetProperty("guardianName").GetString().ShouldBe("Grace Akello");
        guardian.GetProperty("relationship").GetString().ShouldBe("MOTHER");
        guardian.GetProperty("contact").GetString().ShouldBe("+256 772 410 233");
        guardian.GetProperty("evidence").ValueKind.ShouldBe(JsonValueKind.Null);

        await AccountTestData.DeliverOutboxAsync(App.Services, Ct);
        var entry = (await AuditTestData.EntriesAsync(App.Services, Ct)).Single(e => e.ActionCode == "person.consent.record");
        (entry.Outcome, entry.EntityType, entry.EntityId).ShouldBe((AuditOutcome.Success, "Person", brenda));
        var metadata = JsonDocument.Parse(entry.Metadata!).RootElement;
        metadata.GetProperty("consentId").GetGuid().ShouldBe(consent.GetProperty("id").GetGuid());
        Strings(metadata.GetProperty("scopes")).ShouldBe(["DATA_PROCESSING", "PUBLIC_PROFILE", "PHOTO"]);
        entry.Metadata.ShouldNotBeNull().ShouldNotContain("Grace");
        entry.Metadata.ShouldNotContain("772");
        entry.Metadata.ShouldNotContain("MOTHER");
    }

    [Fact]
    public async Task Consent_is_recorded_for_unknown_age_and_refused_for_an_adult()
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var ivan = await CreatePersonAsync(App.Services, kmsa, SystemActor.Instance, IvanMukasa, overrideReason: null, Ct);
        var john = await CreatePersonAsync(App.Services, kmsa, SystemActor.Instance, JohnSsekandi, overrideReason: null, Ct);

        await OkAsync(await registrar.PostAsJsonAsync(ConsentsPath(ivan), _mother, Ct));
        using var adult = await registrar.PostAsJsonAsync(ConsentsPath(john), _mother, Ct);

        await adult.ShouldBeProblemAsync(HttpStatusCode.UnprocessableEntity, "PERSON_CONSENT_NOT_NEEDED");
        (await ConsentsOfAsync(kmsa, john)).ShouldBeEmpty();
    }

    [Theory]
    [InlineData("givenOn", _tomorrow)] // After the tenant's today.
    [InlineData("givenOn", "2011-03-01")] // Before Brenda's date of birth.
    [InlineData("givenOn", null)]
    [InlineData("guardianName", " ")]
    [InlineData("contact", null)]
    [InlineData("relationship", null)]
    public async Task A_consent_with_an_invalid_field_is_refused_on_it(string field, string? value)
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var brenda = await CreatePersonAsync(App.Services, kmsa, SystemActor.Instance, BrendaAkello, overrideReason: null, Ct);

        if (value == _tomorrow)
        {
            var kampala = TimeZoneInfo.FindSystemTimeZoneById("Africa/Kampala");
            value = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(App.Clock.GetUtcNow(), kampala).DateTime).AddDays(1).ToString("O", CultureInfo.InvariantCulture);
        }

        using var response = await registrar.PostAsJsonAsync(ConsentsPath(brenda), With(_mother, field, value), Ct);

        var problem = await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_INVALID_REQUEST");
        Fields(problem).ShouldBe([(field, "VALIDATION_INVALID_REQUEST")]);
        (await ConsentsOfAsync(kmsa, brenda)).ShouldBeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData("PHOTO,PHOTO")]
    public async Task A_consent_needs_distinct_scopes(string list)
    {
        var scopes = list.Split(',', StringSplitOptions.RemoveEmptyEntries);
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var brenda = await CreatePersonAsync(App.Services, kmsa, SystemActor.Instance, BrendaAkello, overrideReason: null, Ct);

        using var response = await registrar.PostAsJsonAsync(ConsentsPath(brenda), With(_mother, "scopes", scopes), Ct);

        var problem = await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_INVALID_REQUEST");
        Fields(problem).ShouldBe([("scopes", "VALIDATION_INVALID_REQUEST")]);
    }

    [Theory]
    [InlineData(PersonTransition.Archive, "PERSON_ARCHIVED")]
    [InlineData(PersonTransition.MarkDeceased, "PERSON_DECEASED")]
    public async Task An_archived_or_deceased_person_takes_no_new_consent_or_evidence_but_one_can_be_withdrawn(
        PersonTransition transition, string code)
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var brenda = await CreatePersonAsync(App.Services, kmsa, SystemActor.Instance, BrendaAkello, overrideReason: null, Ct);
        var consent = ConsentId(await OkAsync(await registrar.PostAsJsonAsync(ConsentsPath(brenda), _mother, Ct)));
        await ChangeStatusAsync(App.Services, kmsa, brenda, transition, Ct);

        using (var record = await registrar.PostAsJsonAsync(ConsentsPath(brenda), _mother, Ct))
        {
            await record.ShouldBeProblemAsync(HttpStatusCode.Conflict, code);
        }

        using (var upload = await registrar.PutAsync(EvidencePath(brenda, consent), Form(TestImages.Pdf()), Ct))
        {
            await upload.ShouldBeProblemAsync(HttpStatusCode.Conflict, code);
        }

        (await AssetCountAsync(kmsa)).ShouldBe(0);
        await OkAsync(await registrar.PostAsJsonAsync(
            WithdrawPath(brenda, consent), Withdrawal("Guardian asked", "PHOTO"), Ct));
    }

    [Fact]
    public async Task Withdrawing_a_scope_keeps_the_record_and_the_rest_in_a_successor()
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var brenda = await CreatePersonAsync(App.Services, kmsa, SystemActor.Instance, BrendaAkello, overrideReason: null, Ct);
        var consent = ConsentId(await OkAsync(await registrar.PostAsJsonAsync(ConsentsPath(brenda), _mother, Ct)));
        App.Clock.Advance(TimeSpan.FromDays(2));

        var withdrawn = await OkAsync(await registrar.PostAsJsonAsync(
            WithdrawPath(brenda, consent), Withdrawal("Guardian asked", "PHOTO"), Ct));

        Strings(withdrawn.GetProperty("activeConsentScopes")).ShouldBe(["DATA_PROCESSING", "PUBLIC_PROFILE"]);
        var consents = withdrawn.GetProperty("consents").EnumerateArray().ToList();
        consents.Count.ShouldBe(2);
        var closed = consents.Single(c => c.GetProperty("id").GetGuid() == consent);
        closed.GetProperty("status").GetString().ShouldBe("WITHDRAWN");
        closed.GetProperty("withdrawalReason").GetString().ShouldBe("Guardian asked");
        closed.GetProperty("withdrawnAt").GetDateTimeOffset().ShouldBe(App.Clock.GetUtcNow());
        var successor = consents.Single(c => c.GetProperty("id").GetGuid() != consent);
        successor.GetProperty("status").GetString().ShouldBe("ACTIVE");
        successor.GetProperty("replacesConsentId").GetGuid().ShouldBe(consent);
        Strings(successor.GetProperty("scopes")).ShouldBe(["DATA_PROCESSING", "PUBLIC_PROFILE"]);
        successor.GetProperty("givenOn").GetString().ShouldBe("2026-10-10");
        var guardian = (await registrar.ReadJsonAsync(PrivatePath(brenda), Ct)).GetProperty("consents").EnumerateArray()
            .Single(c => c.GetProperty("id").GetGuid() == successor.GetProperty("id").GetGuid());
        guardian.GetProperty("guardianName").GetString().ShouldBe("Grace Akello");

        // Withdrawing the rest records no successor.
        var successorId = successor.GetProperty("id").GetGuid();
        var all = await OkAsync(await registrar.PostAsJsonAsync(
            WithdrawPath(brenda, successorId), Withdrawal("Left the club", "DATA_PROCESSING", "PUBLIC_PROFILE"), Ct));
        all.GetProperty("activeConsentScopes").GetArrayLength().ShouldBe(0);
        all.GetProperty("consents").GetArrayLength().ShouldBe(2);

        await AccountTestData.DeliverOutboxAsync(App.Services, Ct);
        var entries = (await AuditTestData.EntriesAsync(App.Services, Ct)).Where(e => e.ActionCode == "person.consent.withdraw").ToList();
        entries.Select(e => e.Reason).ShouldBe(["Guardian asked", "Left the club"], ignoreOrder: true);
        var first = JsonDocument.Parse(entries.Single(e => e.Reason == "Guardian asked").Metadata!).RootElement;
        first.GetProperty("consentId").GetGuid().ShouldBe(consent);
        Strings(first.GetProperty("scopes")).ShouldBe(["PHOTO"]);
        first.GetProperty("successorConsentId").GetGuid().ShouldBe(successorId);
    }

    [Fact]
    public async Task Withdrawing_is_refused_for_a_withdrawn_or_unknown_consent_a_scope_it_does_not_give_or_no_reason()
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var brenda = await CreatePersonAsync(App.Services, kmsa, SystemActor.Instance, BrendaAkello, overrideReason: null, Ct);
        var consent = ConsentId(await OkAsync(await registrar.PostAsJsonAsync(
            ConsentsPath(brenda), With(_mother, "scopes", _dataProcessingOnly), Ct)));

        using (var notGiven = await registrar.PostAsJsonAsync(
            WithdrawPath(brenda, consent), Withdrawal("Guardian asked", "PHOTO"), Ct))
        {
            var problem = await notGiven.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_INVALID_REQUEST");
            Fields(problem).ShouldBe([("scopes", "VALIDATION_INVALID_REQUEST")]);
        }

        using (var noReason = await registrar.PostAsJsonAsync(
            WithdrawPath(brenda, consent), Withdrawal(" ", "DATA_PROCESSING"), Ct))
        {
            var problem = await noReason.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_INVALID_REQUEST");
            Fields(problem).ShouldBe([("reason", "VALIDATION_REASON_REQUIRED")]);
        }

        using (var unknown = await registrar.PostAsJsonAsync(
            WithdrawPath(brenda, Guid.CreateVersion7()), Withdrawal("Guardian asked", "DATA_PROCESSING"), Ct))
        {
            await unknown.ShouldBeProblemAsync(HttpStatusCode.NotFound, "PERSON_CONSENT_NOT_FOUND");
        }

        await OkAsync(await registrar.PostAsJsonAsync(
            WithdrawPath(brenda, consent), Withdrawal("Guardian asked", "DATA_PROCESSING"), Ct));
        using var again = await registrar.PostAsJsonAsync(
            WithdrawPath(brenda, consent), Withdrawal("Guardian asked", "DATA_PROCESSING"), Ct);
        await again.ShouldBeProblemAsync(HttpStatusCode.Conflict, "PERSON_CONSENT_WITHDRAWN");
    }

    [Fact]
    public async Task Evidence_is_linked_only_by_the_private_read_and_replacing_it_removes_the_old_file()
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var brenda = await CreatePersonAsync(App.Services, kmsa, SystemActor.Instance, BrendaAkello, overrideReason: null, Ct);
        var consent = ConsentId(await OkAsync(await registrar.PostAsJsonAsync(ConsentsPath(brenda), _mother, Ct)));
        var pdf = TestImages.Pdf();

        var uploaded = await OkAsync(await registrar.PutAsync(EvidencePath(brenda, consent), Form(pdf, "application/pdf"), Ct));

        uploaded.GetProperty("consents").EnumerateArray().ShouldHaveSingleItem().GetProperty("hasEvidence").GetBoolean().ShouldBeTrue();
        uploaded.GetRawText().ShouldNotContain("/api/public/v1/media/");
        var evidence = (await registrar.ReadJsonAsync(PrivatePath(brenda), Ct))
            .GetProperty("consents").EnumerateArray().Single().GetProperty("evidence");
        evidence.GetProperty("expiresAt").GetDateTimeOffset().ShouldBe(App.Clock.GetUtcNow() + TimeSpan.FromMinutes(10));
        var url = evidence.GetProperty("url").GetString();
        using (var served = await registrar.GetAsync(url, Ct))
        {
            served.StatusCode.ShouldBe(HttpStatusCode.OK);
            served.Content.Headers.ContentType!.MediaType.ShouldBe("application/pdf");
            (await served.Content.ReadAsByteArrayAsync(Ct)).ShouldBe(pdf);
        }

        var first = await EvidenceAssetIdAsync(kmsa, consent);
        await OkAsync(await registrar.PutAsync(EvidencePath(brenda, consent), Form(TestImages.Jpeg(1200, 1600)), Ct));

        var second = await EvidenceAssetIdAsync(kmsa, consent);
        second.ShouldNotBe(first);
        (await AssetStatusAsync(kmsa, first)).ShouldBe(MediaAssetStatus.Removed);
        (await AssetStatusAsync(kmsa, second)).ShouldBe(MediaAssetStatus.Active);

        await AccountTestData.DeliverOutboxAsync(App.Services, Ct);
        var entries = (await AuditTestData.EntriesAsync(App.Services, Ct)).Where(e => e.ActionCode == "person.consent_evidence.upload").ToList();
        entries.Count.ShouldBe(2);
        entries.ShouldAllBe(e => e.Outcome == AuditOutcome.Success && e.EntityId == brenda);
        var metadata = entries.Select(e => JsonDocument.Parse(e.Metadata!).RootElement)
            .Select(m => (m.GetProperty("consentId").GetGuid(), m.GetProperty("assetId").GetGuid(), m.GetProperty("replacedAssetId").GetString()))
            .ToList();
        metadata.ShouldBe([(consent, first, null), (consent, second, first.ToString())], ignoreOrder: true);
    }

    // A partial withdrawal's successor names the same evidence: replacing it there keeps the withdrawn record's file.
    [Fact]
    public async Task Replacing_evidence_a_withdrawn_record_still_names_keeps_its_file()
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var brenda = await CreatePersonAsync(App.Services, kmsa, SystemActor.Instance, BrendaAkello, overrideReason: null, Ct);
        var consent = ConsentId(await OkAsync(await registrar.PostAsJsonAsync(ConsentsPath(brenda), _mother, Ct)));
        await OkAsync(await registrar.PutAsync(EvidencePath(brenda, consent), Form(TestImages.Pdf(), "application/pdf"), Ct));
        var shared = await EvidenceAssetIdAsync(kmsa, consent);
        var withdrawn = await OkAsync(await registrar.PostAsJsonAsync(
            WithdrawPath(brenda, consent), Withdrawal("Guardian asked", "PHOTO"), Ct));
        var successor = withdrawn.GetProperty("consents").EnumerateArray()
            .Single(c => c.GetProperty("status").GetString() == "ACTIVE").GetProperty("id").GetGuid();
        (await EvidenceAssetIdAsync(kmsa, successor)).ShouldBe(shared);

        using (var onWithdrawn = await registrar.PutAsync(EvidencePath(brenda, consent), Form(TestImages.Pdf(), "application/pdf"), Ct))
        {
            await onWithdrawn.ShouldBeProblemAsync(HttpStatusCode.Conflict, "PERSON_CONSENT_WITHDRAWN");
        }

        await OkAsync(await registrar.PutAsync(EvidencePath(brenda, successor), Form(TestImages.Png(800, 1000), "image/png"), Ct));

        (await EvidenceAssetIdAsync(kmsa, successor)).ShouldNotBe(shared);
        (await AssetStatusAsync(kmsa, shared)).ShouldBe(MediaAssetStatus.Active);
        var links = (await registrar.ReadJsonAsync(PrivatePath(brenda), Ct)).GetProperty("consents").EnumerateArray()
            .Select(c => c.GetProperty("evidence").GetProperty("url").GetString()).ToList();
        links.Count.ShouldBe(2);
        links.Distinct().Count().ShouldBe(2);
    }

    [Fact]
    public async Task Evidence_whose_bytes_are_not_a_document_type_is_refused_on_the_file()
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var brenda = await CreatePersonAsync(App.Services, kmsa, SystemActor.Instance, BrendaAkello, overrideReason: null, Ct);
        var consent = ConsentId(await OkAsync(await registrar.PostAsJsonAsync(ConsentsPath(brenda), _mother, Ct)));

        using (var webp = await registrar.PutAsync(EvidencePath(brenda, consent), Form(TestImages.WebP(64, 64), "application/pdf"), Ct))
        {
            var problem = await webp.ShouldBeProblemAsync(HttpStatusCode.UnprocessableEntity, "MEDIA_TYPE_NOT_ALLOWED");
            Fields(problem).ShouldBe([("file", "MEDIA_TYPE_NOT_ALLOWED")]);
        }

        using (var twoFiles = Form(TestImages.Pdf(), "application/pdf"))
        {
            twoFiles.Add(new ByteArrayContent(TestImages.Pdf()), "file", "second.pdf");
            using var response = await registrar.PutAsync(EvidencePath(brenda, consent), twoFiles, Ct);
            var problem = await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_INVALID_REQUEST");
            Fields(problem).ShouldBe([("file", "VALIDATION_INVALID_REQUEST")]);
        }

        (await AssetCountAsync(kmsa)).ShouldBe(0);
    }

    // D19: the form isn't checked by antiforgery, so X-CSRF still is.
    [Fact]
    public async Task An_evidence_upload_without_X_CSRF_is_refused()
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var brenda = await CreatePersonAsync(App.Services, kmsa, SystemActor.Instance, BrendaAkello, overrideReason: null, Ct);
        var consent = ConsentId(await OkAsync(await registrar.PostAsJsonAsync(ConsentsPath(brenda), _mother, Ct)));

        using var response = await registrar.WithoutCsrfHeader()
            .PutAsync(EvidencePath(brenda, consent), Form(TestImages.Pdf(), "application/pdf"), Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "AUTH_CSRF_REQUIRED");
        (await AssetCountAsync(kmsa)).ShouldBe(0);
    }

    [Fact]
    public async Task At_the_storage_limit_evidence_is_refused_before_anything_is_stored()
    {
        var (kmsa, owner) = await TenantAsync("kmsa", IdentityRoles.TenantOwner);
        var brenda = await CreatePersonAsync(App.Services, kmsa, SystemActor.Instance, BrendaAkello, overrideReason: null, Ct);
        var consent = ConsentId(await OkAsync(await owner.PostAsJsonAsync(ConsentsPath(brenda), _mother, Ct)));
        await OverrideStorageAsync(kmsa, limit: 0);

        using var refused = await owner.PutAsync(EvidencePath(brenda, consent), Form(TestImages.Pdf(), "application/pdf"), Ct);

        var problem = await refused.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "ENTITLEMENT_QUOTA_EXCEEDED");
        problem.GetProperty("detail").GetString().ShouldBe("Your plan allows 0 GB of storage.");
        (await AssetCountAsync(kmsa)).ShouldBe(0);
    }

    [Fact]
    public async Task A_stale_If_Match_is_a_conflict()
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var brenda = await CreatePersonAsync(App.Services, kmsa, SystemActor.Instance, BrendaAkello, overrideReason: null, Ct);
        var read = await OkAsync(await registrar.PostAsJsonAsync(ConsentsPath(brenda), _mother, Ct));
        var version = read.GetProperty("version").GetUInt32();
        var consent = ConsentId(read);
        await OkAsync(await registrar.PostAsJsonAsync($"{PersonPath(brenda)}/aliases", new { name = "Bree", kind = "NICKNAME" }, Ct));

        foreach (var request in new[]
        {
            new HttpRequestMessage(HttpMethod.Post, ConsentsPath(brenda)) { Content = JsonContent.Create(_mother) },
            new HttpRequestMessage(HttpMethod.Post, WithdrawPath(brenda, consent))
            {
                Content = JsonContent.Create(Withdrawal("Guardian asked", "PHOTO")),
            },
            new HttpRequestMessage(HttpMethod.Put, EvidencePath(brenda, consent)) { Content = Form(TestImages.Pdf(), "application/pdf") },
        })
        {
            using (request)
            {
                request.Headers.IfMatch.Add(new EntityTagHeaderValue($"\"{version}\""));
                using var response = await registrar.SendAsync(request, Ct);
                await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, "PERSON_CONCURRENCY_CONFLICT");
            }
        }

        (await ConsentsOfAsync(kmsa, brenda)).ShouldHaveSingleItem().Status.ShouldBe(ConsentStatus.Active);
        (await AssetCountAsync(kmsa)).ShouldBe(0);
    }

    [Fact]
    public async Task Another_tenants_person_is_not_found()
    {
        var (_, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var (elssc, theirRegistrar) = await TenantAsync("elssc", IdentityRoles.Registrar);
        var theirs = await CreatePersonAsync(App.Services, elssc, SystemActor.Instance, BrendaAkello, overrideReason: null, Ct);
        var consent = ConsentId(await OkAsync(await theirRegistrar.PostAsJsonAsync(ConsentsPath(theirs), _mother, Ct)));

        using (var record = await registrar.PostAsJsonAsync(ConsentsPath(theirs), _mother, Ct))
        {
            await record.ShouldBeProblemAsync(HttpStatusCode.NotFound, "PERSON_NOT_FOUND");
        }

        using (var withdraw = await registrar.PostAsJsonAsync(
            WithdrawPath(theirs, consent), Withdrawal("Guardian asked", "PHOTO"), Ct))
        {
            await withdraw.ShouldBeProblemAsync(HttpStatusCode.NotFound, "PERSON_NOT_FOUND");
        }

        using var upload = await registrar.PutAsync(EvidencePath(theirs, consent), Form(TestImages.Pdf(), "application/pdf"), Ct);
        await upload.ShouldBeProblemAsync(HttpStatusCode.NotFound, "PERSON_NOT_FOUND");
        (await ConsentsOfAsync(elssc, theirs)).ShouldHaveSingleItem().Status.ShouldBe(ConsentStatus.Active);
    }

    // D21: consents are changed by the owner, the admin and the registrar.
    [Theory]
    [InlineData(IdentityRoles.TenantOwner, true)]
    [InlineData(IdentityRoles.TenantAdmin, true)]
    [InlineData(IdentityRoles.Registrar, true)]
    [InlineData(IdentityRoles.CompetitionManager, false)]
    [InlineData(IdentityRoles.ResultsOfficer, false)]
    [InlineData(IdentityRoles.DataEntry, false)]
    [InlineData(IdentityRoles.Viewer, false)]
    [InlineData(IdentityRoles.Scorer, false)]
    public async Task Only_those_who_change_people_may_record_withdraw_or_upload_evidence(string role, bool allowed)
    {
        var (kmsa, client) = await TenantAsync("kmsa", role);
        var brenda = await SavePersonAsync(App.Services, kmsa, BrendaAkello, Ct);
        var existing = await SaveConsentAsync(kmsa, brenda.Id);

        using var record = await client.PostAsJsonAsync(ConsentsPath(brenda.Id), _mother, Ct);
        using var upload = await client.PutAsync(EvidencePath(brenda.Id, existing), Form(TestImages.Pdf(), "application/pdf"), Ct);
        using var withdraw = await client.PostAsJsonAsync(
            WithdrawPath(brenda.Id, existing), Withdrawal("Guardian asked", "PHOTO"), Ct);

        foreach (var response in new[] { record, upload, withdraw })
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

    [Fact]
    public async Task A_read_only_tenant_changes_no_consent()
    {
        App.Authorization.UseIdentityRoles();
        var kmsa = await TenantTestData.SaveTenantAsync(
            App.Services, "kmsa", TenantStatus.Active, publicEnabled: false, withDomain: true, withSubscription: false, Ct);
        await TenantTestData.SavePeriodAsync(App.Services, kmsa, new DateOnly(2025, 1, 1), new DateOnly(2025, 12, 31), Ct, cancelReason: "Pilot paused");
        var brenda = await SavePersonAsync(App.Services, kmsa, BrendaAkello, Ct);
        var existing = await SaveConsentAsync(kmsa, brenda.Id);
        var member = await CreateMemberAsync(App.Services, kmsa, "brian.ssemakula@kmsa.example", Ct, TenantWide(IdentityRoles.Registrar));
        using var registrar = App.ClientFor(member.UserId, kmsa);

        using var record = await registrar.PostAsJsonAsync(ConsentsPath(brenda.Id), _mother, Ct);
        using var upload = await registrar.PutAsync(EvidencePath(brenda.Id, existing), Form(TestImages.Pdf(), "application/pdf"), Ct);
        using var withdraw = await registrar.PostAsJsonAsync(
            WithdrawPath(brenda.Id, existing), Withdrawal("Guardian asked", "PHOTO"), Ct);

        foreach (var response in new[] { record, upload, withdraw })
        {
            await response.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "TENANT_READ_ONLY");
        }

        // The private read, guardians included, still answers.
        (await registrar.ReadJsonAsync(PrivatePath(brenda.Id), Ct)).GetProperty("consents").GetArrayLength().ShouldBe(1);
    }

    private static object Withdrawal(string reason, params string[] scopes) => new { scopes, reason };

    private static string ConsentsPath(Guid personId) => $"{PersonPath(personId)}/consents";

    private static string WithdrawPath(Guid personId, Guid consentId) => $"{ConsentsPath(personId)}/{consentId}/withdraw";

    private static string EvidencePath(Guid personId, Guid consentId) => $"{ConsentsPath(personId)}/{consentId}/evidence";

    private static Guid ConsentId(JsonElement person) =>
        person.GetProperty("consents").EnumerateArray().Last(c => c.GetProperty("status").GetString() == "ACTIVE").GetProperty("id").GetGuid();

    private static List<string?> Strings(JsonElement array) => [.. array.EnumerateArray().Select(item => item.GetString())];

    private static MultipartFormDataContent Form(byte[] bytes, string contentType = "image/jpeg")
    {
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        return new() { { file, "file", "consent-form" } };
    }

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

    /// <summary>An active consent with every scope, saved as the system (the role tests' starting point).</summary>
    private async Task<Guid> SaveConsentAsync(Guid tenantId, Guid personId)
    {
        await using var scope = ScopeFor(App.Services, tenantId, out var context);
        var person = await context.Set<Person>().SingleAsync(p => p.Id == personId, Ct);
        var details = new GuardianConsentDetails(
            "Grace Akello", GuardianRelationship.Mother, "+256 772 410 233",
            [ConsentScope.DataProcessing, ConsentScope.PublicProfile, ConsentScope.Photo], new DateOnly(2026, 10, 10));
        var consent = person.RecordConsent(details, AgeStatus.Minor, SystemActor.Instance, Now).Value;
        await context.SaveChangesAsync(Ct);
        return consent.Id;
    }

    private async Task<List<GuardianConsent>> ConsentsOfAsync(Guid tenantId, Guid personId)
    {
        await using var scope = ScopeFor(App.Services, tenantId, out var context);
        return await context.Set<GuardianConsent>().AsNoTracking().Where(c => c.PersonId == personId).ToListAsync(Ct);
    }

    private async Task<Guid> EvidenceAssetIdAsync(Guid tenantId, Guid consentId)
    {
        await using var scope = ScopeFor(App.Services, tenantId, out var context);
        return (await context.Set<GuardianConsent>().Where(c => c.Id == consentId).Select(c => c.EvidenceAssetId).SingleAsync(Ct))
            .ShouldNotBeNull();
    }

    private async Task<MediaAssetStatus> AssetStatusAsync(Guid tenantId, Guid assetId)
    {
        await using var scope = App.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Set(tenantId);
        var context = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        return await context.Set<MediaAsset>().Where(a => a.Id == assetId).Select(a => a.Status).SingleAsync(Ct);
    }

    private async Task<long> AssetCountAsync(Guid tenantId) =>
        (await QueryAsync(DatabaseRoles.Platform, "SELECT count(*) FROM platform.media_assets WHERE tenant_id = @tenant",
            reader => reader.GetInt64(0), new Npgsql.NpgsqlParameter("tenant", tenantId))).Single();

    /// <summary>An override setting <c>STORAGE_GB</c> to <paramref name="limit"/> (as the platform records one).</summary>
    private async Task OverrideStorageAsync(Guid tenantId, int limit)
    {
        await ExecuteAsync(
            DatabaseRoles.App,
            $"SELECT set_config('app.tenant_id', '{tenantId}', false); " +
            "INSERT INTO tenancy.tenant_entitlement_overrides (id, tenant_id, entitlement_code, value, reason, " +
            "created_at, created_by, updated_at, updated_by) " +
            $"VALUES ('{Guid.CreateVersion7()}', '{tenantId}', 'STORAGE_GB', '{{\"limit\": {limit}}}', 'Storage paused for the pilot', " +
            $"now(), '{TenantTestData.StaffId}', now(), '{TenantTestData.StaffId}')");
        App.Services.GetRequiredService<TenantAccessCache>().Clear(tenantId);
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

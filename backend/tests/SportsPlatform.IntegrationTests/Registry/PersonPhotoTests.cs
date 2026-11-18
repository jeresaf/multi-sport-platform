using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
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
using SportsPlatform.Modules.Tenancy.Contracts.Tenants;
using SportsPlatform.Modules.Tenancy.Features.Access;
using SportsPlatform.UnitTests.Platform.Media;
using static SportsPlatform.IntegrationTests.Identity.MembershipTestData;
using static SportsPlatform.IntegrationTests.Registry.PeopleRequests;
using static SportsPlatform.IntegrationTests.Registry.RegistryTestData;

namespace SportsPlatform.IntegrationTests.Registry;

/// <summary>
/// A person's photo over HTTP (S06-T5b; D16, D17, D19, D20): the multipart upload checked by the file's bytes, the signed
/// URLs in the person read, replacing and removing (the old asset marked <c>REMOVED</c>), the archived and deceased
/// guards, the version, <c>X-CSRF</c>, the audit, the roles, the read-only tenant and the <c>STORAGE_GB</c> quota.
/// </summary>
public sealed class PersonPhotoTests(PostgresFixture fixture) : DatabaseTest(fixture)
{
    [Fact]
    public async Task A_photo_is_shown_as_signed_URLs_and_replacing_it_removes_the_old_one()
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var person = await CreatePersonAsync(App.Services, kmsa, SystemActor.Instance, DenisOpoka, overrideReason: null, Ct);
        var before = await registrar.ReadJsonAsync(PersonPath(person), Ct);
        before.GetProperty("photo").ValueKind.ShouldBe(JsonValueKind.Null);

        var set = await OkAsync(await registrar.PutAsync(PhotoPath(person), Form(TestImages.Jpeg(2000, 1500)), Ct));

        set.GetProperty("version").GetUInt32().ShouldNotBe(before.GetProperty("version").GetUInt32());
        var photo = set.GetProperty("photo");
        photo.GetProperty("expiresAt").GetDateTimeOffset().ShouldBe(App.Clock.GetUtcNow() + TimeSpan.FromMinutes(10));
        foreach (var (property, size) in new[] { ("thumbnailUrl", (160, 120)), ("mediumUrl", (640, 480)), ("largeUrl", (1600, 1200)) })
        {
            using var served = await registrar.GetAsync(photo.GetProperty(property).GetString(), Ct);
            served.StatusCode.ShouldBe(HttpStatusCode.OK, property);
            served.Content.Headers.ContentType!.MediaType.ShouldBe("image/webp");
            TestImages.SizeOf(await served.Content.ReadAsByteArrayAsync(Ct)).ShouldBe(size);
        }

        var read = await registrar.ReadJsonAsync(PersonPath(person), Ct);
        read.GetProperty("photo").GetProperty("largeUrl").GetString().ShouldStartWith("/api/public/v1/media/");
        var first = await PhotoAssetIdAsync(kmsa, person);

        var replaced = await OkAsync(await registrar.PutAsync(PhotoPath(person), Form(TestImages.Png(800, 800), "image/png"), Ct));

        var second = await PhotoAssetIdAsync(kmsa, person);
        second.ShouldNotBe(first);
        (await AssetStatusAsync(kmsa, first)).ShouldBe(MediaAssetStatus.Removed);
        (await AssetStatusAsync(kmsa, second)).ShouldBe(MediaAssetStatus.Active);
        using (var old = await registrar.GetAsync(photo.GetProperty("mediumUrl").GetString(), Ct))
        {
            await old.ShouldBeProblemAsync(HttpStatusCode.NotFound, "MEDIA_NOT_FOUND");
        }

        replaced.GetProperty("photo").GetProperty("mediumUrl").GetString().ShouldNotBe(photo.GetProperty("mediumUrl").GetString());

        await AccountTestData.DeliverOutboxAsync(App.Services, Ct);
        var entries = (await AuditTestData.EntriesAsync(App.Services, Ct)).Where(e => e.ActionCode == "person.photo.set").ToList();
        entries.Count.ShouldBe(2);
        entries.ShouldAllBe(e => e.Outcome == AuditOutcome.Success && e.EntityType == "Person" && e.EntityId == person);
        var metadata = entries.Select(e => JsonDocument.Parse(e.Metadata!).RootElement)
            .Select(m => (m.GetProperty("assetId").GetGuid(), m.GetProperty("replacedAssetId").GetString()))
            .ToList();
        metadata.ShouldBe([(first, null), (second, first.ToString())], ignoreOrder: true);
    }

    [Fact]
    public async Task Removing_the_photo_works_in_any_status_and_without_one_is_not_found()
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var person = await CreatePersonAsync(App.Services, kmsa, SystemActor.Instance, IvanMukasa, overrideReason: null, Ct);
        await OkAsync(await registrar.PutAsync(PhotoPath(person), Form(TestImages.Jpeg(640, 480)), Ct));
        var asset = await PhotoAssetIdAsync(kmsa, person);
        await ChangeStatusAsync(App.Services, kmsa, person, PersonTransition.Archive, Ct);

        var removed = await OkAsync(await registrar.DeleteAsync(PhotoPath(person), Ct));

        removed.GetProperty("photo").ValueKind.ShouldBe(JsonValueKind.Null);
        (await PhotoAssetIdOrNullAsync(kmsa, person)).ShouldBeNull();
        (await AssetStatusAsync(kmsa, asset)).ShouldBe(MediaAssetStatus.Removed);
        using var again = await registrar.DeleteAsync(PhotoPath(person), Ct);
        await again.ShouldBeProblemAsync(HttpStatusCode.NotFound, "PERSON_PHOTO_NOT_FOUND");

        await AccountTestData.DeliverOutboxAsync(App.Services, Ct);
        var entries = (await AuditTestData.EntriesAsync(App.Services, Ct)).Where(e => e.ActionCode == "person.photo.remove").ToList();
        entries.Select(e => (e.Outcome, e.ErrorCode)).ShouldBe(
            [(AuditOutcome.Success, null), (AuditOutcome.Failure, "PERSON_PHOTO_NOT_FOUND")], ignoreOrder: true);
        JsonDocument.Parse(entries.Single(e => e.Outcome == AuditOutcome.Success).Metadata!).RootElement
            .GetProperty("replacedAssetId").GetGuid().ShouldBe(asset);
    }

    // T5 acceptance: the file's bytes decide, whatever content type the client sends.
    [Theory]
    [InlineData("pdf")]
    [InlineData("html")]
    [InlineData("text")]
    public async Task A_file_whose_bytes_are_not_an_allowed_type_is_refused_whatever_its_content_type(string kind)
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var person = await CreatePersonAsync(App.Services, kmsa, SystemActor.Instance, IvanMukasa, overrideReason: null, Ct);
        var bytes = kind switch
        {
            "pdf" => TestImages.Pdf(),
            "html" => Encoding.UTF8.GetBytes("<html><body><script>alert('photo.jpg')</script></body></html>"),
            _ => Encoding.UTF8.GetBytes("Ivan Mukasa, Kampala"),
        };

        using var response = await registrar.PutAsync(PhotoPath(person), Form(bytes, "image/jpeg"), Ct);

        var problem = await response.ShouldBeProblemAsync(HttpStatusCode.UnprocessableEntity, "MEDIA_TYPE_NOT_ALLOWED");
        Fields(problem).ShouldBe([("file", "MEDIA_TYPE_NOT_ALLOWED")]);
        await ShouldHaveNoPhotoAsync(kmsa, person);
    }

    // T5 acceptance: 5 MB + 1 byte is 413.
    [Fact]
    public async Task A_photo_one_byte_over_5_MB_is_too_large()
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var person = await CreatePersonAsync(App.Services, kmsa, SystemActor.Instance, IvanMukasa, overrideReason: null, Ct);
        var oversized = new byte[(5 * 1024 * 1024) + 1];
        TestImages.Jpeg(64, 64).CopyTo(oversized, 0);

        using var response = await registrar.PutAsync(PhotoPath(person), Form(oversized), Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.RequestEntityTooLarge, "MEDIA_TOO_LARGE");
        await ShouldHaveNoPhotoAsync(kmsa, person);
    }

    [Fact]
    public async Task A_truncated_image_is_unreadable()
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var person = await CreatePersonAsync(App.Services, kmsa, SystemActor.Instance, IvanMukasa, overrideReason: null, Ct);
        var jpeg = TestImages.Jpeg(800, 600);

        using var response = await registrar.PutAsync(PhotoPath(person), Form(jpeg[..(jpeg.Length * 2 / 3)]), Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.UnprocessableEntity, "MEDIA_IMAGE_UNREADABLE");
        await ShouldHaveNoPhotoAsync(kmsa, person);
    }

    [Fact]
    public async Task The_form_must_carry_one_file_in_the_file_field_and_nothing_else()
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var person = await CreatePersonAsync(App.Services, kmsa, SystemActor.Instance, IvanMukasa, overrideReason: null, Ct);
        var jpeg = TestImages.Jpeg(64, 64);

        var twoFiles = Form(jpeg);
        twoFiles.Add(File(jpeg, "image/jpeg"), "file", "second.jpg");
        var withField = Form(jpeg);
        withField.Add(new StringContent("1234"), "version");
        var otherField = new MultipartFormDataContent { { File(jpeg, "image/jpeg"), "photo", "photo.jpg" } };
        var noFile = new MultipartFormDataContent { { new StringContent("x"), "note" } };
        foreach (var content in new HttpContent[] { twoFiles, withField, otherField, noFile })
        {
            using var response = await registrar.PutAsync(PhotoPath(person), content, Ct);
            var problem = await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_INVALID_REQUEST");
            Fields(problem).ShouldBe([("file", "VALIDATION_INVALID_REQUEST")]);
        }

        using (var json = await registrar.PutAsJsonAsync(PhotoPath(person), new { file = "photo.jpg" }, Ct))
        {
            await json.ShouldBeProblemAsync(HttpStatusCode.UnsupportedMediaType, "VALIDATION_UNSUPPORTED_MEDIA_TYPE");
        }

        await ShouldHaveNoPhotoAsync(kmsa, person);
    }

    [Theory]
    [InlineData(PersonTransition.Archive, "PERSON_ARCHIVED")]
    [InlineData(PersonTransition.MarkDeceased, "PERSON_DECEASED")]
    public async Task An_archived_or_deceased_person_takes_no_new_photo(PersonTransition transition, string code)
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var person = await CreatePersonAsync(App.Services, kmsa, SystemActor.Instance, IvanMukasa, overrideReason: null, Ct);
        await ChangeStatusAsync(App.Services, kmsa, person, transition, Ct);

        using var response = await registrar.PutAsync(PhotoPath(person), Form(TestImages.Jpeg(64, 64)), Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, code);
        await ShouldHaveNoPhotoAsync(kmsa, person);
    }

    [Fact]
    public async Task A_stale_If_Match_is_a_conflict_and_stores_nothing()
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var person = await CreatePersonAsync(App.Services, kmsa, SystemActor.Instance, IvanMukasa, overrideReason: null, Ct);
        var version = (await registrar.ReadJsonAsync(PersonPath(person), Ct)).GetProperty("version").GetUInt32();
        await OkAsync(await registrar.PostAsJsonAsync($"{PersonPath(person)}/aliases", new { name = "Kasa", kind = "NICKNAME" }, Ct));

        using var request = new HttpRequestMessage(HttpMethod.Put, PhotoPath(person)) { Content = Form(TestImages.Jpeg(64, 64)) };
        request.Headers.IfMatch.Add(new EntityTagHeaderValue($"\"{version}\""));
        using var response = await registrar.SendAsync(request, Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, "PERSON_CONCURRENCY_CONFLICT");
        await ShouldHaveNoPhotoAsync(kmsa, person);
    }

    // D19: the form isn't checked by antiforgery, so X-CSRF still is.
    [Fact]
    public async Task An_upload_without_X_CSRF_is_refused()
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var person = await CreatePersonAsync(App.Services, kmsa, SystemActor.Instance, IvanMukasa, overrideReason: null, Ct);

        using var response = await registrar.WithoutCsrfHeader().PutAsync(PhotoPath(person), Form(TestImages.Jpeg(64, 64)), Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "AUTH_CSRF_REQUIRED");
        await ShouldHaveNoPhotoAsync(kmsa, person);
    }

    // T5 acceptance: at the STORAGE_GB limit the upload is refused before anything is stored; the usage reads whole GB.
    [Fact]
    public async Task At_the_storage_limit_the_upload_is_refused_and_the_usage_reads_whole_gigabytes()
    {
        var (kmsa, owner) = await TenantAsync("kmsa", IdentityRoles.TenantOwner);
        var person = await CreatePersonAsync(App.Services, kmsa, SystemActor.Instance, IvanMukasa, overrideReason: null, Ct);
        await OkAsync(await owner.PutAsync(PhotoPath(person), Form(TestImages.Jpeg(640, 480)), Ct));
        (await StorageUsageAsync(owner)).ShouldBe((0, 10));
        var stored = await PhotoAssetIdAsync(kmsa, person);
        await OverrideStorageAsync(kmsa, limit: 0);

        using var refused = await owner.PutAsync(PhotoPath(person), Form(TestImages.Jpeg(640, 480)), Ct);

        var problem = await refused.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "ENTITLEMENT_QUOTA_EXCEEDED");
        problem.GetProperty("detail").GetString().ShouldBe("Your plan allows 0 GB of storage.");
        (await PhotoAssetIdAsync(kmsa, person)).ShouldBe(stored);
        (await AssetCountAsync(kmsa)).ShouldBe(1);
        (await StorageUsageAsync(owner)).ShouldBe((0, 0));
        await AccountTestData.DeliverOutboxAsync(App.Services, Ct);
        (await AuditTestData.EntriesAsync(App.Services, Ct))
            .Single(e => e.ActionCode == "person.photo.set" && e.Outcome != AuditOutcome.Success)
            .ErrorCode.ShouldBe("ENTITLEMENT_QUOTA_EXCEEDED");

        // Removing still works at the limit.
        await OkAsync(await owner.DeleteAsync(PhotoPath(person), Ct));
    }

    [Fact]
    public async Task Another_tenants_person_is_not_found()
    {
        var (_, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var (elssc, theirRegistrar) = await TenantAsync("elssc", IdentityRoles.Registrar);
        var theirs = await CreatePersonAsync(App.Services, elssc, SystemActor.Instance, IvanMukasa, overrideReason: null, Ct);
        await OkAsync(await theirRegistrar.PutAsync(PhotoPath(theirs), Form(TestImages.Jpeg(64, 64)), Ct));

        using (var set = await registrar.PutAsync(PhotoPath(theirs), Form(TestImages.Jpeg(64, 64)), Ct))
        {
            await set.ShouldBeProblemAsync(HttpStatusCode.NotFound, "PERSON_NOT_FOUND");
        }

        using var remove = await registrar.DeleteAsync(PhotoPath(theirs), Ct);
        await remove.ShouldBeProblemAsync(HttpStatusCode.NotFound, "PERSON_NOT_FOUND");
        (await PhotoAssetIdOrNullAsync(elssc, theirs)).ShouldNotBeNull();
    }

    // D21: photos are changed by the owner, the admin and the registrar.
    [Theory]
    [InlineData(IdentityRoles.TenantOwner, true)]
    [InlineData(IdentityRoles.TenantAdmin, true)]
    [InlineData(IdentityRoles.Registrar, true)]
    [InlineData(IdentityRoles.CompetitionManager, false)]
    [InlineData(IdentityRoles.ResultsOfficer, false)]
    [InlineData(IdentityRoles.DataEntry, false)]
    [InlineData(IdentityRoles.Viewer, false)]
    [InlineData(IdentityRoles.Scorer, false)]
    public async Task Only_those_who_change_people_may_set_or_remove_a_photo(string role, bool allowed)
    {
        var (kmsa, client) = await TenantAsync("kmsa", role);
        var person = await SavePersonAsync(App.Services, kmsa, IvanMukasa, Ct);

        using var set = await client.PutAsync(PhotoPath(person.Id), Form(TestImages.Jpeg(64, 64)), Ct);
        using var remove = await client.DeleteAsync(PhotoPath(person.Id), Ct);

        foreach (var response in new[] { set, remove })
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
    public async Task A_read_only_tenant_changes_no_photo()
    {
        App.Authorization.UseIdentityRoles();
        var kmsa = await TenantTestData.SaveTenantAsync(
            App.Services, "kmsa", TenantStatus.Active, publicEnabled: false, withDomain: true, withSubscription: false, Ct);
        await TenantTestData.SavePeriodAsync(App.Services, kmsa, new DateOnly(2025, 1, 1), new DateOnly(2025, 12, 31), Ct, cancelReason: "Pilot paused");
        var person = await SavePersonAsync(App.Services, kmsa, IvanMukasa, Ct);
        var member = await CreateMemberAsync(App.Services, kmsa, "brian.ssemakula@kmsa.example", Ct, TenantWide(IdentityRoles.Registrar));
        using var registrar = App.ClientFor(member.UserId, kmsa);

        using (var set = await registrar.PutAsync(PhotoPath(person.Id), Form(TestImages.Jpeg(64, 64)), Ct))
        {
            await set.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "TENANT_READ_ONLY");
        }

        using var remove = await registrar.DeleteAsync(PhotoPath(person.Id), Ct);
        await remove.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "TENANT_READ_ONLY");
    }

    private static string PhotoPath(Guid personId) => $"{PersonPath(personId)}/photo";

    private static MultipartFormDataContent Form(byte[] bytes, string contentType = "image/jpeg") =>
        new() { { File(bytes, contentType), "file", "photo.jpg" } };

    private static ByteArrayContent File(byte[] bytes, string contentType)
    {
        var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        return content;
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

    private async Task<Guid?> PhotoAssetIdOrNullAsync(Guid tenantId, Guid personId)
    {
        await using var scope = ScopeFor(App.Services, tenantId, out var context);
        return await context.Set<Person>().Where(p => p.Id == personId).Select(p => p.PhotoAssetId).SingleAsync(Ct);
    }

    private async Task<Guid> PhotoAssetIdAsync(Guid tenantId, Guid personId) =>
        (await PhotoAssetIdOrNullAsync(tenantId, personId)).ShouldNotBeNull();

    private async Task<MediaAssetStatus> AssetStatusAsync(Guid tenantId, Guid assetId)
    {
        await using var scope = App.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Set(tenantId);
        var context = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        return await context.Set<MediaAsset>().Where(a => a.Id == assetId).Select(a => a.Status).SingleAsync(Ct);
    }

    private async Task<long> AssetCountAsync(Guid tenantId) =>
        (await QueryAsync(DatabaseRoles.Platform, "SELECT count(*) FROM platform.media_assets WHERE tenant_id = @tenant",
            reader => reader.GetInt64(0), new NpgsqlParameter("tenant", tenantId))).Single();

    /// <summary>The person still has no photo and the tenant stores no asset.</summary>
    private async Task ShouldHaveNoPhotoAsync(Guid tenantId, Guid personId)
    {
        (await PhotoAssetIdOrNullAsync(tenantId, personId)).ShouldBeNull();
        (await AssetCountAsync(tenantId)).ShouldBe(0);
    }

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

    /// <summary><c>STORAGE_GB</c>'s used and limit on the tenant's subscription read.</summary>
    private static async Task<(int? Used, int? Limit)> StorageUsageAsync(HttpClient client)
    {
        var usage = (await client.ReadJsonAsync("/api/admin/v1/tenant/subscription", Ct)).GetProperty("usage").EnumerateArray()
            .Single(u => u.GetProperty("code").GetString() == "STORAGE_GB");
        return (usage.GetProperty("used").GetInt32(), usage.GetProperty("limit").GetInt32());
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

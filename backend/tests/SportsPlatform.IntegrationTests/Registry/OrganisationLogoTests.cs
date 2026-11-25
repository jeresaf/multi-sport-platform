using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SportsPlatform.BuildingBlocks.Auditing;
using SportsPlatform.IntegrationTests.Api;
using SportsPlatform.IntegrationTests.Audit;
using SportsPlatform.IntegrationTests.Identity;
using SportsPlatform.IntegrationTests.Infrastructure;
using SportsPlatform.Modules.Identity.Domain.Roles;
using SportsPlatform.Modules.Platform.Domain.Media;
using SportsPlatform.Modules.Registry.Domain.Organisations;
using SportsPlatform.UnitTests.Platform.Media;
using static SportsPlatform.IntegrationTests.Registry.OrganisationRequests;
using static SportsPlatform.IntegrationTests.Registry.PeopleRequests;
using static SportsPlatform.IntegrationTests.Registry.RegistryTestData;

namespace SportsPlatform.IntegrationTests.Registry;

/// <summary>
/// An organisation's logo over HTTP (S07-T4b; D10, D15, D20, D21): the upload as a <c>PUBLIC</c> <c>ORG_LOGO</c>, the signed
/// URLs in the read, replacing and removing (the old asset marked <c>REMOVED</c>), the status guards, the version, the
/// audit, Platform's refusals on the file, the <c>STORAGE_GB</c> quota, another tenant, the roles and the read-only tenant.
/// </summary>
public sealed class OrganisationLogoTests(PostgresFixture fixture) : RegistryImageTest(fixture)
{
    [Fact]
    public async Task A_logo_is_shown_as_signed_URLs_and_replacing_it_removes_the_old_one()
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var organisation = await SaveOrganisationAsync(App.Services, kmsa, "Ntinda Swifts SC");
        var before = await registrar.ReadJsonAsync(OrganisationPath(organisation.Id), Ct);
        before.GetProperty("logo").ValueKind.ShouldBe(JsonValueKind.Null);

        var set = await OkAsync(await registrar.PutAsync(LogoPath(organisation.Id), Form(TestImages.TransparentPng(800, 400)), Ct));

        set.GetProperty("version").GetUInt32().ShouldNotBe(before.GetProperty("version").GetUInt32());
        await ShouldServeImageAsync(registrar, set.GetProperty("logo"), 800, 400);
        var first = await LogoAssetIdAsync(kmsa, organisation.Id);
        (await AssetAsync(kmsa, first.ShouldNotBeNull())).ShouldBe((MediaAssetStatus.Active, "ORG_LOGO", "PUBLIC"));
        (await registrar.ReadJsonAsync(OrganisationPath(organisation.Id), Ct)).GetProperty("logo").ValueKind.ShouldBe(JsonValueKind.Object);

        await OkAsync(await registrar.PutAsync(LogoPath(organisation.Id), Form(TestImages.Jpeg(256, 256), "image/jpeg"), Ct));

        var second = (await LogoAssetIdAsync(kmsa, organisation.Id)).ShouldNotBeNull();
        second.ShouldNotBe(first.Value);
        (await AssetAsync(kmsa, first.Value)).Status.ShouldBe(MediaAssetStatus.Removed);
        (await AssetAsync(kmsa, second)).Status.ShouldBe(MediaAssetStatus.Active);

        await AccountTestData.DeliverOutboxAsync(App.Services, Ct);
        var entries = (await AuditTestData.EntriesAsync(App.Services, Ct)).Where(e => e.ActionCode == "organisation.logo.set").ToList();
        entries.ShouldAllBe(e => e.Outcome == AuditOutcome.Success && e.EntityType == "Organisation" && e.EntityId == organisation.Id);
        entries.Select(e => JsonDocument.Parse(e.Metadata!).RootElement)
            .Select(m => (m.GetProperty("assetId").GetGuid(), m.GetProperty("replacedAssetId").GetString()))
            .ShouldBe([(first.Value, null), (second, first.Value.ToString())], ignoreOrder: true);
    }

    [Fact]
    public async Task Removing_the_logo_works_in_any_status_and_without_one_is_not_found()
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var organisation = await SaveOrganisationAsync(App.Services, kmsa, "Kisaasi Kestrels SC");
        await OkAsync(await registrar.PutAsync(LogoPath(organisation.Id), Form(TestImages.Png(128, 128)), Ct));
        var asset = (await LogoAssetIdAsync(kmsa, organisation.Id)).ShouldNotBeNull();
        await OkAsync(await registrar.PostAsJsonAsync(
            OrganisationStatusPath(organisation.Id), new { status = "ARCHIVED", reason = "No longer listed" }, Ct));

        var removed = await OkAsync(await registrar.DeleteAsync(LogoPath(organisation.Id), Ct));

        removed.GetProperty("logo").ValueKind.ShouldBe(JsonValueKind.Null);
        (await LogoAssetIdAsync(kmsa, organisation.Id)).ShouldBeNull();
        (await AssetAsync(kmsa, asset)).Status.ShouldBe(MediaAssetStatus.Removed);
        using var again = await registrar.DeleteAsync(LogoPath(organisation.Id), Ct);
        await again.ShouldBeProblemAsync(HttpStatusCode.NotFound, "ORGANISATION_LOGO_NOT_FOUND");

        await AccountTestData.DeliverOutboxAsync(App.Services, Ct);
        var entries = (await AuditTestData.EntriesAsync(App.Services, Ct)).Where(e => e.ActionCode == "organisation.logo.remove").ToList();
        entries.Select(e => (e.Outcome, e.ErrorCode)).ShouldBe(
            [(AuditOutcome.Success, null), (AuditOutcome.Failure, "ORGANISATION_LOGO_NOT_FOUND")], ignoreOrder: true);
    }

    // D15 through Platform's checks: the refusal names the file, and nothing is stored.
    [Fact]
    public async Task An_SVG_a_file_over_2_MB_or_an_image_under_64_px_is_refused_on_the_file()
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var organisation = await SaveOrganisationAsync(App.Services, kmsa, "Ntinda Swifts SC");
        var svg = "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"100\" height=\"100\"><script>alert(1)</script></svg>"u8.ToArray();
        var tooLarge = new byte[(2 * 1024 * 1024) + 1];
        TestImages.Png(64, 64).CopyTo(tooLarge, 0);

        foreach (var (bytes, status, code) in new[]
                 {
                     (svg, HttpStatusCode.UnprocessableEntity, "MEDIA_TYPE_NOT_ALLOWED"),
                     (tooLarge, HttpStatusCode.RequestEntityTooLarge, "MEDIA_TOO_LARGE"),
                     (TestImages.Png(200, 63), HttpStatusCode.UnprocessableEntity, "MEDIA_IMAGE_TOO_SMALL"),
                 })
        {
            using var response = await registrar.PutAsync(LogoPath(organisation.Id), Form(bytes), Ct);
            var problem = await response.ShouldBeProblemAsync(status, code);
            Fields(problem).ShouldBe([("file", code)]);
        }

        (await LogoAssetIdAsync(kmsa, organisation.Id)).ShouldBeNull();
        (await AssetCountAsync(kmsa)).ShouldBe(0);
    }

    [Theory]
    [InlineData(OrganisationStatus.Dissolved, "ORGANISATION_DISSOLVED")]
    [InlineData(OrganisationStatus.Archived, "ORGANISATION_ARCHIVED")]
    public async Task A_dissolved_or_archived_organisation_takes_no_new_logo(OrganisationStatus status, string code)
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var organisation = await SaveOrganisationAsync(App.Services, kmsa, "Naguru Nightjars Club", status: status);

        using var response = await registrar.PutAsync(LogoPath(organisation.Id), Form(TestImages.Png(64, 64)), Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, code);
        (await AssetCountAsync(kmsa)).ShouldBe(0);
    }

    [Fact]
    public async Task A_stale_If_Match_is_a_conflict_and_the_form_must_be_one_file()
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var organisation = await SaveOrganisationAsync(App.Services, kmsa, "Ntinda Swifts SC");
        var version = (await registrar.ReadJsonAsync(OrganisationPath(organisation.Id), Ct)).GetProperty("version").GetUInt32();
        await OkAsync(await registrar.PostAsJsonAsync(OrganisationAliasesPath(organisation.Id), new { name = "The Swifts", kind = "NICKNAME" }, Ct));

        using (var request = new HttpRequestMessage(HttpMethod.Put, LogoPath(organisation.Id)) { Content = Form(TestImages.Png(64, 64)) })
        {
            request.Headers.IfMatch.Add(new EntityTagHeaderValue($"\"{version}\""));
            using var stale = await registrar.SendAsync(request, Ct);
            await stale.ShouldBeProblemAsync(HttpStatusCode.Conflict, "ORGANISATION_CONCURRENCY_CONFLICT");
        }

        var withField = Form(TestImages.Png(64, 64));
        withField.Add(new StringContent("1234"), "version");
        using (var malformed = await registrar.PutAsync(LogoPath(organisation.Id), withField, Ct))
        {
            var problem = await malformed.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "VALIDATION_INVALID_REQUEST");
            Fields(problem).ShouldBe([("file", "VALIDATION_INVALID_REQUEST")]);
        }

        (await AssetCountAsync(kmsa)).ShouldBe(0);
    }

    // T4 acceptance: at the STORAGE_GB limit a logo upload is refused before anything is stored.
    [Fact]
    public async Task At_the_storage_limit_a_logo_upload_is_refused()
    {
        var (kmsa, owner) = await TenantAsync("kmsa", IdentityRoles.TenantOwner);
        var organisation = await SaveOrganisationAsync(App.Services, kmsa, "Ntinda Swifts SC");
        await OkAsync(await owner.PutAsync(LogoPath(organisation.Id), Form(TestImages.Png(256, 256)), Ct));
        var stored = await LogoAssetIdAsync(kmsa, organisation.Id);
        await OverrideStorageAsync(kmsa, limit: 0);

        using var refused = await owner.PutAsync(LogoPath(organisation.Id), Form(TestImages.Png(256, 256)), Ct);

        var problem = await refused.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "ENTITLEMENT_QUOTA_EXCEEDED");
        problem.GetProperty("detail").GetString().ShouldBe("Your plan allows 0 GB of storage.");
        (await LogoAssetIdAsync(kmsa, organisation.Id)).ShouldBe(stored);
        (await AssetCountAsync(kmsa)).ShouldBe(1);
        await AccountTestData.DeliverOutboxAsync(App.Services, Ct);
        (await AuditTestData.EntriesAsync(App.Services, Ct))
            .Single(e => e.ActionCode == "organisation.logo.set" && e.Outcome != AuditOutcome.Success)
            .ErrorCode.ShouldBe("ENTITLEMENT_QUOTA_EXCEEDED");

        // Removing still works at the limit.
        await OkAsync(await owner.DeleteAsync(LogoPath(organisation.Id), Ct));
    }

    [Fact]
    public async Task Another_tenants_organisation_is_not_found()
    {
        var (_, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var (elssc, theirRegistrar) = await TenantAsync("elssc", IdentityRoles.Registrar);
        var theirs = await SaveOrganisationAsync(App.Services, elssc, "Lakeshore High School");
        await OkAsync(await theirRegistrar.PutAsync(LogoPath(theirs.Id), Form(TestImages.Png(64, 64)), Ct));

        using (var set = await registrar.PutAsync(LogoPath(theirs.Id), Form(TestImages.Png(64, 64)), Ct))
        {
            await set.ShouldBeProblemAsync(HttpStatusCode.NotFound, "ORGANISATION_NOT_FOUND");
        }

        using var remove = await registrar.DeleteAsync(LogoPath(theirs.Id), Ct);
        await remove.ShouldBeProblemAsync(HttpStatusCode.NotFound, "ORGANISATION_NOT_FOUND");
        (await LogoAssetIdAsync(elssc, theirs.Id)).ShouldNotBeNull();
    }

    // D20: images are changed by the owner, the admin and the registrar.
    [Theory]
    [InlineData(IdentityRoles.TenantOwner, true)]
    [InlineData(IdentityRoles.TenantAdmin, true)]
    [InlineData(IdentityRoles.Registrar, true)]
    [InlineData(IdentityRoles.CompetitionManager, false)]
    [InlineData(IdentityRoles.ResultsOfficer, false)]
    [InlineData(IdentityRoles.DataEntry, false)]
    [InlineData(IdentityRoles.Viewer, false)]
    [InlineData(IdentityRoles.Scorer, false)]
    public async Task Only_those_who_change_organisations_may_set_or_remove_a_logo(string role, bool allowed)
    {
        var (kmsa, client) = await TenantAsync("kmsa", role);
        var organisation = await SaveOrganisationAsync(App.Services, kmsa, "Ntinda Swifts SC");

        using var set = await client.PutAsync(LogoPath(organisation.Id), Form(TestImages.Png(64, 64)), Ct);
        using var remove = await client.DeleteAsync(LogoPath(organisation.Id), Ct);

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
    public async Task A_read_only_tenant_changes_no_logo()
    {
        var (kmsa, registrar) = await ReadOnlyTenantAsync();
        var organisation = await SaveOrganisationAsync(App.Services, kmsa, "Ntinda Swifts SC");

        using (var set = await registrar.PutAsync(LogoPath(organisation.Id), Form(TestImages.Png(64, 64)), Ct))
        {
            await set.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "TENANT_READ_ONLY");
        }

        using var remove = await registrar.DeleteAsync(LogoPath(organisation.Id), Ct);
        await remove.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "TENANT_READ_ONLY");
    }

    private static string LogoPath(Guid organisationId) => $"{OrganisationPath(organisationId)}/logo";

    private async Task<Guid?> LogoAssetIdAsync(Guid tenantId, Guid organisationId)
    {
        await using var scope = ScopeFor(App.Services, tenantId, out var context);
        return await context.Set<Organisation>().Where(o => o.Id == organisationId).Select(o => o.LogoAssetId).SingleAsync(Ct);
    }
}

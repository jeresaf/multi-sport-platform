using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SportsPlatform.BuildingBlocks.Actions;
using SportsPlatform.BuildingBlocks.Auditing;
using SportsPlatform.IntegrationTests.Api;
using SportsPlatform.IntegrationTests.Audit;
using SportsPlatform.IntegrationTests.Identity;
using SportsPlatform.IntegrationTests.Infrastructure;
using SportsPlatform.Modules.Identity.Domain.Roles;
using SportsPlatform.Modules.Platform.Domain.Media;
using SportsPlatform.Modules.Registry.Domain.Teams;
using SportsPlatform.UnitTests.Platform.Media;
using static SportsPlatform.IntegrationTests.Identity.MembershipTestData;
using static SportsPlatform.IntegrationTests.Registry.RegistryTestData;
using static SportsPlatform.IntegrationTests.Registry.TeamRequests;

namespace SportsPlatform.IntegrationTests.Registry;

/// <summary>
/// A team's logo and badge over HTTP (S07-T4b; D10, D15, D20, D21): each a <c>PUBLIC</c> asset of its own purpose, set,
/// replaced and removed without touching the other, with the signed URLs in the read, the status guards, the version,
/// the audit, the <c>STORAGE_GB</c> quota, another tenant, the roles and the read-only tenant. Platform's file checks
/// are the organisation logo's (<see cref="OrganisationLogoTests"/>) and Platform's own.
/// </summary>
public sealed class TeamImageTests(PostgresFixture fixture) : RegistryImageTest(fixture)
{
    public static TheoryData<string, string> Images() => new() { { "logo", "TEAM_LOGO" }, { "badge", "TEAM_BADGE" } };

    [Fact]
    public async Task The_logo_and_the_badge_are_set_replaced_and_removed_each_on_its_own()
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var team = await SaveTeamAsync(App.Services, kmsa, "Bunga Lakeside FC");
        var before = await registrar.ReadJsonAsync(TeamPath(team.Id), Ct);
        before.GetProperty("logo").ValueKind.ShouldBe(JsonValueKind.Null);
        before.GetProperty("badge").ValueKind.ShouldBe(JsonValueKind.Null);

        var withLogo = await OkAsync(await registrar.PutAsync(ImagePath(team.Id, "logo"), Form(TestImages.TransparentPng(400, 800)), Ct));
        var withBoth = await OkAsync(await registrar.PutAsync(ImagePath(team.Id, "badge"), Form(TestImages.WebP(300, 300), "image/webp"), Ct));

        withLogo.GetProperty("badge").ValueKind.ShouldBe(JsonValueKind.Null);
        await ShouldServeImageAsync(registrar, withBoth.GetProperty("logo"), 400, 800);
        await ShouldServeImageAsync(registrar, withBoth.GetProperty("badge"), 300, 300);
        var (logo, badge) = await ImageAssetIdsAsync(kmsa, team.Id);
        (await AssetAsync(kmsa, logo!.Value)).ShouldBe((MediaAssetStatus.Active, "TEAM_LOGO", "PUBLIC"));
        (await AssetAsync(kmsa, badge!.Value)).ShouldBe((MediaAssetStatus.Active, "TEAM_BADGE", "PUBLIC"));

        await OkAsync(await registrar.PutAsync(ImagePath(team.Id, "badge"), Form(TestImages.Png(128, 128)), Ct));
        var (logoAfter, newBadge) = await ImageAssetIdsAsync(kmsa, team.Id);
        logoAfter.ShouldBe(logo);
        newBadge.ShouldNotBe(badge);
        (await AssetAsync(kmsa, badge.Value)).Status.ShouldBe(MediaAssetStatus.Removed);

        var removed = await OkAsync(await registrar.DeleteAsync(ImagePath(team.Id, "logo"), Ct));

        removed.GetProperty("logo").ValueKind.ShouldBe(JsonValueKind.Null);
        removed.GetProperty("badge").ValueKind.ShouldBe(JsonValueKind.Object);
        (await ImageAssetIdsAsync(kmsa, team.Id)).ShouldBe((null, newBadge));
        (await AssetAsync(kmsa, logo.Value)).Status.ShouldBe(MediaAssetStatus.Removed);

        await AccountTestData.DeliverOutboxAsync(App.Services, Ct);
        var entries = (await AuditTestData.EntriesAsync(App.Services, Ct)).Where(e => e.EntityId == team.Id).ToList();
        entries.Select(e => e.ActionCode).ShouldBe(["team.logo.set", "team.badge.set", "team.badge.set", "team.logo.remove"], ignoreOrder: true);
        entries.ShouldAllBe(e => e.Outcome == AuditOutcome.Success && e.EntityType == "Team");
        var replaced = JsonDocument.Parse(entries.Single(e => e.ActionCode == "team.logo.remove").Metadata!).RootElement;
        replaced.GetProperty("replacedAssetId").GetGuid().ShouldBe(logo.Value);
    }

    [Theory]
    [MemberData(nameof(Images))]
    public async Task Removing_works_in_any_status_and_without_one_is_not_found(string image, string purpose)
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var team = await SaveTeamAsync(App.Services, kmsa, "Ntinda Swifts FC");
        await OkAsync(await registrar.PutAsync(ImagePath(team.Id, image), Form(TestImages.Png(64, 64)), Ct));
        await MoveTeamAsync(registrar, team.Id, "ARCHIVED", "Merged into the club's first team");

        await OkAsync(await registrar.DeleteAsync(ImagePath(team.Id, image), Ct));

        using var again = await registrar.DeleteAsync(ImagePath(team.Id, image), Ct);
        await again.ShouldBeProblemAsync(HttpStatusCode.NotFound, $"{purpose}_NOT_FOUND");
        (await AssetCountAsync(kmsa)).ShouldBe(1);
    }

    [Theory]
    [InlineData("logo", TeamStatus.Disbanded, "TEAM_DISBANDED")]
    [InlineData("badge", TeamStatus.Disbanded, "TEAM_DISBANDED")]
    [InlineData("logo", TeamStatus.Archived, "TEAM_ARCHIVED")]
    [InlineData("badge", TeamStatus.Archived, "TEAM_ARCHIVED")]
    public async Task A_disbanded_or_archived_team_takes_no_new_image(string image, TeamStatus status, string code)
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var team = await SaveTeamAsync(App.Services, kmsa, "Seeta Sparrows FC", status: status);

        using var response = await registrar.PutAsync(ImagePath(team.Id, image), Form(TestImages.Png(64, 64)), Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, code);
        (await AssetCountAsync(kmsa)).ShouldBe(0);
    }

    [Fact]
    public async Task A_stale_If_Match_is_a_conflict_and_a_small_badge_is_refused_on_the_file()
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var team = await SaveTeamAsync(App.Services, kmsa, "Kisaasi Kestrels FC");
        var version = (await registrar.ReadJsonAsync(TeamPath(team.Id), Ct)).GetProperty("version").GetUInt32();
        await OkAsync(await registrar.PutAsync(ImagePath(team.Id, "logo"), Form(TestImages.Png(64, 64)), Ct));

        using (var request = new HttpRequestMessage(HttpMethod.Put, ImagePath(team.Id, "badge")) { Content = Form(TestImages.Png(64, 64)) })
        {
            request.Headers.IfMatch.Add(new EntityTagHeaderValue($"\"{version}\""));
            using var stale = await registrar.SendAsync(request, Ct);
            await stale.ShouldBeProblemAsync(HttpStatusCode.Conflict, "TEAM_CONCURRENCY_CONFLICT");
        }

        using (var small = await registrar.PutAsync(ImagePath(team.Id, "badge"), Form(TestImages.Png(63, 400)), Ct))
        {
            var problem = await small.ShouldBeProblemAsync(HttpStatusCode.UnprocessableEntity, "MEDIA_IMAGE_TOO_SMALL");
            PeopleRequests.Fields(problem).ShouldBe([("file", "MEDIA_IMAGE_TOO_SMALL")]);
        }

        (await ImageAssetIdsAsync(kmsa, team.Id)).Badge.ShouldBeNull();
        (await AssetCountAsync(kmsa)).ShouldBe(1);
    }

    // T4 acceptance: at the STORAGE_GB limit an upload is refused before anything is stored; removing still works.
    [Theory]
    [MemberData(nameof(Images))]
    public async Task At_the_storage_limit_an_upload_is_refused(string image, string purpose)
    {
        var (kmsa, owner) = await TenantAsync("kmsa", IdentityRoles.TenantOwner);
        var team = await SaveTeamAsync(App.Services, kmsa, "Bunga Lakeside FC");
        await OkAsync(await owner.PutAsync(ImagePath(team.Id, image), Form(TestImages.Png(128, 128)), Ct));
        await OverrideStorageAsync(kmsa, limit: 0);

        using var refused = await owner.PutAsync(ImagePath(team.Id, image), Form(TestImages.Png(128, 128)), Ct);

        var problem = await refused.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "ENTITLEMENT_QUOTA_EXCEEDED");
        problem.GetProperty("detail").GetString().ShouldBe("Your plan allows 0 GB of storage.");
        (await AssetCountAsync(kmsa)).ShouldBe(1);
        var (logo, badge) = await ImageAssetIdsAsync(kmsa, team.Id);
        (await AssetAsync(kmsa, (logo ?? badge).ShouldNotBeNull())).ShouldBe((MediaAssetStatus.Active, purpose, "PUBLIC"));
        await OkAsync(await owner.DeleteAsync(ImagePath(team.Id, image), Ct));
    }

    [Fact]
    public async Task Another_tenants_team_is_not_found()
    {
        var (_, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var (elssc, theirRegistrar) = await TenantAsync("elssc", IdentityRoles.Registrar);
        var theirs = await SaveTeamAsync(App.Services, elssc, "Lakeshore High School");
        await OkAsync(await theirRegistrar.PutAsync(ImagePath(theirs.Id, "logo"), Form(TestImages.Png(64, 64)), Ct));
        await OkAsync(await theirRegistrar.PutAsync(ImagePath(theirs.Id, "badge"), Form(TestImages.Png(64, 64)), Ct));
        var images = await ImageAssetIdsAsync(elssc, theirs.Id);

        foreach (var image in new[] { "logo", "badge" })
        {
            using var set = await registrar.PutAsync(ImagePath(theirs.Id, image), Form(TestImages.Png(64, 64)), Ct);
            await set.ShouldBeProblemAsync(HttpStatusCode.NotFound, "TEAM_NOT_FOUND");
            using var remove = await registrar.DeleteAsync(ImagePath(theirs.Id, image), Ct);
            await remove.ShouldBeProblemAsync(HttpStatusCode.NotFound, "TEAM_NOT_FOUND");
        }

        (await ImageAssetIdsAsync(elssc, theirs.Id)).ShouldBe(images);
        (await AssetCountAsync(elssc)).ShouldBe(2);
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
    public async Task Only_those_who_change_teams_may_set_or_remove_an_image(string role, bool allowed)
    {
        var (kmsa, client) = await TenantAsync("kmsa", role);
        var team = await SaveTeamAsync(App.Services, kmsa, "Ntinda Swifts FC");

        foreach (var image in new[] { "logo", "badge" })
        {
            using var set = await client.PutAsync(ImagePath(team.Id, image), Form(TestImages.Png(64, 64)), Ct);
            using var remove = await client.DeleteAsync(ImagePath(team.Id, image), Ct);
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
    }

    // D20: a team manager gets none of the team's actions in S07, not even for their own team (review #133 R2).
    [Fact]
    public async Task A_team_manager_changes_no_image_of_their_team()
    {
        var (kmsa, registrar) = await TenantAsync("kmsa", IdentityRoles.Registrar);
        var team = await SaveTeamAsync(App.Services, kmsa, "Bunga Lakeside FC");
        await OkAsync(await registrar.PutAsync(ImagePath(team.Id, "logo"), Form(TestImages.Png(64, 64)), Ct));
        var before = await ImageAssetIdsAsync(kmsa, team.Id);
        var manager = await CreateMemberAsync(
            App.Services, kmsa, MailboxOf(IdentityRoles.TeamManager, "kmsa.example"), Ct, (IdentityRoles.TeamManager, AssignmentScope.Team, team.Id));
        using var client = App.ClientFor(manager.UserId, kmsa);

        foreach (var image in new[] { "logo", "badge" })
        {
            using var set = await client.PutAsync(ImagePath(team.Id, image), Form(TestImages.Png(64, 64)), Ct);
            await set.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "AUTH_FORBIDDEN");
            using var remove = await client.DeleteAsync(ImagePath(team.Id, image), Ct);
            await remove.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "AUTH_FORBIDDEN");
        }

        (await ImageAssetIdsAsync(kmsa, team.Id)).ShouldBe(before);
    }

    [Fact]
    public async Task A_read_only_tenant_changes_no_image()
    {
        var (kmsa, registrar) = await ReadOnlyTenantAsync();
        var team = await SaveTeamAsync(App.Services, kmsa, "Ntinda Swifts FC");

        foreach (var image in new[] { "logo", "badge" })
        {
            using var set = await registrar.PutAsync(ImagePath(team.Id, image), Form(TestImages.Png(64, 64)), Ct);
            await set.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "TENANT_READ_ONLY");
            using var remove = await registrar.DeleteAsync(ImagePath(team.Id, image), Ct);
            await remove.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "TENANT_READ_ONLY");
        }
    }

    private static string ImagePath(Guid teamId, string image) => $"{TeamPath(teamId)}/{image}";

    private async Task<(Guid? Logo, Guid? Badge)> ImageAssetIdsAsync(Guid tenantId, Guid teamId)
    {
        await using var scope = ScopeFor(App.Services, tenantId, out var context);
        var images = await context.Set<Team>().Where(t => t.Id == teamId).Select(t => new { t.LogoAssetId, t.BadgeAssetId }).SingleAsync(Ct);
        return (images.LogoAssetId, images.BadgeAssetId);
    }
}

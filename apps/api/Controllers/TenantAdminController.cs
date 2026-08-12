using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SportsPlatform.Api.Authorization;
using SportsPlatform.Infrastructure.Persistence;
using SportsPlatform.Modules.Contests.Entities;
using SportsPlatform.Modules.Results.Entities;
using SportsPlatform.Modules.Identity.Entities;

namespace SportsPlatform.Api.Controllers;

[ApiController]
[Route("api/tenant-admin")]
[Authorize]
public sealed class TenantAdminController : ControllerBase
{
    private readonly SportsPlatformDbContext _database;

    public TenantAdminController(SportsPlatformDbContext database)
    {
        _database = database;
    }

    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard(
        [FromQuery] Guid? tenantId,
        CancellationToken cancellationToken)
    {
        var selectedTenantId = tenantId ?? User.TenantId();
        if (!selectedTenantId.HasValue)
            return BadRequest(new { error = "A tenant is required." });
        if (!User.CanAccessTenant(selectedTenantId.Value)) return Forbid();

        var id = selectedTenantId.Value;
        var tenant = await _database.Tenants.AsNoTracking()
            .Where(item => item.Id == id)
            .Select(item => new
            {
                item.Id,
                item.Name,
                item.ShortName,
                item.Slug,
                status = item.Status.ToString(),
                item.Timezone
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (tenant is null) return NotFound();

        var branding = await _database.TenantBrandings.AsNoTracking()
            .Where(item => item.TenantId == id)
            .Select(item => new
            {
                item.TenantName,
                item.ShortName,
                item.LogoUrl,
                item.PrimaryColor,
                item.SecondaryColor,
                item.AccentColor
            })
            .SingleOrDefaultAsync(cancellationToken);

        var sportCount = await _database.Competitions.AsNoTracking()
            .Where(item => item.TenantId == id)
            .Select(item => item.SportId)
            .Union(_database.Contests.AsNoTracking()
                .Where(item => item.TenantId == id)
                .Select(item => item.SportId))
            .Distinct()
            .CountAsync(cancellationToken);
        var participantCount = await _database.Participants.AsNoTracking()
            .CountAsync(item => item.TenantId == id, cancellationToken);
        var registrationCount = await _database.Registrations.AsNoTracking()
            .CountAsync(item =>
                (item.CompetitionId.HasValue && _database.Competitions.Any(competition =>
                    competition.Id == item.CompetitionId.Value && competition.TenantId == id))
                || (item.ContestId.HasValue && _database.Contests.Any(contest =>
                    contest.Id == item.ContestId.Value && contest.TenantId == id)),
                cancellationToken);
        var competitionCount = await _database.Competitions.AsNoTracking()
            .CountAsync(item => item.TenantId == id, cancellationToken);
        var fixtureCount = await _database.Contests.AsNoTracking()
            .CountAsync(item => item.TenantId == id, cancellationToken);
        var officialCount = await _database.Officials.AsNoTracking()
            .CountAsync(item => _database.Users.Any(user =>
                user.Id == item.UserId && user.TenantId == id), cancellationToken);
        var liveContestCount = await _database.Contests.AsNoTracking()
            .CountAsync(item => item.TenantId == id &&
                (item.Status == ContestStatus.InProgress || item.Status == ContestStatus.Paused),
                cancellationToken);
        var resultCount = await _database.ContestResults.AsNoTracking()
            .CountAsync(item => _database.Contests.Any(contest =>
                contest.Id == item.ContestId && contest.TenantId == id), cancellationToken);
        var pendingResultCount = await _database.ContestResults.AsNoTracking()
            .CountAsync(item =>
                item.Status != ContestResultStatus.Approved
                && item.Status != ContestResultStatus.Published
                && _database.Contests.Any(contest =>
                    contest.Id == item.ContestId && contest.TenantId == id),
                cancellationToken);
        var reportCount = await _database.Reports.AsNoTracking()
            .CountAsync(item => _database.Users.Any(user =>
                user.Id == item.CreatedBy && user.TenantId == id), cancellationToken);
        var sponsorCount = await _database.Sponsorships.AsNoTracking()
            .Where(item => item.TenantId == id)
            .Select(item => item.SponsorId)
            .Distinct()
            .CountAsync(cancellationToken);

        var upcoming = await _database.Contests.AsNoTracking()
            .Where(item => item.TenantId == id && item.StartTime >= DateTime.UtcNow)
            .OrderBy(item => item.StartTime)
            .Take(6)
            .Select(item => new
            {
                item.Id,
                name = item.Name ?? "Scheduled contest",
                item.StartTime,
                item.Venue,
                status = item.Status.ToString()
            })
            .ToArrayAsync(cancellationToken);

        return Ok(new
        {
            generatedAtUtc = DateTime.UtcNow,
            tenant,
            branding = branding ?? new
            {
                TenantName = tenant.Name,
                ShortName = (string?)tenant.ShortName,
                LogoUrl = (string?)null,
                PrimaryColor = (string?)null,
                SecondaryColor = (string?)null,
                AccentColor = (string?)null
            },
            summary = new { liveContests = liveContestCount, pendingResults = pendingResultCount },
            modules = new[]
            {
                Module("Branding", "branding", branding is null ? 0 : 1, "branding.read"),
                Module("Sports", "sports", sportCount, "sports.read"),
                Module("Participants", "participants", participantCount, "participants.read"),
                Module("Registrations", "registrations", registrationCount, "competitions.read"),
                Module("Competitions", "competitions", competitionCount, "competitions.read"),
                Module("Fixtures", "fixtures", fixtureCount, "contests.read"),
                Module("Officials", "officials", officialCount, "contests.read"),
                Module("Live contests", "live-contests", liveContestCount, "scoring.manage", "scoring.submit"),
                Module("Results", "results", resultCount, "results.manage", "results.approve", "results.submit"),
                Module("Reports", "reports", reportCount, "reports.read"),
                Module("Sponsors", "sponsors", sponsorCount, "sponsorships.read", "sponsorships.manage")
            },
            upcoming
        });
    }

    private object Module(string name, string key, int count, params string[] permissions)
    {
        var accessible = User.IsInRole(SystemRoles.PlatformOwner)
            || User.IsInRole("Admin")
            || permissions.Any(HasPermission);
        return new { name, key, count = accessible ? count : (int?)null, accessible, permissions };
    }

    private bool HasPermission(string permission) => User.Claims.Any(claim =>
        claim.Type == "permission" && string.Equals(claim.Value, permission, StringComparison.Ordinal));
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SportsPlatform.Api.Authorization;
using SportsPlatform.Api.Services;
using SportsPlatform.Infrastructure.Persistence;

namespace SportsPlatform.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/contests/{contestId:guid}/timeline")]
public sealed class ContestTimelineController(
    SportsPlatformDbContext database,
    ContestTimelineService timeline) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(
        Guid contestId,
        [FromQuery] string mode = "all",
        [FromQuery] long? afterSequence = null,
        [FromQuery] long? afterVersion = null,
        [FromQuery] string? period = null,
        [FromQuery] int limit = 100,
        CancellationToken cancellationToken = default)
    {
        if (mode is not ("all" or "key"))
            return BadRequest(new { Message = "Mode must be 'all' or 'key'." });
        var tenantId = await database.Contests.AsNoTracking()
            .Where(item => item.Id == contestId)
            .Select(item => (Guid?)item.TenantId)
            .SingleOrDefaultAsync(cancellationToken);
        if (!tenantId.HasValue) return NotFound();
        if (!User.CanAccessTenant(tenantId.Value)) return Forbid();

        var response = await timeline.GetAsync(
            contestId, tenantId, mode, afterSequence, afterVersion, period,
            Math.Clamp(limit, 1, 200), cancellationToken);
        if (response is null) return NotFound();
        return response.VersionConflict
            ? Conflict(new
            {
                Message = "The supplied timeline version is newer than the server timeline.",
                response.ContestId,
                response.TimelineVersion
            })
            : Ok(response);
    }
}

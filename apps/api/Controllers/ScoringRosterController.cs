using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SportsPlatform.Api.Authorization;
using SportsPlatform.Infrastructure.Persistence;
using SportsPlatform.Modules.Contests.Services;
using SportsPlatform.Modules.Participants.Entities;

namespace SportsPlatform.Api.Controllers;

/// <summary>
/// Lightweight roster management for scorers. This keeps player attribution usable
/// even when a team reaches the scoring console before a seasonal roster was created.
/// </summary>
[ApiController]
[Route("api/livescoring")]
[Authorize]
[RequirePermission("scoring.manage")]
public sealed class ScoringRosterController : ControllerBase
{
    private readonly SportsPlatformDbContext _database;
    private readonly IContestsService _contestsService;

    public ScoringRosterController(
        SportsPlatformDbContext database,
        IContestsService contestsService)
    {
        _database = database;
        _contestsService = contestsService;
    }

    [HttpPost("contests/{contestId:guid}/teams/{teamId:guid}/players")]
    public async Task<IActionResult> AddPlayer(
        Guid contestId,
        Guid teamId,
        [FromBody] AddScoringRosterPlayerRequest request,
        CancellationToken cancellationToken)
    {
        var contest = await _contestsService.GetContestByIdAsync(contestId);
        if (contest is null) return NotFound(new { message = "Contest was not found." });
        if (!User.CanAccessTenant(contest.TenantId)) return Forbid();

        var isContestTeam = contest.Participants.Any(item =>
            item.ParticipantId == teamId
            && string.Equals(item.ParticipantType, "Team", StringComparison.OrdinalIgnoreCase));
        if (!isContestTeam)
            return BadRequest(new { message = "The selected team is not part of this contest." });

        Athlete? athlete = null;
        if (request.AthleteId.HasValue)
        {
            athlete = await _database.Set<Athlete>()
                .SingleOrDefaultAsync(item =>
                    item.Id == request.AthleteId.Value
                    && item.TenantId == contest.TenantId,
                    cancellationToken);
            if (athlete is null)
                return BadRequest(new { message = "The selected athlete was not found for this tenant." });
        }
        else
        {
            var playerName = request.Name?.Trim();
            if (string.IsNullOrWhiteSpace(playerName))
                return BadRequest(new { message = "Enter a player name or choose an existing athlete." });

            athlete = await _database.Set<Athlete>()
                .FirstOrDefaultAsync(item =>
                    item.TenantId == contest.TenantId
                    && item.Name.ToLower() == playerName.ToLower(),
                    cancellationToken);

            if (athlete is null)
            {
                var shortName = string.IsNullOrWhiteSpace(request.ShortName)
                    ? BuildShortName(playerName)
                    : request.ShortName.Trim();
                athlete = new Athlete(Guid.NewGuid(), contest.TenantId, playerName, shortName);
                _database.Set<Athlete>().Add(athlete);
            }
        }

        var roster = await _database.Set<TeamRoster>()
            .Include(item => item.Entries)
            .Where(item => item.TenantId == contest.TenantId
                && item.TeamId == teamId
                && item.IsActive)
            .OrderByDescending(item => item.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (roster is null)
        {
            roster = new TeamRoster(
                Guid.NewGuid(),
                teamId,
                contest.TenantId,
                "Scoring roster",
                $"Scoring {DateTime.UtcNow:yyyy}",
                null,
                null,
                null);
            _database.Set<TeamRoster>().Add(roster);
        }

        try
        {
            if (!roster.Entries.Any(entry => entry.AthleteId == athlete.Id && entry.IsActive))
                roster.AddAthlete(athlete.Id, request.JerseyNumber, request.Position);

            await _database.SaveChangesAsync(cancellationToken);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(new { message = exception.Message });
        }

        var activeEntry = roster.Entries.First(entry => entry.AthleteId == athlete.Id && entry.IsActive);
        return Ok(new
        {
            athleteId = athlete.Id,
            athlete.Name,
            athlete.ShortName,
            activeEntry.JerseyNumber,
            activeEntry.Position,
            rosterId = roster.Id,
            teamId
        });
    }

    private static string BuildShortName(string name)
    {
        var words = name.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var initials = string.Concat(words.Take(3).Select(word => char.ToUpperInvariant(word[0])));
        if (!string.IsNullOrWhiteSpace(initials)) return initials;
        return name.Length <= 50 ? name : name[..50];
    }
}

public sealed record AddScoringRosterPlayerRequest(
    Guid? AthleteId,
    string? Name,
    string? ShortName,
    string? JerseyNumber,
    string? Position);

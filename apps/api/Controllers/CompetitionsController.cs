using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using SportsPlatform.Infrastructure.Services;
using SportsPlatform.Api.Authorization;
using SportsPlatform.Infrastructure.Persistence;
using SportsPlatform.Modules.Competitions.Entities;
using SportsPlatform.Modules.FormatEngines;
using SportsPlatform.Modules.FormatEngines.Interfaces;
using SportsPlatform.Modules.Registrations.Entities;
using SportsPlatform.Modules.Brackets.Entities;
using SportsPlatform.Modules.Contests.Entities;
using SportsPlatform.Modules.Participants.Entities;
using SportsPlatform.Modules.Standings.Entities;
using SportsPlatform.Modules.LiveScoring.Entities;
using SportsPlatform.Modules.Sports.Entities;
using SportsPlatform.Modules.Sports.Services;

namespace SportsPlatform.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CompetitionsController : ControllerBase
{
    private readonly ICompetitionsService _competitionsService;
    private readonly SportsPlatformDbContext _database;
    private readonly IRoundRobinEngine _roundRobinEngine;
    private readonly IKnockoutEngine _knockoutEngine;
    private readonly IGroupPlusKnockoutEngine _groupPlusKnockoutEngine;
    private readonly IBestOfSeriesEngine _bestOfSeriesEngine;
    private readonly IHeatQualificationEngine _heatQualificationEngine;
    private readonly IFeatureEntitlementService _featureEntitlements;
    private readonly IUsageLimitsService _usageLimits;
    private readonly ICompetitionSchedulingService _scheduling;
    public CompetitionsController(
        ICompetitionsService competitionsService,
        SportsPlatformDbContext database,
        IRoundRobinEngine roundRobinEngine,
        IKnockoutEngine knockoutEngine,
        IGroupPlusKnockoutEngine groupPlusKnockoutEngine,
        IBestOfSeriesEngine bestOfSeriesEngine,
        IHeatQualificationEngine heatQualificationEngine,
        IFeatureEntitlementService featureEntitlements,
        IUsageLimitsService usageLimits,
        ICompetitionSchedulingService scheduling)
    {
        _competitionsService = competitionsService;
        _database = database;
        _roundRobinEngine = roundRobinEngine;
        _knockoutEngine = knockoutEngine;
        _groupPlusKnockoutEngine = groupPlusKnockoutEngine;
        _bestOfSeriesEngine = bestOfSeriesEngine;
        _heatQualificationEngine = heatQualificationEngine;
        _featureEntitlements = featureEntitlements;
        _usageLimits = usageLimits;
        _scheduling = scheduling;
    }

    [HttpPost("{id:guid}/fixtures/group-knockout")]
    [RequirePermission("competitions.manage")]
    public async Task<IActionResult> GenerateGroupKnockoutFixtures(
        Guid id,
        [FromBody] GenerateGroupKnockoutRequest request,
        CancellationToken cancellationToken)
    {
        var validTieBreakers = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "points", "pointDifferential", "pointsFor", "wins"
        };
        if (request.GroupsCount < 2 || request.TeamsPerGroup < 2)
            return BadRequest(new { error = "At least two groups with two teams each are required." });
        if (request.QualifiersPerGroup < 1 || request.QualifiersPerGroup >= request.TeamsPerGroup)
            return BadRequest(new { error = "Qualifiers per group must be below the group size." });
        if (request.BestRunnerUpCount < 0 || request.BestRunnerUpCount > request.GroupsCount)
            return BadRequest(new { error = "The best runner-up count is invalid." });
        if (request.TieBreakers.Count == 0
            || request.TieBreakers.Any(item => !validTieBreakers.Contains(item)))
            return BadRequest(new
            {
                error = "Tie-breakers must use points, pointDifferential, pointsFor, or wins."
            });
        var participantCount = request.GroupsCount * request.TeamsPerGroup;
        var qualifierCount = (request.GroupsCount * request.QualifiersPerGroup)
            + request.BestRunnerUpCount;
        if (qualifierCount is < 2 or > 64)
            return BadRequest(new { error = "The knockout stage must contain between 2 and 64 qualifiers." });

        var competition = await _database.Competitions
            .Include(item => item.Editions)
            .ThenInclude(edition => edition.Stages)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (competition is null) return NotFound();
        if (!User.CanAccessTenant(competition.TenantId)) return Forbid();
        var existingStageIds = competition.Editions.SelectMany(item => item.Stages)
            .Select(item => item.Id).ToArray();
        if (existingStageIds.Length > 0 && await _database.Contests.AnyAsync(
                item => item.StageId.HasValue && existingStageIds.Contains(item.StageId.Value),
                cancellationToken))
            return Conflict(new { error = "Fixtures have already been generated for this competition." });

        var eligibleParticipantIds = await _database.Registrations
            .Where(item => item.CompetitionId == id
                && (item.Status == RegistrationStatus.Approved
                    || item.Status == RegistrationStatus.Confirmed))
            .Select(item => item.ParticipantId)
            .Distinct()
            .ToArrayAsync(cancellationToken);
        var participants = await _database.Participants
            .Where(item => item.TenantId == competition.TenantId
                && eligibleParticipantIds.Contains(item.Id))
            .OrderBy(item => item.Id)
            .ToArrayAsync(cancellationToken);
        if (participants.Length != participantCount)
            return BadRequest(new
            {
                error = $"Exactly {participantCount} approved registrations are required."
            });

        var startTimeUtc = request.StartTimeUtc.Kind == DateTimeKind.Utc
            ? request.StartTimeUtc
            : request.StartTimeUtc.ToUniversalTime();
        var edition = competition.AddEdition(
            request.EditionName,
            startTimeUtc.Date,
            request.EndDateUtc?.ToUniversalTime());
        var groupStage = edition.AddStage(request.GroupStageName, StageType.GroupAndKnockout, 1);
        groupStage.ConfigureGroupQualification(
            request.GroupsCount,
            request.TeamsPerGroup,
            request.QualifiersPerGroup,
            request.BestRunnerUpCount,
            request.TieBreakers);
        groupStage.Open();
        var knockoutStage = edition.AddStage(request.KnockoutStageName, StageType.Knockout, 2);
        knockoutStage.Open();
        _database.Entry(edition).State = EntityState.Added;
        _database.Entry(groupStage).State = EntityState.Added;
        _database.Entry(knockoutStage).State = EntityState.Added;

        var generatedGroups = _groupPlusKnockoutEngine.GenerateGroupStage(
                groupStage,
                participants.Select((participant, index) => new ParticipantInfo(
                    participant.Id,
                    participant.Type.ToString(),
                    index + 1)),
                new GroupPlusKnockoutOptions
                {
                    SportId = competition.SportId,
                    GroupsCount = request.GroupsCount,
                    TeamsPerGroup = request.TeamsPerGroup,
                    QualifiersPerGroup = request.QualifiersPerGroup,
                    IncludeBestThirdPlace = request.BestRunnerUpCount > 0,
                    BestThirdPlaceCount = request.BestRunnerUpCount
                })
            .ToArray();
        var names = participants.ToDictionary(item => item.Id, item => item.Name);
        foreach (var fixture in generatedGroups)
        {
            AddSportSegments(fixture.Contest, competition.RulesetVersionId);
            fixture.Contest.SetName(
                $"{fixture.GroupName}: {names[fixture.HomeParticipantId!.Value]} vs "
                + names[fixture.AwayParticipantId!.Value]);
            fixture.Contest.Schedule(
                request.Venue,
                startTimeUtc.AddDays((fixture.Round - 1) * request.DaysBetweenRounds));
        }

        var bracketSize = 1;
        while (bracketSize < qualifierCount) bracketSize *= 2;
        var knockoutRounds = (int)Math.Log2(bracketSize);
        var bracket = new Bracket(
            Guid.NewGuid(), id, request.BracketName, BracketType.SingleElimination, bracketSize);
        bracket.SetSeeded(true);
        var matchesByRound = new List<BracketMatch[]>();
        var knockoutContests = new List<Contest>();
        var knockoutStart = startTimeUtc.AddDays(request.TeamsPerGroup * request.DaysBetweenRounds);
        var contestSortOrder = 1;
        for (var roundNumber = 1; roundNumber <= knockoutRounds; roundNumber++)
        {
            var matchCount = bracketSize / (int)Math.Pow(2, roundNumber);
            var round = new BracketRound(
                Guid.NewGuid(),
                bracket.Id,
                roundNumber,
                roundNumber == knockoutRounds ? "Final" : $"Knockout round {roundNumber}",
                matchCount);
            round.SetScheduledAt(knockoutStart.AddDays((roundNumber - 1) * request.DaysBetweenRounds));
            var roundMatches = new BracketMatch[matchCount];
            for (var matchIndex = 0; matchIndex < matchCount; matchIndex++)
            {
                var contest = new Contest(
                    Guid.NewGuid(), competition.TenantId, competition.SportId, "Knockout");
                contest.AssignToStage(knockoutStage.Id, contestSortOrder++);
                contest.SetName(roundNumber == knockoutRounds
                    ? "Final — qualification pending"
                    : $"Knockout round {roundNumber}, match {matchIndex + 1} — qualification pending");
                AddSportSegments(contest, competition.RulesetVersionId);
                contest.Schedule(
                    request.Venue,
                    knockoutStart.AddDays((roundNumber - 1) * request.DaysBetweenRounds));
                knockoutContests.Add(contest);
                var match = new BracketMatch(Guid.NewGuid(), round.Id, matchIndex + 1);
                match.BindContest(contest.Id);
                round.AddMatch(match);
                roundMatches[matchIndex] = match;
            }
            bracket.AddRound(round);
            matchesByRound.Add(roundMatches);
        }
        for (var roundIndex = 0; roundIndex < matchesByRound.Count - 1; roundIndex++)
        {
            for (var matchIndex = 0; matchIndex < matchesByRound[roundIndex].Length; matchIndex++)
                matchesByRound[roundIndex][matchIndex].AdvanceTo(
                    matchesByRound[roundIndex + 1][matchIndex / 2].Id,
                    (matchIndex % 2) + 1);
        }
        bracket.Publish();
        bracket.Start();

        _database.Brackets.Add(bracket);
        _database.Contests.AddRange(generatedGroups.Select(item => item.Contest));
        _database.Contests.AddRange(knockoutContests);
        await _database.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            competitionId = id,
            editionId = edition.Id,
            groupStageId = groupStage.Id,
            knockoutStageId = knockoutStage.Id,
            bracketId = bracket.Id,
            participantCount,
            qualifierCount,
            groupFixtureCount = generatedGroups.Length,
            knockoutMatchCount = knockoutContests.Count,
            groups = generatedGroups.GroupBy(item => item.GroupName).Select(group => new
            {
                name = group.Key,
                participantIds = group.SelectMany(item => new[]
                    {
                        item.HomeParticipantId!.Value,
                        item.AwayParticipantId!.Value
                    })
                    .Distinct()
            })
        });
    }

    [HttpGet]
    [RequirePermission("competitions.read")]
    public async Task<IActionResult> GetAll([FromQuery] Guid? tenantId, [FromQuery] Guid? sportId)
    {
        var effectiveTenantId = tenantId ?? User.TenantId();
        if (effectiveTenantId.HasValue)
        {
            if (!User.CanAccessTenant(effectiveTenantId.Value)) return Forbid();
            return Ok(await _competitionsService.GetCompetitionsByTenantAsync(effectiveTenantId.Value));
        }
        if (sportId.HasValue)
            return Ok(await _competitionsService.GetCompetitionsBySportAsync(sportId.Value));
        return Ok(Array.Empty<object>());
    }

    [HttpGet("{id:guid}")]
    [RequirePermission("competitions.read")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var competition = await _competitionsService.GetCompetitionByIdAsync(id);
        if (competition == null) return NotFound();
        if (!User.CanAccessTenant(competition.TenantId)) return Forbid();
        return Ok(competition);
    }

    [HttpPost]
    [RequirePermission("competitions.create")]
    public async Task<IActionResult> Create([FromBody] CreateCompetitionRequest request)
    {
        if (!User.CanAccessTenant(request.TenantId)) return Forbid();
        if (!await _database.TenantSports.AnyAsync(item =>
                item.TenantId == request.TenantId
                && item.SportId == request.SportId
                && item.IsEnabled))
            return Conflict(new
            {
                error = "SportNotEnabled",
                sportId = request.SportId,
                message = "Enable this sport for the tenant before creating a competition."
            });
        if (!await _featureEntitlements.HasFeatureAsync(request.TenantId, "sports.max_competitions"))
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                error = "FeatureNotEntitled",
                feature = "sports.max_competitions",
                message = "The current subscription does not include competition creation."
            });
        if (!await _usageLimits.CanCreateResourceAsync(request.TenantId, "max_competitions"))
            return Conflict(new
            {
                error = "UsageLimitExceeded",
                resource = "max_competitions",
                message = "The subscription's competition limit has been reached."
            });
        var competition = await _competitionsService.CreateCompetitionAsync(request.Name, request.SportId, request.TenantId);
        return CreatedAtAction(nameof(GetById), new { id = competition.Id }, competition);
    }

    [HttpPost("{id:guid}/publish")]
    [RequirePermission("competitions.manage")]
    public async Task<IActionResult> Publish(Guid id, CancellationToken cancellationToken)
    {
        var competition = await _competitionsService.GetCompetitionByIdAsync(id);
        if (competition == null) return NotFound();
        if (!User.CanAccessTenant(competition.TenantId)) return Forbid();
        var structure = await ValidatePublicationStructureAsync(competition, cancellationToken);
        if (structure.Count > 0)
            return Conflict(new
            {
                error = "InvalidCompetitionStructure",
                message = "Complete the competition structure before publishing.",
                errors = structure
            });
        var schedule = await _scheduling.ValidateAsync(id, cancellationToken);
        if (schedule is not null && !schedule.CanPublish)
            return Conflict(new
            {
                error = "ScheduleConflicts",
                message = "Resolve or override every scheduling conflict before publishing.",
                conflicts = schedule.Conflicts.Where(item => !item.IsOverridden)
            });
        await _competitionsService.PublishCompetitionAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpGet("{id:guid}/publication-validation")]
    [RequirePermission("competitions.read")]
    public async Task<IActionResult> ValidatePublication(Guid id, CancellationToken cancellationToken)
    {
        var competition = await _competitionsService.GetCompetitionByIdAsync(id, cancellationToken);
        if (competition is null) return NotFound();
        if (!User.CanAccessTenant(competition.TenantId)) return Forbid();
        var errors = await ValidatePublicationStructureAsync(competition, cancellationToken);
        var schedule = errors.Count == 0
            ? await _scheduling.ValidateAsync(id, cancellationToken)
            : null;
        var conflicts = schedule?.Conflicts.Where(item => !item.IsOverridden).ToArray()
            ?? Array.Empty<ScheduleConflictView>();
        return Ok(new
        {
            isValid = errors.Count == 0 && conflicts.Length == 0,
            errors,
            conflicts
        });
    }

    [HttpPut("{id:guid}/ruleset/{rulesetVersionId:guid}")]
    [RequirePermission("competitions.manage")]
    public async Task<IActionResult> AssignRuleset(
        Guid id,
        Guid rulesetVersionId,
        CancellationToken cancellationToken)
    {
        var competition = await _database.Competitions
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (competition is null) return NotFound();
        if (!User.CanAccessTenant(competition.TenantId)) return Forbid();
        var validRuleset = await _database.Sports.AsNoTracking()
            .Where(sport => sport.Id == competition.SportId)
            .SelectMany(sport => sport.Versions)
            .AnyAsync(version => version.Id == rulesetVersionId && version.IsPublished,
                cancellationToken);
        if (!validRuleset)
            return BadRequest(new { error = "Select a published ruleset for the competition sport." });
        try
        {
            competition.AssignRulesetVersion(rulesetVersionId);
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(new { error = exception.Message });
        }
        await _database.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpGet("{id:guid}/presentation")]
    [RequirePermission("competitions.read")]
    public async Task<IActionResult> GetPresentation(
        Guid id,
        CancellationToken cancellationToken)
    {
        var competition = await _database.Competitions.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (competition is null) return NotFound();
        if (!User.CanAccessTenant(competition.TenantId)) return Forbid();
        return Ok(PresentationSettingsResponse(competition.PresentationConfiguration));
    }

    [HttpPut("{id:guid}/presentation")]
    [RequirePermission("competitions.manage")]
    public async Task<IActionResult> UpdatePresentation(
        Guid id,
        [FromBody] CompetitionPresentationRequest request,
        CancellationToken cancellationToken)
    {
        var competition = await _database.Competitions
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (competition is null) return NotFound();
        if (!User.CanAccessTenant(competition.TenantId)) return Forbid();

        var renderers = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "head-to-head", "multi-participant-leaderboard", "race-heat",
            "fight-card-bout", "cricket-scorecard"
        };
        var tabs = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "overview", "fixtures", "results", "standings", "bracket",
            "groups", "series", "leaderboard", "medals", "participants",
            "statistics"
        };
        if (!string.IsNullOrWhiteSpace(request.PrimaryContestRenderer)
            && !renderers.Contains(request.PrimaryContestRenderer))
            return BadRequest(new { error = "The selected contest renderer is unsupported." });
        var requestedTabs = request.VisibleTabs ?? Array.Empty<string>();
        if (requestedTabs.Any(tab => !tabs.Contains(tab)))
            return BadRequest(new { error = "Visible tabs contain an unsupported value." });
        if (!string.IsNullOrWhiteSpace(request.DefaultTab)
            && !tabs.Contains(request.DefaultTab))
            return BadRequest(new { error = "The default tab is unsupported." });
        if (!string.IsNullOrWhiteSpace(request.DefaultTab)
            && requestedTabs.Count > 0
            && !requestedTabs.Contains(request.DefaultTab,
                StringComparer.OrdinalIgnoreCase))
            return BadRequest(new { error = "The default tab must also be visible." });

        var normalized = new CompetitionPresentationRequest(
            request.PrimaryContestRenderer?.Trim(),
            requestedTabs.Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(item => item.ToLowerInvariant()).ToArray(),
            request.DefaultTab?.Trim().ToLowerInvariant(),
            request.ScoreVariant?.Trim(),
            request.StandingsVariant?.Trim(),
            request.LeaderboardVariant?.Trim(),
            request.BracketEnabled,
            request.SeriesEnabled,
            request.MedalTableEnabled);
        competition.UpdatePresentationConfiguration(JsonSerializer.Serialize(
            normalized,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
        await _database.SaveChangesAsync(cancellationToken);
        return Ok(normalized);
    }

    private static CompetitionPresentationRequest PresentationSettingsResponse(
        string configuration)
    {
        try
        {
            return JsonSerializer.Deserialize<CompetitionPresentationRequest>(
                configuration,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? CompetitionPresentationRequest.Default;
        }
        catch (JsonException)
        {
            return CompetitionPresentationRequest.Default;
        }
    }

    [HttpPost("{id:guid}/archive")]
    [RequirePermission("competitions.manage")]
    public async Task<IActionResult> Archive(Guid id)
    {
        var competition = await _competitionsService.GetCompetitionByIdAsync(id);
        if (competition == null) return NotFound();
        if (!User.CanAccessTenant(competition.TenantId)) return Forbid();
        await _competitionsService.ArchiveCompetitionAsync(id);
        return NoContent();
    }

    [HttpGet("{id:guid}/hierarchy")]
    [RequirePermission("competitions.read")]
    public async Task<IActionResult> GetHierarchy(Guid id, CancellationToken cancellationToken)
    {
        var competition = await LoadHierarchyAsync(id, cancellationToken);
        if (competition is null) return NotFound();
        if (!User.CanAccessTenant(competition.TenantId)) return Forbid();
        var stageIds = competition.Editions.SelectMany(item => item.Stages)
            .Select(item => item.Id).ToArray();
        var contests = await _database.Contests.AsNoTracking()
            .Where(item => item.StageId.HasValue && stageIds.Contains(item.StageId.Value))
            .OrderBy(item => item.SortOrder)
            .Select(item => new
            {
                item.Id, item.StageId, item.GroupId, item.RoundId, item.Name,
                item.ContestShape, item.Status, item.SortOrder
            })
            .ToArrayAsync(cancellationToken);
        return Ok(new
        {
            competition.Id,
            competition.Name,
            competition.Status,
            editions = competition.Editions.OrderBy(item => item.StartDate).Select(edition => new
            {
                edition.Id, edition.Name, edition.StartDate, edition.EndDate, edition.Status,
                stages = edition.Stages.OrderBy(item => item.SortOrder).Select(stage => new
                {
                    stage.Id, stage.Name, stage.Type, stage.SortOrder, stage.Status,
                    groups = stage.Groups.OrderBy(item => item.Name).Select(group => new
                    {
                        group.Id, group.Name, group.Status
                    }),
                    rounds = stage.Rounds.OrderBy(item => item.SortOrder).Select(round => new
                    {
                        round.Id, round.Name, round.SortOrder, round.Status
                    }),
                    contests = contests.Where(item => item.StageId == stage.Id)
                })
            })
        });
    }

    [HttpPost("{id:guid}/editions")]
    [RequirePermission("competitions.manage")]
    public async Task<IActionResult> AddEdition(
        Guid id, [FromBody] AddEditionRequest request, CancellationToken cancellationToken)
    {
        var competition = await LoadHierarchyAsync(id, cancellationToken);
        if (competition is null) return NotFound();
        if (!User.CanAccessTenant(competition.TenantId)) return Forbid();
        try
        {
            var edition = competition.AddEdition(request.Name, request.StartDate, request.EndDate);
            _database.Entry(edition).State = EntityState.Added;
            await _database.SaveChangesAsync(cancellationToken);
            return Created($"/api/competitions/{id}/hierarchy", edition);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
    }

    [HttpPost("{id:guid}/editions/{editionId:guid}/stages")]
    [RequirePermission("competitions.manage")]
    public async Task<IActionResult> AddStage(
        Guid id, Guid editionId, [FromBody] AddStageRequest request,
        CancellationToken cancellationToken)
    {
        var competition = await LoadHierarchyAsync(id, cancellationToken);
        if (competition is null) return NotFound();
        if (!User.CanAccessTenant(competition.TenantId)) return Forbid();
        var edition = competition.Editions.SingleOrDefault(item => item.Id == editionId);
        if (edition is null) return NotFound(new { error = "Edition does not belong to this competition." });
        if (!Enum.TryParse<StageType>(request.Type, true, out var type) || !Enum.IsDefined(type))
            return BadRequest(new { error = $"Unknown stage type '{request.Type}'." });
        try
        {
            var stage = edition.AddStage(request.Name, type, request.SortOrder);
            _database.Entry(stage).State = EntityState.Added;
            await _database.SaveChangesAsync(cancellationToken);
            return Created($"/api/competitions/{id}/hierarchy", stage);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
    }

    [HttpPost("{id:guid}/stages/{stageId:guid}/groups")]
    [RequirePermission("competitions.manage")]
    public async Task<IActionResult> AddGroup(
        Guid id, Guid stageId, [FromBody] AddGroupRequest request,
        CancellationToken cancellationToken)
    {
        var competition = await LoadHierarchyAsync(id, cancellationToken);
        if (competition is null) return NotFound();
        if (!User.CanAccessTenant(competition.TenantId)) return Forbid();
        var stage = competition.Editions.SelectMany(item => item.Stages)
            .SingleOrDefault(item => item.Id == stageId);
        if (stage is null) return NotFound(new { error = "Stage does not belong to this competition." });
        try
        {
            var group = stage.AddGroup(request.Name);
            _database.Entry(group).State = EntityState.Added;
            await _database.SaveChangesAsync(cancellationToken);
            return Created($"/api/competitions/{id}/hierarchy", group);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
    }

    [HttpPost("{id:guid}/stages/{stageId:guid}/rounds")]
    [RequirePermission("competitions.manage")]
    public async Task<IActionResult> AddRound(
        Guid id, Guid stageId, [FromBody] AddRoundRequest request,
        CancellationToken cancellationToken)
    {
        var competition = await LoadHierarchyAsync(id, cancellationToken);
        if (competition is null) return NotFound();
        if (!User.CanAccessTenant(competition.TenantId)) return Forbid();
        var stage = competition.Editions.SelectMany(item => item.Stages)
            .SingleOrDefault(item => item.Id == stageId);
        if (stage is null) return NotFound(new { error = "Stage does not belong to this competition." });
        try
        {
            var round = stage.AddRound(request.Name, request.SortOrder);
            _database.Entry(round).State = EntityState.Added;
            await _database.SaveChangesAsync(cancellationToken);
            return Created($"/api/competitions/{id}/hierarchy", round);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
    }

    [HttpPost("{id:guid}/stages/{stageId:guid}/contests")]
    [RequirePermission("competitions.manage")]
    public async Task<IActionResult> AddManualContest(
        Guid id, Guid stageId, [FromBody] AddManualContestRequest request,
        CancellationToken cancellationToken)
    {
        var competition = await LoadHierarchyAsync(id, cancellationToken);
        if (competition is null) return NotFound();
        if (!User.CanAccessTenant(competition.TenantId)) return Forbid();
        var stage = competition.Editions.SelectMany(item => item.Stages)
            .SingleOrDefault(item => item.Id == stageId);
        if (stage is null) return NotFound(new { error = "Stage does not belong to this competition." });
        if (request.GroupId.HasValue && stage.Groups.All(item => item.Id != request.GroupId.Value))
            return BadRequest(new { error = "Group does not belong to the selected stage." });
        if (request.RoundId.HasValue && stage.Rounds.All(item => item.Id != request.RoundId.Value))
            return BadRequest(new { error = "Round does not belong to the selected stage." });
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.ContestShape))
            return BadRequest(new { error = "Contest name and shape are required." });
        try
        {
            var contest = new Contest(Guid.NewGuid(), competition.TenantId,
                competition.SportId, request.ContestShape.Trim());
            contest.SetName(request.Name.Trim());
            contest.AssignToHierarchy(stageId, request.GroupId, request.RoundId, request.SortOrder);
            if (request.AllowedParticipantTypes is { Count: > 0 })
                contest.ConfigureParticipantTypes(request.AllowedParticipantTypes);
            if (competition.RulesetVersionId.HasValue)
                contest.AssignRulesetVersion(competition.RulesetVersionId.Value);
            AddSportSegments(contest, competition.RulesetVersionId);
            _database.Contests.Add(contest);
            await _database.SaveChangesAsync(cancellationToken);
            return CreatedAtAction(nameof(ContestsController.GetById), "Contests",
                new { id = contest.Id }, contest);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
    }

    [HttpPost("{id:guid}/fixtures/round-robin")]
    [RequirePermission("competitions.manage")]
    public async Task<IActionResult> GenerateRoundRobinFixtures(
        Guid id,
        [FromBody] GenerateRoundRobinFixturesRequest request,
        CancellationToken cancellationToken)
    {
        var competition = await _database.Competitions
            .Include(item => item.Editions)
            .ThenInclude(edition => edition.Stages)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (competition is null) return NotFound();
        if (!User.CanAccessTenant(competition.TenantId)) return Forbid();

        var existingStageIds = competition.Editions
            .SelectMany(edition => edition.Stages)
            .Select(stage => stage.Id)
            .ToArray();
        if (existingStageIds.Length > 0
            && await _database.Contests.AnyAsync(
                contest => contest.StageId.HasValue
                    && existingStageIds.Contains(contest.StageId.Value),
                cancellationToken))
        {
            return Conflict(new { error = "Fixtures have already been generated for this competition." });
        }

        var eligibleParticipantIds = await _database.Registrations
            .Where(registration => registration.CompetitionId == id
                && (registration.Status == RegistrationStatus.Approved
                    || registration.Status == RegistrationStatus.Confirmed))
            .Select(registration => registration.ParticipantId)
            .Distinct()
            .ToArrayAsync(cancellationToken);
        var participants = await _database.Participants
            .Where(participant => participant.TenantId == competition.TenantId
                && eligibleParticipantIds.Contains(participant.Id))
            .OrderBy(participant => participant.Id)
            .ToArrayAsync(cancellationToken);
        if (participants.Length < 2)
        {
            return BadRequest(new
            {
                error = "At least two approved competition registrations are required."
            });
        }
        if (request.DaysBetweenRounds is < 1 or > 365)
            return BadRequest(new { error = "Days between rounds must be between 1 and 365." });
        if (string.IsNullOrWhiteSpace(request.Venue))
            return BadRequest(new { error = "A fixture venue is required." });

        var startTimeUtc = request.StartTimeUtc.Kind == DateTimeKind.Utc
            ? request.StartTimeUtc
            : request.StartTimeUtc.ToUniversalTime();
        var edition = competition.AddEdition(
            request.EditionName,
            startTimeUtc.Date,
            request.EndDateUtc?.ToUniversalTime());
        var stage = edition.AddStage(request.StageName, StageType.League, 1);
        var drawSeed = request.DrawSeed
            ?? RandomNumberGenerator.GetInt32(1, int.MaxValue);
        stage.RecordFixtureGeneration(
            drawSeed, request.HomeAndAway, request.DaysBetweenRounds, request.Venue);
        _database.Entry(edition).State = EntityState.Added;
        _database.Entry(stage).State = EntityState.Added;

        var participantNames = participants.ToDictionary(
            participant => participant.Id,
            participant => participant.Name);
        var generated = _roundRobinEngine.GenerateContests(
                stage,
                participants.Select(participant => new ParticipantInfo(
                    participant.Id,
                    participant.Type.ToString())),
                new RoundRobinOptions
                {
                    SportId = competition.SportId,
                    HomeAndAway = request.HomeAndAway,
                    BalanceVenues = true,
                    DrawSeed = drawSeed
                })
            .ToArray();

        var roundSlots = new Dictionary<int, int>();
        foreach (var fixture in generated)
        {
            var slot = roundSlots.GetValueOrDefault(fixture.Round);
            roundSlots[fixture.Round] = slot + 1;
            AddSportSegments(fixture.Contest, competition.RulesetVersionId);
            fixture.Contest.SetName(
                $"{participantNames[fixture.HomeParticipantId!.Value]} vs "
                + participantNames[fixture.AwayParticipantId!.Value]);
            fixture.Contest.Schedule(
                request.Venue,
                startTimeUtc
                    .AddDays((fixture.Round - 1) * request.DaysBetweenRounds)
                    .AddMinutes(slot * 120),
                90);
        }

        _database.Contests.AddRange(generated.Select(fixture => fixture.Contest));
        await _database.SaveChangesAsync(cancellationToken);

        return Ok(new FixtureGenerationResponse(
            id,
            edition.Id,
            stage.Id,
            participants.Length,
            generated.Length,
            drawSeed,
            generated.Select(fixture => new GeneratedFixtureResponse(
                fixture.Contest.Id,
                fixture.Round,
                fixture.MatchNumber,
                fixture.HomeParticipantId!.Value,
                fixture.AwayParticipantId!.Value,
                fixture.Contest.StartTime!.Value)).ToArray()));
    }

    [HttpPost("{id:guid}/playoffs/top-four")]
    [RequirePermission("competitions.manage")]
    public async Task<IActionResult> GenerateTopFourPlayoffs(
        Guid id,
        [FromBody] GenerateTopFourPlayoffsRequest request,
        CancellationToken cancellationToken)
    {
        var competition = await _database.Competitions
            .Include(item => item.Editions)
            .ThenInclude(edition => edition.Stages)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (competition is null) return NotFound();
        if (!User.CanAccessTenant(competition.TenantId)) return Forbid();
        if (await _database.Brackets.AnyAsync(
                bracket => bracket.CompetitionId == id,
                cancellationToken))
        {
            return Conflict(new { error = "A playoff bracket already exists." });
        }

        var qualifiers = await _database.Standings
            .AsNoTracking()
            .Where(standing => standing.CompetitionId == id)
            .OrderBy(standing => standing.Position)
            .ThenByDescending(standing => standing.Points)
            .ThenByDescending(standing => standing.PointDifferential)
            .Take(4)
            .ToArrayAsync(cancellationToken);
        if (qualifiers.Length != 4)
        {
            return BadRequest(new
            {
                error = "Four ranked standings entries are required to generate the playoffs."
            });
        }

        var participantIds = qualifiers.Select(item => item.ParticipantId).ToArray();
        var participants = await _database.Participants
            .Where(participant => participant.TenantId == competition.TenantId
                && participantIds.Contains(participant.Id))
            .ToDictionaryAsync(participant => participant.Id, cancellationToken);
        if (participants.Count != 4)
            return BadRequest(new { error = "Every qualifier must resolve to a tenant participant." });
        if (!IsValidSeriesLength(request.SemifinalBestOf)
            || !IsValidSeriesLength(request.FinalBestOf))
        {
            return BadRequest(new
            {
                error = "Semifinal and final formats must be a single game or best-of-3, best-of-5, or best-of-7."
            });
        }
        if (!TryParseHomeSequence(
                request.SemifinalHomeSequence,
                request.SemifinalBestOf,
                out _)
            || !TryParseHomeSequence(
                request.FinalHomeSequence,
                request.FinalBestOf,
                out _))
            return BadRequest(new
            {
                error = "Each home sequence must contain one H/A entry per scheduled game."
            });
        if (request.LegsPerTie is not 1 and not 2)
            return BadRequest(new { error = "Knockout ties support one or two legs." });
        if ((request.SemifinalBestOf > 1 || request.FinalBestOf > 1)
            && (request.LegsPerTie > 1 || request.IncludeThirdPlace))
        {
            return BadRequest(new
            {
                error = "Best-of series cannot be combined with aggregate legs or a third-place playoff."
            });
        }

        var edition = competition.Editions
            .OrderByDescending(item => item.StartDate)
            .FirstOrDefault();
        if (edition is null)
            return BadRequest(new { error = "The competition must have an edition before playoffs." });
        var stage = edition.AddStage(request.StageName, StageType.Knockout, 2);
        _database.Entry(stage).State = EntityState.Added;

        if (request.SemifinalBestOf > 1 || request.FinalBestOf > 1)
        {
            return await GenerateSeriesPlayoffsAsync(
                competition,
                stage,
                qualifiers,
                participants,
                request,
                cancellationToken);
        }

        var effectiveDrawSeed = request.RandomDraw
            ? request.DrawSeed ?? Random.Shared.Next()
            : request.DrawSeed;
        var ordered = qualifiers.Select((standing, index) => new ParticipantInfo(
            standing.ParticipantId,
            participants[standing.ParticipantId].Type.ToString(),
            index + 1));
        var generated = _knockoutEngine.GenerateContests(
                stage,
                ordered,
                new KnockoutOptions
                {
                    SportId = competition.SportId,
                    RandomDraw = request.RandomDraw,
                    DrawSeed = effectiveDrawSeed,
                    IncludeThirdPlace = request.IncludeThirdPlace,
                    LegsPerTie = request.LegsPerTie
                })
            .ToArray();
        var semiFinalTies = generated.Where(item => item.Round == 1)
            .GroupBy(item => item.MatchNumber)
            .OrderBy(group => group.Key)
            .ToArray();
        var finalTie = generated.Where(item => item.Round == 2 && !item.IsThirdPlace)
            .GroupBy(item => item.MatchNumber)
            .Single();
        var thirdPlaceTie = generated.Where(item => item.IsThirdPlace)
            .GroupBy(item => item.MatchNumber)
            .SingleOrDefault();
        if (semiFinalTies.Length != 2)
            return StatusCode(500, new { error = "The knockout engine did not create two semifinals." });

        var startTimeUtc = request.StartTimeUtc.Kind == DateTimeKind.Utc
            ? request.StartTimeUtc
            : request.StartTimeUtc.ToUniversalTime();
        foreach (var generatedContest in generated)
        {
            AddSportSegments(generatedContest.Contest, competition.RulesetVersionId);
            var phase = generatedContest.IsThirdPlace
                ? "Third-place playoff"
                : generatedContest.Round == 1
                    ? $"Semifinal {generatedContest.MatchNumber}"
                    : "Final";
            var participantLabel = generatedContest.HomeParticipantId.HasValue
                && generatedContest.AwayParticipantId.HasValue
                ? $": {participants[generatedContest.HomeParticipantId.Value].Name} vs "
                    + participants[generatedContest.AwayParticipantId.Value].Name
                : string.Empty;
            var legLabel = request.LegsPerTie == 2 && !generatedContest.IsThirdPlace
                ? $" — Leg {generatedContest.LegNumber}"
                : string.Empty;
            generatedContest.Contest.SetName($"{phase}{legLabel}{participantLabel}");
            var phaseOffset = generatedContest.Round == 1 && !generatedContest.IsThirdPlace
                ? 0
                : request.DaysUntilFinal;
            generatedContest.Contest.Schedule(
                request.Venue,
                startTimeUtc.AddDays(phaseOffset
                    + ((generatedContest.LegNumber - 1) * request.DaysBetweenLegs)));
        }

        var bracket = new Bracket(
            Guid.NewGuid(),
            id,
            request.BracketName,
            BracketType.SingleElimination,
            4);
        bracket.SetSeeded(!request.RandomDraw);
        bracket.SetDrawSeed(effectiveDrawSeed);
        var semiFinalRound = new BracketRound(
            Guid.NewGuid(),
            bracket.Id,
            1,
            "Semifinals",
            2);
        var finalRound = new BracketRound(
            Guid.NewGuid(),
            bracket.Id,
            2,
            request.IncludeThirdPlace ? "Finals" : "Final",
            request.IncludeThirdPlace ? 2 : 1);
        var finalMatch = new BracketMatch(Guid.NewGuid(), finalRound.Id, 1);
        var finalFirstLeg = finalTie.OrderBy(item => item.LegNumber).First();
        if (finalFirstLeg.TieId.HasValue)
            finalMatch.BindAggregateTie(finalFirstLeg.TieId.Value, finalFirstLeg.Contest.Id);
        else
            finalMatch.BindContest(finalFirstLeg.Contest.Id);
        finalRound.AddMatch(finalMatch);

        BracketMatch? thirdPlaceMatch = null;
        if (thirdPlaceTie is not null)
        {
            var thirdPlaceContest = thirdPlaceTie.Single();
            thirdPlaceMatch = new BracketMatch(Guid.NewGuid(), finalRound.Id, 2);
            thirdPlaceMatch.BindContest(thirdPlaceContest.Contest.Id);
            thirdPlaceMatch.MarkAsThirdPlace();
            finalRound.AddMatch(thirdPlaceMatch);
        }

        for (var index = 0; index < semiFinalTies.Length; index++)
        {
            var tie = semiFinalTies[index].OrderBy(item => item.LegNumber).ToArray();
            var fixture = tie[0];
            var match = new BracketMatch(
                Guid.NewGuid(),
                semiFinalRound.Id,
                index + 1);
            match.SetParticipants(
                fixture.HomeParticipantId,
                fixture.AwayParticipantId);
            if (fixture.TieId.HasValue)
                match.BindAggregateTie(fixture.TieId.Value, fixture.Contest.Id);
            else
                match.BindContest(fixture.Contest.Id);
            match.AdvanceTo(finalMatch.Id, index + 1);
            if (thirdPlaceMatch is not null)
                match.AdvanceLoserTo(thirdPlaceMatch.Id, index + 1);
            semiFinalRound.AddMatch(match);
        }
        bracket.AddRound(semiFinalRound);
        bracket.AddRound(finalRound);
        bracket.Publish();
        bracket.Start();

        _database.Brackets.Add(bracket);
        _database.Contests.AddRange(generated.Select(item => item.Contest));
        await _database.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            bracket.Id,
            bracket.CompetitionId,
            bracket.Status,
            Qualifiers = qualifiers.Select(item => new
            {
                item.ParticipantId,
                participants[item.ParticipantId].Name,
                item.Position
            }),
            SemiFinalContestIds = semiFinalTies.SelectMany(group => group).Select(item => item.Contest.Id),
            FinalContestIds = finalTie.Select(item => item.Contest.Id),
            FinalContestId = finalFirstLeg.Contest.Id,
            ThirdPlaceContestId = thirdPlaceTie?.Single().Contest.Id,
            request.LegsPerTie,
            request.IncludeThirdPlace,
            DrawSeed = effectiveDrawSeed
        });
    }

    [HttpPost("{id:guid}/playoffs/knockout")]
    [RequirePermission("competitions.manage")]
    public async Task<IActionResult> GenerateKnockoutPlayoffs(
        Guid id,
        [FromBody] GenerateKnockoutPlayoffsRequest request,
        CancellationToken cancellationToken)
    {
        if (request.QualifierCount is < 2 or > 64)
            return BadRequest(new { error = "Knockout brackets require between 2 and 64 qualifiers." });
        if (request.LegsPerTie is not 1 and not 2)
            return BadRequest(new { error = "Knockout ties support one or two legs." });
        if (request.IncludeThirdPlace && request.QualifierCount < 4)
            return BadRequest(new { error = "A third-place playoff requires at least four qualifiers." });
        if (request.DaysBetweenRounds < 1 || request.DaysBetweenLegs < 1)
            return BadRequest(new { error = "Round and leg intervals must be at least one day." });

        var competition = await _database.Competitions
            .Include(item => item.Editions)
            .ThenInclude(edition => edition.Stages)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (competition is null) return NotFound();
        if (!User.CanAccessTenant(competition.TenantId)) return Forbid();
        if (await _database.Brackets.AnyAsync(
                bracket => bracket.CompetitionId == id,
                cancellationToken))
            return Conflict(new { error = "A playoff bracket already exists." });

        var qualifiers = await _database.Standings
            .AsNoTracking()
            .Where(standing => standing.CompetitionId == id)
            .OrderBy(standing => standing.Position)
            .ThenByDescending(standing => standing.Points)
            .ThenByDescending(standing => standing.PointDifferential)
            .Take(request.QualifierCount)
            .ToArrayAsync(cancellationToken);
        if (qualifiers.Length != request.QualifierCount)
            return BadRequest(new
            {
                error = $"{request.QualifierCount} ranked standings entries are required to generate the playoffs."
            });

        var participantIds = qualifiers.Select(item => item.ParticipantId).ToArray();
        var participants = await _database.Participants
            .Where(participant => participant.TenantId == competition.TenantId
                && participantIds.Contains(participant.Id))
            .ToDictionaryAsync(participant => participant.Id, cancellationToken);
        if (participants.Count != request.QualifierCount)
            return BadRequest(new { error = "Every qualifier must resolve to a tenant participant." });

        var edition = competition.Editions
            .OrderByDescending(item => item.StartDate)
            .FirstOrDefault();
        if (edition is null)
            return BadRequest(new { error = "The competition must have an edition before playoffs." });
        var roundCount = _knockoutEngine.CalculateRounds(request.QualifierCount, request.IncludeThirdPlace);
        var stage = edition.AddStage(request.StageName, StageType.Knockout, roundCount);
        _database.Entry(stage).State = EntityState.Added;

        var effectiveDrawSeed = request.RandomDraw
            ? request.DrawSeed ?? Random.Shared.Next()
            : request.DrawSeed;
        var generated = _knockoutEngine.GenerateContests(
                stage,
                qualifiers.Select((standing, index) => new ParticipantInfo(
                    standing.ParticipantId,
                    participants[standing.ParticipantId].Type.ToString(),
                    index + 1)),
                new KnockoutOptions
                {
                    SportId = competition.SportId,
                    RandomDraw = request.RandomDraw,
                    DrawSeed = effectiveDrawSeed,
                    IncludeThirdPlace = request.IncludeThirdPlace,
                    LegsPerTie = request.LegsPerTie
                })
            .ToArray();
        var normalTies = generated.Where(item => !item.IsThirdPlace)
            .GroupBy(item => (item.Round, item.MatchNumber))
            .OrderBy(group => group.Key.Round)
            .ThenBy(group => group.Key.MatchNumber)
            .ToArray();
        var thirdPlaceTie = generated.Where(item => item.IsThirdPlace).ToArray();
        var startTimeUtc = request.StartTimeUtc.Kind == DateTimeKind.Utc
            ? request.StartTimeUtc
            : request.StartTimeUtc.ToUniversalTime();

        foreach (var fixture in generated)
        {
            AddSportSegments(fixture.Contest, competition.RulesetVersionId);
            var phase = fixture.IsThirdPlace
                ? "Third-place playoff"
                : fixture.Round == roundCount
                    ? "Final"
                    : $"Round {fixture.Round} — Match {fixture.MatchNumber}";
            var legLabel = request.LegsPerTie == 2 && !fixture.IsThirdPlace
                ? $" — Leg {fixture.LegNumber}"
                : string.Empty;
            fixture.Contest.SetName($"{phase}{legLabel}");
            fixture.Contest.Schedule(
                request.Venue,
                startTimeUtc.AddDays(
                    ((fixture.Round - 1) * request.DaysBetweenRounds)
                    + ((fixture.LegNumber - 1) * request.DaysBetweenLegs)));
        }

        var bracketSize = 1;
        while (bracketSize < request.QualifierCount) bracketSize *= 2;
        var bracket = new Bracket(
            Guid.NewGuid(),
            competition.Id,
            request.BracketName,
            BracketType.SingleElimination,
            bracketSize);
        bracket.SetSeeded(!request.RandomDraw);
        bracket.SetDrawSeed(effectiveDrawSeed);

        var matchesByTie = new Dictionary<(int Round, int MatchNumber), BracketMatch>();
        var tiesByMatchId = new Dictionary<Guid, ContestGenerationResult[]>();
        for (var roundNumber = 1; roundNumber <= roundCount; roundNumber++)
        {
            var roundTies = normalTies.Where(group => group.Key.Round == roundNumber).ToArray();
            var hasThirdPlace = roundNumber == roundCount && thirdPlaceTie.Length > 0;
            var roundName = roundNumber == roundCount
                ? hasThirdPlace ? "Finals" : "Final"
                : roundNumber == roundCount - 1 ? "Semifinals" : $"Round {roundNumber}";
            var round = new BracketRound(
                Guid.NewGuid(),
                bracket.Id,
                roundNumber,
                roundName,
                roundTies.Length + (hasThirdPlace ? 1 : 0));
            round.SetScheduledAt(startTimeUtc.AddDays((roundNumber - 1) * request.DaysBetweenRounds));

            for (var index = 0; index < roundTies.Length; index++)
            {
                var tie = roundTies[index].OrderBy(item => item.LegNumber).ToArray();
                var firstLeg = tie[0];
                var match = new BracketMatch(Guid.NewGuid(), round.Id, index + 1);
                match.SetParticipants(firstLeg.HomeParticipantId, firstLeg.AwayParticipantId);
                if (firstLeg.TieId.HasValue)
                    match.BindAggregateTie(firstLeg.TieId.Value, firstLeg.Contest.Id);
                else
                    match.BindContest(firstLeg.Contest.Id);
                round.AddMatch(match);
                matchesByTie[roundTies[index].Key] = match;
                tiesByMatchId[match.Id] = tie;
            }

            if (hasThirdPlace)
            {
                var fixture = thirdPlaceTie.Single();
                var match = new BracketMatch(Guid.NewGuid(), round.Id, roundTies.Length + 1);
                match.BindContest(fixture.Contest.Id);
                match.MarkAsThirdPlace();
                round.AddMatch(match);
                tiesByMatchId[match.Id] = thirdPlaceTie;
            }
            bracket.AddRound(round);
        }

        for (var roundNumber = 1; roundNumber < roundCount; roundNumber++)
        {
            var current = matchesByTie.Where(item => item.Key.Round == roundNumber)
                .OrderBy(item => item.Key.MatchNumber)
                .Select(item => item.Value)
                .ToArray();
            var next = matchesByTie.Where(item => item.Key.Round == roundNumber + 1)
                .OrderBy(item => item.Key.MatchNumber)
                .Select(item => item.Value)
                .ToArray();
            for (var index = 0; index < current.Length; index++)
                current[index].AdvanceTo(next[index / 2].Id, (index % 2) + 1);
        }

        if (thirdPlaceTie.Length > 0)
        {
            var thirdPlaceMatch = bracket.Rounds.SelectMany(round => round.Matches)
                .Single(match => match.IsThirdPlace);
            var semifinals = matchesByTie.Where(item => item.Key.Round == roundCount - 1)
                .OrderBy(item => item.Key.MatchNumber)
                .Select(item => item.Value)
                .ToArray();
            for (var index = 0; index < semifinals.Length; index++)
                semifinals[index].AdvanceLoserTo(thirdPlaceMatch.Id, index + 1);
        }

        var allMatches = bracket.Rounds.SelectMany(round => round.Matches).ToDictionary(match => match.Id);
        var byeCount = 0;
        foreach (var match in matchesByTie.Where(item => item.Key.Round == 1)
                     .OrderBy(item => item.Key.MatchNumber)
                     .Select(item => item.Value))
        {
            var byeWinner = match.Participant1Id ?? match.Participant2Id;
            if (!byeWinner.HasValue || (match.Participant1Id.HasValue && match.Participant2Id.HasValue))
                continue;

            byeCount++;
            match.SetWinner(byeWinner.Value);
            foreach (var contest in tiesByMatchId[match.Id].Select(item => item.Contest))
                contest.Cancel();
            if (!match.NextMatchId.HasValue || !match.NextMatchSlot.HasValue) continue;

            var target = allMatches[match.NextMatchId.Value];
            target.SetParticipants(
                match.NextMatchSlot == 1 ? byeWinner : target.Participant1Id,
                match.NextMatchSlot == 2 ? byeWinner : target.Participant2Id);
            foreach (var contest in tiesByMatchId[target.Id].Select(item => item.Contest))
            {
                var slot = target.IsAggregateTie && contest.SeriesGameNumber == 2
                    ? 3 - match.NextMatchSlot.Value
                    : match.NextMatchSlot.Value;
                contest.AddParticipant(
                    byeWinner.Value,
                    participants[byeWinner.Value].Type.ToString(),
                    slot);
            }
        }

        bracket.Publish();
        bracket.Start();
        _database.Brackets.Add(bracket);
        _database.Contests.AddRange(generated.Select(item => item.Contest));
        await _database.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            bracket.Id,
            bracket.CompetitionId,
            bracket.Size,
            bracket.Status,
            bracket.IsSeeded,
            bracket.DrawSeed,
            QualifierCount = qualifiers.Length,
            ByeCount = byeCount,
            ContestCount = generated.Length,
            request.LegsPerTie,
            request.IncludeThirdPlace
        });
    }

    [HttpPost("{id:guid}/golf/tournament")]
    [RequirePermission("competitions.manage")]
    public async Task<IActionResult> GenerateGolfTournament(
        Guid id,
        [FromBody] GenerateGolfTournamentRequest request,
        CancellationToken cancellationToken)
    {
        var competition = await _database.Competitions
            .Include(item => item.Editions)
            .ThenInclude(edition => edition.Stages)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (competition is null) return NotFound();
        if (!User.CanAccessTenant(competition.TenantId)) return Forbid();
        if (competition.SportId != Guid.Parse(
                "00000000-0000-0000-0000-000000000102"))
            return BadRequest(new { error = "The competition is not configured for golf." });
        if (request.TotalRounds != 4 || request.HolesPerRound != 18)
        {
            return BadRequest(new
            {
                error = "The authoritative golf format requires four 18-hole rounds."
            });
        }
        if (request.CutAfterRound is < 1 or >= 4
            || request.CutToParticipants < 1)
        {
            return BadRequest(new { error = "The cut configuration is invalid." });
        }
        var holePars = request.HolePars?.ToArray()
            ?? GolfRulesetService.StandardHolePars.ToArray();
        if (string.IsNullOrWhiteSpace(request.CourseName)
            || string.IsNullOrWhiteSpace(request.CourseLocation)
            || holePars.Length != 18
            || holePars.Any(par => par is < 3 or > 5)
            || holePars.Sum() != request.ParPerRound)
        {
            return BadRequest(new
            {
                error = "Course name/location and exactly 18 hole pars from 3 through 5 "
                    + "that add up to par per round are required."
            });
        }

        var existingStageIds = competition.Editions
            .SelectMany(edition => edition.Stages)
            .Select(stage => stage.Id)
            .ToArray();
        if (await _database.Contests.AnyAsync(
                contest => contest.StageId.HasValue
                    && existingStageIds.Contains(contest.StageId.Value)
                    && contest.ContestShape == "MultiRoundLeaderboard",
                cancellationToken))
        {
            return Conflict(new { error = "A golf tournament already exists." });
        }

        var eligibleIds = await _database.Registrations
            .Where(registration => registration.CompetitionId == id
                && (registration.Status == RegistrationStatus.Approved
                    || registration.Status == RegistrationStatus.Confirmed))
            .Select(registration => registration.ParticipantId)
            .Distinct()
            .ToArrayAsync(cancellationToken);
        var participants = await _database.Participants
            .Where(participant => participant.TenantId == competition.TenantId
                && eligibleIds.Contains(participant.Id))
            .OrderBy(participant => participant.Id)
            .ToArrayAsync(cancellationToken);
        if (participants.Length != request.ExpectedParticipants)
        {
            return BadRequest(new
            {
                error = $"Exactly {request.ExpectedParticipants} approved golfers are required."
            });
        }
        if (request.CutToParticipants > participants.Length)
            return BadRequest(new { error = "The cut cannot exceed the field size." });

        var startTimeUtc = request.StartTimeUtc.Kind == DateTimeKind.Utc
            ? request.StartTimeUtc
            : request.StartTimeUtc.ToUniversalTime();
        var edition = competition.AddEdition(
            request.EditionName,
            startTimeUtc.Date,
            startTimeUtc.Date.AddDays(request.TotalRounds));
        var stage = edition.AddStage(request.StageName, StageType.Leaderboard, 1);
        _database.Entry(edition).State = EntityState.Added;
        _database.Entry(stage).State = EntityState.Added;

        var contest = new Contest(
            Guid.NewGuid(),
            competition.TenantId,
            competition.SportId,
            "MultiRoundLeaderboard");
        var rulesetSport = await _database.Sports
            .AsNoTracking()
            .Include(sport => sport.Versions)
            .SingleAsync(sport => sport.Id == competition.SportId, cancellationToken);
        var publishedRulesetId = rulesetSport.Versions
            .Where(version => version.IsPublished)
            .OrderByDescending(version => version.VersionNumber)
            .Select(version => (Guid?)version.Id)
            .FirstOrDefault();
        if (publishedRulesetId.HasValue)
            contest.AssignRulesetVersion(publishedRulesetId.Value);
        contest.AssignToStage(stage.Id, 1);
        contest.SetName(request.TournamentName);
        contest.ConfigureLeaderboard(
            request.ParPerRound,
            request.CutAfterRound,
            request.CutToParticipants,
            request.CourseName,
            request.CourseLocation,
            holePars);
        foreach (var participant in participants)
            contest.AddParticipant(participant.Id, participant.Type.ToString());
        for (var roundNumber = 1; roundNumber <= request.TotalRounds; roundNumber++)
        {
            var round = contest.AddSegment(
                $"Round {roundNumber}",
                "Round",
                roundNumber);
            for (var holeNumber = 1; holeNumber <= request.HolesPerRound; holeNumber++)
            {
                round.AddNestedSegment(
                    $"Hole {holeNumber} (Par {holePars[holeNumber - 1]})",
                    "Hole",
                    holeNumber);
            }
        }
        contest.Schedule(request.Venue, startTimeUtc);
        _database.Contests.Add(contest);
        await _database.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            competitionId = id,
            editionId = edition.Id,
            stageId = stage.Id,
            contestId = contest.Id,
            participantCount = participants.Length,
            totalRounds = request.TotalRounds,
            holesPerRound = request.HolesPerRound,
            cutAfterRound = request.CutAfterRound,
            cutToParticipants = request.CutToParticipants,
            courseName = request.CourseName,
            courseLocation = request.CourseLocation,
            parPerRound = request.ParPerRound,
            holePars
        });
    }

    [HttpPost("{id:guid}/golf/scorecards")]
    [RequirePermission("competitions.manage")]
    public async Task<IActionResult> SubmitGolfScorecard(
        Guid id,
        [FromBody] SubmitGolfScorecardRequest request,
        CancellationToken cancellationToken)
    {
        var competition = await _database.Competitions
            .AsNoTracking()
            .Include(item => item.Editions)
            .ThenInclude(edition => edition.Stages)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (competition is null) return NotFound();
        if (!User.CanAccessTenant(competition.TenantId)) return Forbid();
        var stageIds = competition.Editions
            .SelectMany(edition => edition.Stages)
            .Select(stage => stage.Id)
            .ToArray();
        var contest = await _database.Contests
            .Include(item => item.Participants)
            .Include(item => item.Segments)
            .ThenInclude(segment => segment.NestedSegments)
            .SingleOrDefaultAsync(item => item.StageId.HasValue
                && stageIds.Contains(item.StageId.Value)
                && item.ContestShape == "MultiRoundLeaderboard",
                cancellationToken);
        if (contest is null) return NotFound(new { error = "Golf tournament not found." });
        var participant = contest.Participants.SingleOrDefault(
            item => item.ParticipantId == request.ParticipantId);
        if (participant is null)
            return BadRequest(new { error = "The golfer is not in this tournament." });
        if (participant.Status is ContestParticipantStatus.Withdrawn
            or ContestParticipantStatus.Disqualified)
            return Conflict(new { error = $"The golfer is {participant.Status}." });
        if (request.RoundNumber > contest.CutAfterRound
            && contest.CutAppliedAt is null)
            return Conflict(new { error = "The cut must be applied before round three." });
        if (request.RoundNumber > contest.CutAfterRound
            && participant.MadeCut != true)
            return Conflict(new { error = "The golfer did not make the cut." });

        var round = contest.Segments.SingleOrDefault(
            item => item.SegmentType == "Round"
                && item.SortOrder == request.RoundNumber);
        if (round is null) return BadRequest(new { error = "The round is invalid." });
        var holes = request.Holes.OrderBy(item => item.HoleNumber).ToArray();
        if (holes.Length != 18
            || holes.Select(item => item.HoleNumber).Distinct().Count() != 18
            || holes[0].HoleNumber != 1
            || holes[^1].HoleNumber != 18
            || holes.Any(item => item.Strokes is < 1 or > 20
                || item.Penalties is < 0 or > 20))
        {
            return BadRequest(new
            {
                error = "A scorecard must contain one valid score for every hole 1–18."
            });
        }
        var holeSegments = round.NestedSegments.ToDictionary(
            item => item.SortOrder);
        var holeIds = holeSegments.Values.Select(item => item.Id).ToArray();
        if (await _database.LiveScores.AnyAsync(item =>
                item.ContestId == contest.Id
                && item.ParticipantId == request.ParticipantId
                && item.MetricName == "strokes"
                && item.SegmentId.HasValue
                && holeIds.Contains(item.SegmentId.Value),
                cancellationToken))
        {
            return Conflict(new { error = "This round scorecard was already submitted." });
        }

        var sequence = await _database.LiveScores
            .Where(item => item.ContestId == contest.Id)
            .MaxAsync(item => (int?)item.Sequence, cancellationToken) ?? 0;
        foreach (var hole in holes)
        {
            var segmentId = holeSegments[hole.HoleNumber].Id;
            var strokes = new LiveScore(
                Guid.NewGuid(),
                contest.Id,
                Guid.NewGuid(),
                request.ParticipantId,
                "strokes",
                hole.Strokes,
                segmentId);
            strokes.SetSequence(++sequence);
            strokes.SetDisplayValue(hole.Strokes.ToString());
            strokes.SetScorer(User.Identity?.Name, null);
            _database.LiveScores.Add(strokes);
            if (hole.Penalties > 0)
            {
                var penalty = new LiveScore(
                    Guid.NewGuid(),
                    contest.Id,
                    Guid.NewGuid(),
                    request.ParticipantId,
                    "penalties",
                    hole.Penalties,
                    segmentId);
                penalty.SetSequence(++sequence);
                penalty.SetDisplayValue($"+{hole.Penalties}");
                penalty.SetScorer(User.Identity?.Name, null);
                _database.LiveScores.Add(penalty);
            }
        }
        await _database.SaveChangesAsync(cancellationToken);
        await CompleteGolfTournamentIfReadyAsync(contest, cancellationToken);
        var configuredHolePars = JsonSerializer.Deserialize<int[]>(
            contest.HoleParsJson ?? "[]") ?? Array.Empty<int>();
        var roundPar = configuredHolePars.Length == 18
            ? configuredHolePars.Sum()
            : contest.ParPerRound ?? 0;

        return Ok(new
        {
            contestId = contest.Id,
            request.ParticipantId,
            request.RoundNumber,
            holeCount = holes.Length,
            total = holes.Sum(item => item.Strokes + item.Penalties),
            toPar = holes.Sum(item => item.Strokes + item.Penalties)
                - roundPar,
            status = "Accepted"
        });
    }

    [HttpPost("{id:guid}/golf/cut")]
    [RequirePermission("competitions.manage")]
    public async Task<IActionResult> ApplyGolfCut(
        Guid id,
        CancellationToken cancellationToken)
    {
        var competition = await _database.Competitions
            .AsNoTracking()
            .Include(item => item.Editions)
            .ThenInclude(edition => edition.Stages)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (competition is null) return NotFound();
        if (!User.CanAccessTenant(competition.TenantId)) return Forbid();
        var stageIds = competition.Editions.SelectMany(item => item.Stages)
            .Select(item => item.Id)
            .ToArray();
        var contest = await _database.Contests
            .Include(item => item.Participants)
            .Include(item => item.Segments)
            .ThenInclude(item => item.NestedSegments)
            .SingleOrDefaultAsync(item => item.StageId.HasValue
                && stageIds.Contains(item.StageId.Value)
                && item.ContestShape == "MultiRoundLeaderboard",
                cancellationToken);
        if (contest is null) return NotFound(new { error = "Golf tournament not found." });
        if (contest.CutAppliedAt.HasValue)
            return Conflict(new { error = "The cut has already been applied." });

        var cutRound = contest.CutAfterRound ?? 2;
        var cutHoleIds = contest.Segments
            .Where(round => round.SegmentType == "Round"
                && round.SortOrder <= cutRound)
            .SelectMany(round => round.NestedSegments)
            .Select(hole => hole.Id)
            .ToArray();
        var scores = await _database.LiveScores
            .AsNoTracking()
            .Where(item => item.ContestId == contest.Id
                && item.SegmentId.HasValue
                && cutHoleIds.Contains(item.SegmentId.Value)
                && (item.MetricName == "strokes"
                    || item.MetricName == "penalties"))
            .ToArrayAsync(cancellationToken);
        var strokeCounts = scores
            .Where(item => item.MetricName == "strokes")
            .GroupBy(item => item.ParticipantId)
            .ToDictionary(group => group.Key, group => group.Count());
        var expectedHoles = cutRound * 18;
        if (contest.Participants.Any(item =>
                item.Status == ContestParticipantStatus.Entered
                && strokeCounts.GetValueOrDefault(item.ParticipantId) != expectedHoles))
        {
            return Conflict(new
            {
                error = $"Every active golfer needs {expectedHoles} hole scores before the cut."
            });
        }

        var totals = scores.GroupBy(item => item.ParticipantId)
            .Select(group => new
            {
                ParticipantId = group.Key,
                Total = group.Sum(item => item.Value)
            })
            .OrderBy(item => item.Total)
            .ThenBy(item => item.ParticipantId)
            .ToArray();
        var cutTo = contest.CutToParticipants ?? totals.Length;
        var cutScore = totals[Math.Min(cutTo, totals.Length) - 1].Total;
        var totalByParticipant = totals.ToDictionary(
            item => item.ParticipantId,
            item => item.Total);
        foreach (var entry in contest.Participants)
        {
            var eligible = entry.Status == ContestParticipantStatus.Entered
                && totalByParticipant.GetValueOrDefault(
                    entry.ParticipantId,
                    decimal.MaxValue) <= cutScore;
            entry.SetCutStatus(eligible);
        }
        contest.MarkCutApplied();
        await _database.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            contestId = contest.Id,
            cutAfterRound = cutRound,
            requestedCutSize = cutTo,
            cutScore,
            qualifiedCount = contest.Participants.Count(item => item.MadeCut == true),
            qualifiedParticipantIds = contest.Participants
                .Where(item => item.MadeCut == true)
                .Select(item => item.ParticipantId)
        });
    }

    private async Task CompleteGolfTournamentIfReadyAsync(
        Contest contest,
        CancellationToken cancellationToken)
    {
        if (contest.CutAppliedAt is null) return;
        var activeIds = contest.Participants
            .Where(item => item.MadeCut == true
                && item.Status == ContestParticipantStatus.Entered)
            .Select(item => item.ParticipantId)
            .ToArray();
        var allHoleIds = contest.Segments
            .SelectMany(round => round.NestedSegments)
            .Select(hole => hole.Id)
            .ToArray();
        var counts = await _database.LiveScores
            .AsNoTracking()
            .Where(item => item.ContestId == contest.Id
                && activeIds.Contains(item.ParticipantId)
                && item.MetricName == "strokes"
                && item.SegmentId.HasValue
                && allHoleIds.Contains(item.SegmentId.Value))
            .GroupBy(item => item.ParticipantId)
            .Select(group => group.Count())
            .ToArrayAsync(cancellationToken);
        if (counts.Length == activeIds.Length
            && counts.All(count => count == 72))
        {
            if (contest.Status is ContestStatus.Scheduled or ContestStatus.Ready)
                contest.Start();
            contest.Complete();
            await _database.SaveChangesAsync(cancellationToken);
        }
    }

    [HttpPost("{id:guid}/combat/card")]
    [RequirePermission("competitions.manage")]
    public async Task<IActionResult> GenerateCombatCard(
        Guid id,
        [FromBody] GenerateCombatCardRequest request,
        CancellationToken cancellationToken)
    {
        var competition = await _database.Competitions
            .Include(item => item.Editions)
            .ThenInclude(edition => edition.Stages)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (competition is null) return NotFound();
        if (!User.CanAccessTenant(competition.TenantId)) return Forbid();
        if (competition.SportId != Guid.Parse(
                "00000000-0000-0000-0000-000000000105"))
            return BadRequest(new { error = "The competition is not configured for combat sports." });
        if (request.Bouts.Count != 8)
            return BadRequest(new { error = "The authoritative fight card requires exactly eight bouts." });

        var combatSport = await _database.Sports
            .Include(sport => sport.Versions)
            .SingleOrDefaultAsync(sport => sport.Code == "MMA", cancellationToken);
        var combatRuleset = competition.RulesetVersionId.HasValue
            ? combatSport?.Versions.SingleOrDefault(version =>
                version.Id == competition.RulesetVersionId.Value && version.IsPublished)
            : combatSport?.Versions
                .Where(version => version.IsPublished)
                .OrderByDescending(version => version.VersionNumber)
                .FirstOrDefault();
        if (combatRuleset is null)
            return Conflict(new
            {
                error = competition.RulesetVersionId.HasValue
                    ? "The competition's combat ruleset is not published."
                    : "Configure and publish the combat sports ruleset before generating a card."
            });

        var supportedResultMethods = ParseCombatResultMethods(combatRuleset.ResultMethods);
        var supportedWeightClasses = ParseCombatWeightClasses(
            combatRuleset.DisplayConfiguration);

        var requestedMethods = request.AllowedResultMethods is null
            || request.AllowedResultMethods.Count == 0
                ? supportedResultMethods
                : request.AllowedResultMethods;
        var allowedResultMethods = requestedMethods
            .Select(CanonicalCombatResultMethod)
            .ToArray();
        if (allowedResultMethods.Any(method => method is null)
            || allowedResultMethods.Distinct(StringComparer.OrdinalIgnoreCase).Count()
                != allowedResultMethods.Length
            || allowedResultMethods.Any(method => method is not null
                && !supportedResultMethods.Contains(
                    method,
                    StringComparer.OrdinalIgnoreCase)))
            return BadRequest(new
            {
                error = "Result methods must be unique supported combat methods."
            });
        var configuredResultMethods = allowedResultMethods.Select(method => method!).ToArray();

        var sections = new HashSet<string>(
            new[] { "PreliminaryCard", "MainCard", "CoMainEvent", "MainEvent" },
            StringComparer.OrdinalIgnoreCase);
        if (request.Bouts.Any(bout =>
                bout.Fighter1Id == bout.Fighter2Id
                || string.IsNullOrWhiteSpace(bout.WeightClass)
                || !supportedWeightClasses.Contains(bout.WeightClass.Trim())
                || !sections.Contains(bout.CardSection)
                || bout.ScheduledRounds is not (3 or 5))
            || request.Bouts.Count(bout =>
                bout.CardSection.Equals("MainEvent", StringComparison.OrdinalIgnoreCase)) != 1
            || request.Bouts.Count(bout =>
                bout.CardSection.Equals("CoMainEvent", StringComparison.OrdinalIgnoreCase)) != 1
            || request.Bouts.Any(bout =>
                bout.CardSection.Equals("MainEvent", StringComparison.OrdinalIgnoreCase)
                    ? bout.ScheduledRounds != 5
                    : bout.ScheduledRounds != 3))
        {
            return BadRequest(new
            {
                error = "Use preliminary, main, co-main, and one five-round main event with valid weight classes."
            });
        }

        var fighterIds = request.Bouts
            .SelectMany(bout => new[] { bout.Fighter1Id, bout.Fighter2Id })
            .ToArray();
        if (fighterIds.Distinct().Count() != 16)
            return BadRequest(new { error = "Each of the sixteen fighters may appear only once." });

        foreach (var bout in request.Bouts)
        {
            var distinctOfficials = bout.Officials
                .Select(official => official.OfficialId)
                .Distinct()
                .Count();
            var refereeCount = bout.Officials.Count(official =>
                official.Role.Equals("Referee", StringComparison.OrdinalIgnoreCase));
            var judgeCount = bout.Officials.Count(official =>
                official.Role.Equals("Judge", StringComparison.OrdinalIgnoreCase));
            if (distinctOfficials != bout.Officials.Count
                || refereeCount != 1
                || judgeCount < 3
                || bout.Officials.Any(official =>
                    string.IsNullOrWhiteSpace(official.DisplayName)))
            {
                return BadRequest(new
                {
                    error = "Every bout requires one referee and at least three distinct named judges."
                });
            }
        }

        var registeredIds = await _database.Registrations
            .Where(registration => registration.CompetitionId == id
                && (registration.Status == RegistrationStatus.Approved
                    || registration.Status == RegistrationStatus.Confirmed))
            .Select(registration => registration.ParticipantId)
            .Distinct()
            .ToArrayAsync(cancellationToken);
        if (fighterIds.Any(fighterId => !registeredIds.Contains(fighterId)))
            return BadRequest(new { error = "Every fighter must have an approved registration." });
        var fighters = await _database.Participants
            .Where(participant => participant.TenantId == competition.TenantId
                && fighterIds.Contains(participant.Id)
                && participant.Type == ParticipantType.Athlete)
            .ToDictionaryAsync(participant => participant.Id, cancellationToken);
        if (fighters.Count != 16)
            return BadRequest(new { error = "All card participants must be tenant athletes." });

        var existingStageIds = competition.Editions
            .SelectMany(edition => edition.Stages)
            .Select(stage => stage.Id)
            .ToArray();
        if (await _database.Contests.AnyAsync(contest =>
                contest.StageId.HasValue
                && existingStageIds.Contains(contest.StageId.Value)
                && contest.ContestShape == "Bout",
                cancellationToken))
            return Conflict(new { error = "A fight card already exists for this competition." });

        var startTimeUtc = request.StartTimeUtc.Kind == DateTimeKind.Utc
            ? request.StartTimeUtc
            : request.StartTimeUtc.ToUniversalTime();
        var edition = competition.AddEdition(
            request.EditionName,
            startTimeUtc.Date,
            startTimeUtc.Date.AddDays(1));
        var stage = edition.AddStage(request.StageName, StageType.FightCard, 1);
        competition.AssignRulesetVersion(combatRuleset.Id);
        _database.Entry(edition).State = EntityState.Added;
        _database.Entry(stage).State = EntityState.Added;

        var contests = new List<Contest>();
        for (var index = 0; index < request.Bouts.Count; index++)
        {
            var definition = request.Bouts[index];
            var boutNumber = index + 1;
            var contest = new Contest(
                Guid.NewGuid(),
                competition.TenantId,
                competition.SportId,
                "Bout");
            contest.AssignToStage(stage.Id, boutNumber);
            contest.AssignRulesetVersion(combatRuleset.Id);
            contest.SetName(
                $"Bout {boutNumber}: {fighters[definition.Fighter1Id].Name} vs "
                + fighters[definition.Fighter2Id].Name);
            contest.ConfigureBout(
                definition.WeightClass,
                CanonicalCardSection(definition.CardSection),
                definition.ScheduledRounds,
                configuredResultMethods);
            contest.AddParticipant(
                definition.Fighter1Id,
                ParticipantType.Athlete.ToString(),
                1);
            contest.AddParticipant(
                definition.Fighter2Id,
                ParticipantType.Athlete.ToString(),
                2);
            foreach (var official in definition.Officials)
                contest.AddOfficial(
                    official.OfficialId,
                    CanonicalOfficialRole(official.Role),
                    official.DisplayName);
            for (var round = 1; round <= definition.ScheduledRounds; round++)
                contest.AddSegment($"Round {round}", "Round", round);
            contest.Schedule(
                request.Venue,
                startTimeUtc.AddMinutes(index * request.MinutesBetweenBouts));
            contests.Add(contest);
        }

        _database.Contests.AddRange(contests);
        await _database.SaveChangesAsync(cancellationToken);
        return Ok(new
        {
            competitionId = id,
            editionId = edition.Id,
            stageId = stage.Id,
            rulesetVersionId = combatRuleset.Id,
            boutCount = contests.Count,
            allowedResultMethods = configuredResultMethods,
            bouts = contests.Select(contest => new
            {
                contest.Id,
                contest.Name,
                contest.SortOrder,
                contest.CardSection,
                contest.WeightClass,
                contest.ScheduledRounds,
                contest.RulesetVersionId,
                officialCount = contest.Officials.Count
            })
        });
    }

    [HttpPut("{id:guid}/combat/card/order")]
    [RequirePermission("competitions.manage")]
    public async Task<IActionResult> ReorderCombatCard(
        Guid id,
        [FromBody] ReorderCombatCardRequest request,
        CancellationToken cancellationToken)
    {
        var competition = await _database.Competitions
            .AsNoTracking()
            .Include(item => item.Editions)
            .ThenInclude(edition => edition.Stages)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (competition is null) return NotFound();
        if (!User.CanAccessTenant(competition.TenantId)) return Forbid();
        if (competition.Status != CompetitionStatus.Draft)
            return Conflict(new { error = "Fight-card order is locked after publication." });
        if (request.MinutesBetweenBouts is < 1 or > 180)
            return BadRequest(new { error = "Minutes between bouts must be from 1 through 180." });

        var stageIds = competition.Editions.SelectMany(edition => edition.Stages)
            .Where(stage => stage.Type == StageType.FightCard)
            .Select(stage => stage.Id)
            .ToArray();
        var bouts = await _database.Contests
            .Include(contest => contest.Participants)
            .Where(contest => contest.TenantId == competition.TenantId
                && contest.StageId.HasValue
                && stageIds.Contains(contest.StageId.Value)
                && contest.ContestShape == "Bout")
            .ToArrayAsync(cancellationToken);
        if (bouts.Length == 0) return NotFound(new { error = "Fight card not found." });
        if (request.BoutIds.Count != bouts.Length
            || request.BoutIds.Distinct().Count() != bouts.Length
            || request.BoutIds.Any(boutId => bouts.All(bout => bout.Id != boutId)))
            return BadRequest(new { error = "The order must contain every bout exactly once." });
        if (bouts.Any(bout => bout.Status != ContestStatus.Scheduled
            || bout.ResultMethod is not null))
            return Conflict(new { error = "Only an unstarted scheduled card can be reordered." });

        var sectionRanks = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["PreliminaryCard"] = 0,
            ["MainCard"] = 1,
            ["CoMainEvent"] = 2,
            ["MainEvent"] = 3
        };
        var byId = bouts.ToDictionary(bout => bout.Id);
        var ordered = request.BoutIds.Select(boutId => byId[boutId]).ToArray();
        var ranks = ordered.Select(bout => sectionRanks.GetValueOrDefault(
            bout.CardSection ?? string.Empty,
            int.MaxValue)).ToArray();
        if (!ranks.SequenceEqual(ranks.Order()))
            return BadRequest(new
            {
                error = "The order must preserve preliminary, main-card, co-main, and main-event sections."
            });

        var participantIds = bouts.SelectMany(bout => bout.Participants)
            .Select(participant => participant.ParticipantId)
            .ToArray();
        var names = await _database.Participants.AsNoTracking()
            .Where(participant => participant.TenantId == competition.TenantId
                && participantIds.Contains(participant.Id))
            .ToDictionaryAsync(participant => participant.Id, participant => participant.Name,
                cancellationToken);
        var firstStart = bouts.Min(bout => bout.StartTime) ?? DateTime.UtcNow;
        for (var index = 0; index < ordered.Length; index++)
        {
            var bout = ordered[index];
            var fighters = bout.Participants.OrderBy(participant => participant.SortOrder)
                .Select(participant => names.GetValueOrDefault(
                    participant.ParticipantId,
                    participant.ParticipantId.ToString()))
                .ToArray();
            bout.AssignToStage(bout.StageId!.Value, index + 1);
            bout.SetName($"Bout {index + 1}: {fighters[0]} vs {fighters[1]}");
            bout.Schedule(
                bout.Venue ?? "Combat Arena",
                firstStart.AddMinutes(index * request.MinutesBetweenBouts));
        }
        await _database.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            competitionId = id,
            bouts = ordered.Select(bout => new
            {
                bout.Id,
                bout.Name,
                bout.SortOrder,
                bout.CardSection,
                bout.StartTime
            })
        });
    }

    [HttpGet("{id:guid}/combat/card")]
    [RequirePermission("competitions.manage")]
    public async Task<IActionResult> GetCombatCardForAdministration(
        Guid id,
        CancellationToken cancellationToken)
    {
        var competition = await _database.Competitions.AsNoTracking()
            .Include(item => item.Editions)
            .ThenInclude(edition => edition.Stages)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (competition is null) return NotFound();
        if (!User.CanAccessTenant(competition.TenantId)) return Forbid();
        var stageIds = competition.Editions.SelectMany(edition => edition.Stages)
            .Where(stage => stage.Type == StageType.FightCard)
            .Select(stage => stage.Id)
            .ToArray();
        var bouts = await _database.Contests.AsNoTracking()
            .Include(contest => contest.Participants)
            .Include(contest => contest.Officials)
            .Include(contest => contest.Segments)
            .Include(contest => contest.Events)
            .Where(contest => contest.TenantId == competition.TenantId
                && contest.StageId.HasValue
                && stageIds.Contains(contest.StageId.Value)
                && contest.ContestShape == "Bout")
            .OrderBy(contest => contest.SortOrder)
            .ToArrayAsync(cancellationToken);
        if (bouts.Length == 0) return NotFound(new { error = "Fight card not found." });
        var participantIds = bouts.SelectMany(bout => bout.Participants)
            .Select(participant => participant.ParticipantId)
            .ToArray();
        var names = await _database.Participants.AsNoTracking()
            .Where(participant => participant.TenantId == competition.TenantId
                && participantIds.Contains(participant.Id))
            .ToDictionaryAsync(participant => participant.Id, participant => participant.Name,
                cancellationToken);

        return Ok(new
        {
            competition.Id,
            competition.Name,
            status = competition.Status.ToString(),
            competition.RulesetVersionId,
            canReorder = competition.Status == CompetitionStatus.Draft
                && bouts.All(bout => bout.Status == ContestStatus.Scheduled
                    && bout.ResultMethod == null),
            allowedResultMethods = bouts[0].GetAllowedResultMethods(),
            bouts = bouts.Select(bout => new
            {
                bout.Id,
                bout.Name,
                bout.SortOrder,
                bout.CardSection,
                bout.WeightClass,
                bout.ScheduledRounds,
                bout.RulesetVersionId,
                status = bout.Status.ToString(),
                bout.WinnerId,
                bout.ResultMethod,
                bout.ResultRound,
                bout.ResultTimeSeconds,
                fighters = bout.Participants.OrderBy(participant => participant.SortOrder)
                    .Select(participant => new
                    {
                        id = participant.ParticipantId,
                        name = names.GetValueOrDefault(participant.ParticipantId)
                    }),
                officials = bout.Officials.OrderBy(official => official.Role)
                    .ThenBy(official => official.DisplayName)
                    .Select(official => new
                    {
                        id = official.OfficialId,
                        official.Role,
                        official.DisplayName
                    }),
                rounds = bout.Segments
                    .Where(segment => segment.SegmentType == "Round")
                    .OrderBy(segment => segment.SortOrder)
                    .Select(segment => new
                    {
                        number = segment.SortOrder,
                        status = segment.Status.ToString()
                    }),
                pendingCorrections = bout.Events
                    .Where(item => item.EventType == "CombatResultCorrection"
                        && item.Status == EventStatus.Pending)
                    .Select(item => new
                    {
                        id = item.Id,
                        requestedAt = item.ServerTimestamp,
                        payload = item.MetricValue
                    })
            })
        });
    }

    [HttpPost("{id:guid}/combat/bouts/{boutId:guid}/rounds/{roundNumber:int}/{roundAction}")]
    [RequirePermission("competitions.manage")]
    public async Task<IActionResult> UpdateCombatRound(
        Guid id,
        Guid boutId,
        int roundNumber,
        string roundAction,
        CancellationToken cancellationToken)
    {
        var contest = await FindCombatBoutAsync(id, boutId, cancellationToken);
        if (contest is null) return NotFound();
        if (!User.CanAccessTenant(contest.TenantId)) return Forbid();
        var round = contest.Segments.SingleOrDefault(segment =>
            segment.SegmentType == "Round" && segment.SortOrder == roundNumber);
        if (round is null) return BadRequest(new { error = "The round is invalid." });

        if (roundAction.Equals("start", StringComparison.OrdinalIgnoreCase))
        {
            if (round.Status != SegmentStatus.Pending)
                return Conflict(new { error = "Only a pending round can be started." });
            contest.Start();
            round.Start();
        }
        else if (roundAction.Equals("end", StringComparison.OrdinalIgnoreCase))
        {
            if (round.Status != SegmentStatus.InProgress)
                return Conflict(new { error = "The round must be started before it can end." });
            round.End();
        }
        else
        {
            return BadRequest(new { error = "The action must be start or end." });
        }

        await _database.SaveChangesAsync(cancellationToken);
        return Ok(new
        {
            contestId = contest.Id,
            roundNumber,
            status = round.Status.ToString()
        });
    }

    [HttpPost("{id:guid}/combat/bouts/{boutId:guid}/result")]
    [RequirePermission("competitions.manage")]
    public async Task<IActionResult> SubmitCombatBoutResult(
        Guid id,
        Guid boutId,
        [FromBody] SubmitCombatBoutResultRequest request,
        CancellationToken cancellationToken)
    {
        var contest = await FindCombatBoutAsync(id, boutId, cancellationToken);
        if (contest is null) return NotFound();
        if (!User.CanAccessTenant(contest.TenantId)) return Forbid();
        if (contest.ResultMethod is not null)
            return Conflict(new { error = "This bout already has a result." });

        var validationError = ValidateCombatResultRequest(contest, request, out var method);
        if (validationError is not null)
            return BadRequest(new { error = validationError });

        var isDecision = method.EndsWith("Decision", StringComparison.Ordinal);
        var isDraw = method == "Draw";
        var participantIds = contest.Participants
            .Select(participant => participant.ParticipantId)
            .ToArray();

        var sequence = contest.Events.Select(item => item.SequenceNumber)
            .DefaultIfEmpty()
            .Max();
        foreach (var scorecard in request.Scorecards)
        {
            var scoreEvent = contest.AddEvent(
                "JudgeScorecard",
                participantIds[0],
                null,
                JsonSerializer.Serialize(scorecard),
                scorecard.OfficialId,
                "combat-api",
                ++sequence,
                $"combat-scorecard-{contest.Id}-{scorecard.OfficialId}");
            scoreEvent.Approve();
            _database.Entry(scoreEvent).State = EntityState.Added;
        }
        contest.RecordBoutResult(
            request.WinnerId,
            method,
            isDecision || isDraw ? contest.ScheduledRounds : request.Round,
            isDecision || isDraw ? null : request.TimeInRoundSeconds,
            request.Notes);
        if (contest.Status is ContestStatus.Scheduled or ContestStatus.Ready)
            contest.Start();
        contest.Complete();
        await _database.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            contestId = contest.Id,
            contest.WinnerId,
            contest.ResultMethod,
            contest.ResultRound,
            contest.ResultTimeSeconds,
            scorecardCount = request.Scorecards.Count,
            status = contest.Status.ToString()
        });
    }

    [HttpPost("{id:guid}/combat/bouts/{boutId:guid}/result/corrections")]
    [RequirePermission("competitions.manage")]
    public async Task<IActionResult> RequestCombatResultCorrection(
        Guid id,
        Guid boutId,
        [FromBody] CombatResultCorrectionRequest request,
        CancellationToken cancellationToken)
    {
        var contest = await FindCombatBoutAsync(id, boutId, cancellationToken);
        if (contest is null) return NotFound();
        if (!User.CanAccessTenant(contest.TenantId)) return Forbid();
        if (contest.ResultMethod is null)
            return Conflict(new { error = "Record the original result before requesting a correction." });
        if (string.IsNullOrWhiteSpace(request.Reason))
            return BadRequest(new { error = "A correction reason is required." });
        if (contest.Events.Any(item => item.EventType == "CombatResultCorrection"
            && item.Status == EventStatus.Pending))
            return Conflict(new { error = "A combat result correction is already pending." });

        var validationError = ValidateCombatResultRequest(
            contest, request.Result, out _);
        if (validationError is not null)
            return BadRequest(new { error = validationError });

        var participantId = request.Result.WinnerId
            ?? contest.Participants.OrderBy(item => item.SortOrder).First().ParticipantId;
        var sequence = contest.Events.Select(item => item.SequenceNumber).DefaultIfEmpty().Max();
        var correction = contest.AddEvent(
            "CombatResultCorrection",
            participantId,
            null,
            JsonSerializer.Serialize(request),
            null,
            "combat-admin",
            ++sequence,
            $"combat-result-correction-{contest.Id}-{Guid.NewGuid():N}");
        _database.Entry(correction).State = EntityState.Added;
        if (contest.Status == ContestStatus.Published)
            contest.MarkCorrected();
        contest.SubmitForApproval();
        await _database.SaveChangesAsync(cancellationToken);
        return Accepted(new
        {
            correctionId = correction.Id,
            status = correction.Status.ToString(),
            contestStatus = contest.Status.ToString()
        });
    }

    [HttpPost("{id:guid}/combat/bouts/{boutId:guid}/result/corrections/{correctionId:guid}/{reviewAction}")]
    [RequirePermission("competitions.manage")]
    public async Task<IActionResult> ReviewCombatResultCorrection(
        Guid id,
        Guid boutId,
        Guid correctionId,
        string reviewAction,
        [FromBody] CombatCorrectionReviewRequest request,
        CancellationToken cancellationToken)
    {
        var contest = await FindCombatBoutAsync(id, boutId, cancellationToken);
        if (contest is null) return NotFound();
        if (!User.CanAccessTenant(contest.TenantId)) return Forbid();
        var correction = contest.Events.SingleOrDefault(item =>
            item.Id == correctionId && item.EventType == "CombatResultCorrection");
        if (correction is null) return NotFound();
        if (correction.Status != EventStatus.Pending)
            return Conflict(new { error = "This correction has already been reviewed." });

        if (reviewAction.Equals("reject", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(request.Reason))
                return BadRequest(new { error = "A rejection reason is required." });
            correction.Reject(request.Reason);
            contest.TransitionTo(ContestStatus.Completed);
        }
        else if (reviewAction.Equals("approve", StringComparison.OrdinalIgnoreCase))
        {
            var correctionRequest = JsonSerializer.Deserialize<CombatResultCorrectionRequest>(
                correction.MetricValue!,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (correctionRequest is null)
                return Conflict(new { error = "The correction payload is invalid." });
            var validationError = ValidateCombatResultRequest(
                contest, correctionRequest.Result, out var method);
            if (validationError is not null)
                return Conflict(new { error = validationError });

            contest.RecordBoutResult(
                correctionRequest.Result.WinnerId,
                method,
                method.EndsWith("Decision", StringComparison.Ordinal) || method == "Draw"
                    ? contest.ScheduledRounds
                    : correctionRequest.Result.Round,
                method.EndsWith("Decision", StringComparison.Ordinal) || method == "Draw"
                    ? null
                    : correctionRequest.Result.TimeInRoundSeconds,
                correctionRequest.Result.Notes);
            correction.Approve();
            contest.MarkCorrected();
        }
        else
        {
            return BadRequest(new { error = "The review action must be approve or reject." });
        }

        await _database.SaveChangesAsync(cancellationToken);
        return Ok(new
        {
            correctionId,
            correctionStatus = correction.Status.ToString(),
            contestStatus = contest.Status.ToString(),
            contest.WinnerId,
            contest.ResultMethod,
            contest.ResultRound,
            contest.ResultTimeSeconds
        });
    }

    private async Task<Contest?> FindCombatBoutAsync(
        Guid competitionId,
        Guid boutId,
        CancellationToken cancellationToken)
    {
        var competition = await _database.Competitions
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == competitionId, cancellationToken);
        if (competition is null) return null;
        return await _database.Contests
            .Include(contest => contest.Participants)
            .Include(contest => contest.Officials)
            .Include(contest => contest.Segments)
            .Include(contest => contest.Events)
            .SingleOrDefaultAsync(contest => contest.Id == boutId
                && contest.TenantId == competition.TenantId
                && contest.SportId == competition.SportId
                && contest.ContestShape == "Bout",
                cancellationToken);
    }

    private static string? ValidateCombatResultRequest(
        Contest contest,
        SubmitCombatBoutResultRequest request,
        out string method)
    {
        method = CanonicalCombatResultMethod(request.ResultMethod) ?? string.Empty;
        if (method.Length == 0)
            return "The result method is not supported by the combat ruleset.";
        if (!contest.AllowsResultMethod(method))
            return "The result method is not enabled for this fight card.";

        var isDecision = method.EndsWith("Decision", StringComparison.Ordinal);
        var isDraw = method == "Draw";
        var noWinner = isDraw || method == "No Contest";
        var participantIds = contest.Participants
            .Select(participant => participant.ParticipantId)
            .ToArray();
        if (noWinner != !request.WinnerId.HasValue
            || request.WinnerId.HasValue && !participantIds.Contains(request.WinnerId.Value))
            return noWinner
                ? "Draws and no contests cannot have a winner."
                : "A valid bout winner is required.";

        if (!isDecision && !isDraw && method != "No Contest"
            && (request.Round is null
                || request.Round < 1
                || request.Round > contest.ScheduledRounds
                || request.TimeInRoundSeconds is null
                || request.TimeInRoundSeconds is < 0 or > 300))
            return "Stoppage results require a valid round and time from 0 through 300 seconds.";

        var judges = contest.Officials
            .Where(official => official.Role == "Judge")
            .Select(official => official.OfficialId)
            .ToHashSet();
        if (isDecision || isDraw)
            return ValidateJudgeScorecards(
                contest, request.WinnerId, method, request.Scorecards, judges);
        return request.Scorecards.Count > 0
            ? "Judge scorecards are only accepted for decisions and draws."
            : null;
    }

    private static string? ValidateJudgeScorecards(
        Contest contest,
        Guid? winnerId,
        string method,
        IReadOnlyList<CombatJudgeScorecardRequest> scorecards,
        IReadOnlySet<Guid> assignedJudges)
    {
        if (scorecards.Count != 3
            || scorecards.Select(card => card.OfficialId).Distinct().Count() != 3
            || scorecards.Any(card => !assignedJudges.Contains(card.OfficialId)))
            return "Exactly three distinct assigned judges must submit scorecards.";
        if (scorecards.Any(card =>
                card.Rounds.Count != contest.ScheduledRounds
                || card.Rounds.Select(round => round.RoundNumber).Distinct().Count()
                    != contest.ScheduledRounds
                || card.Rounds.Any(round =>
                    round.RoundNumber < 1
                    || round.RoundNumber > contest.ScheduledRounds
                    || round.Fighter1Score is < 7 or > 10
                    || round.Fighter2Score is < 7 or > 10
                    || round.Fighter1Score == round.Fighter2Score)))
            return "Each judge must score every scheduled round using valid 10-point-must scores.";

        var fighter1Id = contest.Participants.OrderBy(item => item.SortOrder)
            .First().ParticipantId;
        var comparisons = scorecards.Select(card =>
        {
            var fighter1Total = card.Rounds.Sum(round => round.Fighter1Score);
            var fighter2Total = card.Rounds.Sum(round => round.Fighter2Score);
            return fighter1Total.CompareTo(fighter2Total);
        }).ToArray();
        if (method == "Draw")
            return comparisons.Count(value => value == 0) >= 2
                ? null
                : "A draw requires at least two tied judge scorecards.";

        var winnerDirection = winnerId == fighter1Id ? 1 : -1;
        var winnerCards = comparisons.Count(value => value == winnerDirection);
        var tiedCards = comparisons.Count(value => value == 0);
        return method switch
        {
            "Unanimous Decision" when winnerCards == 3 => null,
            "Split Decision" when winnerCards == 2 && tiedCards == 0 => null,
            "Majority Decision" when winnerCards == 2 && tiedCards == 1 => null,
            _ => $"The judge scorecards do not support a {method.ToLowerInvariant()}."
        };
    }

    private static string CanonicalCardSection(string value) =>
        value.Trim().ToUpperInvariant() switch
        {
            "PRELIMINARYCARD" => "PreliminaryCard",
            "MAINCARD" => "MainCard",
            "COMAINEVENT" => "CoMainEvent",
            "MAINEVENT" => "MainEvent",
            _ => value.Trim()
        };

    private static string CanonicalOfficialRole(string value) =>
        value.Equals("Referee", StringComparison.OrdinalIgnoreCase)
            ? "Referee"
            : value.Equals("Judge", StringComparison.OrdinalIgnoreCase)
                ? "Judge"
                : value.Trim();

    private static string? CanonicalCombatResultMethod(string value) =>
        value.Trim().ToUpperInvariant() switch
        {
            "KO" or "KNOCKOUT" => "Knockout",
            "TKO" or "TECHNICAL KNOCKOUT" or "TKO - DOCTOR STOPPAGE"
                or "TKO - CORNER STOPPAGE" or "TKO - REFEREE STOPPAGE"
                => "Technical Knockout",
            "SUB" or "SUBMISSION" or "SUBMISSION BY STRIKES" => "Submission",
            "UD" or "UNANIMOUS DECISION" or "DECISION - UNANIMOUS"
                => "Unanimous Decision",
            "SD" or "SPLIT DECISION" or "DECISION - SPLIT" => "Split Decision",
            "MD" or "MAJORITY DECISION" or "DECISION - MAJORITY"
                => "Majority Decision",
            "DQ" or "DISQUALIFICATION" => "Disqualification",
            "DRAW" or "DRAW - SPLIT" => "Draw",
            "NC" or "NO CONTEST" => "No Contest",
            _ => null
        };

    private static string[] ParseCombatResultMethods(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.EnumerateArray()
            .Select(item => item.GetProperty("name").GetString())
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => CanonicalCombatResultMethod(name!))
            .Where(name => name is not null)
            .Select(name => name!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static HashSet<string> ParseCombatWeightClasses(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.GetProperty("weightClasses")
            .EnumerateArray()
            .Select(item => item.GetProperty("name").GetString())
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    [HttpGet("{id:guid}/athletics/meet")]
    [RequirePermission("competitions.manage")]
    public async Task<IActionResult> GetAthleticsMeetForAdministration(
        Guid id,
        CancellationToken cancellationToken)
    {
        var competition = await _database.Competitions.AsNoTracking()
            .Include(item => item.Editions)
            .ThenInclude(edition => edition.Stages)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (competition is null) return NotFound();
        if (!User.CanAccessTenant(competition.TenantId)) return Forbid();
        var stageIds = competition.Editions.SelectMany(edition => edition.Stages)
            .Where(stage => stage.Type == StageType.Heat)
            .Select(stage => stage.Id)
            .ToArray();
        var contests = await _database.Contests.AsNoTracking()
            .Include(contest => contest.Participants)
            .Include(contest => contest.Events)
            .Where(contest => contest.TenantId == competition.TenantId
                && contest.StageId.HasValue
                && stageIds.Contains(contest.StageId.Value)
                && (contest.ContestShape == "Heat"
                    || contest.ContestShape == "AthleticsSemifinal"
                    || contest.ContestShape == "AthleticsFinal"
                    || contest.ContestShape == "AthleticsFieldEvent"))
            .OrderBy(contest => contest.SortOrder)
            .ToArrayAsync(cancellationToken);
        if (contests.Length == 0)
            return NotFound(new { error = "Athletics meet not found." });
        var participantIds = contests.SelectMany(contest => contest.Participants)
            .Select(participant => participant.ParticipantId).Distinct().ToArray();
        var names = await _database.Participants.AsNoTracking()
            .Where(participant => participant.TenantId == competition.TenantId
                && participantIds.Contains(participant.Id))
            .ToDictionaryAsync(participant => participant.Id, participant => participant.Name,
                cancellationToken);
        var contestIds = contests.Select(contest => contest.Id).ToArray();
        var times = await _database.LiveScores.AsNoTracking()
            .Where(score => contestIds.Contains(score.ContestId)
                && score.MetricName == "finish_time_ms")
            .ToDictionaryAsync(
                score => (score.ContestId, score.ParticipantId),
                score => (int)score.Value,
                cancellationToken);

        var races = contests.Where(contest => contest.ContestShape != "AthleticsFieldEvent")
            .Select(race => new
            {
                race.Id,
                race.Name,
                race.ContestShape,
                status = race.Status.ToString(),
                race.QualifyByPlace,
                race.QualifyByTime,
                qualificationApplied = race.QualificationAppliedAt.HasValue,
                participants = race.Participants.OrderBy(participant => participant.SortOrder)
                    .Select(participant => new
                    {
                        id = participant.ParticipantId,
                        name = names.GetValueOrDefault(participant.ParticipantId),
                        lane = participant.SortOrder,
                        status = participant.Status.ToString(),
                        participant.QualificationStatus,
                        participant.QualificationReason,
                        timeMilliseconds = times.GetValueOrDefault(
                            (race.Id, participant.ParticipantId), 0) is var time && time > 0
                                ? time
                                : (int?)null
                    })
            });
        var fieldEvents = contests.Where(contest =>
                contest.ContestShape == "AthleticsFieldEvent")
            .Select(fieldEvent =>
            {
                var attempts = fieldEvent.Events
                    .Where(item => item.EventType == "FieldAttempt"
                        && item.Status == EventStatus.Approved)
                    .Select(item =>
                    {
                        using var payload = JsonDocument.Parse(item.MetricValue!);
                        var measurement = payload.RootElement.TryGetProperty(
                            "measurementMeters", out var camelMeasurement)
                                ? camelMeasurement
                                : payload.RootElement.GetProperty("MeasurementMeters");
                        return new
                        {
                            item.ParticipantId,
                            attemptNumber = payload.RootElement.GetProperty("attemptNumber").GetInt32(),
                            measurementMeters = measurement.ValueKind == JsonValueKind.Null
                                    ? (decimal?)null
                                    : measurement.GetDecimal()
                        };
                    }).ToArray();
                var disqualifications = fieldEvent.Events
                    .Where(item => item.EventType == "FieldDisqualification"
                        && item.Status == EventStatus.Approved)
                    .ToDictionary(item => item.ParticipantId, item => item.MetricValue);
                return new
                {
                    fieldEvent.Id,
                    fieldEvent.Name,
                    status = fieldEvent.Status.ToString(),
                    metricType = fieldEvent.AthleticsMetricType,
                    maxAttempts = fieldEvent.AthleticsMaxAttempts,
                    recordThresholdMeters = fieldEvent.AthleticsRecordThreshold,
                    participants = fieldEvent.Participants
                        .OrderBy(participant => participant.SortOrder)
                        .Select(participant =>
                        {
                            var marks = attempts
                                .Where(attempt => attempt.ParticipantId
                                    == participant.ParticipantId)
                                .OrderBy(attempt => attempt.attemptNumber)
                                .Select(attempt => attempt.measurementMeters)
                                .ToArray();
                            var best = marks.Where(mark => mark.HasValue)
                                .Select(mark => mark!.Value).DefaultIfEmpty().Max();
                            return new
                            {
                                id = participant.ParticipantId,
                                name = names.GetValueOrDefault(participant.ParticipantId),
                                attempts = marks,
                                bestMeasurement = best > 0 ? best : (decimal?)null,
                                disqualificationReason = disqualifications.GetValueOrDefault(
                                    participant.ParticipantId),
                                isRecord = best > 0 && fieldEvent.AthleticsRecordThreshold.HasValue
                                    && best >= fieldEvent.AthleticsRecordThreshold.Value
                            };
                        })
                };
            });

        return Ok(new
        {
            competition.Id,
            competition.Name,
            status = competition.Status.ToString(),
            competition.RulesetVersionId,
            races,
            fieldEvents
        });
    }

    [HttpPost("{id:guid}/athletics/field-events")]
    [RequirePermission("competitions.manage")]
    public async Task<IActionResult> CreateAthleticsFieldEvent(
        Guid id,
        [FromBody] CreateAthleticsFieldEventRequest request,
        CancellationToken cancellationToken)
    {
        var competition = await _database.Competitions
            .Include(item => item.Editions)
            .ThenInclude(edition => edition.Stages)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (competition is null) return NotFound();
        if (!User.CanAccessTenant(competition.TenantId)) return Forbid();
        if (competition.SportId != Guid.Parse(
                "00000000-0000-0000-0000-000000000103"))
            return BadRequest(new { error = "The competition is not configured for athletics." });
        if (!new[] { "Distance", "Height" }.Contains(
                request.MetricType, StringComparer.OrdinalIgnoreCase)
            || request.MaxAttempts is < 1 or > 6
            || request.ParticipantIds.Count < 2
            || request.ParticipantIds.Distinct().Count() != request.ParticipantIds.Count
            || request.RecordThresholdMeters is <= 0)
            return BadRequest(new
            {
                error = "Use a Distance or Height metric, two or more distinct athletes, one through six attempts, and a positive record threshold."
            });

        var athleticsSport = await _database.Sports
            .Include(sport => sport.Versions)
            .SingleOrDefaultAsync(sport => sport.Code == "ATHLETICS", cancellationToken);
        var ruleset = competition.RulesetVersionId.HasValue
            ? athleticsSport?.Versions.SingleOrDefault(version =>
                version.Id == competition.RulesetVersionId.Value && version.IsPublished)
            : athleticsSport?.Versions.Where(version => version.IsPublished)
                .OrderByDescending(version => version.VersionNumber).FirstOrDefault();
        if (ruleset is null)
            return Conflict(new { error = "Configure and publish the athletics ruleset before creating a field event." });

        var registeredIds = await _database.Registrations
            .Where(registration => registration.CompetitionId == id
                && (registration.Status == RegistrationStatus.Approved
                    || registration.Status == RegistrationStatus.Confirmed)
                && request.ParticipantIds.Contains(registration.ParticipantId))
            .Select(registration => registration.ParticipantId)
            .Distinct()
            .ToArrayAsync(cancellationToken);
        var athletes = await _database.Participants
            .Where(participant => participant.TenantId == competition.TenantId
                && registeredIds.Contains(participant.Id)
                && participant.Type == ParticipantType.Athlete)
            .ToDictionaryAsync(participant => participant.Id, cancellationToken);
        if (request.ParticipantIds.Any(participantId => !athletes.ContainsKey(participantId)))
            return BadRequest(new { error = "Every field-event athlete must have an approved registration." });

        var startTimeUtc = request.StartTimeUtc.Kind == DateTimeKind.Utc
            ? request.StartTimeUtc
            : request.StartTimeUtc.ToUniversalTime();
        var edition = competition.AddEdition(
            request.EditionName,
            startTimeUtc.Date,
            startTimeUtc.Date.AddDays(1));
        var stage = edition.AddStage(request.StageName, StageType.Heat, 1);
        competition.AssignRulesetVersion(ruleset.Id);
        _database.Entry(edition).State = EntityState.Added;
        _database.Entry(stage).State = EntityState.Added;

        var fieldEvent = new Contest(
            Guid.NewGuid(), competition.TenantId, competition.SportId, "AthleticsFieldEvent");
        fieldEvent.AssignToStage(stage.Id, 1);
        fieldEvent.AssignRulesetVersion(ruleset.Id);
        fieldEvent.SetName(request.EventName);
        fieldEvent.ConfigureAthleticsFieldEvent(
            request.EventName,
            request.MetricType,
            request.MaxAttempts,
            request.RecordThresholdMeters);
        for (var index = 0; index < request.ParticipantIds.Count; index++)
        {
            var participantId = request.ParticipantIds[index];
            fieldEvent.AddParticipant(
                participantId,
                ParticipantType.Athlete.ToString(),
                index + 1);
        }
        fieldEvent.Schedule(request.Venue, startTimeUtc);
        _database.Contests.Add(fieldEvent);
        await _database.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            competitionId = id,
            fieldEventId = fieldEvent.Id,
            rulesetVersionId = ruleset.Id,
            fieldEvent.Name,
            fieldEvent.AthleticsMetricType,
            fieldEvent.AthleticsMaxAttempts,
            fieldEvent.AthleticsRecordThreshold,
            participants = request.ParticipantIds.Select(participantId => new
            {
                id = participantId,
                name = athletes[participantId].Name
            })
        });
    }

    [HttpPost("{id:guid}/athletics/field-events/{fieldEventId:guid}/results")]
    [RequirePermission("competitions.manage")]
    public async Task<IActionResult> SubmitAthleticsFieldEventResults(
        Guid id,
        Guid fieldEventId,
        [FromBody] SubmitAthleticsFieldEventResultsRequest request,
        CancellationToken cancellationToken)
    {
        var competition = await _database.Competitions.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (competition is null) return NotFound();
        if (!User.CanAccessTenant(competition.TenantId)) return Forbid();
        var fieldEvent = await _database.Contests
            .Include(contest => contest.Participants)
            .Include(contest => contest.Events)
            .SingleOrDefaultAsync(contest => contest.Id == fieldEventId
                && contest.TenantId == competition.TenantId
                && contest.SportId == competition.SportId
                && contest.ContestShape == "AthleticsFieldEvent",
                cancellationToken);
        if (fieldEvent is null) return NotFound();
        if (fieldEvent.Status == ContestStatus.Completed)
            return Conflict(new { error = "Field-event results have already been recorded." });

        var participantIds = fieldEvent.Participants
            .Select(participant => participant.ParticipantId).ToArray();
        if (request.Results.Count != participantIds.Length
            || request.Results.Select(result => result.ParticipantId).Distinct().Count()
                != participantIds.Length
            || request.Results.Any(result => !participantIds.Contains(result.ParticipantId)))
            return BadRequest(new { error = "Submit one result for every field-event athlete." });
        var maxAttempts = fieldEvent.AthleticsMaxAttempts ?? 6;
        if (request.Results.Any(result =>
                result.Disqualified
                    ? string.IsNullOrWhiteSpace(result.DisqualificationReason)
                        || result.Attempts.Count > 0
                    : result.Attempts.Count != maxAttempts
                        || result.Attempts.All(attempt => !attempt.MeasurementMeters.HasValue)
                        || result.Attempts.Any(attempt => attempt.MeasurementMeters is <= 0)))
            return BadRequest(new
            {
                error = "Each eligible athlete needs the configured attempts with a positive measurement or foul; disqualifications require a reason and no attempts."
            });
        if (request.Results.All(result => result.Disqualified))
            return BadRequest(new { error = "At least one athlete must record a valid mark." });

        var sequence = fieldEvent.Events.Select(item => item.SequenceNumber).DefaultIfEmpty().Max();
        foreach (var result in request.Results)
        {
            if (result.Disqualified)
            {
                var disqualification = fieldEvent.AddEvent(
                    "FieldDisqualification", result.ParticipantId, null,
                    result.DisqualificationReason, null, "athletics-api", ++sequence,
                    $"field-dq-{fieldEvent.Id}-{result.ParticipantId}");
                disqualification.Approve();
                _database.Entry(disqualification).State = EntityState.Added;
                continue;
            }

            for (var index = 0; index < result.Attempts.Count; index++)
            {
                var attempt = fieldEvent.AddEvent(
                    "FieldAttempt", result.ParticipantId, null,
                    JsonSerializer.Serialize(new
                    {
                        attemptNumber = index + 1,
                        measurementMeters = result.Attempts[index].MeasurementMeters,
                        foul = !result.Attempts[index].MeasurementMeters.HasValue
                    }),
                    null, "athletics-api", ++sequence,
                    $"field-attempt-{fieldEvent.Id}-{result.ParticipantId}-{index + 1}");
                attempt.Approve();
                _database.Entry(attempt).State = EntityState.Added;
            }
        }

        var eligible = request.Results
            .Where(result => !result.Disqualified)
            .Select(result => new
            {
                result.ParticipantId,
                BestMeasurement = result.Attempts
                    .Where(attempt => attempt.MeasurementMeters.HasValue)
                    .Max(attempt => attempt.MeasurementMeters!.Value)
            })
            .OrderByDescending(result => result.BestMeasurement)
            .ThenBy(result => result.ParticipantId)
            .ToArray();
        var ranked = eligible.Select((result, index) => new
        {
            result.ParticipantId,
            result.BestMeasurement,
            Position = index == 0 || result.BestMeasurement != eligible[index - 1].BestMeasurement
                ? index + 1
                : 0
        }).ToArray();
        var lastPosition = 0;
        var rankings = ranked.Select(result =>
        {
            if (result.Position > 0) lastPosition = result.Position;
            return new
            {
                result.ParticipantId,
                result.BestMeasurement,
                position = lastPosition,
                medal = lastPosition switch { 1 => "Gold", 2 => "Silver", 3 => "Bronze", _ => null },
                isRecord = fieldEvent.AthleticsRecordThreshold.HasValue
                    && result.BestMeasurement >= fieldEvent.AthleticsRecordThreshold.Value
            };
        }).ToArray();

        fieldEvent.SetWinner(rankings[0].ParticipantId);
        fieldEvent.Start();
        fieldEvent.Complete();
        await _database.SaveChangesAsync(cancellationToken);
        return Ok(new
        {
            fieldEventId,
            metricType = fieldEvent.AthleticsMetricType,
            rankingDirection = "HigherIsBetter",
            unit = "meters",
            rankings,
            disqualifications = request.Results.Where(result => result.Disqualified)
                .Select(result => new
                {
                    result.ParticipantId,
                    reason = result.DisqualificationReason
                })
        });
    }

    [HttpPost("{id:guid}/athletics/meet")]
    [RequirePermission("competitions.manage")]
    public async Task<IActionResult> GenerateAthleticsMeet(
        Guid id,
        [FromBody] GenerateAthleticsMeetRequest request,
        CancellationToken cancellationToken)
    {
        var competition = await _database.Competitions
            .Include(item => item.Editions)
            .ThenInclude(edition => edition.Stages)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (competition is null) return NotFound();
        if (!User.CanAccessTenant(competition.TenantId)) return Forbid();
        if (competition.SportId != Guid.Parse(
                "00000000-0000-0000-0000-000000000103"))
            return BadRequest(new { error = "The competition is not configured for athletics." });
        var athleticsSport = await _database.Sports
            .Include(sport => sport.Versions)
            .SingleOrDefaultAsync(sport => sport.Code == "ATHLETICS", cancellationToken);
        var athleticsRuleset = competition.RulesetVersionId.HasValue
            ? athleticsSport?.Versions.SingleOrDefault(version =>
                version.Id == competition.RulesetVersionId.Value && version.IsPublished)
            : athleticsSport?.Versions
                .Where(version => version.IsPublished)
                .OrderByDescending(version => version.VersionNumber)
                .FirstOrDefault();
        if (athleticsRuleset is null)
            return Conflict(new
            {
                error = competition.RulesetVersionId.HasValue
                    ? "The competition's athletics ruleset is not published."
                    : "Configure and publish the athletics ruleset before generating a meet."
            });
        if (request.DistanceMeters != 100
            || request.HeatCount < 1
            || request.ExpectedParticipants < 4
            || request.LanesPerHeat < 2
            || request.ExpectedParticipants > request.HeatCount * request.LanesPerHeat
            || request.QualifyByPlace < 1
            || request.QualifyByTime < 0
            || request.SemifinalCount < 0
            || request.SemifinalCount > request.HeatCount
            || request.SemifinalQualifyByPlace < 1
            || request.SemifinalQualifyByTime < 0
            || !new[] { "Standard", "Seeded", "Random" }.Contains(
                request.LaneAssignment,
                StringComparer.OrdinalIgnoreCase))
        {
            return BadRequest(new
            {
                error = "Configure valid 100m heats, lanes, qualification counts, and a Standard, Seeded, or Random lane strategy."
            });
        }
        var initialQualificationSlots =
            (request.QualifyByPlace * request.HeatCount) + request.QualifyByTime;
        var finalQualificationSlots = request.SemifinalCount > 0
            ? (request.SemifinalQualifyByPlace * request.SemifinalCount)
                + request.SemifinalQualifyByTime
            : initialQualificationSlots;
        if (initialQualificationSlots > request.ExpectedParticipants
            || request.SemifinalCount > 0
                && (initialQualificationSlots > request.SemifinalCount * request.LanesPerHeat
                    || finalQualificationSlots > initialQualificationSlots)
            || finalQualificationSlots < 2
            || finalQualificationSlots > request.LanesPerHeat)
        {
            return BadRequest(new { error = "The qualification plan does not fit the configured rounds and lanes." });
        }
        var advancePlaceTies = request.AdvancePlaceTies ?? request.AdvanceTies;
        var advanceTimeTies = request.AdvanceTimeTies ?? request.AdvanceTies;

        var existingStageIds = competition.Editions
            .SelectMany(edition => edition.Stages)
            .Select(stage => stage.Id)
            .ToArray();
        if (await _database.Contests.AnyAsync(contest =>
                contest.StageId.HasValue
                && existingStageIds.Contains(contest.StageId.Value)
                && (contest.ContestShape == "Heat"
                    || contest.ContestShape == "AthleticsSemifinal"
                    || contest.ContestShape == "AthleticsFinal"),
                cancellationToken))
            return Conflict(new { error = "An athletics meet already exists for this competition." });

        var registeredIds = await _database.Registrations
            .Where(registration => registration.CompetitionId == id
                && (registration.Status == RegistrationStatus.Approved
                    || registration.Status == RegistrationStatus.Confirmed))
            .Select(registration => registration.ParticipantId)
            .Distinct()
            .ToArrayAsync(cancellationToken);
        var athletes = await _database.Participants
            .Where(participant => participant.TenantId == competition.TenantId
                && registeredIds.Contains(participant.Id)
                && participant.Type == ParticipantType.Athlete)
            .OrderBy(participant => participant.Id)
            .ToArrayAsync(cancellationToken);
        if (athletes.Length != request.ExpectedParticipants)
            return BadRequest(new
            {
                error = $"Exactly {request.ExpectedParticipants} approved athletes are required."
            });

        var startTimeUtc = request.StartTimeUtc.Kind == DateTimeKind.Utc
            ? request.StartTimeUtc
            : request.StartTimeUtc.ToUniversalTime();
        var edition = competition.AddEdition(
            request.EditionName,
            startTimeUtc.Date,
            startTimeUtc.Date.AddDays(1));
        var stage = edition.AddStage(request.StageName, StageType.Heat, 1);
        competition.AssignRulesetVersion(athleticsRuleset.Id);
        _database.Entry(edition).State = EntityState.Added;
        _database.Entry(stage).State = EntityState.Added;
        var participantsPerHeat = (int)Math.Ceiling(
            athletes.Length / (double)request.HeatCount);
        var generated = _heatQualificationEngine.GenerateContests(
                stage,
                athletes.Select((athlete, index) => new ParticipantInfo(
                    athlete.Id,
                    athlete.Type.ToString(),
                    index + 1)),
                new HeatQualificationOptions
                {
                    SportId = competition.SportId,
                    ParticipantsPerHeat = participantsPerHeat,
                    LanesPerHeat = request.LanesPerHeat,
                    RoundNumber = 1,
                    QualifyByPlace = request.QualifyByPlace,
                    TotalQualificationSlots =
                        initialQualificationSlots,
                    UseSeeding = true,
                    UseSerpentineSeeding = true,
                    LaneAssignment = request.LaneAssignment,
                    AdvanceTies = request.AdvanceTies,
                    AdvancePlaceTies = advancePlaceTies,
                    AdvanceTimeTies = advanceTimeTies
                })
            .OrderBy(item => item.HeatNumber)
            .ToArray();
        if (generated.Length != request.HeatCount)
            return StatusCode(500, new { error = "The heat engine did not create the configured heats." });

        foreach (var generatedHeat in generated)
        {
            generatedHeat.Heat.SetName(
                $"{request.EventName} — Heat {generatedHeat.HeatNumber}");
            generatedHeat.Heat.AssignRulesetVersion(athleticsRuleset.Id);
            generatedHeat.Heat.ConfigureAthleticsRace(
                request.EventName,
                request.DistanceMeters,
                request.QualifyByPlace,
                request.QualifyByTime,
                request.AdvanceTies,
                request.LanesPerHeat,
                request.LaneAssignment,
                request.SemifinalCount,
                request.SemifinalQualifyByPlace,
                request.SemifinalQualifyByTime,
                advancePlaceTies,
                advanceTimeTies);
            generatedHeat.Heat.Schedule(
                request.Venue,
                startTimeUtc.AddMinutes(
                    (generatedHeat.HeatNumber - 1) * request.MinutesBetweenHeats));
        }
        _database.Contests.AddRange(generated.Select(item => item.Heat));
        await _database.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            competitionId = id,
            editionId = edition.Id,
            stageId = stage.Id,
            rulesetVersionId = athleticsRuleset.Id,
            eventName = request.EventName,
            heatCount = generated.Length,
            qualificationSlots =
                initialQualificationSlots,
            request.SemifinalCount,
            finalQualificationSlots,
            request.LaneAssignment,
            advancePlaceTies,
            advanceTimeTies,
            heats = generated.Select(item => new
            {
                item.Heat.Id,
                item.Heat.Name,
                item.HeatNumber,
                lanes = item.Heat.Participants
                    .OrderBy(participant => participant.SortOrder)
                    .Select(participant => new
                    {
                        participant.ParticipantId,
                        lane = participant.SortOrder
                    })
            })
        });
    }

    [HttpPost("{id:guid}/athletics/races/{raceId:guid}/results")]
    [RequirePermission("competitions.manage")]
    public async Task<IActionResult> SubmitAthleticsRaceResults(
        Guid id,
        Guid raceId,
        [FromBody] SubmitAthleticsRaceResultsRequest request,
        CancellationToken cancellationToken)
    {
        var contest = await FindAthleticsRaceAsync(id, raceId, cancellationToken);
        if (contest is null) return NotFound();
        if (!User.CanAccessTenant(contest.TenantId)) return Forbid();
        if (contest.Status == ContestStatus.Completed)
            return Conflict(new { error = "Results have already been submitted for this race." });
        var entries = request.Results.ToArray();
        var participantIds = contest.Participants
            .Select(participant => participant.ParticipantId)
            .ToHashSet();
        if (entries.Length != participantIds.Count
            || entries.Select(entry => entry.ParticipantId).Distinct().Count()
                != participantIds.Count
            || entries.Any(entry => !participantIds.Contains(entry.ParticipantId)
                || (!entry.Disqualified
                    && (entry.TimeMilliseconds is < 8_000 or > 60_000))
                || (entry.Disqualified
                    && string.IsNullOrWhiteSpace(entry.DisqualificationReason))))
        {
            return BadRequest(new
            {
                error = "Submit one valid finish time or explained disqualification for every race participant."
            });
        }
        if (await _database.LiveScores.AnyAsync(score =>
                score.ContestId == contest.Id
                && score.MetricName == "finish_time_ms",
                cancellationToken))
            return Conflict(new { error = "Race times have already been recorded." });

        var segmentId = contest.Segments.Single(segment =>
            segment.SegmentType == "Race").Id;
        var sequence = 0;
        foreach (var entry in entries)
        {
            var participant = contest.Participants.Single(item =>
                item.ParticipantId == entry.ParticipantId);
            if (entry.Disqualified)
            {
                participant.Disqualify();
                participant.SetQualification(
                    "Disqualified",
                    entry.DisqualificationReason!.Trim());
                continue;
            }
            var score = new LiveScore(
                Guid.NewGuid(),
                contest.Id,
                Guid.NewGuid(),
                entry.ParticipantId,
                "finish_time_ms",
                entry.TimeMilliseconds!.Value,
                segmentId);
            score.SetSequence(++sequence);
            score.SetDisplayValue(FormatRaceTime(entry.TimeMilliseconds.Value));
            score.SetScorer(User.Identity?.Name, null);
            _database.LiveScores.Add(score);
        }

        if (contest.Status is ContestStatus.Scheduled or ContestStatus.Ready)
            contest.Start();
        contest.Complete();
        if (contest.ContestShape == "AthleticsFinal")
        {
            var winner = entries
                .Where(entry => !entry.Disqualified)
                .OrderBy(entry => entry.TimeMilliseconds)
                .ThenBy(entry => contest.Participants.Single(participant =>
                    participant.ParticipantId == entry.ParticipantId).SortOrder)
                .FirstOrDefault();
            if (winner is not null) contest.SetWinner(winner.ParticipantId);
        }
        await _database.SaveChangesAsync(cancellationToken);

        var ranked = entries.Where(entry => !entry.Disqualified)
            .OrderBy(entry => entry.TimeMilliseconds)
            .ThenBy(entry => contest.Participants.Single(participant =>
                participant.ParticipantId == entry.ParticipantId).SortOrder)
            .ToArray();
        return Ok(new
        {
            contestId = contest.Id,
            status = contest.Status.ToString(),
            results = ranked.Select((entry, index) => new
            {
                entry.ParticipantId,
                entry.TimeMilliseconds,
                place = 1 + ranked.Take(index).Count(previous =>
                    previous.TimeMilliseconds < entry.TimeMilliseconds),
                isTied = ranked.Count(other =>
                    other.TimeMilliseconds == entry.TimeMilliseconds) > 1
            }),
            disqualifiedParticipantIds = entries
                .Where(entry => entry.Disqualified)
                .Select(entry => entry.ParticipantId)
        });
    }

    [HttpPost("{id:guid}/athletics/qualify")]
    [RequirePermission("competitions.manage")]
    public async Task<IActionResult> ApplyAthleticsQualification(
        Guid id,
        CancellationToken cancellationToken)
    {
        var competition = await _database.Competitions
            .AsNoTracking()
            .Include(item => item.Editions)
            .ThenInclude(edition => edition.Stages)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (competition is null) return NotFound();
        if (!User.CanAccessTenant(competition.TenantId)) return Forbid();
        var stageIds = competition.Editions.SelectMany(edition => edition.Stages)
            .Where(stage => stage.Type == StageType.Heat)
            .Select(stage => stage.Id)
            .ToArray();
        var roundContests = await _database.Contests
            .Include(contest => contest.Participants)
            .Where(contest => contest.TenantId == competition.TenantId
                && contest.StageId.HasValue
                && stageIds.Contains(contest.StageId.Value)
                && (contest.ContestShape == "Heat"
                    || contest.ContestShape == "AthleticsSemifinal"))
            .OrderBy(contest => contest.SortOrder)
            .ToArrayAsync(cancellationToken);
        var sourceShape = roundContests.Any(contest =>
            contest.ContestShape == "AthleticsSemifinal")
                ? "AthleticsSemifinal"
                : "Heat";
        var heats = roundContests
            .Where(contest => contest.ContestShape == sourceShape)
            .ToArray();
        if (heats.Length == 0)
            return NotFound(new { error = "No athletics qualification round was found." });
        if (heats.Any(heat => heat.Status != ContestStatus.Completed))
            return Conflict(new { error = "Every race in the current round must be completed before qualification." });
        var firstHeat = heats[0];
        var targetShape = sourceShape == "Heat" && firstHeat.NextRoundHeatCount > 0
            ? "AthleticsSemifinal"
            : "AthleticsFinal";
        if (heats.Any(heat => heat.QualificationAppliedAt.HasValue)
            || await _database.Contests.AnyAsync(contest =>
                contest.StageId.HasValue
                && stageIds.Contains(contest.StageId.Value)
                && contest.ContestShape == targetShape,
                cancellationToken))
            return Conflict(new { error = "Qualification has already been applied." });

        var heatIds = heats.Select(heat => heat.Id).ToArray();
        var scores = await _database.LiveScores
            .AsNoTracking()
            .Where(score => heatIds.Contains(score.ContestId)
                && score.MetricName == "finish_time_ms")
            .ToArrayAsync(cancellationToken);
        var heatResults = heats.Select(heat =>
        {
            var ranked = heat.Participants
                .Where(participant =>
                    participant.Status != ContestParticipantStatus.Disqualified)
                .Select(participant => new
                {
                    Participant = participant,
                    Score = scores.Single(score =>
                        score.ContestId == heat.Id
                        && score.ParticipantId == participant.ParticipantId)
                })
                .OrderBy(item => item.Score.Value)
                .ThenBy(item => item.Participant.SortOrder)
                .ToArray();
            return new HeatResult(
                heat.Id,
                heat.SortOrder,
                heat.Participants.Select(participant =>
                {
                    var rankedIndex = Array.FindIndex(
                        ranked,
                        item => item.Participant.ParticipantId
                            == participant.ParticipantId);
                    var time = rankedIndex >= 0
                        ? TimeSpan.FromMilliseconds(
                            decimal.ToDouble(ranked[rankedIndex].Score.Value))
                        : (TimeSpan?)null;
                    var place = rankedIndex >= 0
                        ? 1 + ranked.Take(rankedIndex).Count(previous =>
                            previous.Score.Value < ranked[rankedIndex].Score.Value)
                        : (int?)null;
                    return new HeatParticipantResult(
                        participant.ParticipantId,
                        participant.SortOrder,
                        place,
                        time,
                        false,
                        null,
                        participant.Status == ContestParticipantStatus.Disqualified);
                }));
        }).ToArray();
        var qualification = _heatQualificationEngine.CalculateQualification(
            heatResults,
            new HeatQualificationOptions
            {
                QualifyByPlace = firstHeat.QualifyByPlace ?? 3,
                TotalQualificationSlots =
                    ((firstHeat.QualifyByPlace ?? 3) * heats.Length)
                    + (firstHeat.QualifyByTime ?? 2),
                AdvanceTies = firstHeat.AdvanceQualificationTies ?? true,
                AdvancePlaceTies = firstHeat.AdvancePlaceTies,
                AdvanceTimeTies = firstHeat.AdvanceTimeTies
            });
        var qualified = qualification.QualifiedParticipants.ToArray();
        var qualifiedById = qualified.ToDictionary(item => item.ParticipantId);
        foreach (var heat in heats)
        {
            foreach (var participant in heat.Participants)
            {
                if (participant.Status == ContestParticipantStatus.Disqualified)
                    continue;
                if (qualifiedById.TryGetValue(participant.ParticipantId, out var entry))
                    participant.SetQualification("Qualified", entry.QualificationReason);
                else
                    participant.SetQualification("Eliminated", "Did not qualify");
            }
            heat.MarkQualificationApplied();
        }

        var stage = competition.Editions.SelectMany(edition => edition.Stages)
            .Single(item => item.Id == firstHeat.StageId);
        var orderedQualified = qualified
            .OrderBy(item => item.Time)
            .ThenBy(item => item.ParticipantId)
            .ToArray();
        var lanesPerHeat = firstHeat.AthleticsLanesPerHeat ?? 8;
        var laneAssignment = firstHeat.AthleticsLaneAssignment ?? "Seeded";
        var nextRoundHeatCount = targetShape == "AthleticsSemifinal"
            ? firstHeat.NextRoundHeatCount!.Value
            : 1;
        var nextRoundLanes = Math.Max(
            lanesPerHeat,
            (int)Math.Ceiling(orderedQualified.Length / (double)nextRoundHeatCount));
        var generatedNextRound = _heatQualificationEngine.GenerateContests(
                stage,
                orderedQualified.Select((athlete, index) => new ParticipantInfo(
                    athlete.ParticipantId,
                    ParticipantType.Athlete.ToString(),
                    index + 1)),
                new HeatQualificationOptions
                {
                    SportId = competition.SportId,
                    ParticipantsPerHeat = (int)Math.Ceiling(
                        orderedQualified.Length / (double)nextRoundHeatCount),
                    LanesPerHeat = nextRoundLanes,
                    RoundNumber = sourceShape == "Heat" ? 2 : 3,
                    QualifyByPlace = targetShape == "AthleticsSemifinal"
                        ? firstHeat.NextRoundQualifyByPlace ?? 0
                        : 0,
                    TotalQualificationSlots = targetShape == "AthleticsSemifinal"
                        ? ((firstHeat.NextRoundQualifyByPlace ?? 0) * nextRoundHeatCount)
                            + (firstHeat.NextRoundQualifyByTime ?? 0)
                        : 0,
                    UseSeeding = true,
                    UseSerpentineSeeding = true,
                    LaneAssignment = laneAssignment,
                    AdvanceTies = firstHeat.AdvanceQualificationTies ?? true,
                    AdvancePlaceTies = firstHeat.AdvancePlaceTies,
                    AdvanceTimeTies = firstHeat.AdvanceTimeTies,
                    ContestShape = targetShape,
                    ContestNamePrefix = targetShape == "AthleticsSemifinal"
                        ? $"{firstHeat.AthleticsEventName} — Semifinal"
                        : $"{firstHeat.AthleticsEventName} — Final",
                    ContestSortOrderOffset = roundContests.Max(contest => contest.SortOrder)
                })
            .OrderBy(item => item.HeatNumber)
            .ToArray();
        foreach (var generatedRace in generatedNextRound)
        {
            if (competition.RulesetVersionId.HasValue)
                generatedRace.Heat.AssignRulesetVersion(
                    competition.RulesetVersionId.Value);
            generatedRace.Heat.ConfigureAthleticsRace(
                firstHeat.AthleticsEventName ?? "Men's 100m",
                firstHeat.DistanceMeters ?? 100,
                targetShape == "AthleticsSemifinal"
                    ? firstHeat.NextRoundQualifyByPlace ?? 0
                    : 0,
                targetShape == "AthleticsSemifinal"
                    ? firstHeat.NextRoundQualifyByTime ?? 0
                    : 0,
                firstHeat.AdvanceQualificationTies ?? true,
                nextRoundLanes,
                laneAssignment,
                0,
                0,
                0,
                firstHeat.AdvancePlaceTies,
                firstHeat.AdvanceTimeTies);
            generatedRace.Heat.Schedule(
                firstHeat.Venue ?? "Main Track",
                (heats.Max(heat => heat.StartTime) ?? DateTime.UtcNow)
                    .AddHours(targetShape == "AthleticsSemifinal" ? 1 : 2)
                    .AddMinutes((generatedRace.HeatNumber - 1) * 15));
        }
        _database.Contests.AddRange(generatedNextRound.Select(item => item.Heat));
        await _database.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            nextRound = targetShape == "AthleticsSemifinal" ? "Semifinal" : "Final",
            finalContestId = targetShape == "AthleticsFinal"
                ? generatedNextRound.Single().Heat.Id
                : (Guid?)null,
            nextRoundContestIds = generatedNextRound.Select(item => item.Heat.Id),
            sourceRound = sourceShape == "Heat" ? "Heat" : "Semifinal",
            requestedSlots = qualification.TotalQualificationSlots,
            qualifiedCount = qualified.Length,
            qualifiers = qualified.Select(item => new
            {
                item.ParticipantId,
                timeMilliseconds = item.Time?.TotalMilliseconds,
                item.QualificationReason,
                item.HeatNumber
            }),
            eliminated = qualification.NotQualifiedParticipants.Select(item => new
            {
                item.ParticipantId,
                timeMilliseconds = item.Time?.TotalMilliseconds,
                qualificationReason = heats
                    .SelectMany(heat => heat.Participants)
                    .First(participant => participant.ParticipantId == item.ParticipantId)
                    .QualificationReason ?? item.QualificationReason,
                item.HeatNumber
            })
        });
    }

    private async Task<Contest?> FindAthleticsRaceAsync(
        Guid competitionId,
        Guid raceId,
        CancellationToken cancellationToken)
    {
        var competition = await _database.Competitions.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == competitionId, cancellationToken);
        if (competition is null) return null;
        return await _database.Contests
            .Include(contest => contest.Participants)
            .Include(contest => contest.Segments)
            .SingleOrDefaultAsync(contest => contest.Id == raceId
                && contest.TenantId == competition.TenantId
                && contest.SportId == competition.SportId
                && (contest.ContestShape == "Heat"
                    || contest.ContestShape == "AthleticsSemifinal"
                    || contest.ContestShape == "AthleticsFinal"),
                cancellationToken);
    }

    private static string FormatRaceTime(int milliseconds) =>
        $"{milliseconds / 1000}.{milliseconds % 1000:D3}s";

    private async Task<IActionResult> GenerateSeriesPlayoffsAsync(
        Competition competition,
        Stage stage,
        IReadOnlyList<Standing> qualifiers,
        IReadOnlyDictionary<Guid, Participant> participants,
        GenerateTopFourPlayoffsRequest request,
        CancellationToken cancellationToken)
    {
        var startTimeUtc = request.StartTimeUtc.Kind == DateTimeKind.Utc
            ? request.StartTimeUtc
            : request.StartTimeUtc.ToUniversalTime();
        var pairings = new[]
        {
            new[] { qualifiers[0], qualifiers[3] },
            new[] { qualifiers[1], qualifiers[2] }
        };
        TryParseHomeSequence(
            request.SemifinalHomeSequence,
            request.SemifinalBestOf,
            out var semifinalHomeSequence);
        TryParseHomeSequence(
            request.FinalHomeSequence,
            request.FinalBestOf,
            out var finalHomeSequence);
        var generatedSeries = new List<Contest[]>();
        for (var seriesIndex = 0; seriesIndex < pairings.Length; seriesIndex++)
        {
            var pairing = pairings[seriesIndex];
            var games = _bestOfSeriesEngine.GenerateContests(
                    stage,
                    pairing.Select(item => new ParticipantInfo(
                        item.ParticipantId,
                        participants[item.ParticipantId].Type.ToString(),
                        item.Position)),
                    new BestOfSeriesOptions
                    {
                        SportId = competition.SportId,
                        TotalGames = request.SemifinalBestOf,
                        AlternateHomeGames = false,
                        HomeSequence = semifinalHomeSequence,
                        HomeAdvantage = HomeAdvantage.HigherSeed
                    })
                .Select(item => item.Contest)
                .ToArray();
            foreach (var game in games)
            {
                AddSportSegments(game, competition.RulesetVersionId);
                game.SetName(
                    $"Semifinal {seriesIndex + 1} — Game {game.SeriesGameNumber}: "
                    + $"{participants[game.Participants.Single(item => item.SortOrder == 1).ParticipantId].Name} vs "
                    + participants[game.Participants.Single(item => item.SortOrder == 2).ParticipantId].Name);
                game.Schedule(
                    request.Venue,
                    startTimeUtc.AddDays(
                        ((game.SeriesGameNumber ?? 1) - 1)
                        * request.DaysBetweenSeriesGames));
            }
            generatedSeries.Add(games);
        }

        var finalSeriesId = Guid.NewGuid();
        var finalGames = Enumerable.Range(1, request.FinalBestOf)
            .Select(gameNumber =>
            {
                var game = new Contest(
                    Guid.NewGuid(),
                    competition.TenantId,
                    competition.SportId,
                    "SeriesGame");
                game.AssignToStage(stage.Id, 100 + gameNumber);
                game.AssignToSeries(
                    finalSeriesId,
                    gameNumber,
                    request.FinalBestOf,
                    finalHomeSequence[gameNumber - 1]);
                game.SetName($"Final — Game {gameNumber}");
                AddSportSegments(game, competition.RulesetVersionId);
                game.Schedule(
                    request.Venue,
                    startTimeUtc.AddDays(
                        request.DaysUntilFinal
                        + ((gameNumber - 1) * request.DaysBetweenSeriesGames)));
                return game;
            })
            .ToArray();

        var bracket = new Bracket(
            Guid.NewGuid(),
            competition.Id,
            request.BracketName,
            BracketType.SingleElimination,
            4);
        bracket.SetSeeded(true);
        var semiFinalRound = new BracketRound(
            Guid.NewGuid(), bracket.Id, 1, $"Best-of-{request.SemifinalBestOf} Semifinals", 2);
        var finalRound = new BracketRound(
            Guid.NewGuid(), bracket.Id, 2, "Final", 1);
        var finalMatch = new BracketMatch(Guid.NewGuid(), finalRound.Id, 1);
        finalMatch.BindSeries(
            finalSeriesId,
            finalGames[0].Id,
            request.FinalBestOf);
        finalRound.AddMatch(finalMatch);

        for (var index = 0; index < generatedSeries.Count; index++)
        {
            var games = generatedSeries[index];
            var match = new BracketMatch(
                Guid.NewGuid(), semiFinalRound.Id, index + 1);
            match.SetParticipants(
                pairings[index][0].ParticipantId,
                pairings[index][1].ParticipantId);
            match.BindSeries(
                games[0].SeriesId!.Value,
                games[0].Id,
                request.SemifinalBestOf);
            match.AdvanceTo(finalMatch.Id, index + 1);
            semiFinalRound.AddMatch(match);
        }
        bracket.AddRound(semiFinalRound);
        bracket.AddRound(finalRound);
        bracket.Publish();
        bracket.Start();

        _database.Brackets.Add(bracket);
        _database.Contests.AddRange(
            generatedSeries.SelectMany(games => games).Concat(finalGames));
        await _database.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            bracket.Id,
            bracket.CompetitionId,
            Status = bracket.Status.ToString(),
            Qualifiers = qualifiers.Select(item => new
            {
                item.ParticipantId,
                participants[item.ParticipantId].Name,
                item.Position
            }),
            SemiFinalSeries = generatedSeries.Select(games => new
            {
                SeriesId = games[0].SeriesId,
                BestOf = games[0].SeriesBestOf,
                HomeSequence = semifinalHomeSequence,
                ContestIds = games.Select(game => game.Id)
            }),
            FinalSeries = new
            {
                SeriesId = finalSeriesId,
                BestOf = request.FinalBestOf,
                HomeSequence = finalHomeSequence,
                ContestIds = finalGames.Select(game => game.Id)
            }
        });
    }

    private static bool IsValidSeriesLength(int bestOf) =>
        bestOf is 1 or 3 or 5 or 7;

    private static bool TryParseHomeSequence(
        string? value,
        int bestOf,
        out int[] sequence)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            sequence = Enumerable.Range(0, bestOf)
                .Select(index => index % 2 == 0 ? 1 : 2)
                .ToArray();
            return true;
        }
        var tokens = value.Split(
            new[] { ',', '-', ' ', ';' },
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        sequence = tokens.Select(token => token.ToUpperInvariant() switch
            {
                "H" or "1" => 1,
                "A" or "2" => 2,
                _ => 0
            })
            .ToArray();
        return sequence.Length == bestOf && sequence.All(slot => slot is 1 or 2);
    }

    private async Task<List<PublicationStructureError>> ValidatePublicationStructureAsync(
        Competition competition,
        CancellationToken cancellationToken)
    {
        var errors = new List<PublicationStructureError>();
        if (competition.Editions.Count == 0)
            errors.Add(new("edition", "At least one edition is required."));
        var stages = competition.Editions.SelectMany(edition => edition.Stages).ToArray();
        if (stages.Length == 0)
            errors.Add(new("stages", "At least one competition stage is required."));
        if (stages.Length == 0) return errors;

        var stageIds = stages.Select(stage => stage.Id).ToArray();
        var contests = await _database.Contests.AsNoTracking()
            .Where(contest => contest.TenantId == competition.TenantId
                && contest.StageId.HasValue
                && stageIds.Contains(contest.StageId.Value))
            .Select(contest => new { contest.StartTime, contest.Venue })
            .ToArrayAsync(cancellationToken);
        if (contests.Length == 0)
            errors.Add(new("fixtures", "At least one fixture or contest is required."));
        else if (contests.Any(contest => !contest.StartTime.HasValue
            || string.IsNullOrWhiteSpace(contest.Venue)))
            errors.Add(new("scheduling", "Every fixture must have a start time and venue."));
        return errors;
    }

    private static void AddSportSegments(Contest contest, Guid? rulesetVersionId = null)
    {
        if (rulesetVersionId.HasValue)
            contest.AssignRulesetVersion(rulesetVersionId.Value);
        if (contest.SportId == CricketController.CricketSportId)
        {
            CricketController.AddCricketSegments(contest);
            return;
        }

        if (contest.SportId == Guid.Parse(
            "00000000-0000-0000-0000-000000000100"))
        {
            contest.AddSegment("First Half", "Half", 1);
            contest.AddSegment("Second Half", "Half", 2);
            contest.AddSegment(
                "Extra Time",
                "Overtime",
                3,
                isOptional: true,
                activationCondition: "tie_after_regulation");
            contest.AddSegment(
                "Penalty Shootout",
                "Shootout",
                4,
                isOptional: true,
                activationCondition: "tie_after_extra_time");
            return;
        }

        if (contest.SportId == Guid.Parse(
            "00000000-0000-0000-0000-000000000106"))
        {
            contest.AddSegment("First Half", "Half", 1);
            contest.AddSegment("Second Half", "Half", 2);
            contest.AddSegment(
                "Extra Time",
                "ExtraTime",
                3,
                isOptional: true,
                activationCondition: "tie_after_regulation_in_knockout");
            return;
        }

        if (contest.SportId != Guid.Parse(
            "00000000-0000-0000-0000-000000000101"))
            return;

        for (var quarter = 1; quarter <= 4; quarter++)
            contest.AddSegment($"Quarter {quarter}", "Quarter", quarter);
        contest.AddSegment(
            "Overtime",
            "Overtime",
            5,
            isOptional: true,
            activationCondition: "tie_after_regulation");
    }

    [HttpGet("{id:guid}/playoffs")]
    [RequirePermission("competitions.read")]
    public async Task<IActionResult> GetPlayoffs(
        Guid id,
        CancellationToken cancellationToken)
    {
        var competition = await _competitionsService.GetCompetitionByIdAsync(
            id,
            cancellationToken);
        if (competition is null) return NotFound();
        if (!User.CanAccessTenant(competition.TenantId)) return Forbid();

        var bracket = await _database.Brackets
            .AsNoTracking()
            .Include(item => item.Rounds)
            .ThenInclude(round => round.Matches)
            .SingleOrDefaultAsync(item => item.CompetitionId == id, cancellationToken);
        return bracket is null ? NotFound() : Ok(bracket);
    }

    [HttpPost("{id:guid}/playoffs/matches/{matchId:guid}/overrides")]
    [RequirePermission("competitions.manage")]
    public async Task<IActionResult> RequestPlayoffOverride(
        Guid id,
        Guid matchId,
        [FromBody] RequestBracketOverride request,
        CancellationToken cancellationToken)
    {
        var context = await GetBracketContextAsync(id, cancellationToken);
        if (context.Competition is null) return NotFound();
        if (!User.CanAccessTenant(context.Competition.TenantId)) return Forbid();
        var match = context.Bracket?.Rounds.SelectMany(round => round.Matches)
            .SingleOrDefault(item => item.Id == matchId);
        if (match is null) return NotFound();
        if (match.WinnerId.HasValue)
            return Conflict(new { error = "A completed bracket match cannot be overridden." });
        if (request.WinnerId != match.Participant1Id && request.WinnerId != match.Participant2Id)
            return BadRequest(new { error = "The override winner must be assigned to the bracket match." });
        if (string.IsNullOrWhiteSpace(request.Reason))
            return BadRequest(new { error = "An override reason is required." });
        if (!TryGetActorId(out var actorId)) return Unauthorized();
        if (await _database.BracketAdvancementOverrides.AnyAsync(item =>
                item.BracketMatchId == matchId && item.Status == BracketOverrideStatus.Pending,
                cancellationToken))
            return Conflict(new { error = "A pending override already exists for this match." });

        var advancementOverride = new BracketAdvancementOverride(
            Guid.NewGuid(), matchId, request.WinnerId, request.Reason, actorId);
        _database.BracketAdvancementOverrides.Add(advancementOverride);
        await _database.SaveChangesAsync(cancellationToken);
        return Accepted(advancementOverride);
    }

    [HttpGet("{id:guid}/playoffs/overrides")]
    [RequirePermission("competitions.read")]
    public async Task<IActionResult> GetPlayoffOverrideHistory(
        Guid id,
        CancellationToken cancellationToken)
    {
        var context = await GetBracketContextAsync(id, cancellationToken);
        if (context.Competition is null) return NotFound();
        if (!User.CanAccessTenant(context.Competition.TenantId)) return Forbid();
        if (context.Bracket is null) return NotFound();
        var matchIds = context.Bracket.Rounds.SelectMany(round => round.Matches)
            .Select(match => match.Id).ToArray();
        return Ok(await _database.BracketAdvancementOverrides.AsNoTracking()
            .Where(item => matchIds.Contains(item.BracketMatchId))
            .OrderBy(item => item.RequestedAt)
            .ToArrayAsync(cancellationToken));
    }

    [HttpPost("{id:guid}/playoffs/overrides/{overrideId:guid}/approve")]
    [RequirePermission("competitions.manage")]
    public async Task<IActionResult> ApprovePlayoffOverride(
        Guid id,
        Guid overrideId,
        [FromBody] ReviewBracketOverride request,
        CancellationToken cancellationToken)
    {
        var context = await GetBracketContextAsync(id, cancellationToken);
        if (context.Competition is null) return NotFound();
        if (!User.CanAccessTenant(context.Competition.TenantId)) return Forbid();
        if (!TryGetActorId(out var actorId)) return Unauthorized();
        var advancementOverride = await _database.BracketAdvancementOverrides
            .SingleOrDefaultAsync(item => item.Id == overrideId, cancellationToken);
        var match = context.Bracket?.Rounds.SelectMany(round => round.Matches)
            .SingleOrDefault(item => item.Id == advancementOverride?.BracketMatchId);
        if (advancementOverride is null || match is null) return NotFound();
        if (match.WinnerId.HasValue)
            return Conflict(new { error = "The bracket match has already been resolved." });

        try
        {
            advancementOverride.Approve(actorId, request.Notes);
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(new { error = exception.Message });
        }
        await ApplyApprovedOverrideAsync(
            context.Competition,
            context.Bracket!,
            match,
            advancementOverride.WinnerId,
            cancellationToken);
        await _database.SaveChangesAsync(cancellationToken);
        return Ok(advancementOverride);
    }

    [HttpPost("{id:guid}/playoffs/overrides/{overrideId:guid}/reject")]
    [RequirePermission("competitions.manage")]
    public async Task<IActionResult> RejectPlayoffOverride(
        Guid id,
        Guid overrideId,
        [FromBody] ReviewBracketOverride request,
        CancellationToken cancellationToken)
    {
        var context = await GetBracketContextAsync(id, cancellationToken);
        if (context.Competition is null) return NotFound();
        if (!User.CanAccessTenant(context.Competition.TenantId)) return Forbid();
        if (!TryGetActorId(out var actorId)) return Unauthorized();
        var matchIds = context.Bracket?.Rounds.SelectMany(round => round.Matches)
            .Select(match => match.Id).ToArray() ?? Array.Empty<Guid>();
        var advancementOverride = await _database.BracketAdvancementOverrides
            .SingleOrDefaultAsync(item => item.Id == overrideId
                && matchIds.Contains(item.BracketMatchId), cancellationToken);
        if (advancementOverride is null) return NotFound();
        try
        {
            advancementOverride.Reject(actorId, request.Notes);
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(new { error = exception.Message });
        }
        await _database.SaveChangesAsync(cancellationToken);
        return Ok(advancementOverride);
    }

    private async Task<(Competition? Competition, Bracket? Bracket)> GetBracketContextAsync(
        Guid competitionId,
        CancellationToken cancellationToken)
    {
        var competition = await _database.Competitions
            .SingleOrDefaultAsync(item => item.Id == competitionId, cancellationToken);
        var bracket = competition is null
            ? null
            : await _database.Brackets
                .Include(item => item.Rounds)
                .ThenInclude(round => round.Matches)
                .SingleOrDefaultAsync(item => item.CompetitionId == competitionId, cancellationToken);
        return (competition, bracket);
    }

    private async Task ApplyApprovedOverrideAsync(
        Competition competition,
        Bracket bracket,
        BracketMatch match,
        Guid winnerId,
        CancellationToken cancellationToken)
    {
        match.SetWinner(winnerId);
        var loserId = match.Participant1Id == winnerId ? match.Participant2Id : match.Participant1Id;
        if (match.NextMatchId.HasValue)
            await PopulateOverrideTargetAsync(match.NextMatchId.Value, match.NextMatchSlot!.Value, winnerId, competition, cancellationToken);
        else if (!match.IsThirdPlace)
            bracket.Complete(winnerId);
        if (loserId.HasValue && match.LoserNextMatchId.HasValue)
            await PopulateOverrideTargetAsync(match.LoserNextMatchId.Value, match.LoserNextMatchSlot!.Value, loserId.Value, competition, cancellationToken);
    }

    private async Task PopulateOverrideTargetAsync(
        Guid matchId,
        int slot,
        Guid participantId,
        Competition competition,
        CancellationToken cancellationToken)
    {
        var target = await _database.BracketMatches.SingleAsync(item => item.Id == matchId, cancellationToken);
        target.SetParticipants(
            slot == 1 ? participantId : target.Participant1Id,
            slot == 2 ? participantId : target.Participant2Id);
        var participantType = (await _database.Participants.AsNoTracking()
            .SingleAsync(item => item.Id == participantId && item.TenantId == competition.TenantId, cancellationToken))
            .Type.ToString();
        var contests = await _database.Contests.Include(item => item.Participants)
            .Where(item => target.SeriesId.HasValue ? item.SeriesId == target.SeriesId : item.Id == target.ContestId)
            .ToListAsync(cancellationToken);
        foreach (var contest in contests.Where(item =>
                     item.Participants.All(participant => participant.ParticipantId != participantId)))
        {
            var contestSlot = target.IsAggregateTie && contest.SeriesGameNumber == 2
                ? 3 - slot
                : target.SeriesId.HasValue && !target.IsAggregateTie
                    && contest.SeriesHomeParticipantSlot.HasValue
                    ? contest.SeriesHomeParticipantSlot.Value == slot ? 1 : 2
                    : slot;
            var participant = contest.AddParticipant(participantId, participantType, contestSlot);
            _database.Entry(participant).State = EntityState.Added;
        }
    }

    private Task<Competition?> LoadHierarchyAsync(Guid competitionId, CancellationToken cancellationToken) =>
        _database.Competitions
            .AsSplitQuery()
            .Include(item => item.Editions)
                .ThenInclude(item => item.Stages)
                    .ThenInclude(item => item.Groups)
            .Include(item => item.Editions)
                .ThenInclude(item => item.Stages)
                    .ThenInclude(item => item.Rounds)
            .SingleOrDefaultAsync(item => item.Id == competitionId, cancellationToken);

    private bool TryGetActorId(out Guid actorId) =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out actorId);
}

public record CreateCompetitionRequest(string Name, Guid SportId, Guid TenantId);
public sealed record AddEditionRequest(string Name, DateTime StartDate, DateTime? EndDate = null);
public sealed record AddStageRequest(string Name, string Type, int SortOrder);
public sealed record AddGroupRequest(string Name);
public sealed record AddRoundRequest(string Name, int SortOrder);
public sealed record AddManualContestRequest(
    string Name,
    string ContestShape,
    int SortOrder,
    Guid? GroupId = null,
    Guid? RoundId = null,
    IReadOnlyList<string>? AllowedParticipantTypes = null);

public sealed record PublicationStructureError(string Field, string Message);

public sealed record GenerateRoundRobinFixturesRequest(
    string EditionName,
    string StageName,
    DateTime StartTimeUtc,
    DateTime? EndDateUtc,
    string Venue = "Main Stadium",
    bool HomeAndAway = true,
    int DaysBetweenRounds = 7,
    int? DrawSeed = null);

public sealed record FixtureGenerationResponse(
    Guid CompetitionId,
    Guid EditionId,
    Guid StageId,
    int EligibleParticipantCount,
    int FixtureCount,
    int DrawSeed,
    IReadOnlyList<GeneratedFixtureResponse> Fixtures);

public sealed record GeneratedFixtureResponse(
    Guid ContestId,
    int Round,
    int MatchNumber,
    Guid HomeParticipantId,
    Guid AwayParticipantId,
    DateTime StartTimeUtc);

public sealed record GenerateTopFourPlayoffsRequest(
    string BracketName,
    string StageName,
    DateTime StartTimeUtc,
    string Venue = "Main Stadium",
    int DaysUntilFinal = 7,
    int SemifinalBestOf = 1,
    int FinalBestOf = 1,
    int DaysBetweenSeriesGames = 2,
    string? SemifinalHomeSequence = null,
    string? FinalHomeSequence = null,
    int LegsPerTie = 1,
    int DaysBetweenLegs = 7,
    bool IncludeThirdPlace = false,
    bool RandomDraw = false,
    int? DrawSeed = null);

public sealed record GenerateGroupKnockoutRequest(
    string EditionName,
    string GroupStageName,
    string KnockoutStageName,
    string BracketName,
    DateTime StartTimeUtc,
    int GroupsCount,
    int TeamsPerGroup,
    int QualifiersPerGroup,
    int BestRunnerUpCount,
    IReadOnlyList<string> TieBreakers,
    string Venue = "Main Stadium",
    int DaysBetweenRounds = 7,
    DateTime? EndDateUtc = null);

public sealed record GenerateKnockoutPlayoffsRequest(
    string BracketName,
    string StageName,
    DateTime StartTimeUtc,
    int QualifierCount,
    string Venue = "Main Stadium",
    int DaysBetweenRounds = 7,
    int LegsPerTie = 1,
    int DaysBetweenLegs = 7,
    bool IncludeThirdPlace = false,
    bool RandomDraw = false,
    int? DrawSeed = null);

public sealed record CompetitionPresentationRequest(
    string? PrimaryContestRenderer,
    IReadOnlyList<string> VisibleTabs,
    string? DefaultTab,
    string? ScoreVariant,
    string? StandingsVariant,
    string? LeaderboardVariant,
    bool? BracketEnabled,
    bool? SeriesEnabled,
    bool? MedalTableEnabled)
{
    public static CompetitionPresentationRequest Default { get; } = new(
        null, Array.Empty<string>(), null, null, null, null,
        null, null, null);
}

public sealed record RequestBracketOverride(Guid WinnerId, string Reason);
public sealed record ReviewBracketOverride(string? Notes);

public sealed record GenerateGolfTournamentRequest(
    string TournamentName,
    string EditionName,
    string StageName,
    DateTime StartTimeUtc,
    string Venue = "Championship Course",
    int ExpectedParticipants = 24,
    int TotalRounds = 4,
    int HolesPerRound = 18,
    int ParPerRound = 72,
    int CutAfterRound = 2,
    int CutToParticipants = 12,
    string CourseName = "Championship Course",
    string CourseLocation = "Main Course",
    IReadOnlyList<int>? HolePars = null);

public sealed record SubmitGolfScorecardRequest(
    Guid ParticipantId,
    int RoundNumber,
    IReadOnlyList<GolfHoleScoreRequest> Holes);

public sealed record GolfHoleScoreRequest(
    int HoleNumber,
    int Strokes,
    int Penalties = 0);

public sealed record GenerateCombatCardRequest(
    string EditionName,
    string StageName,
    DateTime StartTimeUtc,
    IReadOnlyList<CombatBoutRequest> Bouts,
    string Venue = "Combat Arena",
    int MinutesBetweenBouts = 30,
    IReadOnlyList<string>? AllowedResultMethods = null);

public sealed record ReorderCombatCardRequest(
    IReadOnlyList<Guid> BoutIds,
    int MinutesBetweenBouts = 30);

public sealed record CombatBoutRequest(
    Guid Fighter1Id,
    Guid Fighter2Id,
    string WeightClass,
    string CardSection,
    int ScheduledRounds,
    IReadOnlyList<CombatOfficialRequest> Officials);

public sealed record CombatOfficialRequest(
    Guid OfficialId,
    string DisplayName,
    string Role);

public sealed record SubmitCombatBoutResultRequest(
    Guid? WinnerId,
    string ResultMethod,
    int? Round,
    int? TimeInRoundSeconds,
    IReadOnlyList<CombatJudgeScorecardRequest> Scorecards,
    string? Notes = null);

public sealed record CombatResultCorrectionRequest(
    SubmitCombatBoutResultRequest Result,
    string Reason);

public sealed record CombatCorrectionReviewRequest(string? Reason = null);

public sealed record CombatJudgeScorecardRequest(
    Guid OfficialId,
    IReadOnlyList<CombatJudgeRoundScoreRequest> Rounds);

public sealed record CombatJudgeRoundScoreRequest(
    int RoundNumber,
    int Fighter1Score,
    int Fighter2Score);

public sealed record CreateAthleticsFieldEventRequest(
    string EventName,
    string MetricType,
    string EditionName,
    string StageName,
    DateTime StartTimeUtc,
    IReadOnlyList<Guid> ParticipantIds,
    string Venue = "Field Arena",
    int MaxAttempts = 6,
    decimal? RecordThresholdMeters = null);

public sealed record SubmitAthleticsFieldEventResultsRequest(
    IReadOnlyList<AthleticsFieldResultRequest> Results);

public sealed record AthleticsFieldResultRequest(
    Guid ParticipantId,
    IReadOnlyList<AthleticsFieldAttemptRequest> Attempts,
    bool Disqualified = false,
    string? DisqualificationReason = null);

public sealed record AthleticsFieldAttemptRequest(decimal? MeasurementMeters);

public sealed record GenerateAthleticsMeetRequest(
    string EventName,
    string EditionName,
    string StageName,
    DateTime StartTimeUtc,
    string Venue = "Main Track",
    int DistanceMeters = 100,
    int ExpectedParticipants = 16,
    int HeatCount = 2,
    int LanesPerHeat = 8,
    int QualifyByPlace = 3,
    int QualifyByTime = 2,
    bool AdvanceTies = true,
    int MinutesBetweenHeats = 15,
    int SemifinalCount = 0,
    int SemifinalQualifyByPlace = 3,
    int SemifinalQualifyByTime = 2,
    string LaneAssignment = "Seeded",
    bool? AdvancePlaceTies = null,
    bool? AdvanceTimeTies = null);

public sealed record SubmitAthleticsRaceResultsRequest(
    IReadOnlyList<AthleticsRaceResultRequest> Results);

public sealed record AthleticsRaceResultRequest(
    Guid ParticipantId,
    int? TimeMilliseconds,
    bool Disqualified = false,
    string? DisqualificationReason = null);

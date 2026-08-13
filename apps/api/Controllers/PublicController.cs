using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using SportsPlatform.Infrastructure.Persistence;
using SportsPlatform.Modules.Competitions.Entities;
using SportsPlatform.Modules.Contests.Entities;
using SportsPlatform.Modules.Participants.Entities;
using SportsPlatform.Modules.Results.Entities;
using SportsPlatform.Modules.Sponsorship.Entities;
using SportsPlatform.Modules.Sports.Entities;
using SportsPlatform.Modules.Sports.Services;
using SportsPlatform.Modules.Tenancy.Entities;

namespace SportsPlatform.Api.Controllers;

/// <summary>
/// Read-only, tenant-aware projection used by public web and supporter clients.
/// Draft and unapproved records are deliberately excluded.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/public/tenants/{slug}")]
public sealed class PublicController : ControllerBase
{
    private static readonly Guid RugbySportId =
        Guid.Parse("00000000-0000-0000-0000-000000000106");
    private readonly SportsPlatformDbContext _db;

    public PublicController(SportsPlatformDbContext db)
    {
        _db = db;
    }

    [HttpGet("cricket/tournament")]
    public async Task<IActionResult> GetCricketTournament(
        string slug,
        [FromQuery] Guid competitionId,
        CancellationToken cancellationToken)
    {
        var tenant = await ResolveTenantAsync(slug, cancellationToken);
        if (tenant is null) return NotFound();
        var competition = await PublishedCompetitions(tenant.Id)
            .AsSplitQuery()
            .Include(item => item.Editions)
            .ThenInclude(edition => edition.Stages)
            .SingleOrDefaultAsync(item => item.Id == competitionId
                && item.SportId == CricketController.CricketSportId,
                cancellationToken);
        if (competition is null) return NotFound();

        return Ok(await CricketTournamentProjection.BuildAsync(
            _db,
            competition,
            cancellationToken));
    }

    [HttpGet("rugby/tournament")]
    public async Task<IActionResult> GetRugbyTournament(
        string slug,
        [FromQuery] Guid competitionId,
        CancellationToken cancellationToken)
    {
        var tenant = await ResolveTenantAsync(slug, cancellationToken);
        if (tenant is null) return NotFound();
        var competition = await PublishedCompetitions(tenant.Id)
            .AsSplitQuery()
            .Include(item => item.Editions)
            .ThenInclude(edition => edition.Stages)
            .SingleOrDefaultAsync(item => item.Id == competitionId
                && item.SportId == RugbySportId,
                cancellationToken);
        if (competition is null) return NotFound();

        var ruleset = competition.RulesetVersionId.HasValue
            ? await _db.Sports.AsNoTracking()
                .SelectMany(item => item.Versions)
                .SingleOrDefaultAsync(item => item.Id == competition.RulesetVersionId.Value,
                    cancellationToken)
            : null;
        var variant = RugbyVariants.FromValidationConfiguration(ruleset?.ValidationConfiguration)
            ?? RugbyVariants.Fifteens;
        var isSevens = variant == RugbyVariants.Sevens;

        var stageIds = competition.Editions
            .SelectMany(edition => edition.Stages)
            .Select(stage => stage.Id)
            .ToArray();
        var contests = await PublicContests(tenant.Id)
            .AsSplitQuery()
            .Include(item => item.Participants)
            .Where(item => item.StageId.HasValue
                && stageIds.Contains(item.StageId.Value))
            .OrderBy(item => item.SortOrder)
            .ToListAsync(cancellationToken);
        var participantIds = contests
            .SelectMany(item => item.Participants)
            .Select(item => item.ParticipantId)
            .Distinct()
            .ToArray();
        var names = await _db.Participants.AsNoTracking()
            .Where(item => participantIds.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, item => item.Name, cancellationToken);
        var contestIds = contests.Select(item => item.Id).ToArray();
        var scores = await _db.LiveScores.AsNoTracking()
            .Where(item => contestIds.Contains(item.ContestId)
                && item.MetricName == "score")
            .GroupBy(item => new { item.ContestId, item.ParticipantId })
            .Select(group => new
            {
                group.Key.ContestId,
                group.Key.ParticipantId,
                Score = group.Sum(item => item.Value)
            })
            .ToDictionaryAsync(
                item => (item.ContestId, item.ParticipantId),
                item => item.Score,
                cancellationToken);
        var standings = await _db.Standings.AsNoTracking()
            .Where(item => item.CompetitionId == competition.Id)
            .OrderBy(item => item.Position)
            .ToListAsync(cancellationToken);

        return Ok(new
        {
            competition.Id,
            competition.Name,
            Status = competition.Status.ToString(),
            Variant = variant,
            FormatName = isSevens ? "Rugby Sevens" : "Rugby Union 15s",
            PlayersPerSide = isSevens ? 7 : 15,
            HalfDurationMinutes = isSevens ? 7 : 40,
            StandingsModel = new
            {
                WinPoints = isSevens ? 3 : 4,
                DrawPoints = 2,
                LossPoints = isSevens ? 1 : 0,
                UsesTryBonus = !isSevens,
                UsesLosingBonus = !isSevens,
                UsesGrandSlamBonus = !isSevens
            },
            Matches = contests.Select(contest => new
            {
                contest.Id,
                contest.Name,
                Status = contest.Status.ToString(),
                contest.StartTime,
                contest.Venue,
                Teams = contest.Participants.OrderBy(item => item.SortOrder).Select(item => new
                {
                    item.ParticipantId,
                    Name = names.GetValueOrDefault(item.ParticipantId, "Team"),
                    Score = scores.GetValueOrDefault((contest.Id, item.ParticipantId))
                })
            }),
            Standings = standings.Select(item => new
            {
                item.Id,
                item.ParticipantId,
                ParticipantName = names.GetValueOrDefault(item.ParticipantId, "Team"),
                item.Position,
                item.Played,
                item.Won,
                item.Drawn,
                item.Lost,
                item.PointsFor,
                item.PointsAgainst,
                item.PointDifferential,
                item.Points,
                item.Metadata
            })
        });
    }

    [HttpGet]
    public async Task<IActionResult> GetTenant(string slug, CancellationToken cancellationToken)
    {
        var tenant = await ResolveTenantAsync(slug, cancellationToken);
        if (tenant is null) return NotFound();

        var branding = await _db.TenantBrandings
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.TenantId == tenant.Id, cancellationToken);

        return Ok(new
        {
            tenant.Id,
            tenant.Name,
            tenant.ShortName,
            tenant.Slug,
            tenant.Description,
            tenant.Country,
            tenant.Timezone,
            tenant.Currency,
            tenant.DefaultLanguage,
            Branding = branding is null ? null : new
            {
                branding.LogoUrl,
                branding.PrimaryColor,
                branding.SecondaryColor,
                branding.AccentColor,
                branding.PublicSlug,
                branding.ContactEmail,
                branding.ContactPhone,
                branding.Website
            }
        });
    }

    [HttpGet("home")]
    public async Task<IActionResult> GetHome(string slug, CancellationToken cancellationToken)
    {
        var tenant = await ResolveTenantAsync(slug, cancellationToken);
        if (tenant is null) return NotFound();

        Response.Headers["Cache-Control"] =
            "public, max-age=30, stale-while-revalidate=120";

        var competitions = await PublishedCompetitions(tenant.Id)
            .OrderByDescending(x => x.PublishedAt)
            .Take(8)
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.Description,
                x.SportId,
                SportCode = _db.Sports.Where(sport => sport.Id == x.SportId)
                    .Select(sport => sport.Code).FirstOrDefault(),
                SportName = _db.Sports.Where(sport => sport.Id == x.SportId)
                    .Select(sport => sport.Name).FirstOrDefault(),
                Status = x.Status.ToString(),
                x.PublishedAt
            })
            .ToListAsync(cancellationToken);

        var contests = await PublicContests(tenant.Id)
            .Where(x => x.Status == ContestStatus.InProgress
                || x.Status == ContestStatus.Ready
                || x.Status == ContestStatus.Scheduled)
            .OrderBy(x => x.Status == ContestStatus.InProgress
                ? 0
                : x.Status == ContestStatus.Ready ? 1 : 2)
            .ThenBy(x => x.StartTime)
            .Take(30)
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.SportId,
                x.ContestShape,
                Status = x.Status.ToString(),
                x.Venue,
                x.StartTime,
                x.EndTime
            })
            .ToListAsync(cancellationToken);

        var branding = await _db.TenantBrandings
            .AsNoTracking()
            .Where(item => item.TenantId == tenant.Id)
            .Select(item => new
            {
                item.LogoUrl,
                item.PrimaryColor,
                item.SecondaryColor,
                item.AccentColor,
                item.PublicSlug,
                item.ContactEmail,
                item.ContactPhone,
                item.Website
            })
            .FirstOrDefaultAsync(cancellationToken);

        var publicContestIds = PublicContests(tenant.Id).Select(item => item.Id);
        var recentResultRows = await _db.ContestResults
            .AsNoTracking()
            .Where(item => publicContestIds.Contains(item.ContestId)
                && (item.Status == ContestResultStatus.Published
                    || item.Status == ContestResultStatus.Official))
            .OrderByDescending(item => item.ApprovedAt ?? item.UpdatedAt ?? item.CreatedAt)
            .Take(8)
            .Select(item => new
            {
                item.Id,
                item.ContestId,
                item.ParticipantId,
                item.Rank,
                item.Position,
                item.Score,
                item.ScoreDisplay,
                item.Time,
                Status = item.Status.ToString(),
                item.ApprovedAt
            })
            .ToListAsync(cancellationToken);
        var recentContestIds = recentResultRows
            .Select(item => item.ContestId)
            .Distinct()
            .ToArray();
        var recentParticipantIds = recentResultRows
            .Select(item => item.ParticipantId)
            .Distinct()
            .ToArray();
        var recentContestNames = await _db.Contests
            .AsNoTracking()
            .Where(item => recentContestIds.Contains(item.Id))
            .ToDictionaryAsync(
                item => item.Id,
                item => item.Name ?? item.ContestShape,
                cancellationToken);
        var recentParticipantNames = await _db.Participants
            .AsNoTracking()
            .Where(item => item.TenantId == tenant.Id
                && recentParticipantIds.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, item => item.Name, cancellationToken);

        var tenantCompetitionIds = _db.Competitions
            .AsNoTracking()
            .Where(item => item.TenantId == tenant.Id)
            .Select(item => item.Id);
        var tenantContestIds = _db.Contests
            .AsNoTracking()
            .Where(item => item.TenantId == tenant.Id)
            .Select(item => item.Id);
        var today = DateTime.UtcNow;
        var sponsorPlacements = _db.Sponsorships
            .AsNoTracking()
            .Where(item => item.Status == SponsorshipStatus.Active
                && item.TenantId == tenant.Id
                && item.Placement == SponsorPlacements.PublicHome
                && (!item.StartDate.HasValue || item.StartDate <= today)
                && (!item.EndDate.HasValue || item.EndDate >= today)
                && ((item.CompetitionId.HasValue
                        && tenantCompetitionIds.Contains(item.CompetitionId.Value))
                    || (item.ContestId.HasValue
                        && tenantContestIds.Contains(item.ContestId.Value))))
            .GroupBy(item => item.SponsorId)
            .Select(group => new
            {
                SponsorId = group.Key,
                DisplayOrder = group.Min(item => item.DisplayOrder)
            });
        var sponsors = await (
            from sponsor in _db.Sponsors.AsNoTracking()
            join placement in sponsorPlacements
                on sponsor.Id equals placement.SponsorId
            where sponsor.TenantId == tenant.Id && sponsor.IsActive
            orderby placement.DisplayOrder, sponsor.Name
            select new
            {
                sponsor.Id,
                sponsor.Name,
                sponsor.Description,
                sponsor.LogoUrl,
                sponsor.Website,
                Tier = sponsor.Tier.ToString()
            })
            .Take(12)
            .ToListAsync(cancellationToken);

        return Ok(new
        {
            Tenant = new
            {
                tenant.Id,
                tenant.Name,
                tenant.ShortName,
                tenant.Slug,
                Branding = branding
            },
            Competitions = competitions,
            FeaturedCompetitions = competitions.Take(6),
            LiveContests = contests.Where(x => x.Status == nameof(ContestStatus.InProgress)),
            UpcomingContests = contests.Where(x => x.Status is nameof(ContestStatus.Scheduled) or nameof(ContestStatus.Ready)),
            RecentResults = recentResultRows.Select(item => new
            {
                item.Id,
                item.ContestId,
                ContestName = recentContestNames.GetValueOrDefault(
                    item.ContestId,
                    "Contest"),
                item.ParticipantId,
                ParticipantName = recentParticipantNames.GetValueOrDefault(
                    item.ParticipantId,
                    "Participant"),
                item.Rank,
                item.Position,
                item.Score,
                item.ScoreDisplay,
                item.Time,
                item.Status,
                item.ApprovedAt
            }),
            Sponsors = sponsors
        });
    }

    [HttpGet("competitions")]
    public async Task<IActionResult> GetCompetitions(
        string slug,
        [FromQuery] Guid? sportId,
        [FromQuery] string? search,
        CancellationToken cancellationToken)
    {
        var tenant = await ResolveTenantAsync(slug, cancellationToken);
        if (tenant is null) return NotFound();

        var query = PublishedCompetitions(tenant.Id);
        if (sportId.HasValue) query = query.Where(x => x.SportId == sportId);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(x => EF.Functions.ILike(x.Name, $"%{term}%"));
        }

        var results = await query
            .OrderBy(x => x.Name)
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.Description,
                x.SportId,
                SportCode = _db.Sports.Where(sport => sport.Id == x.SportId)
                    .Select(sport => sport.Code).FirstOrDefault(),
                SportName = _db.Sports.Where(sport => sport.Id == x.SportId)
                    .Select(sport => sport.Name).FirstOrDefault(),
                Status = x.Status.ToString(),
                x.PublishedAt
            })
            .ToListAsync(cancellationToken);
        return Ok(results);
    }

    [HttpGet("search")]
    public async Task<IActionResult> Search(
        string slug,
        [FromQuery] string? q,
        [FromQuery] string? type,
        [FromQuery] Guid? sportId,
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        [FromQuery] string? status,
        [FromQuery] Guid? competitionId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var tenant = await ResolveTenantAsync(slug, cancellationToken);
        if (tenant is null) return NotFound();
        if (page < 1) return BadRequest(new { Message = "Page must be at least 1." });
        if (pageSize is < 1 or > 50)
            return BadRequest(new { Message = "Page size must be between 1 and 50." });
        if (dateFrom.HasValue && dateTo.HasValue && dateFrom > dateTo)
            return BadRequest(new { Message = "The start date must not be after the end date." });

        var normalizedType = string.IsNullOrWhiteSpace(type)
            ? "all"
            : type.Trim().ToLowerInvariant();
        var supportedTypes = new HashSet<string>
        {
            "all", "competition", "team", "athlete", "contest", "venue", "sport"
        };
        if (!supportedTypes.Contains(normalizedType))
            return BadRequest(new { Message = "The requested search type is not supported." });

        var competitionStageIds = competitionId.HasValue
            ? await PublishedCompetitions(tenant.Id)
                .Where(item => item.Id == competitionId.Value)
                .SelectMany(item => item.Editions)
                .SelectMany(item => item.Stages)
                .Select(item => item.Id)
                .ToArrayAsync(cancellationToken)
            : Array.Empty<Guid>();
        if (competitionId.HasValue && competitionStageIds.Length == 0)
            return Ok(EmptyPublicSearch(page, pageSize));

        IQueryable<Contest> contestQuery = PublicContests(tenant.Id);
        if (sportId.HasValue)
            contestQuery = contestQuery.Where(item => item.SportId == sportId.Value);
        if (dateFrom.HasValue)
            contestQuery = contestQuery.Where(item => item.StartTime >= dateFrom.Value);
        if (dateTo.HasValue)
        {
            var inclusiveEnd = dateTo.Value.Date.AddDays(1);
            contestQuery = contestQuery.Where(item => item.StartTime < inclusiveEnd);
        }
        if (competitionId.HasValue)
            contestQuery = contestQuery.Where(item => item.StageId.HasValue
                && competitionStageIds.Contains(item.StageId.Value));
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<ContestStatus>(status.Trim(), true, out var contestStatus))
                contestQuery = contestQuery.Where(_ => false);
            else
                contestQuery = contestQuery.Where(item => item.Status == contestStatus);
        }
        var contests = await contestQuery
            .Include(item => item.Participants)
            .ToListAsync(cancellationToken);

        var competitionQuery = PublishedCompetitions(tenant.Id);
        if (sportId.HasValue)
            competitionQuery = competitionQuery.Where(item => item.SportId == sportId.Value);
        if (competitionId.HasValue)
            competitionQuery = competitionQuery.Where(item => item.Id == competitionId.Value);
        if (dateFrom.HasValue)
            competitionQuery = competitionQuery.Where(item => item.Editions.Any(
                edition => edition.EndDate >= dateFrom.Value));
        if (dateTo.HasValue)
            competitionQuery = competitionQuery.Where(item => item.Editions.Any(
                edition => edition.StartDate < dateTo.Value.Date.AddDays(1)));
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<CompetitionStatus>(status.Trim(), true, out var competitionStatus))
                competitionQuery = competitionQuery.Where(_ => false);
            else
                competitionQuery = competitionQuery.Where(item => item.Status == competitionStatus);
        }
        var competitions = await competitionQuery.ToListAsync(cancellationToken);

        var hasContestFilter = sportId.HasValue || dateFrom.HasValue || dateTo.HasValue
            || !string.IsNullOrWhiteSpace(status) || competitionId.HasValue;
        var eligibleParticipantIds = contests
            .SelectMany(item => item.Participants)
            .Select(item => item.ParticipantId)
            .Distinct()
            .ToArray();
        var participantQuery = _db.Participants.AsNoTracking()
            .Where(item => item.TenantId == tenant.Id
                && item.Status == ParticipantStatus.Active
                && (item.Type == ParticipantType.Team
                    || item.Type == ParticipantType.Athlete));
        if (hasContestFilter)
            participantQuery = participantQuery.Where(item =>
                eligibleParticipantIds.Contains(item.Id));
        var participants = await participantQuery.ToListAsync(cancellationToken);

        var term = q?.Trim();
        bool Matches(string? value) => string.IsNullOrWhiteSpace(term)
            || (!string.IsNullOrWhiteSpace(value)
                && value.Contains(term, StringComparison.OrdinalIgnoreCase));
        var filterSportIds = await PublishedCompetitions(tenant.Id)
            .Select(item => item.SportId)
            .Concat(PublicContests(tenant.Id).Select(item => item.SportId))
            .Distinct()
            .ToArrayAsync(cancellationToken);
        var filterSports = await _db.Sports.AsNoTracking()
            .Where(item => filterSportIds.Contains(item.Id) && item.IsActive)
            .OrderBy(item => item.Name)
            .Select(item => new { item.Id, item.Code, item.Name, item.Description })
            .ToListAsync(cancellationToken);
        var items = new List<PublicSearchItem>();
        if (normalizedType is "all" or "sport")
            items.AddRange(filterSports.Where(item =>
                    Matches(item.Name) || Matches(item.Code) || Matches(item.Description))
                .Select(item => new PublicSearchItem(
                    item.Id.ToString(), "Sport", item.Name,
                    item.Description, item.Id, null, "Active", null, null,
                    $"/sport/{item.Code.ToLowerInvariant()}")));
        if (normalizedType is "all" or "competition")
            items.AddRange(competitions.Where(item =>
                    Matches(item.Name) || Matches(item.Description))
                .Select(item => new PublicSearchItem(
                    item.Id.ToString(), "Competition", item.Name,
                    item.Description, item.SportId, item.PublishedAt,
                    item.Status.ToString(), item.Id, null,
                    $"/competition/{item.Id}")));
        if (normalizedType is "all" or "contest")
            items.AddRange(contests.Where(item =>
                    Matches(item.Name) || Matches(item.Venue)
                    || Matches(item.ContestShape))
                .Select(item => new PublicSearchItem(
                    item.Id.ToString(), "Contest",
                    item.Name ?? "Published contest", item.Venue,
                    item.SportId, item.StartTime, item.Status.ToString(),
                    null, item.Venue, $"/contest/{item.Id}")));
        if (normalizedType is "all" or "team")
            items.AddRange(participants.Where(item => item.Type == ParticipantType.Team
                    && (Matches(item.Name) || Matches(item.ShortName)))
                .Select(item => new PublicSearchItem(
                    item.Id.ToString(), "Team", item.Name, item.ShortName,
                    null, null, item.Status.ToString(), null, null,
                    $"/team/{item.Id}")));
        if (normalizedType is "all" or "athlete")
            items.AddRange(participants.Where(item => item.Type == ParticipantType.Athlete
                    && (Matches(item.Name) || Matches(item.ShortName)))
                .Select(item => new PublicSearchItem(
                    item.Id.ToString(), "Athlete", item.Name, item.ShortName,
                    null, null, item.Status.ToString(), null, null,
                    $"/athlete/{item.Id}")));
        if (normalizedType is "all" or "venue")
            items.AddRange(contests.Where(item => !string.IsNullOrWhiteSpace(item.Venue)
                    && Matches(item.Venue))
                .GroupBy(item => item.Venue!, StringComparer.OrdinalIgnoreCase)
                .Select(group => new PublicSearchItem(
                    group.Key, "Venue", group.Key,
                    $"{group.Count()} public contest{(group.Count() == 1 ? "" : "s")}",
                    null, group.Min(item => item.StartTime), null, null,
                    group.Key, null)));

        var ordered = items
            .OrderByDescending(item => !string.IsNullOrWhiteSpace(term)
                && item.Title.StartsWith(term, StringComparison.OrdinalIgnoreCase))
            .ThenBy(item => item.Type)
            .ThenBy(item => item.Title)
            .ToArray();
        var total = ordered.Length;
        var pageItems = ordered.Skip((page - 1) * pageSize).Take(pageSize).ToArray();
        var filterCompetitionRows = await PublishedCompetitions(tenant.Id)
            .OrderBy(item => item.Name)
            .Select(item => new { item.Id, item.Name })
            .ToListAsync(cancellationToken);
        Response.Headers.CacheControl = "public, max-age=15, stale-while-revalidate=60";
        return Ok(new
        {
            Query = term ?? string.Empty,
            Page = page,
            PageSize = pageSize,
            Total = total,
            TotalPages = total == 0 ? 0 : (int)Math.Ceiling(total / (double)pageSize),
            Items = pageItems,
            Filters = new
            {
                Sports = filterSports.Select(item => new { item.Id, item.Name }),
                Competitions = filterCompetitionRows,
                Statuses = Enum.GetNames<ContestStatus>()
                    .Concat(Enum.GetNames<CompetitionStatus>())
                    .Where(value => value is not "Draft" and not "Cancelled" and not "Archived")
                    .Distinct()
                    .OrderBy(value => value)
            }
        });
    }

    [HttpGet("competitions/{id:guid}")]
    public async Task<IActionResult> GetCompetition(string slug, Guid id, CancellationToken cancellationToken)
    {
        var tenant = await ResolveTenantAsync(slug, cancellationToken);
        if (tenant is null) return NotFound();

        var competition = await PublishedCompetitions(tenant.Id)
            .AsSplitQuery()
            .Include(item => item.Editions)
            .ThenInclude(edition => edition.Stages)
            .ThenInclude(stage => stage.Groups)
            .Where(item => item.Id == id)
            .FirstOrDefaultAsync(cancellationToken);
        if (competition is null) return NotFound();

        Response.Headers["Cache-Control"] =
            "public, max-age=30, stale-while-revalidate=120";

        var stageIds = competition.Editions
            .SelectMany(edition => edition.Stages)
            .Select(stage => stage.Id)
            .ToArray();
        var contests = await PublicContests(tenant.Id)
            .AsSplitQuery()
            .Include(item => item.Participants)
            .Where(item => item.StageId.HasValue
                && stageIds.Contains(item.StageId.Value))
            .OrderBy(item => item.StartTime)
            .ThenBy(item => item.SortOrder)
            .ToListAsync(cancellationToken);
        var contestIds = contests.Select(item => item.Id).ToArray();
        var participantIds = contests
            .SelectMany(item => item.Participants)
            .Select(item => item.ParticipantId)
            .Distinct()
            .ToArray();
        var participantNames = await _db.Participants
            .AsNoTracking()
            .Where(item => item.TenantId == tenant.Id
                && participantIds.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, item => item.Name, cancellationToken);
        var results = await _db.ContestResults
            .AsNoTracking()
            .Where(item => contestIds.Contains(item.ContestId)
                && (item.Status == ContestResultStatus.Published
                    || item.Status == ContestResultStatus.Official))
            .OrderBy(item => item.Rank)
            .ThenBy(item => item.ParticipantId)
            .ToListAsync(cancellationToken);
        var standings = await _db.Standings
            .AsNoTracking()
            .Where(item => item.CompetitionId == competition.Id)
            .OrderBy(item => item.GroupId)
            .ThenBy(item => item.Position)
            .ToListAsync(cancellationToken);
        var athleticsCompetition = competition.SportId == Guid.Parse(
            "00000000-0000-0000-0000-000000000103");
        var medalRows = results
            .Select(item => new
            {
                item.ParticipantId,
                Medal = item.Position is not null
                    && (item.Position.Equals("Gold", StringComparison.OrdinalIgnoreCase)
                        || item.Position.Equals("Silver", StringComparison.OrdinalIgnoreCase)
                        || item.Position.Equals("Bronze", StringComparison.OrdinalIgnoreCase))
                    ? item.Position
                    : athleticsCompetition && item.Rank is >= 1 and <= 3
                        ? item.Rank == 1 ? "Gold" : item.Rank == 2 ? "Silver" : "Bronze"
                        : null
            })
            .Where(item => item.Medal is not null)
            .GroupBy(item => item.ParticipantId)
            .Select(group => new
            {
                ParticipantId = group.Key,
                ParticipantName = participantNames.GetValueOrDefault(group.Key,
                    "Participant"),
                Gold = group.Count(item => item.Medal!.Equals("Gold",
                    StringComparison.OrdinalIgnoreCase)),
                Silver = group.Count(item => item.Medal!.Equals("Silver",
                    StringComparison.OrdinalIgnoreCase)),
                Bronze = group.Count(item => item.Medal!.Equals("Bronze",
                    StringComparison.OrdinalIgnoreCase)),
                Total = group.Count()
            })
            .OrderByDescending(item => item.Gold)
            .ThenByDescending(item => item.Silver)
            .ThenByDescending(item => item.Bronze)
            .ThenBy(item => item.ParticipantName)
            .ToArray();
        var groupNames = competition.Editions
            .SelectMany(edition => edition.Stages)
            .SelectMany(stage => stage.Groups)
            .ToDictionary(group => group.Id, group => group.Name);
        var bracket = await _db.Brackets
            .AsNoTracking()
            .AsSplitQuery()
            .Include(item => item.Rounds)
            .ThenInclude(round => round.Matches)
            .FirstOrDefaultAsync(item => item.CompetitionId == competition.Id,
                cancellationToken);
        var sponsors = await (
                from sponsorship in _db.Sponsorships.AsNoTracking()
                join sponsor in _db.Sponsors.AsNoTracking()
                    on sponsorship.SponsorId equals sponsor.Id
                where sponsorship.TenantId == tenant.Id
                    && sponsor.IsActive
                    && sponsorship.Status == SponsorshipStatus.Active
                    && (sponsorship.CompetitionId == competition.Id
                        || (sponsorship.CompetitionId == null
                            && sponsorship.Placement == SponsorPlacements.CompetitionHeader))
                    && (!sponsorship.StartDate.HasValue
                        || sponsorship.StartDate <= DateTime.UtcNow)
                    && (!sponsorship.EndDate.HasValue
                        || sponsorship.EndDate >= DateTime.UtcNow)
                orderby sponsorship.DisplayOrder, sponsor.Name
                select new
                {
                    sponsor.Id,
                    sponsor.Name,
                    sponsor.LogoUrl,
                    sponsor.Website,
                    Tier = sponsor.Tier.ToString(),
                    sponsorship.Placement
                })
            .ToArrayAsync(cancellationToken);
        var scoringEventCount = await _db.LiveScores
            .AsNoTracking()
            .CountAsync(item => contestIds.Contains(item.ContestId), cancellationToken);
        var sport = await _db.Sports
            .AsNoTracking()
            .Include(item => item.Versions)
            .FirstOrDefaultAsync(item => item.Id == competition.SportId,
                cancellationToken);
        var ruleset = competition.RulesetVersionId.HasValue
            ? sport?.Versions.FirstOrDefault(
                item => item.Id == competition.RulesetVersionId.Value)
            : sport?.Versions
                .Where(item => item.IsPublished)
                .OrderByDescending(item => item.VersionNumber)
                .FirstOrDefault();
        var labels = CompetitionTabLabels(
            competition.SportId,
            ruleset?.DisplayConfiguration);
        var presentation = PublicCompetitionPresentation.Parse(
            competition.PresentationConfiguration);

        var availableTabs = new List<string> { "overview" };
        if (contests.Count > 0) availableTabs.Add("fixtures");
        if (results.Count > 0) availableTabs.Add("results");
        if (standings.Count > 0) availableTabs.Add("standings");
        if (standings.Select(item => item.GroupId).Distinct().Count() > 1)
            availableTabs.Add("groups");
        if (bracket is not null && presentation.BracketEnabled != false)
            availableTabs.Add("bracket");
        if ((bracket?.Rounds.SelectMany(round => round.Matches)
                .Any(match => match.SeriesId.HasValue) == true
                || contests.Any(contest => contest.SeriesId.HasValue))
            && presentation.SeriesEnabled != false)
            availableTabs.Add("series");
        if (competition.SportId == Guid.Parse(
                "00000000-0000-0000-0000-000000000102"))
            availableTabs.Add("leaderboard");
        if (medalRows.Length > 0 && presentation.MedalTableEnabled != false)
            availableTabs.Add("medals");
        if (participantIds.Length > 0) availableTabs.Add("participants");
        if (scoringEventCount > 0 || results.Count > 0)
            availableTabs.Add("statistics");
        if (presentation.VisibleTabs.Count > 0)
        {
            availableTabs = availableTabs
                .Where(presentation.VisibleTabs.Contains)
                .ToList();
            if (!availableTabs.Contains("overview")) availableTabs.Insert(0, "overview");
        }

        return Ok(new
        {
            competition.Id,
            competition.Name,
            competition.Description,
            competition.SportId,
            Sport = sport is null ? null : new { sport.Id, sport.Code, sport.Name },
            Region = tenant.Country,
            Status = competition.Status.ToString(),
            competition.PublishedAt,
            Presentation = new
            {
                presentation.PrimaryContestRenderer,
                presentation.DefaultTab,
                presentation.ScoreVariant,
                presentation.StandingsVariant,
                presentation.LeaderboardVariant,
                presentation.BracketEnabled,
                presentation.SeriesEnabled,
                presentation.MedalTableEnabled
            },
            Ruleset = ruleset is null ? null : new
            {
                ruleset.Id,
                ruleset.Name,
                ruleset.VersionNumber
            },
            Labels = labels,
            AvailableTabs = availableTabs,
            Editions = competition.Editions
                .OrderBy(item => item.StartDate)
                .Select(item => new
                {
                    item.Id,
                    item.Name,
                    item.StartDate,
                    item.EndDate,
                    Status = item.Status.ToString(),
                    Stages = item.Stages.OrderBy(stage => stage.SortOrder).Select(stage => new
                    {
                        stage.Id,
                        stage.Name,
                        StageType = stage.Type.ToString(),
                        stage.SortOrder
                    })
                }),
            Fixtures = contests.Select(contest => new
            {
                contest.Id,
                contest.Name,
                contest.ContestShape,
                Renderer = presentation.PrimaryContestRenderer
                    ?? PublicContestRenderer(contest.SportId, contest.ContestShape,
                        contest.Participants.Count),
                Status = contest.Status.ToString(),
                contest.StartTime,
                contest.EndTime,
                contest.Venue,
                Participants = contest.Participants
                    .OrderBy(item => item.SortOrder)
                    .Select(item => new
                    {
                        item.ParticipantId,
                        Name = participantNames.GetValueOrDefault(
                            item.ParticipantId,
                            "Participant")
                    })
            }),
            Results = results.Select(item => new
            {
                item.Id,
                item.ContestId,
                ContestName = contests.First(contest => contest.Id == item.ContestId)
                    .Name ?? contests.First(contest => contest.Id == item.ContestId).ContestShape,
                item.ParticipantId,
                ParticipantName = participantNames.GetValueOrDefault(
                    item.ParticipantId,
                    "Participant"),
                item.Rank,
                item.Position,
                item.Score,
                item.ScoreDisplay,
                item.Time,
                Status = item.Status.ToString()
            }),
            Standings = standings.Select(item => new
            {
                item.Id,
                item.GroupId,
                GroupName = groupNames.GetValueOrDefault(item.GroupId),
                item.ParticipantId,
                ParticipantName = participantNames.GetValueOrDefault(
                    item.ParticipantId,
                    "Participant"),
                item.Position,
                item.Played,
                item.Won,
                item.Drawn,
                item.Lost,
                item.PointsFor,
                item.PointsAgainst,
                item.PointDifferential,
                item.Points,
                item.Metadata
            }),
            Bracket = bracket is null ? null : new
            {
                bracket.Id,
                bracket.Name,
                Status = bracket.Status.ToString(),
                RoundCount = bracket.Rounds.Count,
                bracket.ChampionId,
                ChampionName = bracket.ChampionId.HasValue
                    ? participantNames.GetValueOrDefault(bracket.ChampionId.Value)
                    : null
            },
            Leaderboard = competition.SportId == Guid.Parse(
                    "00000000-0000-0000-0000-000000000102")
                ? results.OrderBy(item => item.Rank ?? int.MaxValue).Select(item => new
                {
                    item.ParticipantId,
                    ParticipantName = participantNames.GetValueOrDefault(
                        item.ParticipantId,
                        "Participant"),
                    item.Rank,
                    item.Score,
                    item.ScoreDisplay
                })
                : null,
            Medals = medalRows,
            Participants = participantIds.Select(participantId => new
            {
                Id = participantId,
                Name = participantNames.GetValueOrDefault(participantId, "Participant")
            }),
            Sponsors = sponsors,
            Statistics = new
            {
                ContestCount = contests.Count,
                CompletedContests = contests.Count(item => item.Status == ContestStatus.Completed),
                LiveContests = contests.Count(item => item.Status == ContestStatus.InProgress),
                UpcomingContests = contests.Count(item => item.Status is ContestStatus.Scheduled or ContestStatus.Ready),
                ParticipantCount = participantIds.Length,
                PublishedResultCount = results.Count,
                ScoringEventCount = scoringEventCount
            }
        });
    }

    [HttpGet("contests/{id:guid}")]
    public async Task<IActionResult> GetContest(string slug, Guid id, CancellationToken cancellationToken)
    {
        var tenant = await ResolveTenantAsync(slug, cancellationToken);
        if (tenant is null) return NotFound();

        var contest = await PublicContests(tenant.Id)
            .Include(x => x.Participants)
            .Include(x => x.Segments)
            .Include(x => x.Events)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (contest is null) return NotFound();

        Response.Headers.CacheControl = "no-store";

        var participantIds = contest.Participants.Select(x => x.ParticipantId).ToArray();
        var names = await _db.Participants
            .AsNoTracking()
            .Where(x => x.TenantId == tenant.Id && participantIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Name, cancellationToken);
        var results = await _db.ContestResults
            .AsNoTracking()
            .Where(x => x.ContestId == contest.Id &&
                        (x.Status == ContestResultStatus.Published || x.Status == ContestResultStatus.Official))
            .Select(x => new
            {
                x.ParticipantId,
                x.Rank,
                x.Position,
                x.Score,
                x.ScoreDisplay,
                x.Time
            })
            .ToListAsync(cancellationToken);
        var liveScores = await _db.LiveScores
            .AsNoTracking()
            .Where(x => x.ContestId == contest.Id)
            .OrderByDescending(x => x.ServerTimestamp)
            .Take(100)
            .OrderBy(x => x.ServerTimestamp)
            .Select(x => new
            {
                EventId = x.Id,
                x.ParticipantId,
                x.SecondaryParticipantId,
                x.SegmentId,
                x.EventType,
                x.MetricName,
                x.Value,
                x.DisplayValue,
                x.ClockSeconds,
                ScoredAt = x.ServerTimestamp
            })
            .ToListAsync(cancellationToken);
        var session = await _db.LiveScoreSessions
            .AsNoTracking()
            .Where(x => x.ContestId == contest.Id)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new
            {
                Status = x.Status.ToString(),
                x.ActiveSegmentId,
                x.ClockSeconds,
                x.ClockCountsDown,
                x.IsClockRunning,
                x.ClockStartedAt
            })
            .FirstOrDefaultAsync(cancellationToken);

        return Ok(new
        {
            contest.Id,
            contest.Name,
            contest.SportId,
            contest.ContestShape,
            Renderer = PublicContestRenderer(
                contest.SportId,
                contest.ContestShape,
                contest.Participants.Count),
            Status = contest.Status.ToString(),
            contest.Venue,
            contest.StartTime,
            contest.EndTime,
            contest.WinnerId,
            Configuration = new
            {
                contest.ParPerRound,
                contest.CourseName,
                contest.CourseLocation,
                contest.WeightClass,
                contest.CardSection,
                contest.ScheduledRounds,
                contest.ResultMethod,
                contest.ResultRound,
                contest.ResultTimeSeconds,
                contest.AthleticsEventName,
                contest.DistanceMeters,
                contest.AthleticsMetricType,
                contest.AthleticsMaxAttempts
            },
            Session = session,
            Participants = contest.Participants.OrderBy(x => x.SortOrder).Select(x => new
            {
                x.ParticipantId,
                Name = names.GetValueOrDefault(x.ParticipantId, "Participant"),
                x.ParticipantType,
                Status = x.Status.ToString(),
                x.SortOrder
            }),
            Segments = contest.Segments.OrderBy(x => x.SortOrder).Select(x => new
            {
                x.Id,
                x.Name,
                x.SegmentType,
                Status = x.Status.ToString(),
                x.SortOrder,
                x.IsOptional,
                x.ActivationCondition,
                NestedSegments = x.NestedSegments.OrderBy(nested => nested.SortOrder)
                    .Select(nested => new
                    {
                        nested.Id,
                        nested.Name,
                        nested.SegmentType,
                        Status = nested.Status.ToString(),
                        nested.SortOrder,
                        nested.IsOptional,
                        nested.ActivationCondition
                    })
            }),
            Events = contest.Events.OrderByDescending(x => x.CreatedAt).Take(100).Select(x => new
            {
                x.Id,
                x.EventType,
                x.ParticipantId,
                x.MetricValue,
                x.SequenceNumber,
                x.CreatedAt
            }),
            Results = results,
            LiveScores = liveScores
        });
    }

    [HttpGet("participants/{id:guid}")]
    public async Task<IActionResult> GetParticipant(string slug, Guid id, CancellationToken cancellationToken)
    {
        var tenant = await ResolveTenantAsync(slug, cancellationToken);
        if (tenant is null) return NotFound();

        var participant = await _db.Participants
            .AsNoTracking()
            .Where(item => item.TenantId == tenant.Id && item.Id == id)
            .FirstOrDefaultAsync(cancellationToken);
        if (participant is null) return NotFound();

        Response.Headers.CacheControl = "public, max-age=30, stale-while-revalidate=120";
        var athlete = participant.Type == ParticipantType.Athlete
            ? await _db.Participants.OfType<Athlete>()
                .AsNoTracking()
                .Include(item => item.SportRegistrations)
                .SingleAsync(item => item.Id == id, cancellationToken)
            : null;
        var team = participant.Type == ParticipantType.Team
            ? await _db.Participants.OfType<Team>()
                .AsNoTracking()
                .Include(item => item.Rosters)
                    .ThenInclude(roster => roster.Entries)
                .SingleAsync(item => item.Id == id, cancellationToken)
            : null;
        var club = participant.Type == ParticipantType.Club
            ? await _db.Participants.OfType<Club>()
                .AsNoTracking()
                .SingleAsync(item => item.Id == id, cancellationToken)
            : null;

        var sportIds = athlete?.SportRegistrations
            .Where(item => item.IsActive)
            .Select(item => item.SportId)
            .Distinct()
            .ToArray() ?? Array.Empty<Guid>();
        var sportNames = await _db.Sports.AsNoTracking()
            .Where(item => sportIds.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, item => item.Name, cancellationToken);
        var rosterAthleteIds = team?.Rosters
            .Where(roster => roster.IsActive)
            .SelectMany(roster => roster.Entries)
            .Where(entry => entry.IsActive)
            .Select(entry => entry.AthleteId)
            .Distinct()
            .ToArray() ?? Array.Empty<Guid>();
        var rosterAthleteNames = await _db.Participants.AsNoTracking()
            .Where(item => item.TenantId == tenant.Id
                && rosterAthleteIds.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, item => item.Name, cancellationToken);

        var now = DateTime.UtcNow;
        var upcoming = await PublicContests(tenant.Id)
            .Include(item => item.Participants)
            .Where(item => item.Participants.Any(entry => entry.ParticipantId == id)
                && item.Status != ContestStatus.Completed
                && item.Status != ContestStatus.PendingApproval
                && item.Status != ContestStatus.Approved
                && item.Status != ContestStatus.Published
                && item.Status != ContestStatus.Corrected
                && item.Status != ContestStatus.Abandoned
                && (!item.StartTime.HasValue || item.StartTime >= now))
            .OrderBy(item => item.StartTime)
            .Take(20)
            .ToListAsync(cancellationToken);
        var opponentIds = upcoming
            .SelectMany(item => item.Participants)
            .Select(item => item.ParticipantId)
            .Where(participantId => participantId != id)
            .Distinct()
            .ToArray();
        var opponentNames = await _db.Participants.AsNoTracking()
            .Where(item => item.TenantId == tenant.Id
                && opponentIds.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, item => item.Name, cancellationToken);

        var publicContestIds = PublicContests(tenant.Id).Select(item => item.Id);
        var results = await _db.ContestResults.AsNoTracking()
            .Where(item => item.ParticipantId == id
                && publicContestIds.Contains(item.ContestId)
                && (item.Status == ContestResultStatus.Published
                    || item.Status == ContestResultStatus.Official))
            .OrderByDescending(item => item.ApprovedAt)
            .Take(50)
            .ToListAsync(cancellationToken);
        var resultContestIds = results.Select(item => item.ContestId).Distinct().ToArray();
        var resultContests = await _db.Contests.AsNoTracking()
            .Where(item => item.TenantId == tenant.Id
                && resultContestIds.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, cancellationToken);

        object profile = athlete is not null
            ? new
            {
                Kind = "Athlete",
                athlete.Nationality,
                PhotoUrl = athlete.Photo,
                Sports = sportIds.Select(sportId => new
                {
                    Id = sportId,
                    Name = sportNames.GetValueOrDefault(sportId, "Sport")
                })
            }
            : team is not null
                ? new
                {
                    Kind = "Team",
                    team.HomeVenue,
                    team.Coach,
                    team.Manager,
                    Rosters = team.Rosters.Where(roster => roster.IsActive).Select(roster => new
                    {
                        roster.Id,
                        roster.Name,
                        roster.Season,
                        roster.CompetitionId,
                        Members = roster.Entries.Where(entry => entry.IsActive).Select(entry => new
                        {
                            entry.AthleteId,
                            Name = rosterAthleteNames.GetValueOrDefault(entry.AthleteId, "Athlete"),
                            entry.JerseyNumber,
                            entry.Position
                        })
                    })
                }
                : club is not null
                    ? new
                    {
                        Kind = "Club",
                        club.HomeVenue,
                        club.Coach,
                        club.Manager
                    }
                : new { Kind = participant.Type.ToString() };

        return Ok(new
        {
            participant.Id,
            participant.Name,
            participant.ShortName,
            participant.Logo,
            Type = participant.Type.ToString(),
            Status = participant.Status.ToString(),
            Profile = profile,
            UpcomingContests = upcoming.Select(contest => new
            {
                contest.Id,
                contest.Name,
                contest.SportId,
                contest.ContestShape,
                Status = contest.Status.ToString(),
                contest.Venue,
                contest.StartTime,
                Opponents = contest.Participants
                    .Where(entry => entry.ParticipantId != id)
                    .OrderBy(entry => entry.SortOrder)
                    .Select(entry => new
                    {
                        Id = entry.ParticipantId,
                        Name = opponentNames.GetValueOrDefault(entry.ParticipantId, "Participant")
                    })
            }),
            Results = results.Select(result => new
            {
                result.Id,
                result.ContestId,
                ContestName = resultContests.GetValueOrDefault(result.ContestId)?.Name
                    ?? "Published contest",
                ContestShape = resultContests.GetValueOrDefault(result.ContestId)?.ContestShape,
                StartTime = resultContests.GetValueOrDefault(result.ContestId)?.StartTime,
                result.Rank,
                result.Position,
                result.Score,
                result.ScoreDisplay,
                result.Time,
                Status = result.Status.ToString(),
                result.ApprovedAt
            }),
            Statistics = new
            {
                ResultCount = results.Count,
                Wins = results.Count(result => result.Rank == 1),
                Podiums = results.Count(result => result.Rank is >= 1 and <= 3),
                UpcomingContestCount = upcoming.Count
            }
        });
    }

    [HttpGet("fixtures")]
    public async Task<IActionResult> GetFixtures(
        string slug,
        [FromQuery] Guid? competitionId,
        [FromQuery] string? status,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken cancellationToken)
    {
        var tenant = await ResolveTenantAsync(slug, cancellationToken);
        if (tenant is null) return NotFound();

        IQueryable<Competition> competitionsQuery = PublishedCompetitions(tenant.Id)
            .Include(item => item.Editions)
            .ThenInclude(edition => edition.Stages);
        if (competitionId.HasValue)
            competitionsQuery = competitionsQuery.Where(item => item.Id == competitionId.Value);

        var competitions = await competitionsQuery.ToArrayAsync(cancellationToken);
        var fixtureSportIds = competitions.Select(item => item.SportId).Distinct().ToArray();
        var fixtureSports = await _db.Sports.AsNoTracking()
            .Where(item => fixtureSportIds.Contains(item.Id))
            .Select(item => new { item.Id, item.Code, item.Name })
            .ToDictionaryAsync(item => item.Id, cancellationToken);
        var stages = competitions
            .SelectMany(competition => competition.Editions.SelectMany(edition =>
                edition.Stages.Select(stage => new
                {
                    StageId = stage.Id,
                    CompetitionId = competition.Id,
                    CompetitionName = competition.Name,
                    competition.SportId,
                    competition.PresentationConfiguration,
                    SportCode = fixtureSports.GetValueOrDefault(competition.SportId)?.Code,
                    SportName = fixtureSports.GetValueOrDefault(competition.SportId)?.Name
                })))
            .ToArray();
        var stageIds = stages.Select(item => item.StageId).ToArray();
        var stageLookup = stages.ToDictionary(item => item.StageId);

        var query = PublicContests(tenant.Id)
            .AsSplitQuery()
            .Include(item => item.Participants)
            .Include(item => item.Metrics)
            .Include(item => item.Events)
            .Where(item => item.StageId.HasValue && stageIds.Contains(item.StageId.Value));
        if (!string.IsNullOrWhiteSpace(status)
            && Enum.TryParse<ContestStatus>(status, true, out var parsedStatus))
            query = query.Where(item => item.Status == parsedStatus);
        if (from.HasValue) query = query.Where(item => item.StartTime >= from.Value);
        if (to.HasValue) query = query.Where(item => item.StartTime < to.Value);

        var contests = await query
            .OrderBy(item => item.StartTime)
            .ThenBy(item => item.SortOrder)
            .Take(200)
            .ToArrayAsync(cancellationToken);
        var participantIds = contests
            .SelectMany(item => item.Participants)
            .Select(item => item.ParticipantId)
            .Distinct()
            .ToArray();
        var participantNames = await _db.Participants.AsNoTracking()
            .Where(item => item.TenantId == tenant.Id
                && participantIds.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, item => item.Name, cancellationToken);
        var contestIds = contests.Select(item => item.Id).ToArray();
        var publishedResults = await _db.ContestResults.AsNoTracking()
            .Where(item => contestIds.Contains(item.ContestId)
                && (item.Status == ContestResultStatus.Published
                    || item.Status == ContestResultStatus.Official))
            .Select(item => new
            {
                item.ContestId,
                item.ParticipantId,
                item.Rank,
                item.Position,
                item.Score,
                item.ScoreDisplay,
                ResultStatus = item.Status.ToString()
            })
            .ToArrayAsync(cancellationToken);
        var resultLookup = publishedResults.ToLookup(
            item => (item.ContestId, item.ParticipantId));
        var liveScoreRows = await _db.LiveScores.AsNoTracking()
            .Where(item => contestIds.Contains(item.ContestId)
                && item.MetricName == "score")
            .OrderBy(item => item.Sequence)
            .ThenBy(item => item.ServerTimestamp)
            .Select(item => new
            {
                item.ContestId,
                item.ParticipantId,
                item.Value,
                item.DisplayValue
            })
            .ToArrayAsync(cancellationToken);
        var liveScoreLookup = liveScoreRows
            .GroupBy(item => (item.ContestId, item.ParticipantId))
            .ToDictionary(
                group => group.Key,
                group => (
                    Score: group.Sum(item => item.Value),
                    Display: group.Count() == 1
                        ? group.Single().DisplayValue
                        : null));

        Response.Headers["Cache-Control"] =
            "public, max-age=30, stale-while-revalidate=120";
        return Ok(contests.Select(contest =>
        {
            var stage = stageLookup[contest.StageId!.Value];
            var presentation = PublicCompetitionPresentation.Parse(
                stage.PresentationConfiguration);
            var presentationMetrics = contest.Metrics
                .Where(item => item.Name.StartsWith("presentation.",
                    StringComparison.OrdinalIgnoreCase))
                .GroupBy(item => item.Name["presentation.".Length..],
                    StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key,
                    group => group.OrderByDescending(item => item.RecordedAt)
                        .First().TextValue,
                    StringComparer.OrdinalIgnoreCase);
            return new
            {
                contest.Id,
                stage.CompetitionId,
                stage.CompetitionName,
                stage.SportId,
                stage.SportCode,
                stage.SportName,
                contest.Name,
                contest.ContestShape,
                Renderer = presentation.PrimaryContestRenderer
                    ?? PublicContestRenderer(
                    contest.SportId,
                    contest.ContestShape,
                    contest.Participants.Count),
                Status = contest.Status.ToString(),
                contest.StartTime,
                contest.EndTime,
                contest.Venue,
                Presentation = new
                {
                    presentation.ScoreVariant,
                    StageLabel = presentationMetrics.GetValueOrDefault("stageLabel"),
                    PeriodLabel = presentationMetrics.GetValueOrDefault("periodLabel"),
                    Clock = presentationMetrics.GetValueOrDefault("clock"),
                    Summary = presentationMetrics.GetValueOrDefault("summary"),
                    contest.SeriesGameNumber,
                    contest.SeriesBestOf,
                    EventSummary = contest.Events
                        .Where(item => item.Status == EventStatus.Approved)
                        .GroupBy(item => new { item.ParticipantId, item.EventType })
                        .Select(group => new
                        {
                            group.Key.ParticipantId,
                            group.Key.EventType,
                            Count = group.Count()
                        })
                        .OrderBy(item => item.ParticipantId)
                        .ThenBy(item => item.EventType)
                },
                Participants = contest.Participants
                    .OrderBy(item => item.SortOrder)
                    .Select(item => new
                    {
                        item.ParticipantId,
                        Name = participantNames.GetValueOrDefault(
                            item.ParticipantId,
                            "Participant"),
                        Score = resultLookup[(contest.Id, item.ParticipantId)]
                            .Select(result => result.Score)
                            .FirstOrDefault()
                            ?? (liveScoreLookup.TryGetValue(
                                (contest.Id, item.ParticipantId), out var liveScore)
                                ? liveScore.Score
                                : null),
                        ScoreDisplay = resultLookup[(contest.Id, item.ParticipantId)]
                            .Select(result => result.ScoreDisplay)
                            .FirstOrDefault()
                            ?? (liveScoreLookup.TryGetValue(
                                (contest.Id, item.ParticipantId), out var liveDisplay)
                                ? liveDisplay.Display
                                : null),
                        Position = resultLookup[(contest.Id, item.ParticipantId)]
                            .Select(result => result.Position)
                            .FirstOrDefault(),
                        Rank = resultLookup[(contest.Id, item.ParticipantId)]
                            .Select(result => result.Rank)
                            .FirstOrDefault(),
                        ResultStatus = resultLookup[(contest.Id, item.ParticipantId)]
                            .Select(result => result.ResultStatus)
                            .FirstOrDefault()
                    })
            };
        }));
    }

    [HttpGet("results")]
    public async Task<IActionResult> GetResults(
        string slug,
        [FromQuery] Guid? contestId,
        CancellationToken cancellationToken)
    {
        var tenant = await ResolveTenantAsync(slug, cancellationToken);
        if (tenant is null) return NotFound();

        var publicContestIds = PublicContests(tenant.Id).Select(x => x.Id);
        var query = _db.ContestResults.AsNoTracking().Where(
            x => publicContestIds.Contains(x.ContestId) &&
                 (x.Status == ContestResultStatus.Published ||
                  x.Status == ContestResultStatus.Official));
        if (contestId.HasValue) query = query.Where(x => x.ContestId == contestId.Value);

        var rows = await query
            .OrderByDescending(x => x.ApprovedAt)
            .Take(100)
            .Select(x => new
            {
                x.Id,
                x.ContestId,
                x.ParticipantId,
                x.Rank,
                x.Position,
                x.Score,
                x.ScoreDisplay,
                x.Time,
                Status = x.Status.ToString(),
                x.ApprovedAt
            })
            .ToListAsync(cancellationToken);

        var participantIds = rows.Select(x => x.ParticipantId).Distinct().ToArray();
        var names = await _db.Participants.AsNoTracking()
            .Where(x => x.TenantId == tenant.Id && participantIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Name, cancellationToken);
        var resultContestIds = rows.Select(item => item.ContestId).Distinct().ToArray();
        var contestNames = await _db.Contests.AsNoTracking()
            .Where(item => item.TenantId == tenant.Id
                && resultContestIds.Contains(item.Id))
            .ToDictionaryAsync(
                item => item.Id,
                item => item.Name ?? item.ContestShape,
                cancellationToken);
        return Ok(rows.Select(x => new
        {
            x.Id,
            x.ContestId,
            ContestName = contestNames.GetValueOrDefault(x.ContestId, "Contest"),
            x.ParticipantId,
            ParticipantName = names.GetValueOrDefault(x.ParticipantId, "Participant"),
            x.Rank,
            x.Position,
            x.Score,
            x.ScoreDisplay,
            x.Time,
            x.Status,
            x.ApprovedAt
        }));
    }

    [HttpGet("standings")]
    public async Task<IActionResult> GetStandings(
        string slug,
        [FromQuery] Guid? competitionId,
        CancellationToken cancellationToken)
    {
        var tenant = await ResolveTenantAsync(slug, cancellationToken);
        if (tenant is null) return NotFound();

        var competitionIds = PublishedCompetitions(tenant.Id).Select(x => x.Id);
        var query = _db.Standings.AsNoTracking()
            .Where(x => competitionIds.Contains(x.CompetitionId));
        if (competitionId.HasValue)
            query = query.Where(x => x.CompetitionId == competitionId.Value);

        var rows = await query
            .OrderBy(x => x.CompetitionId)
            .ThenBy(x => x.Position)
            .Select(x => new
            {
                x.Id,
                x.CompetitionId,
                x.GroupId,
                x.ParticipantId,
                x.Position,
                x.Played,
                x.Won,
                x.Drawn,
                x.Lost,
                x.PointsFor,
                x.PointsAgainst,
                x.PointDifferential,
                x.Points,
                x.Metadata,
                x.UpdatedAt
            })
            .ToListAsync(cancellationToken);

        var participantIds = rows.Select(x => x.ParticipantId).Distinct().ToArray();
        var names = await _db.Participants.AsNoTracking()
            .Where(x => x.TenantId == tenant.Id && participantIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Name, cancellationToken);

        var rowCompetitionIds = rows.Select(item => item.CompetitionId).Distinct().ToArray();
        var competitionNames = await _db.Competitions.AsNoTracking()
            .Where(item => rowCompetitionIds.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, item => item.Name, cancellationToken);
        var competitionsWithGroups = await _db.Competitions.AsNoTracking()
            .Include(item => item.Editions)
            .ThenInclude(edition => edition.Stages)
            .ThenInclude(stage => stage.Groups)
            .Where(item => rowCompetitionIds.Contains(item.Id))
            .ToArrayAsync(cancellationToken);
        var groupNames = competitionsWithGroups.SelectMany(item => item.Editions)
            .SelectMany(item => item.Stages)
            .SelectMany(item => item.Groups)
            .ToDictionary(item => item.Id, item => item.Name);

        return Ok(rows.Select(x => new
        {
            x.Id,
            x.CompetitionId,
            CompetitionName = competitionNames.GetValueOrDefault(
                x.CompetitionId,
                "Competition"),
            x.GroupId,
            GroupName = x.GroupId == Guid.Empty
                ? null
                : groupNames.GetValueOrDefault(x.GroupId, "Group"),
            x.ParticipantId,
            ParticipantName = names.GetValueOrDefault(x.ParticipantId, "Participant"),
            x.Position,
            x.Played,
            x.Won,
            x.Drawn,
            x.Lost,
            x.PointsFor,
            x.PointsAgainst,
            x.PointDifferential,
            x.Points,
            x.Metadata,
            x.UpdatedAt
        }));
    }

    [HttpGet("playoffs")]
    public async Task<IActionResult> GetPlayoffs(
        string slug,
        [FromQuery] Guid competitionId,
        CancellationToken cancellationToken)
    {
        var tenant = await ResolveTenantAsync(slug, cancellationToken);
        if (tenant is null) return NotFound();
        var competition = await PublishedCompetitions(tenant.Id)
            .AsSplitQuery()
            .Include(item => item.Editions)
            .ThenInclude(edition => edition.Stages)
            .SingleOrDefaultAsync(item => item.Id == competitionId, cancellationToken);
        if (competition is null) return NotFound();

        var bracket = await _db.Brackets
            .AsNoTracking()
            .Include(item => item.Rounds)
            .ThenInclude(round => round.Matches)
            .SingleOrDefaultAsync(
                item => item.CompetitionId == competitionId,
                cancellationToken);
        if (bracket is null)
        {
            var stageIds = competition.Editions
                .SelectMany(edition => edition.Stages)
                .Select(stage => stage.Id)
                .ToArray();
            var games = await PublicContests(tenant.Id)
                .AsNoTracking()
                .Include(item => item.Participants)
                .Where(item => item.StageId.HasValue
                    && stageIds.Contains(item.StageId.Value)
                    && item.SeriesId.HasValue)
                .OrderBy(item => item.SeriesGameNumber)
                .ToArrayAsync(cancellationToken);
            if (games.Length == 0) return NotFound();

            var gameIds = games.Select(item => item.Id).ToArray();
            var seriesParticipantIds = games
                .SelectMany(item => item.Participants)
                .Select(item => item.ParticipantId)
                .Distinct()
                .ToArray();
            var seriesNames = await _db.Participants.AsNoTracking()
                .Where(item => item.TenantId == tenant.Id
                    && seriesParticipantIds.Contains(item.Id))
                .ToDictionaryAsync(item => item.Id, item => item.Name,
                    cancellationToken);
            var gameResults = await _db.ContestResults.AsNoTracking()
                .Where(item => gameIds.Contains(item.ContestId)
                    && (item.Status == ContestResultStatus.Published
                        || item.Status == ContestResultStatus.Official))
                .Select(item => new
                {
                    item.ContestId,
                    item.ParticipantId,
                    item.Rank,
                    item.Score,
                    item.ScoreDisplay
                })
                .ToArrayAsync(cancellationToken);
            var gameResultLookup = gameResults.ToLookup(
                item => (item.ContestId, item.ParticipantId));
            var stages = competition.Editions.SelectMany(item => item.Stages)
                .ToDictionary(item => item.Id, item => item.Name);
            var groupedByStage = games
                .GroupBy(item => item.StageId!.Value)
                .OrderBy(group => competition.Editions
                    .SelectMany(item => item.Stages)
                    .First(stage => stage.Id == group.Key).SortOrder)
                .ToArray();

            return Ok(new
            {
                Id = competition.Id,
                CompetitionId = competition.Id,
                Name = $"{competition.Name} Series",
                Status = competition.Status.ToString(),
                ChampionName = (string?)null,
                Rounds = groupedByStage.Select((stage, roundIndex) => new
                {
                    Id = stage.Key,
                    RoundNumber = roundIndex + 1,
                    Name = stages.GetValueOrDefault(stage.Key, "Series"),
                    Matches = stage.GroupBy(item => item.SeriesId!.Value)
                        .Select((series, matchIndex) =>
                        {
                            var orderedGames = series
                                .OrderBy(item => item.SeriesGameNumber)
                                .ToArray();
                            var first = orderedGames[0];
                            var participants = first.Participants
                                .OrderBy(item => item.SortOrder)
                                .Select(item => item.ParticipantId)
                                .ToArray();
                            Guid? GameWinner(Contest game)
                            {
                                if (game.WinnerId.HasValue) return game.WinnerId;
                                foreach (var participant in game.Participants)
                                    if (gameResultLookup[(game.Id, participant.ParticipantId)]
                                        .Any(result => result.Rank == 1))
                                        return participant.ParticipantId;
                                return null;
                            }
                            var wins = orderedGames
                                .Select(GameWinner)
                                .Where(item => item.HasValue)
                                .GroupBy(item => item!.Value)
                                .ToDictionary(group => group.Key, group => group.Count());
                            var requiredWins = (first.SeriesBestOf ?? 1) / 2 + 1;
                            var winnerId = wins.FirstOrDefault(item => item.Value >= requiredWins).Key;
                            return new
                            {
                                Id = series.Key,
                                ContestId = first.Id,
                                SeriesId = series.Key,
                                BestOf = first.SeriesBestOf ?? orderedGames.Length,
                                IsAggregateTie = false,
                                IsThirdPlace = false,
                                Score1 = participants.ElementAtOrDefault(0) != Guid.Empty
                                    ? wins.GetValueOrDefault(participants[0]).ToString()
                                    : null,
                                Score2 = participants.ElementAtOrDefault(1) != Guid.Empty
                                    ? wins.GetValueOrDefault(participants[1]).ToString()
                                    : null,
                                MatchNumber = matchIndex + 1,
                                Participant1Name = participants.ElementAtOrDefault(0) != Guid.Empty
                                    ? seriesNames.GetValueOrDefault(participants[0])
                                    : null,
                                Participant2Name = participants.ElementAtOrDefault(1) != Guid.Empty
                                    ? seriesNames.GetValueOrDefault(participants[1])
                                    : null,
                                WinnerName = winnerId != Guid.Empty
                                    ? seriesNames.GetValueOrDefault(winnerId)
                                    : null,
                                Status = winnerId != Guid.Empty ? "Completed" : "InProgress",
                                SeriesGames = orderedGames.Select(game =>
                                {
                                    var gameParticipants = game.Participants
                                        .OrderBy(item => item.SortOrder)
                                        .Select(item => item.ParticipantId)
                                        .ToArray();
                                    string? Score(Guid participantId) => gameResultLookup[
                                            (game.Id, participantId)]
                                        .Select(result => result.ScoreDisplay
                                            ?? result.Score.ToString())
                                        .FirstOrDefault();
                                    var gameWinnerId = GameWinner(game);
                                    return new
                                    {
                                        game.Id,
                                        game.SeriesGameNumber,
                                        HomeParticipantName = gameParticipants.ElementAtOrDefault(0) != Guid.Empty
                                            ? seriesNames.GetValueOrDefault(gameParticipants[0])
                                            : null,
                                        AwayParticipantName = gameParticipants.ElementAtOrDefault(1) != Guid.Empty
                                            ? seriesNames.GetValueOrDefault(gameParticipants[1])
                                            : null,
                                        Status = game.Status.ToString(),
                                        WinnerName = gameWinnerId.HasValue
                                            ? seriesNames.GetValueOrDefault(gameWinnerId.Value)
                                            : null,
                                        HomeScore = gameParticipants.ElementAtOrDefault(0) != Guid.Empty
                                            ? Score(gameParticipants[0]) : null,
                                        AwayScore = gameParticipants.ElementAtOrDefault(1) != Guid.Empty
                                            ? Score(gameParticipants[1]) : null
                                    };
                                })
                            };
                        })
                })
            });
        }

        var participantIds = bracket.Rounds
            .SelectMany(round => round.Matches)
            .SelectMany(match => new[]
            {
                match.Participant1Id,
                match.Participant2Id,
                match.WinnerId
            })
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Append(bracket.ChampionId ?? Guid.Empty)
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToArray();
        var names = await _db.Participants
            .AsNoTracking()
            .Where(item => item.TenantId == tenant.Id
                && participantIds.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, item => item.Name, cancellationToken);
        var seriesIds = bracket.Rounds
            .SelectMany(round => round.Matches)
            .Where(match => match.SeriesId.HasValue)
            .Select(match => match.SeriesId!.Value)
            .Distinct()
            .ToArray();
        var seriesGames = await _db.Contests
            .AsNoTracking()
            .Include(item => item.Participants)
            .Where(item => item.SeriesId.HasValue
                && seriesIds.Contains(item.SeriesId.Value))
            .OrderBy(item => item.SeriesGameNumber)
            .ToArrayAsync(cancellationToken);
        var seriesGameIds = seriesGames.Select(item => item.Id).ToArray();
        var seriesGameResults = await _db.ContestResults.AsNoTracking()
            .Where(item => seriesGameIds.Contains(item.ContestId)
                && (item.Status == ContestResultStatus.Published
                    || item.Status == ContestResultStatus.Official))
            .Select(item => new
            {
                item.ContestId,
                item.ParticipantId,
                item.Score,
                item.ScoreDisplay
            })
            .ToArrayAsync(cancellationToken);
        var seriesResultLookup = seriesGameResults.ToLookup(
            item => (item.ContestId, item.ParticipantId));

        return Ok(new
        {
            bracket.Id,
            bracket.CompetitionId,
            bracket.Name,
            Status = bracket.Status.ToString(),
            bracket.ChampionId,
            ChampionName = bracket.ChampionId.HasValue
                ? names.GetValueOrDefault(bracket.ChampionId.Value)
                : null,
            Rounds = bracket.Rounds
                .OrderBy(round => round.RoundNumber)
                .Select(round => new
                {
                    round.Id,
                    round.RoundNumber,
                    round.Name,
                    Matches = round.Matches
                        .OrderBy(match => match.MatchNumber)
                        .Select(match => new
                        {
                            match.Id,
                            match.ContestId,
                            match.SeriesId,
                            match.BestOf,
                            match.IsAggregateTie,
                            match.IsThirdPlace,
                            match.Score1,
                            match.Score2,
                            match.MatchNumber,
                            match.Participant1Id,
                            Participant1Name = match.Participant1Id.HasValue
                                ? names.GetValueOrDefault(match.Participant1Id.Value)
                                : null,
                            match.Participant2Id,
                            Participant2Name = match.Participant2Id.HasValue
                                ? names.GetValueOrDefault(match.Participant2Id.Value)
                                : null,
                            match.WinnerId,
                            WinnerName = match.WinnerId.HasValue
                                ? names.GetValueOrDefault(match.WinnerId.Value)
                                : null,
                            Status = match.Status.ToString(),
                            SeriesGames = match.SeriesId.HasValue
                                ? seriesGames
                                    .Where(game =>
                                        game.SeriesId == match.SeriesId)
                                    .Select(game => (object)new
                                    {
                                        game.Id,
                                        game.SeriesGameNumber,
                                        HomeParticipantName = game.Participants
                                            .Where(item => item.SortOrder == 1)
                                            .Select(item => names.GetValueOrDefault(item.ParticipantId))
                                            .FirstOrDefault(),
                                        AwayParticipantName = game.Participants
                                            .Where(item => item.SortOrder == 2)
                                            .Select(item => names.GetValueOrDefault(item.ParticipantId))
                                            .FirstOrDefault(),
                                        Status = game.Status.ToString(),
                                        game.WinnerId,
                                        WinnerName = game.WinnerId.HasValue
                                            ? names.GetValueOrDefault(
                                                game.WinnerId.Value)
                                            : null,
                                        HomeScore = game.Participants
                                            .Where(item => item.SortOrder == 1)
                                            .Select(item => seriesResultLookup[
                                                (game.Id, item.ParticipantId)]
                                                .Select(result => result.ScoreDisplay
                                                    ?? result.Score.ToString())
                                                .FirstOrDefault())
                                            .FirstOrDefault(),
                                        AwayScore = game.Participants
                                            .Where(item => item.SortOrder == 2)
                                            .Select(item => seriesResultLookup[
                                                (game.Id, item.ParticipantId)]
                                                .Select(result => result.ScoreDisplay
                                                    ?? result.Score.ToString())
                                                .FirstOrDefault())
                                            .FirstOrDefault()
                                    })
                                : Array.Empty<object>()
                        })
                })
        });
    }

    [HttpGet("golf/leaderboard")]
    public async Task<IActionResult> GetGolfLeaderboard(
        string slug,
        [FromQuery] Guid competitionId,
        CancellationToken cancellationToken)
    {
        var tenant = await ResolveTenantAsync(slug, cancellationToken);
        if (tenant is null) return NotFound();
        var competition = await PublishedCompetitions(tenant.Id)
            .Include(item => item.Editions)
            .ThenInclude(edition => edition.Stages)
            .SingleOrDefaultAsync(item => item.Id == competitionId, cancellationToken);
        if (competition is null) return NotFound();
        var stageIds = competition.Editions.SelectMany(item => item.Stages)
            .Select(item => item.Id)
            .ToArray();
        var contest = await _db.Contests
            .AsNoTracking()
            .Include(item => item.Participants)
            .Include(item => item.Segments)
            .ThenInclude(item => item.NestedSegments)
            .Where(item => item.StageId.HasValue
                && stageIds.Contains(item.StageId.Value)
                && item.ContestShape == "MultiRoundLeaderboard")
            .OrderBy(item => item.Status == ContestStatus.Published
                ? 0
                : item.Status == ContestStatus.InProgress ? 1 : 2)
            .ThenBy(item => item.StartTime)
            .FirstOrDefaultAsync(cancellationToken);
        if (contest is null) return NotFound();

        var participantIds = contest.Participants
            .Select(item => item.ParticipantId)
            .ToArray();
        var names = await _db.Participants
            .AsNoTracking()
            .Where(item => item.TenantId == tenant.Id
                && participantIds.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, item => item.Name, cancellationToken);
        var holeById = contest.Segments
            .SelectMany(round => round.NestedSegments.Select(
                hole => new { hole.Id, Round = round.SortOrder, Hole = hole.SortOrder }))
            .ToDictionary(item => item.Id);
        var holeIds = holeById.Keys.ToArray();
        var holePars = JsonSerializer.Deserialize<int[]>(contest.HoleParsJson ?? "[]")
            ?? Array.Empty<int>();
        var scores = await _db.LiveScores
            .AsNoTracking()
            .Where(item => item.ContestId == contest.Id
                && item.SegmentId.HasValue
                && holeIds.Contains(item.SegmentId.Value)
                && (item.MetricName == "strokes"
                    || item.MetricName == "penalties"))
            .ToArrayAsync(cancellationToken);
        var participantEntries = contest.Participants.Select(participant =>
        {
            var participantScores = scores
                .Where(item => item.ParticipantId == participant.ParticipantId)
                .ToArray();
            var rounds = participantScores
                .GroupBy(item => holeById[item.SegmentId!.Value].Round)
                .ToDictionary(
                    group => group.Key,
                    group => decimal.ToInt32(group.Sum(item => item.Value)));
            var currentRound = rounds.Count == 0 ? 0 : rounds.Keys.Max();
            var currentRoundHoles = participantScores
                .Where(item => currentRound > 0
                    && holeById[item.SegmentId!.Value].Round == currentRound)
                .GroupBy(item => item.SegmentId!.Value)
                .Select(group => new
                {
                    Hole = holeById[group.Key].Hole,
                    Score = decimal.ToInt32(group.Sum(item => item.Value))
                })
                .ToArray();
            var todayPar = currentRoundHoles.Sum(item =>
                item.Hole > 0 && item.Hole <= holePars.Length
                    ? holePars[item.Hole - 1]
                    : (contest.ParPerRound ?? 72) / 18);
            var total = rounds.Values.Sum();
            var toPar = total - (rounds.Count * (contest.ParPerRound ?? 72));
            var status = participant.Status switch
            {
                ContestParticipantStatus.Withdrawn => "Withdrawn",
                ContestParticipantStatus.Disqualified => "Disqualified",
                _ when participant.MadeCut == false => "MissedCut",
                _ when participant.MadeCut == true => "MadeCut",
                _ => "Active"
            };
            var statusOrder = status switch
            {
                "MadeCut" or "Active" => 0,
                "MissedCut" => 1,
                "Withdrawn" => 2,
                _ => 3
            };
            return new
            {
                participant.ParticipantId,
                Name = names.GetValueOrDefault(participant.ParticipantId),
                participant.MadeCut,
                Status = status,
                StatusOrder = statusOrder,
                TotalStrokes = total,
                ToPar = toPar,
                RoundsPlayed = rounds.Count,
                TodayToPar = currentRoundHoles.Sum(item => item.Score) - todayPar,
                Thru = currentRoundHoles.Length,
                RoundScores = Enumerable.Range(1, 4)
                    .Select(round => rounds.GetValueOrDefault(round))
                    .ToArray()
            };
        })
        .OrderBy(item => item.StatusOrder)
        .ThenBy(item => item.ToPar)
        .ThenBy(item => item.TotalStrokes)
        .ThenBy(item => item.Name)
        .ToArray();

        var positionsByRound = Enumerable.Range(1, 4).ToDictionary(
            roundNumber => roundNumber,
            roundNumber =>
            {
                var eligible = participantEntries
                    .Where(item => item.Status != "Disqualified"
                        && item.RoundScores[roundNumber - 1] > 0)
                    .Select(item => new
                    {
                        item.ParticipantId,
                        Total = item.RoundScores.Take(roundNumber).Sum()
                    })
                    .OrderBy(item => item.Total)
                    .ThenBy(item => item.ParticipantId)
                    .ToArray();
                var positions = new Dictionary<Guid, int>();
                int? previousTotal = null;
                var previousPosition = 0;
                for (var index = 0; index < eligible.Length; index++)
                {
                    var position = previousTotal == eligible[index].Total
                        ? previousPosition
                        : index + 1;
                    positions[eligible[index].ParticipantId] = position;
                    previousTotal = eligible[index].Total;
                    previousPosition = position;
                }
                return positions;
            });

        var positioned = new List<object>();
        var previousPosition = 0;
        int? previousStatusOrder = null;
        int? previousToPar = null;
        for (var index = 0; index < participantEntries.Length; index++)
        {
            var entry = participantEntries[index];
            var tied = previousStatusOrder == entry.StatusOrder
                && previousToPar == entry.ToPar;
            var position = tied ? previousPosition : index + 1;
            positioned.Add(new
            {
                Position = position,
                IsTied = tied || (index + 1 < participantEntries.Length
                    && participantEntries[index + 1].StatusOrder == entry.StatusOrder
                    && participantEntries[index + 1].ToPar == entry.ToPar),
                entry.ParticipantId,
                entry.Name,
                entry.MadeCut,
                entry.Status,
                entry.TotalStrokes,
                entry.ToPar,
                entry.RoundsPlayed,
                entry.TodayToPar,
                entry.Thru,
                entry.RoundScores,
                RoundPositions = Enumerable.Range(1, 4)
                    .Select(roundNumber => positionsByRound[roundNumber]
                        .TryGetValue(entry.ParticipantId, out var position)
                            ? (int?)position
                            : null)
                    .ToArray()
            });
            previousPosition = position;
            previousStatusOrder = entry.StatusOrder;
            previousToPar = entry.ToPar;
        }

        return Ok(new
        {
            contest.Id,
            contest.Name,
            Status = contest.Status.ToString(),
            contest.ParPerRound,
            contest.CourseName,
            contest.CourseLocation,
            HolePars = holePars,
            contest.CutAfterRound,
            contest.CutToParticipants,
            contest.CutAppliedAt,
            ScoringDirection = "LowerIsBetter",
            Entries = positioned
        });
    }

    [HttpGet("combat/card")]
    public async Task<IActionResult> GetCombatCard(
        string slug,
        [FromQuery] Guid competitionId,
        CancellationToken cancellationToken)
    {
        var tenant = await ResolveTenantAsync(slug, cancellationToken);
        if (tenant is null) return NotFound();
        var competition = await PublishedCompetitions(tenant.Id)
            .Include(item => item.Editions)
            .ThenInclude(edition => edition.Stages)
            .SingleOrDefaultAsync(item => item.Id == competitionId, cancellationToken);
        if (competition is null) return NotFound();
        var stageIds = competition.Editions
            .SelectMany(edition => edition.Stages)
            .Where(stage => stage.Type == StageType.FightCard)
            .Select(stage => stage.Id)
            .ToArray();
        var bouts = await _db.Contests
            .AsNoTracking()
            .Include(contest => contest.Participants)
            .Include(contest => contest.Officials)
            .Include(contest => contest.Events)
            .Where(contest => contest.TenantId == tenant.Id
                && contest.StageId.HasValue
                && stageIds.Contains(contest.StageId.Value)
                && contest.ContestShape == "Bout"
                && contest.Status != ContestStatus.Cancelled)
            .OrderBy(contest => contest.SortOrder)
            .ToArrayAsync(cancellationToken);
        if (bouts.Length == 0) return NotFound();

        var participantIds = bouts.SelectMany(bout => bout.Participants)
            .Select(participant => participant.ParticipantId)
            .Distinct()
            .ToArray();
        var names = await _db.Participants
            .AsNoTracking()
            .Where(participant => participant.TenantId == tenant.Id
                && participantIds.Contains(participant.Id))
            .ToDictionaryAsync(
                participant => participant.Id,
                participant => participant.Name,
                cancellationToken);

        return Ok(new
        {
            competition.Id,
            competition.Name,
            Status = competition.Status.ToString(),
            BoutCount = bouts.Length,
            CompletedBoutCount = bouts.Count(bout => bout.ResultMethod is not null),
            AllowedResultMethods = bouts.FirstOrDefault()?.GetAllowedResultMethods()
                ?? Array.Empty<string>(),
            Bouts = bouts.Select(bout =>
            {
                var orderedParticipants = bout.Participants
                    .OrderBy(participant => participant.SortOrder)
                    .ToArray();
                var scorecards = bout.Events
                    .Where(item => item.EventType == "JudgeScorecard"
                        && item.Status == EventStatus.Approved
                        && item.MetricValue is not null)
                    .Select(item =>
                    {
                        try
                        {
                            return JsonSerializer.Deserialize<CombatJudgeScorecardRequest>(
                                item.MetricValue!);
                        }
                        catch (JsonException)
                        {
                            return null;
                        }
                    })
                    .Where(item => item is not null);
                return new
                {
                    bout.Id,
                    bout.Name,
                    BoutNumber = bout.SortOrder,
                    bout.CardSection,
                    bout.WeightClass,
                    bout.ScheduledRounds,
                    Status = bout.Status.ToString(),
                    Fighters = orderedParticipants.Select(participant => new
                    {
                        participant.ParticipantId,
                        Name = names.GetValueOrDefault(participant.ParticipantId),
                        Corner = participant.SortOrder == 1 ? "Red" : "Blue"
                    }),
                    Officials = bout.Officials.Select(official => new
                    {
                        official.OfficialId,
                        official.DisplayName,
                        official.Role
                    }),
                    bout.WinnerId,
                    WinnerName = bout.WinnerId.HasValue
                        ? names.GetValueOrDefault(bout.WinnerId.Value)
                        : null,
                    bout.ResultMethod,
                    bout.ResultRound,
                    bout.ResultTimeSeconds,
                    bout.ResultNotes,
                    Scorecards = scorecards
                };
            })
        });
    }

    [HttpGet("athletics/meet")]
    public async Task<IActionResult> GetAthleticsMeet(
        string slug,
        [FromQuery] Guid competitionId,
        CancellationToken cancellationToken)
    {
        var tenant = await ResolveTenantAsync(slug, cancellationToken);
        if (tenant is null) return NotFound();
        var competition = await PublishedCompetitions(tenant.Id)
            .Include(item => item.Editions)
            .ThenInclude(edition => edition.Stages)
            .SingleOrDefaultAsync(item => item.Id == competitionId, cancellationToken);
        if (competition is null) return NotFound();
        var stageIds = competition.Editions.SelectMany(edition => edition.Stages)
            .Where(stage => stage.Type == StageType.Heat)
            .Select(stage => stage.Id)
            .ToArray();
        var races = await _db.Contests
            .AsNoTracking()
            .Include(contest => contest.Participants)
            .Where(contest => contest.TenantId == tenant.Id
                && contest.StageId.HasValue
                && stageIds.Contains(contest.StageId.Value)
                && (contest.ContestShape == "Heat"
                    || contest.ContestShape == "AthleticsSemifinal"
                    || contest.ContestShape == "AthleticsFinal")
                && contest.Status != ContestStatus.Cancelled)
            .OrderBy(contest => contest.SortOrder)
            .ToArrayAsync(cancellationToken);
        if (races.Length == 0) return NotFound();

        var participantIds = races.SelectMany(race => race.Participants)
            .Select(participant => participant.ParticipantId)
            .Distinct()
            .ToArray();
        var names = await _db.Participants
            .AsNoTracking()
            .Where(participant => participant.TenantId == tenant.Id
                && participantIds.Contains(participant.Id))
            .ToDictionaryAsync(
                participant => participant.Id,
                participant => participant.Name,
                cancellationToken);
        var raceIds = races.Select(race => race.Id).ToArray();
        var scores = await _db.LiveScores
            .AsNoTracking()
            .Where(score => raceIds.Contains(score.ContestId)
                && score.MetricName == "finish_time_ms")
            .ToArrayAsync(cancellationToken);

        object ProjectRace(Contest race, bool includeMedals)
        {
            var entries = race.Participants.Select(participant => new
                {
                    Participant = participant,
                    Score = scores.SingleOrDefault(score =>
                        score.ContestId == race.Id
                        && score.ParticipantId == participant.ParticipantId)
                })
                .OrderBy(item =>
                    item.Participant.Status == ContestParticipantStatus.Disqualified
                        ? 1
                        : 0)
                .ThenBy(item => item.Score?.Value ?? decimal.MaxValue)
                .ThenBy(item => item.Participant.SortOrder)
                .ToArray();
            return new
            {
                race.Id,
                race.Name,
                raceNumber = race.SortOrder,
                race.ContestShape,
                status = race.Status.ToString(),
                race.QualifyByPlace,
                race.QualifyByTime,
                race.AdvancePlaceTies,
                race.AdvanceTimeTies,
                laneAssignment = race.AthleticsLaneAssignment,
                entries = entries.Select((entry, index) =>
                {
                    var valid = entry.Participant.Status
                        != ContestParticipantStatus.Disqualified
                        && entry.Score is not null;
                    var place = valid
                        ? 1 + entries.Take(index).Count(previous =>
                            previous.Participant.Status
                                != ContestParticipantStatus.Disqualified
                            && previous.Score is not null
                            && previous.Score.Value < entry.Score!.Value)
                        : (int?)null;
                    var tied = valid && entries.Count(other =>
                        other.Participant.Status
                            != ContestParticipantStatus.Disqualified
                        && other.Score?.Value == entry.Score!.Value) > 1;
                    return new
                    {
                        entry.Participant.ParticipantId,
                        name = names.GetValueOrDefault(
                            entry.Participant.ParticipantId),
                        lane = entry.Participant.SortOrder,
                        timeMilliseconds = entry.Score?.Value,
                        displayTime = entry.Score?.DisplayValue,
                        place,
                        isTied = tied,
                        status = entry.Participant.Status
                            == ContestParticipantStatus.Disqualified
                            ? "Disqualified"
                            : entry.Participant.QualificationStatus ?? "Entered",
                        entry.Participant.QualificationReason,
                        medal = includeMedals ? place switch
                        {
                            1 => "Gold",
                            2 => "Silver",
                            3 => "Bronze",
                            _ => null
                        } : null
                    };
                })
            };
        }

        var heats = races.Where(race => race.ContestShape == "Heat")
            .Select(race => ProjectRace(race, false))
            .ToArray();
        var semifinals = races
            .Where(race => race.ContestShape == "AthleticsSemifinal")
            .Select(race => ProjectRace(race, false))
            .ToArray();
        var final = races.SingleOrDefault(
            race => race.ContestShape == "AthleticsFinal");
        return Ok(new
        {
            competition.Id,
            competition.Name,
            status = competition.Status.ToString(),
            eventName = races[0].AthleticsEventName,
            distanceMeters = races[0].DistanceMeters,
            qualificationAppliedAt = races
                .Where(race => race.ContestShape is "Heat" or "AthleticsSemifinal")
                .Select(race => race.QualificationAppliedAt)
                .OrderByDescending(value => value)
                .FirstOrDefault(value => value.HasValue),
            heats,
            semifinals,
            final = final is null ? null : ProjectRace(final, true)
        });
    }

    private Task<Tenant?> ResolveTenantAsync(string slug, CancellationToken cancellationToken) =>
        _db.Tenants.AsNoTracking().FirstOrDefaultAsync(
            x => x.Slug == slug && x.Status == TenantStatus.Active,
            cancellationToken);

    private IQueryable<Competition> PublishedCompetitions(Guid tenantId) =>
        _db.Competitions.AsNoTracking().Where(
            x => x.TenantId == tenantId &&
                 x.Status != CompetitionStatus.Draft &&
                 x.Status != CompetitionStatus.Archived);

    private IQueryable<Contest> PublicContests(Guid tenantId) =>
        _db.Contests.AsNoTracking().Where(
            x => x.TenantId == tenantId &&
                 x.Status != ContestStatus.Draft &&
                 x.Status != ContestStatus.Cancelled);

    private static object EmptyPublicSearch(int page, int pageSize) => new
    {
        Query = string.Empty,
        Page = page,
        PageSize = pageSize,
        Total = 0,
        TotalPages = 0,
        Items = Array.Empty<PublicSearchItem>(),
        Filters = new
        {
            Sports = Array.Empty<object>(),
            Competitions = Array.Empty<object>(),
            Statuses = Array.Empty<string>()
        }
    };

    private sealed record PublicSearchItem(
        string Id,
        string Type,
        string Title,
        string? Subtitle,
        Guid? SportId,
        DateTime? Date,
        string? Status,
        Guid? CompetitionId,
        string? Venue,
        string? Href);

    private static string PublicContestRenderer(
        Guid sportId,
        string contestShape,
        int participantCount)
    {
        var golf = Guid.Parse("00000000-0000-0000-0000-000000000102");
        var athletics = Guid.Parse("00000000-0000-0000-0000-000000000103");
        var cricket = Guid.Parse("00000000-0000-0000-0000-000000000104");
        var combat = Guid.Parse("00000000-0000-0000-0000-000000000105");
        if (sportId == combat || contestShape.Contains("bout", StringComparison.OrdinalIgnoreCase))
            return "fight-card-bout";
        if (sportId == cricket || contestShape.Contains("cricket", StringComparison.OrdinalIgnoreCase))
            return "cricket-scorecard";
        if (sportId == athletics
            || contestShape.Contains("heat", StringComparison.OrdinalIgnoreCase)
            || contestShape.Contains("race", StringComparison.OrdinalIgnoreCase))
            return "race-heat";
        if (sportId == golf
            || contestShape.Contains("leaderboard", StringComparison.OrdinalIgnoreCase))
            return "multi-participant-leaderboard";
        return participantCount <= 2 ? "head-to-head" : "multi-participant-leaderboard";
    }

    private static IReadOnlyDictionary<string, string> CompetitionTabLabels(
        Guid sportId,
        string? displayConfiguration)
    {
        var labels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["overview"] = "Overview",
            ["fixtures"] = "Fixtures",
            ["results"] = "Results",
            ["standings"] = "Standings",
            ["bracket"] = "Bracket",
            ["leaderboard"] = "Leaderboard",
            ["participants"] = "Participants",
            ["statistics"] = "Statistics"
        };
        var athletics = Guid.Parse("00000000-0000-0000-0000-000000000103");
        var golf = Guid.Parse("00000000-0000-0000-0000-000000000102");
        var combat = Guid.Parse("00000000-0000-0000-0000-000000000105");
        var cricket = Guid.Parse("00000000-0000-0000-0000-000000000104");
        if (sportId == athletics)
        {
            labels["fixtures"] = "Events";
            labels["participants"] = "Athletes";
        }
        else if (sportId == golf)
        {
            labels["fixtures"] = "Tee times";
            labels["participants"] = "Golfers";
        }
        else if (sportId == combat)
        {
            labels["fixtures"] = "Fight card";
            labels["participants"] = "Fighters";
        }
        else if (sportId == cricket)
        {
            labels["fixtures"] = "Matches";
            labels["statistics"] = "Match statistics";
        }

        if (string.IsNullOrWhiteSpace(displayConfiguration)) return labels;
        try
        {
            using var document = JsonDocument.Parse(displayConfiguration);
            if (!document.RootElement.TryGetProperty("labels", out var configuredLabels)
                || configuredLabels.ValueKind != JsonValueKind.Object)
                return labels;
            foreach (var property in configuredLabels.EnumerateObject())
            {
                if (labels.ContainsKey(property.Name)
                    && property.Value.ValueKind == JsonValueKind.String
                    && !string.IsNullOrWhiteSpace(property.Value.GetString()))
                {
                    labels[property.Name] = property.Value.GetString()!.Trim();
                }
            }
        }
        catch (JsonException)
        {
            // A malformed optional display block must not break public coverage.
        }
        return labels;
    }

    private sealed record PublicCompetitionPresentation(
        string? PrimaryContestRenderer,
        IReadOnlySet<string> VisibleTabs,
        string? DefaultTab,
        string? ScoreVariant,
        string? StandingsVariant,
        string? LeaderboardVariant,
        bool? BracketEnabled,
        bool? SeriesEnabled,
        bool? MedalTableEnabled)
    {
        public static PublicCompetitionPresentation Parse(string? configuration)
        {
            if (string.IsNullOrWhiteSpace(configuration)) return Empty;
            try
            {
                using var document = JsonDocument.Parse(configuration);
                var root = document.RootElement;
                var tabs = root.TryGetProperty("visibleTabs", out var visibleTabs)
                    && visibleTabs.ValueKind == JsonValueKind.Array
                    ? visibleTabs.EnumerateArray()
                        .Where(item => item.ValueKind == JsonValueKind.String)
                        .Select(item => item.GetString()!)
                        .ToHashSet(StringComparer.OrdinalIgnoreCase)
                    : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                return new(
                    Text(root, "primaryContestRenderer"), tabs,
                    Text(root, "defaultTab"), Text(root, "scoreVariant"),
                    Text(root, "standingsVariant"), Text(root, "leaderboardVariant"),
                    Boolean(root, "bracketEnabled"), Boolean(root, "seriesEnabled"),
                    Boolean(root, "medalTableEnabled"));
            }
            catch (JsonException)
            {
                return Empty;
            }
        }

        private static string? Text(JsonElement root, string name) =>
            root.TryGetProperty(name, out var value)
                && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;

        private static bool? Boolean(JsonElement root, string name) =>
            root.TryGetProperty(name, out var value)
                && value.ValueKind is JsonValueKind.True or JsonValueKind.False
                ? value.GetBoolean()
                : null;

        private static PublicCompetitionPresentation Empty { get; } = new(
            null, new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            null, null, null, null, null, null, null);
    }
}

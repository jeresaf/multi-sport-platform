using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SportsPlatform.Api.Authorization;
using SportsPlatform.Infrastructure.Persistence;
using SportsPlatform.Modules.Competitions.Entities;
using SportsPlatform.Modules.Contests.Entities;
using SportsPlatform.Modules.Registrations.Entities;

namespace SportsPlatform.Api.Controllers;

[ApiController]
[Route("api/competitions/{competitionId:guid}/cricket")]
[Authorize]
public sealed class CricketController : ControllerBase
{
    public static readonly Guid CricketSportId =
        Guid.Parse("00000000-0000-0000-0000-000000000104");

    private readonly SportsPlatformDbContext _database;

    public CricketController(SportsPlatformDbContext database)
    {
        _database = database;
    }

    [HttpPost("tournament")]
    [RequirePermission("competitions.manage")]
    public async Task<IActionResult> CreateTournament(
        Guid competitionId,
        [FromBody] CreateCricketTournamentRequest request,
        CancellationToken cancellationToken)
    {
        var competition = await _database.Competitions
            .Include(item => item.Editions)
            .ThenInclude(edition => edition.Stages)
            .SingleOrDefaultAsync(item => item.Id == competitionId, cancellationToken);
        if (competition is null) return NotFound();
        if (!User.CanAccessTenant(competition.TenantId)) return Forbid();
        if (competition.SportId != CricketSportId)
            return BadRequest(new { error = "The competition is not configured for cricket." });
        if (competition.Editions.SelectMany(item => item.Stages).Any())
            return Conflict(new { error = "A cricket tournament already exists." });

        var rulesetVersionId = await ResolvePublishedRulesetIdAsync(
            competition,
            cancellationToken);
        if (!rulesetVersionId.HasValue)
        {
            return Conflict(new
            {
                error = "Configure and publish the cricket T20 ruleset before creating a tournament."
            });
        }

        var registeredIds = await _database.Registrations
            .Where(item => item.CompetitionId == competitionId
                && (item.Status == RegistrationStatus.Approved
                    || item.Status == RegistrationStatus.Confirmed))
            .Select(item => item.ParticipantId)
            .Distinct()
            .ToArrayAsync(cancellationToken);
        var teams = await _database.Participants
            .Where(item => item.TenantId == competition.TenantId
                && registeredIds.Contains(item.Id))
            .OrderBy(item => item.Id)
            .ToArrayAsync(cancellationToken);
        if (teams.Length != 4)
            return BadRequest(new { error = "Exactly four approved teams are required." });

        var start = request.StartTimeUtc.Kind == DateTimeKind.Utc
            ? request.StartTimeUtc
            : request.StartTimeUtc.ToUniversalTime();
        var edition = competition.AddEdition(
            request.EditionName,
            start.Date,
            start.Date.AddDays(10));
        edition.Open();
        var stage = edition.AddStage(request.GroupStageName, StageType.Group, 1);
        stage.Open();
        competition.AssignRulesetVersion(rulesetVersionId.Value);
        _database.Entry(edition).State = EntityState.Added;
        _database.Entry(stage).State = EntityState.Added;

        var matches = new List<Contest>();
        var matchNumber = 1;
        for (var first = 0; first < teams.Length; first++)
        {
            for (var second = first + 1; second < teams.Length; second++)
            {
                var match = CreateMatch(
                    competition.TenantId,
                    stage.Id,
                    "CricketGroupMatch",
                    matchNumber,
                    teams[first].Id,
                    teams[second].Id,
                    $"{teams[first].Name} vs {teams[second].Name}",
                    request.Venue,
                    start.AddDays(matchNumber - 1),
                    rulesetVersionId.Value);
                matches.Add(match);
                matchNumber++;
            }
        }

        _database.Contests.AddRange(matches);
        await _database.SaveChangesAsync(cancellationToken);
        return Ok(new
        {
            competitionId,
            rulesetVersionId,
            editionId = edition.Id,
            groupStageId = stage.Id,
            teamCount = teams.Length,
            matches = matches.Select(ProjectMatch)
        });
    }

    [HttpPost("matches/{matchId:guid}/innings")]
    [RequirePermission("scoring.submit")]
    public async Task<IActionResult> SubmitInnings(
        Guid competitionId,
        Guid matchId,
        [FromBody] SubmitCricketInningsRequest request,
        CancellationToken cancellationToken)
    {
        if (request.InningsNumber is < 1 or > 2)
            return BadRequest(new { error = "Innings number must be 1 or 2." });
        if (request.Deliveries.Count == 0 || request.Deliveries.Count > 120)
            return BadRequest(new { error = "An innings must contain between 1 and 120 legal deliveries." });

        var context = await LoadTournamentAsync(competitionId, cancellationToken);
        if (context is null) return NotFound();
        if (!User.CanAccessTenant(context.Competition.TenantId)) return Forbid();
        var match = await _database.Contests
            .Include(item => item.Participants)
            .Include(item => item.Segments)
            .ThenInclude(item => item.NestedSegments)
            .Include(item => item.Events)
            .SingleOrDefaultAsync(item => item.Id == matchId
                && item.TenantId == context.Competition.TenantId
                && item.SportId == CricketSportId,
                cancellationToken);
        if (match is null || !context.StageIds.Contains(match.StageId ?? Guid.Empty))
            return NotFound();
        if (match.Participants.All(item => item.ParticipantId != request.BattingTeamId))
            return BadRequest(new { error = "The batting team is not a participant in this match." });

        var existing = ReadInnings(match);
        if (existing.ContainsKey(request.InningsNumber))
            return Conflict(new { error = $"Innings {request.InningsNumber} has already been recorded." });
        if (request.InningsNumber == 2 && !existing.ContainsKey(1))
            return BadRequest(new { error = "Record the first innings before the chase." });
        if (request.InningsNumber == 2
            && existing[1].BattingTeamId == request.BattingTeamId)
            return BadRequest(new { error = "The second innings must be batted by the opposing team." });

        var validation = ValidateDeliveries(request.Deliveries);
        if (validation is not null) return BadRequest(new { error = validation });

        var runs = request.Deliveries.Sum(item => item.Runs + item.ExtraRuns);
        var wickets = request.Deliveries.Count(item => item.IsWicket);
        if (wickets > 10)
            return BadRequest(new { error = "An innings cannot lose more than ten wickets." });
        var isChaseComplete = request.InningsNumber == 2 && runs > existing[1].Runs;
        if (request.Deliveries.Count < 120 && wickets < 10 && !isChaseComplete)
        {
            return BadRequest(new
            {
                error = "An innings may end early only when all out or when the target is reached."
            });
        }

        match.Start();
        var innings = match.Segments.OrderBy(item => item.SortOrder)
            .ElementAt(request.InningsNumber - 1);
        var sequence = match.Events.Select(item => item.SequenceNumber)
            .DefaultIfEmpty(0)
            .Max();
        foreach (var delivery in request.Deliveries)
        {
            var over = innings.NestedSegments.Single(item =>
                item.SortOrder == delivery.OverNumber);
            var payload = JsonSerializer.Serialize(new CricketDeliveryPayload(
                request.InningsNumber,
                delivery.OverNumber,
                delivery.BallNumber,
                delivery.Runs,
                delivery.ExtraRuns,
                delivery.ExtraType,
                delivery.IsWicket));
            var contestEvent = match.AddEvent(
                "CricketDelivery",
                request.BattingTeamId,
                over.Id,
                payload,
                User.UserId(),
                "cricket-scorecard",
                ++sequence,
                $"{match.Id}:innings:{request.InningsNumber}:"
                    + $"{delivery.OverNumber}.{delivery.BallNumber}");
            contestEvent.Approve();
            _database.Entry(contestEvent).State = EntityState.Added;
        }
        var inningsSummary = new CricketInningsSummary(
            request.InningsNumber,
            request.BattingTeamId,
            runs,
            wickets,
            request.Deliveries.Count,
            request.Deliveries.Sum(item => item.ExtraRuns));
        var summaryEvent = match.AddEvent(
            "CricketInningsSummary",
            request.BattingTeamId,
            innings.Id,
            JsonSerializer.Serialize(inningsSummary),
            User.UserId(),
            "cricket-scorecard",
            ++sequence,
            $"{match.Id}:innings:{request.InningsNumber}:summary");
        summaryEvent.Approve();
        _database.Entry(summaryEvent).State = EntityState.Added;
        Guid? winnerId = null;
        string? result = null;
        if (request.InningsNumber == 2)
        {
            var first = existing[1];
            var otherTeamId = match.Participants
                .Single(item => item.ParticipantId != request.BattingTeamId)
                .ParticipantId;
            if (runs > first.Runs)
            {
                winnerId = request.BattingTeamId;
                result = $"Won by {10 - wickets} wickets";
            }
            else if (runs < first.Runs)
            {
                winnerId = otherTeamId;
                result = $"Won by {first.Runs - runs} runs";
            }
            else
            {
                result = "Tied — Super Over required";
            }

            if (winnerId.HasValue) match.SetWinner(winnerId.Value);
            match.Complete();
            await AdvanceKnockoutWinnerAsync(
                context,
                match,
                winnerId,
                cancellationToken);
        }

        NormalizeExistingOwnedRows(match);
        var autoDetectChanges = _database.ChangeTracker.AutoDetectChangesEnabled;
        _database.ChangeTracker.AutoDetectChangesEnabled = false;
        try
        {
            await _database.SaveChangesAsync(cancellationToken);
        }
        finally
        {
            _database.ChangeTracker.AutoDetectChangesEnabled = autoDetectChanges;
        }
        return Ok(new
        {
            match.Id,
            request.InningsNumber,
            request.BattingTeamId,
            runs,
            wickets,
            balls = request.Deliveries.Count,
            overs = FormatOvers(request.Deliveries.Count),
            target = request.InningsNumber == 2 ? existing[1].Runs + 1 : runs + 1,
            winnerId,
            result,
            superOverRequired = request.InningsNumber == 2 && winnerId is null
        });
    }

    [HttpGet("matches/{matchId:guid}/scorecard")]
    [RequirePermission("competitions.read")]
    public async Task<IActionResult> GetScorecard(
        Guid competitionId,
        Guid matchId,
        CancellationToken cancellationToken)
    {
        var context = await LoadTournamentAsync(competitionId, cancellationToken);
        if (context is null) return NotFound();
        if (!User.CanAccessTenant(context.Competition.TenantId)) return Forbid();
        var match = await LoadMatchAsync(context, matchId, cancellationToken);
        return match is null
            ? NotFound()
            : Ok(await ProjectScorecardAsync(match, cancellationToken));
    }

    [HttpPost("matches/{matchId:guid}/deliveries")]
    [RequirePermission("scoring.submit")]
    public async Task<IActionResult> RecordDelivery(
        Guid competitionId,
        Guid matchId,
        [FromBody] RecordCricketDeliveryRequest request,
        CancellationToken cancellationToken)
    {
        if (request.InningsNumber is < 1 or > 2)
            return BadRequest(new { error = "Innings number must be 1 or 2." });
        var outcomeError = ValidateDeliveryOutcome(
            request.Runs, request.ExtraRuns, request.ExtraType);
        if (outcomeError is not null) return BadRequest(new { error = outcomeError });

        var context = await LoadTournamentAsync(competitionId, cancellationToken);
        if (context is null) return NotFound();
        if (!User.CanAccessTenant(context.Competition.TenantId)) return Forbid();
        var match = await LoadMatchAsync(context, matchId, cancellationToken);
        if (match is null) return NotFound();
        if (match.Participants.All(item => item.ParticipantId != request.BattingTeamId))
            return BadRequest(new { error = "The batting team is not a participant in this match." });

        var summaries = ReadCompletedInnings(match);
        if (summaries.ContainsKey(request.InningsNumber))
            return Conflict(new { error = $"Innings {request.InningsNumber} is already complete." });
        if (request.InningsNumber == 2 && !summaries.ContainsKey(1))
            return BadRequest(new { error = "Complete the first innings before recording the chase." });
        if (request.InningsNumber == 2
            && summaries[1].BattingTeamId == request.BattingTeamId)
            return BadRequest(new { error = "The second innings must be batted by the opposing team." });

        var deliveries = ReadDeliveries(match, request.InningsNumber);
        if (deliveries.Any(item => item.BattingTeamId != request.BattingTeamId))
            return BadRequest(new { error = "The batting team cannot change during an innings." });
        if (deliveries.Count >= 120 || deliveries.Count(item => item.Payload.IsWicket) >= 10)
            return Conflict(new { error = "The innings has no legal deliveries remaining." });
        if (request.InningsNumber == 2
            && deliveries.Sum(item => item.Payload.Runs + item.Payload.ExtraRuns)
                > summaries[1].Runs)
            return Conflict(new { error = "The target has already been reached." });
        if (request.IsWicket && deliveries.Count(item => item.Payload.IsWicket) == 9)
        {
            // This delivery is the final available wicket; it is valid but no later ball is.
        }

        var ballIndex = deliveries.Count;
        var overNumber = (ballIndex / 6) + 1;
        var ballNumber = (ballIndex % 6) + 1;
        var innings = match.Segments.OrderBy(item => item.SortOrder)
            .ElementAt(request.InningsNumber - 1);
        var over = innings.NestedSegments.Single(item => item.SortOrder == overNumber);
        var payload = new CricketDeliveryPayload(
            request.InningsNumber,
            overNumber,
            ballNumber,
            request.Runs,
            request.ExtraRuns,
            NormalizeExtraType(request.ExtraType),
            request.IsWicket);
        var sequence = match.Events.Select(item => item.SequenceNumber)
            .DefaultIfEmpty(0).Max() + 1;
        var contestEvent = match.AddEvent(
            "CricketDelivery",
            request.BattingTeamId,
            over.Id,
            JsonSerializer.Serialize(payload),
            User.UserId(),
            "cricket-scorecard",
            sequence,
            $"{match.Id}:innings:{request.InningsNumber}:{overNumber}.{ballNumber}:"
                + Guid.NewGuid().ToString("N"));
        contestEvent.Approve();
        _database.Entry(contestEvent).State = EntityState.Added;
        match.Start();
        NormalizeExistingOwnedRows(match);
        await _database.SaveChangesAsync(cancellationToken);
        return Ok(await ProjectScorecardAsync(match, cancellationToken));
    }

    [HttpPost("matches/{matchId:guid}/innings/{inningsNumber:int}/complete")]
    [RequirePermission("scoring.submit")]
    public async Task<IActionResult> CompleteInnings(
        Guid competitionId,
        Guid matchId,
        int inningsNumber,
        [FromBody] CompleteCricketInningsRequest request,
        CancellationToken cancellationToken)
    {
        if (inningsNumber is < 1 or > 2)
            return BadRequest(new { error = "Innings number must be 1 or 2." });
        var context = await LoadTournamentAsync(competitionId, cancellationToken);
        if (context is null) return NotFound();
        if (!User.CanAccessTenant(context.Competition.TenantId)) return Forbid();
        var match = await LoadMatchAsync(context, matchId, cancellationToken);
        if (match is null) return NotFound();
        var summaries = ReadCompletedInnings(match);
        if (summaries.ContainsKey(inningsNumber))
            return Conflict(new { error = $"Innings {inningsNumber} is already complete." });
        if (inningsNumber == 2 && !summaries.ContainsKey(1))
            return BadRequest(new { error = "Complete the first innings before the chase." });

        var deliveries = ReadDeliveries(match, inningsNumber);
        if (deliveries.Count == 0)
            return BadRequest(new { error = "Record at least one delivery before completing an innings." });
        if (deliveries.Any(item => item.BattingTeamId != request.BattingTeamId))
            return BadRequest(new { error = "The batting team does not match the recorded deliveries." });
        var summary = BuildSummary(inningsNumber, request.BattingTeamId, deliveries);
        var chaseComplete = inningsNumber == 2 && summary.Runs > summaries[1].Runs;
        if (summary.Balls < 120 && summary.Wickets < 10 && !chaseComplete)
        {
            return BadRequest(new
            {
                error = "An innings may end early only when all out or when the target is reached."
            });
        }

        var innings = match.Segments.OrderBy(item => item.SortOrder)
            .ElementAt(inningsNumber - 1);
        var sequence = match.Events.Select(item => item.SequenceNumber)
            .DefaultIfEmpty(0).Max() + 1;
        var summaryEvent = match.AddEvent(
            "CricketInningsSummary",
            request.BattingTeamId,
            innings.Id,
            JsonSerializer.Serialize(summary),
            User.UserId(),
            "cricket-scorecard",
            sequence,
            $"{match.Id}:innings:{inningsNumber}:summary:{Guid.NewGuid():N}");
        summaryEvent.Approve();
        _database.Entry(summaryEvent).State = EntityState.Added;
        if (inningsNumber == 2)
        {
            summaries[2] = summary;
            await ApplyCompletedMatchOutcomeAsync(
                context, match, summaries, cancellationToken);
        }
        NormalizeExistingOwnedRows(match);
        await _database.SaveChangesAsync(cancellationToken);
        return Ok(await ProjectScorecardAsync(match, cancellationToken));
    }

    [HttpPut("matches/{matchId:guid}/deliveries/{deliveryEventId:guid}")]
    [RequirePermission("scoring.correct")]
    public async Task<IActionResult> CorrectDelivery(
        Guid competitionId,
        Guid matchId,
        Guid deliveryEventId,
        [FromBody] CorrectCricketDeliveryRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
            return BadRequest(new { error = "A correction reason is required." });
        var outcomeError = ValidateDeliveryOutcome(
            request.Runs, request.ExtraRuns, request.ExtraType);
        if (outcomeError is not null) return BadRequest(new { error = outcomeError });

        var context = await LoadTournamentAsync(competitionId, cancellationToken);
        if (context is null) return NotFound();
        if (!User.CanAccessTenant(context.Competition.TenantId)) return Forbid();
        var match = await LoadMatchAsync(context, matchId, cancellationToken);
        if (match is null) return NotFound();
        var original = match.Events.SingleOrDefault(item =>
            item.Id == deliveryEventId
            && item.EventType == "CricketDelivery"
            && item.Status == EventStatus.Approved);
        if (original is null) return NotFound();
        var originalPayload = DeserializeDelivery(original);
        if (originalPayload is null)
            return Conflict(new { error = "The delivery payload cannot be corrected." });

        var replacementPayload = originalPayload with
        {
            Runs = request.Runs,
            ExtraRuns = request.ExtraRuns,
            ExtraType = NormalizeExtraType(request.ExtraType),
            IsWicket = request.IsWicket
        };
        var innings = match.Segments.OrderBy(item => item.SortOrder)
            .ElementAt(originalPayload.InningsNumber - 1);
        var over = innings.NestedSegments.Single(item =>
            item.SortOrder == originalPayload.OverNumber);
        var sequence = match.Events.Select(item => item.SequenceNumber)
            .DefaultIfEmpty(0).Max() + 1;
        var replacement = match.AddEvent(
            "CricketDelivery",
            original.ParticipantId,
            over.Id,
            JsonSerializer.Serialize(replacementPayload),
            User.UserId(),
            "cricket-scorecard",
            sequence,
            $"{match.Id}:correction:{deliveryEventId}:{Guid.NewGuid():N}");
        replacement.Approve();
        original.Correct(request.Reason.Trim(), replacement.Id);
        _database.Entry(replacement).State = EntityState.Added;

        var changedEvents = new List<ContestEvent> { original };
        var deliveries = ReadDeliveries(match, originalPayload.InningsNumber);
        if (deliveries.Count(item => item.Payload.IsWicket) > 10)
            return BadRequest(new { error = "An innings cannot lose more than ten wickets." });
        var oldSummaryEvent = match.Events.SingleOrDefault(item =>
            item.EventType == "CricketInningsSummary"
            && item.Status == EventStatus.Approved
            && DeserializeSummary(item)?.InningsNumber == originalPayload.InningsNumber);
        if (oldSummaryEvent is not null)
        {
            var rebuilt = BuildSummary(
                originalPayload.InningsNumber,
                original.ParticipantId,
                deliveries);
            var rebuiltEvent = match.AddEvent(
                "CricketInningsSummary",
                original.ParticipantId,
                innings.Id,
                JsonSerializer.Serialize(rebuilt),
                User.UserId(),
                "cricket-scorecard",
                ++sequence,
                $"{match.Id}:correction-summary:{deliveryEventId}:{Guid.NewGuid():N}");
            rebuiltEvent.Approve();
            oldSummaryEvent.Correct(request.Reason.Trim(), rebuiltEvent.Id);
            _database.Entry(rebuiltEvent).State = EntityState.Added;
            changedEvents.Add(oldSummaryEvent);

            var summaries = ReadCompletedInnings(match);
            if (summaries.Count == 2)
                await ApplyCompletedMatchOutcomeAsync(
                    context, match, summaries, cancellationToken, false);
        }

        NormalizeExistingOwnedRows(
            match,
            changedEvents.Select(item => item.Id).ToHashSet());
        foreach (var changedEvent in changedEvents)
            _database.Entry(changedEvent).State = EntityState.Modified;
        await _database.SaveChangesAsync(cancellationToken);
        return Ok(await ProjectScorecardAsync(match, cancellationToken));
    }

    [HttpPost("qualify")]
    [RequirePermission("competitions.manage")]
    public async Task<IActionResult> CreateKnockout(
        Guid competitionId,
        [FromBody] CreateCricketKnockoutRequest request,
        CancellationToken cancellationToken)
    {
        var context = await LoadTournamentAsync(competitionId, cancellationToken);
        if (context is null) return NotFound();
        if (!User.CanAccessTenant(context.Competition.TenantId)) return Forbid();
        if (context.Stages.Any(item => item.Type == StageType.Knockout))
            return Conflict(new { error = "The cricket knockout stage already exists." });

        var rulesetVersionId = await ResolvePublishedRulesetIdAsync(
            context.Competition,
            cancellationToken);
        if (!rulesetVersionId.HasValue)
            return Conflict(new { error = "The tournament has no published cricket ruleset." });
        context.Competition.AssignRulesetVersion(rulesetVersionId.Value);

        var groupStageIds = context.Stages
            .Where(item => item.Type == StageType.Group)
            .Select(item => item.Id)
            .ToArray();
        var groupMatches = await LoadMatchesAsync(groupStageIds, cancellationToken);
        if (groupMatches.Length != 6
            || groupMatches.Any(item => item.Status != ContestStatus.Completed))
        {
            return BadRequest(new { error = "Complete all six group matches before qualification." });
        }
        var standings = CricketTournamentProjection.CalculateStandings(groupMatches);
        if (standings.Count != 4)
            return BadRequest(new { error = "Four ranked group teams are required." });

        var edition = context.Competition.Editions
            .OrderByDescending(item => item.StartDate)
            .First();
        var stage = edition.AddStage(request.StageName, StageType.Knockout, 2);
        stage.Open();
        _database.Entry(stage).State = EntityState.Added;
        var names = await _database.Participants
            .Where(item => standings.Select(row => row.ParticipantId).Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, item => item.Name, cancellationToken);
        var start = request.StartTimeUtc.Kind == DateTimeKind.Utc
            ? request.StartTimeUtc
            : request.StartTimeUtc.ToUniversalTime();
        var semifinal1 = CreateMatch(
            context.Competition.TenantId, stage.Id, "CricketSemifinal", 1,
            standings[0].ParticipantId, standings[3].ParticipantId,
            $"Semifinal 1: {names[standings[0].ParticipantId]} vs {names[standings[3].ParticipantId]}",
            request.Venue, start, rulesetVersionId.Value);
        var semifinal2 = CreateMatch(
            context.Competition.TenantId, stage.Id, "CricketSemifinal", 2,
            standings[1].ParticipantId, standings[2].ParticipantId,
            $"Semifinal 2: {names[standings[1].ParticipantId]} vs {names[standings[2].ParticipantId]}",
            request.Venue, start.AddHours(5), rulesetVersionId.Value);
        var final = new Contest(
            Guid.NewGuid(),
            context.Competition.TenantId,
            CricketSportId,
            "CricketFinal");
        final.AssignToStage(stage.Id, 3);
        final.AssignRulesetVersion(rulesetVersionId.Value);
        final.SetName("T20 Final");
        AddCricketSegments(final);
        final.Schedule(request.Venue, start.AddDays(request.DaysUntilFinal));
        _database.Contests.AddRange(semifinal1, semifinal2, final);
        await _database.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            stageId = stage.Id,
            standings,
            semifinals = new[] { ProjectMatch(semifinal1), ProjectMatch(semifinal2) },
            final = ProjectMatch(final)
        });
    }

    [HttpGet("tournament")]
    [RequirePermission("competitions.read")]
    public async Task<IActionResult> GetTournament(
        Guid competitionId,
        CancellationToken cancellationToken)
    {
        var context = await LoadTournamentAsync(competitionId, cancellationToken);
        if (context is null) return NotFound();
        if (!User.CanAccessTenant(context.Competition.TenantId)) return Forbid();
        return Ok(await CricketTournamentProjection.BuildAsync(
            _database, context.Competition, cancellationToken));
    }

    private async Task AdvanceKnockoutWinnerAsync(
        CricketTournamentContext context,
        Contest match,
        Guid? winnerId,
        CancellationToken cancellationToken)
    {
        if (!winnerId.HasValue || match.ContestShape != "CricketSemifinal") return;
        var knockoutStageIds = context.Stages
            .Where(item => item.Type == StageType.Knockout)
            .Select(item => item.Id)
            .ToArray();
        var final = await _database.Contests
            .Include(item => item.Participants)
            .SingleAsync(item => item.StageId.HasValue
                && knockoutStageIds.Contains(item.StageId.Value)
                && item.ContestShape == "CricketFinal",
                cancellationToken);
        if (final.Participants.All(item => item.ParticipantId != winnerId.Value))
        {
            var advancedParticipant = final.AddParticipant(winnerId.Value, "Team");
            _database.Entry(advancedParticipant).State = EntityState.Added;
        }
        if (final.Participants.Count == 2)
        {
            var names = await _database.Participants
                .Where(item => final.Participants
                    .Select(entry => entry.ParticipantId)
                    .Contains(item.Id))
                .ToDictionaryAsync(item => item.Id, item => item.Name, cancellationToken);
            final.SetName(
                $"{names[final.Participants.OrderBy(item => item.SortOrder).First().ParticipantId]}"
                + " vs "
                + names[final.Participants.OrderBy(item => item.SortOrder).Last().ParticipantId]);
        }
    }

    private async Task<CricketTournamentContext?> LoadTournamentAsync(
        Guid competitionId,
        CancellationToken cancellationToken)
    {
        var competition = await _database.Competitions
            .Include(item => item.Editions)
            .ThenInclude(edition => edition.Stages)
            .SingleOrDefaultAsync(item => item.Id == competitionId
                && item.SportId == CricketSportId,
                cancellationToken);
        return competition is null
            ? null
            : new CricketTournamentContext(
                competition,
                competition.Editions.SelectMany(item => item.Stages).ToArray());
    }

    private async Task<Contest[]> LoadMatchesAsync(
        IReadOnlyCollection<Guid> stageIds,
        CancellationToken cancellationToken) =>
        await _database.Contests
            .Include(item => item.Participants)
            .Include(item => item.Events)
            .Where(item => item.StageId.HasValue
                && stageIds.Contains(item.StageId.Value)
                && item.SportId == CricketSportId)
            .OrderBy(item => item.SortOrder)
            .ToArrayAsync(cancellationToken);

    private async Task<Contest?> LoadMatchAsync(
        CricketTournamentContext context,
        Guid matchId,
        CancellationToken cancellationToken)
    {
        var match = await _database.Contests
            .Include(item => item.Participants)
            .Include(item => item.Segments)
            .ThenInclude(item => item.NestedSegments)
            .Include(item => item.Events)
            .SingleOrDefaultAsync(item => item.Id == matchId
                && item.TenantId == context.Competition.TenantId
                && item.SportId == CricketSportId,
                cancellationToken);
        return match is not null && context.StageIds.Contains(match.StageId ?? Guid.Empty)
            ? match
            : null;
    }

    private async Task<object> ProjectScorecardAsync(
        Contest match,
        CancellationToken cancellationToken)
    {
        var teamIds = match.Participants.Select(item => item.ParticipantId).ToArray();
        var names = await _database.Participants
            .AsNoTracking()
            .Where(item => teamIds.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, item => item.Name, cancellationToken);
        var completed = ReadCompletedInnings(match);
        var innings = Enumerable.Range(1, 2).Select(inningsNumber =>
        {
            var deliveries = ReadDeliveries(match, inningsNumber);
            var battingTeamId = completed.TryGetValue(inningsNumber, out var summary)
                ? summary.BattingTeamId
                : deliveries.FirstOrDefault()?.BattingTeamId;
            var runs = deliveries.Sum(item => item.Payload.Runs + item.Payload.ExtraRuns);
            var wickets = deliveries.Count(item => item.Payload.IsWicket);
            var extras = deliveries.Sum(item => item.Payload.ExtraRuns);
            return new
            {
                inningsNumber,
                battingTeamId,
                battingTeamName = battingTeamId.HasValue
                    ? names.GetValueOrDefault(battingTeamId.Value)
                    : null,
                isComplete = completed.ContainsKey(inningsNumber),
                runs,
                wickets,
                balls = deliveries.Count,
                overs = FormatOvers(deliveries.Count),
                extras,
                deliveries = deliveries.Select(item => new
                {
                    eventId = item.EventId,
                    item.Payload.OverNumber,
                    item.Payload.BallNumber,
                    item.Payload.Runs,
                    item.Payload.ExtraRuns,
                    item.Payload.ExtraType,
                    item.Payload.IsWicket
                }),
                overSummaries = deliveries
                    .GroupBy(item => item.Payload.OverNumber)
                    .OrderBy(group => group.Key)
                    .Select(group => new
                    {
                        overNumber = group.Key,
                        runs = group.Sum(item =>
                            item.Payload.Runs + item.Payload.ExtraRuns),
                        wickets = group.Count(item => item.Payload.IsWicket),
                        balls = group.Count()
                    })
            };
        }).ToArray();
        var target = completed.TryGetValue(1, out var first) ? first.Runs + 1 : (int?)null;
        var chaseRuns = innings[1].runs;
        return new
        {
            match.Id,
            match.Name,
            status = match.Status.ToString(),
            match.RulesetVersionId,
            teams = match.Participants.OrderBy(item => item.SortOrder).Select(item => new
            {
                id = item.ParticipantId,
                name = names.GetValueOrDefault(item.ParticipantId)
            }),
            innings,
            target,
            runsRequired = target.HasValue ? Math.Max(0, target.Value - chaseRuns) : (int?)null,
            match.WinnerId,
            result = completed.Count == 2
                ? CricketTournamentProjection.ResultDescription(match, completed)
                : null
        };
    }

    private async Task ApplyCompletedMatchOutcomeAsync(
        CricketTournamentContext context,
        Contest match,
        IReadOnlyDictionary<int, CricketInningsSummary> summaries,
        CancellationToken cancellationToken,
        bool advanceKnockout = true)
    {
        var first = summaries[1];
        var second = summaries[2];
        Guid? winnerId = null;
        if (second.Runs > first.Runs)
            winnerId = second.BattingTeamId;
        else if (second.Runs < first.Runs)
            winnerId = first.BattingTeamId;

        if (winnerId.HasValue) match.SetWinner(winnerId.Value);
        else match.ClearWinner();
        if (match.Status == ContestStatus.InProgress) match.Complete();
        if (advanceKnockout)
            await AdvanceKnockoutWinnerAsync(
                context, match, winnerId, cancellationToken);
    }

    private static List<CricketRecordedDelivery> ReadDeliveries(
        Contest match,
        int inningsNumber) =>
        match.Events
            .Where(item => item.EventType == "CricketDelivery"
                && item.Status == EventStatus.Approved)
            .Select(item => new
            {
                Event = item,
                Payload = DeserializeDelivery(item)
            })
            .Where(item => item.Payload?.InningsNumber == inningsNumber)
            .OrderBy(item => item.Payload!.OverNumber)
            .ThenBy(item => item.Payload!.BallNumber)
            .Select(item => new CricketRecordedDelivery(
                item.Event.Id,
                item.Event.ParticipantId,
                item.Payload!))
            .ToList();

    private static CricketDeliveryPayload? DeserializeDelivery(ContestEvent item)
    {
        try
        {
            return string.IsNullOrWhiteSpace(item.MetricValue)
                ? null
                : JsonSerializer.Deserialize<CricketDeliveryPayload>(item.MetricValue);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static CricketInningsSummary? DeserializeSummary(ContestEvent item)
    {
        try
        {
            return string.IsNullOrWhiteSpace(item.MetricValue)
                ? null
                : JsonSerializer.Deserialize<CricketInningsSummary>(item.MetricValue);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static CricketInningsSummary BuildSummary(
        int inningsNumber,
        Guid battingTeamId,
        IReadOnlyCollection<CricketRecordedDelivery> deliveries) =>
        new(
            inningsNumber,
            battingTeamId,
            deliveries.Sum(item => item.Payload.Runs + item.Payload.ExtraRuns),
            deliveries.Count(item => item.Payload.IsWicket),
            deliveries.Count,
            deliveries.Sum(item => item.Payload.ExtraRuns));

    private static string? ValidateDeliveryOutcome(
        int runs,
        int extraRuns,
        string? extraType)
    {
        if (runs is < 0 or > 6 || extraRuns is < 0 or > 7)
            return "Runs must be within the supported delivery range.";
        var normalized = NormalizeExtraType(extraType);
        var allowedExtras = new[] { "Wide", "NoBall", "Bye", "LegBye", "Penalty" };
        if (extraRuns > 0 && normalized is null)
            return "Select an extra type when extra runs are recorded.";
        if (normalized is not null && !allowedExtras.Contains(normalized))
            return "Extra type must be Wide, NoBall, Bye, LegBye, or Penalty.";
        return null;
    }

    private static string? NormalizeExtraType(string? extraType)
    {
        if (string.IsNullOrWhiteSpace(extraType)) return null;
        return extraType.Trim().ToLowerInvariant() switch
        {
            "wide" => "Wide",
            "noball" or "no ball" or "no-ball" => "NoBall",
            "bye" => "Bye",
            "legbye" or "leg bye" or "leg-bye" => "LegBye",
            "penalty" => "Penalty",
            _ => extraType.Trim()
        };
    }

    private async Task<Guid?> ResolvePublishedRulesetIdAsync(
        Competition competition,
        CancellationToken cancellationToken)
    {
        var cricket = await _database.Sports
            .Include(item => item.Versions)
            .SingleOrDefaultAsync(item => item.Id == CricketSportId, cancellationToken);
        if (cricket is null) return null;

        var ruleset = competition.RulesetVersionId.HasValue
            ? cricket.Versions.SingleOrDefault(item =>
                item.Id == competition.RulesetVersionId.Value && item.IsPublished)
            : cricket.Versions
                .Where(item => item.IsPublished)
                .OrderByDescending(item => item.VersionNumber)
                .FirstOrDefault();
        return ruleset?.Id;
    }

    private static Contest CreateMatch(
        Guid tenantId,
        Guid stageId,
        string shape,
        int sortOrder,
        Guid firstTeamId,
        Guid secondTeamId,
        string name,
        string venue,
        DateTime start,
        Guid rulesetVersionId)
    {
        var match = new Contest(Guid.NewGuid(), tenantId, CricketSportId, shape);
        match.AssignToStage(stageId, sortOrder);
        match.AssignRulesetVersion(rulesetVersionId);
        match.AddParticipant(firstTeamId, "Team", 1);
        match.AddParticipant(secondTeamId, "Team", 2);
        match.SetName(name);
        AddCricketSegments(match);
        match.Schedule(venue, start);
        return match;
    }

    internal static void AddCricketSegments(Contest contest)
    {
        for (var inningsNumber = 1; inningsNumber <= 2; inningsNumber++)
        {
            var innings = contest.AddSegment(
                $"Innings {inningsNumber}",
                "Innings",
                inningsNumber);
            for (var over = 1; over <= 20; over++)
                innings.AddNestedSegment($"Over {over}", "Over", over);
        }
    }

    private static string? ValidateDeliveries(
        IReadOnlyList<CricketDeliveryRequest> deliveries)
    {
        for (var index = 0; index < deliveries.Count; index++)
        {
            var delivery = deliveries[index];
            var expectedOver = (index / 6) + 1;
            var expectedBall = (index % 6) + 1;
            if (delivery.OverNumber != expectedOver
                || delivery.BallNumber != expectedBall)
            {
                return $"Delivery {index + 1} must be recorded as "
                    + $"{expectedOver}.{expectedBall}.";
            }
            if (delivery.Runs is < 0 or > 6 || delivery.ExtraRuns is < 0 or > 7)
                return "Runs must be within the supported delivery range.";
        }
        return null;
    }

    internal static Dictionary<int, CricketInningsSummary> ReadInnings(Contest match)
    {
        var summaries = ReadCompletedInnings(match);
        if (summaries.Count > 0) return summaries;

        return match.Events
            .Where(item => item.EventType == "CricketDelivery"
                && item.Status == EventStatus.Approved
                && !string.IsNullOrWhiteSpace(item.MetricValue))
            .Select(item =>
            {
                try
                {
                    return (Event: item, Payload:
                        JsonSerializer.Deserialize<CricketDeliveryPayload>(
                            item.MetricValue!));
                }
                catch (JsonException)
                {
                    return (Event: item, Payload: null);
                }
            })
            .Where(item => item.Payload is not null)
            .GroupBy(item => item.Payload!.InningsNumber)
            .ToDictionary(
                group => group.Key,
                group => new CricketInningsSummary(
                    group.Key,
                    group.First().Event.ParticipantId,
                    group.Sum(item => item.Payload!.Runs + item.Payload.ExtraRuns),
                    group.Count(item => item.Payload!.IsWicket),
                    group.Count(),
                    group.Sum(item => item.Payload!.ExtraRuns)));
    }

    private static Dictionary<int, CricketInningsSummary> ReadCompletedInnings(
        Contest match) =>
        match.Events
            .Where(item => item.EventType == "CricketInningsSummary"
                && item.Status == EventStatus.Approved
                && !string.IsNullOrWhiteSpace(item.MetricValue))
            .Select(item =>
            {
                try
                {
                    return (Event: item, Summary:
                        JsonSerializer.Deserialize<CricketInningsSummary>(
                            item.MetricValue!));
                }
                catch (JsonException)
                {
                    return (Event: item, Summary: null);
                }
            })
            .Where(item => item.Summary is not null)
            .GroupBy(item => item.Summary!.InningsNumber)
            .ToDictionary(
                group => group.Key,
                group => group.OrderByDescending(item => item.Event.SequenceNumber)
                    .First().Summary!);

    internal static string FormatOvers(int balls) => $"{balls / 6}.{balls % 6}";

    private void NormalizeExistingOwnedRows(
        Contest match,
        IReadOnlySet<Guid>? preserveModifiedEventIds = null)
    {
        // Contest collections are mapped as owned rows. Updating the aggregate
        // can make unchanged owned rows appear modified on SQLite/PostgreSQL;
        // retain only intentional additions and participant status changes.
        _database.ChangeTracker.DetectChanges();
        foreach (var participant in match.Participants)
        {
            var entry = _database.Entry(participant);
            if (entry.State == EntityState.Modified)
                entry.State = EntityState.Unchanged;
        }
        foreach (var segment in match.Segments)
        {
            var entry = _database.Entry(segment);
            if (entry.State == EntityState.Modified)
                entry.State = EntityState.Unchanged;
            foreach (var nested in segment.NestedSegments)
            {
                var nestedEntry = _database.Entry(nested);
                if (nestedEntry.State == EntityState.Modified)
                    nestedEntry.State = EntityState.Unchanged;
            }
        }
        foreach (var contestEvent in match.Events)
        {
            var entry = _database.Entry(contestEvent);
            if (entry.State == EntityState.Modified
                && !(preserveModifiedEventIds?.Contains(contestEvent.Id) ?? false))
                entry.State = EntityState.Unchanged;
        }
        foreach (var entry in _database.ChangeTracker
                     .Entries<ContestParticipant>()
                     .Where(item => item.State == EntityState.Modified))
            entry.State = EntityState.Unchanged;
    }

    private static object ProjectMatch(Contest item) => new
    {
        item.Id,
        item.Name,
        item.ContestShape,
        item.RulesetVersionId,
        item.Status,
        item.StartTime,
        participantIds = item.Participants
            .OrderBy(entry => entry.SortOrder)
            .Select(entry => entry.ParticipantId)
    };

    private sealed record CricketTournamentContext(
        Competition Competition,
        IReadOnlyList<Stage> Stages)
    {
        public IReadOnlySet<Guid> StageIds =>
            Stages.Select(item => item.Id).ToHashSet();
    }
}

public sealed record CreateCricketTournamentRequest(
    string EditionName,
    string GroupStageName,
    string Venue,
    DateTime StartTimeUtc);

public sealed record CreateCricketKnockoutRequest(
    string StageName,
    string Venue,
    DateTime StartTimeUtc,
    int DaysUntilFinal = 2);

public sealed record SubmitCricketInningsRequest(
    int InningsNumber,
    Guid BattingTeamId,
    IReadOnlyList<CricketDeliveryRequest> Deliveries);

public sealed record RecordCricketDeliveryRequest(
    int InningsNumber,
    Guid BattingTeamId,
    int Runs,
    int ExtraRuns = 0,
    string? ExtraType = null,
    bool IsWicket = false);

public sealed record CompleteCricketInningsRequest(Guid BattingTeamId);

public sealed record CorrectCricketDeliveryRequest(
    int Runs,
    int ExtraRuns,
    string? ExtraType,
    bool IsWicket,
    string Reason);

public sealed record CricketDeliveryRequest(
    int OverNumber,
    int BallNumber,
    int Runs,
    int ExtraRuns = 0,
    string? ExtraType = null,
    bool IsWicket = false);

public sealed record CricketDeliveryPayload(
    int InningsNumber,
    int OverNumber,
    int BallNumber,
    int Runs,
    int ExtraRuns,
    string? ExtraType,
    bool IsWicket);

internal sealed record CricketRecordedDelivery(
    Guid EventId,
    Guid BattingTeamId,
    CricketDeliveryPayload Payload);

public sealed record CricketInningsSummary(
    int InningsNumber,
    Guid BattingTeamId,
    int Runs,
    int Wickets,
    int Balls,
    int Extras);

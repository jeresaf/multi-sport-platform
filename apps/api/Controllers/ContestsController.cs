using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text.Json;
using SportsPlatform.Infrastructure.Services;
using SportsPlatform.Api.Authorization;
using SportsPlatform.Infrastructure.Persistence;
using SportsPlatform.Modules.Contests.Entities;
using SportsPlatform.Modules.Sports.Entities;

namespace SportsPlatform.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ContestsController : ControllerBase
{
    private readonly IContestsService _contestsService;
    private readonly IContestLifecycleService _lifecycleService;
    private readonly SportsPlatformDbContext _database;

    public ContestsController(
        IContestsService contestsService,
        IContestLifecycleService lifecycleService,
        SportsPlatformDbContext database)
    {
        _contestsService = contestsService;
        _lifecycleService = lifecycleService;
        _database = database;
    }

    [HttpGet]
    [RequirePermission("contests.read")]
    public async Task<IActionResult> GetAll([FromQuery] Guid? tenantId)
    {
        var effectiveTenantId = tenantId ?? User.TenantId();
        if (effectiveTenantId.HasValue)
        {
            if (!User.CanAccessTenant(effectiveTenantId.Value)) return Forbid();
            return Ok(await _contestsService.GetContestsByTenantAsync(effectiveTenantId.Value));
        }
        return Ok(Array.Empty<object>());
    }

    [HttpGet("{id:guid}")]
    [RequirePermission("contests.read")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var contest = await _contestsService.GetContestByIdAsync(id);
        if (contest == null) return NotFound();
        if (!User.CanAccessTenant(contest.TenantId)) return Forbid();
        return Ok(contest);
    }

    [HttpGet("shapes")]
    [RequirePermission("contests.read")]
    public IActionResult GetShapes() => Ok(Enum.GetNames<ContestShapeKind>()
        .Where(item => item != nameof(ContestShapeKind.Custom)));

    [HttpGet("{id:guid}/model")]
    [RequirePermission("contests.read")]
    public async Task<IActionResult> GetModel(Guid id, CancellationToken cancellationToken)
    {
        var contest = await _contestsService.GetContestByIdAsync(id, cancellationToken);
        if (contest is null) return NotFound();
        if (!User.CanAccessTenant(contest.TenantId)) return Forbid();
        var results = await _database.ContestResults.AsNoTracking()
            .Where(item => item.ContestId == id)
            .OrderBy(item => item.Rank)
            .ToArrayAsync(cancellationToken);
        return Ok(new
        {
            contest.Id, contest.TenantId, contest.SportId, contest.RulesetVersionId,
            contest.ContestShape, shapeKind = contest.ShapeKind.ToString(), contest.Name,
            contest.Status, contest.Venue, contest.StartTime, contest.EndTime,
            contest.Participants, contest.Segments, contest.Events, contest.Metrics,
            contest.Officials, results
        });
    }

    [HttpPost]
    [RequirePermission("contests.create")]
    public async Task<IActionResult> Create([FromBody] CreateContestRequest request)
    {
        if (!User.CanAccessTenant(request.TenantId)) return Forbid();
        Contest contest;
        try
        {
            contest = await _contestsService.CreateContestAsync(
                request.TenantId, request.SportId, request.ContestShape,
                request.AllowedParticipantTypes);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
        if (!string.IsNullOrWhiteSpace(request.Name))
        {
            contest.SetName(request.Name.Trim());
            await _contestsService.UpdateContestAsync(contest);
        }
        return CreatedAtAction(nameof(GetById), new { id = contest.Id }, contest);
    }

    [HttpPost("{id:guid}/participants")]
    [RequirePermission("contests.manage")]
    public async Task<IActionResult> AddParticipant(
        Guid id,
        [FromBody] AddContestParticipantRequest request)
    {
        var contest = await _contestsService.GetContestByIdAsync(id);
        if (contest == null) return NotFound();
        if (!User.CanAccessTenant(contest.TenantId)) return Forbid();
        try
        {
            var participant = await _contestsService.AddParticipantAsync(
                id, request.ParticipantId, request.ParticipantType);
            return Ok(participant);
        }
        catch (KeyNotFoundException exception)
        {
            return NotFound(new { error = exception.Message });
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(new { error = exception.Message });
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
    }

    [HttpPost("{id:guid}/segments")]
    [RequirePermission("contests.manage")]
    public async Task<IActionResult> AddSegment(
        Guid id,
        [FromBody] AddContestSegmentRequest request)
    {
        var contest = await _contestsService.GetContestByIdAsync(id);
        if (contest == null) return NotFound();
        if (!User.CanAccessTenant(contest.TenantId)) return Forbid();
        if (string.IsNullOrWhiteSpace(request.Name)
            || string.IsNullOrWhiteSpace(request.SegmentType)
            || request.SortOrder < 1)
            return BadRequest(new { error = "Name, segment type, and a positive sort order are required." });
        var segment = await _contestsService.AddSegmentAsync(
            id,
            request.Name.Trim(), request.SegmentType.Trim(), request.SortOrder,
            request.IsOptional, request.ActivationCondition);
        return Ok(segment);
    }

    [HttpPost("{id:guid}/metrics")]
    [RequirePermission("contests.manage")]
    public async Task<IActionResult> RecordMetric(
        Guid id, [FromBody] RecordContestMetricRequest request,
        CancellationToken cancellationToken)
    {
        var contest = await _contestsService.GetContestByIdAsync(id, cancellationToken);
        if (contest is null) return NotFound();
        if (!User.CanAccessTenant(contest.TenantId)) return Forbid();
        try
        {
            var rawValue = request.Value
                ?? request.NumericValue?.ToString(CultureInfo.InvariantCulture)
                ?? request.TextValue;
            if (string.IsNullOrWhiteSpace(rawValue))
                return BadRequest(new { error = "A metric value is required." });

            MetricDefinition definition;
            if (contest.RulesetVersionId.HasValue)
            {
                var definitionsJson = await _database.Set<SportDefinitionVersion>()
                    .AsNoTracking()
                    .Where(item => item.Id == contest.RulesetVersionId.Value)
                    .Select(item => item.MetricDefinitions)
                    .SingleOrDefaultAsync(cancellationToken);
                if (definitionsJson is null)
                    return BadRequest(new { error = "The contest ruleset metric definitions were not found." });
                definition = MetricFramework.ParseDefinitions(definitionsJson)
                    .FirstOrDefault(item => item.Code.Equals(request.Name, StringComparison.OrdinalIgnoreCase)
                        || item.Name.Equals(request.Name, StringComparison.OrdinalIgnoreCase))
                    ?? throw new ArgumentException($"Metric '{request.Name}' is not defined by the contest ruleset.");
            }
            else
            {
                var dataType = request.DataType
                    ?? (request.NumericValue.HasValue ? nameof(MetricDataType.Decimal) : nameof(MetricDataType.Text));
                var precision = request.Precision
                    ?? (request.NumericValue.HasValue ? DecimalScale(request.NumericValue.Value) : 0);
                definition = MetricFramework.ParseDefinition(JsonSerializer.SerializeToElement(new
                {
                    id = request.Name,
                    name = request.Name,
                    type = dataType,
                    unit = request.Unit,
                    validation = new { precision }
                }));
            }
            var normalized = MetricFramework.ValidateValue(definition, rawValue, request.Unit);
            return Ok(await _contestsService.RecordMetricAsync(
                id, definition.Code, normalized.NumericValue, normalized.TextValue,
                normalized.Unit, request.ParticipantId, request.SegmentId,
                normalized.DataType.ToString(), normalized.Precision, cancellationToken));
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(new { error = exception.Message });
        }
    }

    private static int DecimalScale(decimal value)
    {
        var scale = (decimal.GetBits(value)[3] >> 16) & 0x7F;
        return Math.Min(scale, 6);
    }

    [HttpPost("{id:guid}/officials")]
    [RequirePermission("contests.manage")]
    public async Task<IActionResult> AddOfficial(
        Guid id, [FromBody] AddContestOfficialRequest request,
        CancellationToken cancellationToken)
    {
        var contest = await _contestsService.GetContestByIdAsync(id, cancellationToken);
        if (contest is null) return NotFound();
        if (!User.CanAccessTenant(contest.TenantId)) return Forbid();
        if (!await _database.Officials.AsNoTracking().AnyAsync(item =>
                item.Id == request.OfficialId
                && _database.Users.Any(user =>
                    user.Id == item.UserId && user.TenantId == contest.TenantId),
                cancellationToken))
            return BadRequest(new { error = "Select an official from the contest tenant." });
        try
        {
            return Ok(await _contestsService.AddOfficialAsync(
                id, request.OfficialId, request.Role, request.DisplayName,
                cancellationToken));
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
    }

    [HttpPost("{id:guid}/start")]
    [RequirePermission("contests.manage")]
    public async Task<IActionResult> Start(Guid id)
    {
        var contest = await _contestsService.GetContestByIdAsync(id);
        if (contest == null) return NotFound();
        if (!User.CanAccessTenant(contest.TenantId)) return Forbid();
        return await Transition(id, "InProgress", null);
    }

    [HttpPost("{id:guid}/complete")]
    [RequirePermission("contests.manage")]
    public async Task<IActionResult> Complete(Guid id)
    {
        var contest = await _contestsService.GetContestByIdAsync(id);
        if (contest == null) return NotFound();
        if (!User.CanAccessTenant(contest.TenantId)) return Forbid();
        return await Transition(id, "Completed", null);
    }

    [HttpPost("{id:guid}/cancel")]
    [RequirePermission("contests.manage")]
    public async Task<IActionResult> Cancel(Guid id, [FromBody] ContestTransitionRequest? request)
    {
        var contest = await _contestsService.GetContestByIdAsync(id);
        if (contest == null) return NotFound();
        if (!User.CanAccessTenant(contest.TenantId)) return Forbid();
        return await Transition(id, "Cancelled", request?.Reason);
    }

    [HttpGet("{id:guid}/lifecycle")]
    [RequirePermission("contests.read")]
    public async Task<IActionResult> GetLifecycle(Guid id)
    {
        var contest = await _contestsService.GetContestByIdAsync(id);
        if (contest == null) return NotFound();
        if (!User.CanAccessTenant(contest.TenantId)) return Forbid();
        return Ok(await _lifecycleService.GetAsync(id));
    }

    [HttpGet("{id:guid}/lifecycle/history")]
    [RequirePermission("contests.read")]
    public async Task<IActionResult> GetLifecycleHistory(Guid id)
    {
        var contest = await _contestsService.GetContestByIdAsync(id);
        if (contest == null) return NotFound();
        if (!User.CanAccessTenant(contest.TenantId)) return Forbid();
        return Ok(await _lifecycleService.HistoryAsync(id));
    }

    [HttpPost("{id:guid}/transition")]
    [RequirePermission("contests.manage")]
    public async Task<IActionResult> TransitionContest(
        Guid id,
        [FromBody] ContestTransitionRequest request) =>
        await Transition(id, request.TargetStatus, request.Reason);

    private async Task<IActionResult> Transition(Guid id, string targetStatus, string? reason)
    {
        var contest = await _contestsService.GetContestByIdAsync(id);
        if (contest == null) return NotFound();
        if (!User.CanAccessTenant(contest.TenantId)) return Forbid();
        if (!Enum.TryParse<SportsPlatform.Modules.Contests.Entities.ContestStatus>(
                targetStatus, true, out var target))
            return BadRequest(new { error = $"Unknown contest status '{targetStatus}'." });
        var actorId = User.UserId();
        if (!actorId.HasValue) return Unauthorized();
        try
        {
            return Ok(await _lifecycleService.TransitionAsync(
                id, target, actorId.Value, reason));
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(new { error = exception.Message });
        }
    }
}

public record CreateContestRequest(
    Guid TenantId,
    Guid SportId,
    string ContestShape,
    string? Name = null,
    IReadOnlyList<string>? AllowedParticipantTypes = null);
public record AddContestParticipantRequest(Guid ParticipantId, string ParticipantType);
public record AddContestSegmentRequest(
    string Name,
    string SegmentType,
    int SortOrder,
    bool IsOptional = false,
    string? ActivationCondition = null);
public record ContestTransitionRequest(string TargetStatus = "Cancelled", string? Reason = null);
public record RecordContestMetricRequest(
    string Name,
    decimal? NumericValue = null,
    string? TextValue = null,
    string? Unit = null,
    Guid? ParticipantId = null,
    Guid? SegmentId = null,
    string? Value = null,
    string? DataType = null,
    int? Precision = null);
public record AddContestOfficialRequest(Guid OfficialId, string Role, string? DisplayName = null);

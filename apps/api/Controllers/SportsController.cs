using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SportsPlatform.Infrastructure.Services;
using SportsPlatform.Infrastructure.Persistence;
using SportsPlatform.Api.Authorization;
using SportsPlatform.Modules.Audit;
using SportsPlatform.Modules.Sports;
using SportsPlatform.Modules.Sports.Entities;
using SportsPlatform.Modules.Sports.Services;
using System.Text.Json;

namespace SportsPlatform.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SportsController : ControllerBase
{
    private readonly ISportsService _sportsService;
    private readonly SportsPlatformDbContext _database;
    private readonly IFeatureEntitlementService _featureEntitlements;
    private readonly IUsageLimitsService _usageLimits;
    private readonly IAuditService _audit;
    private readonly IFootballRulesetService _footballRulesetService;
    private readonly IBasketballRulesetService _basketballRulesetService;
    private readonly IRugbyRulesetService _rugbyRulesetService;
    private readonly IGolfRulesetService _golfRulesetService;
    private readonly ICombatSportsRulesetService _combatSportsRulesetService;
    private readonly IAthleticsRulesetService _athleticsRulesetService;
    private readonly ICricketT20RulesetService _cricketT20RulesetService;

    public SportsController(
        ISportsService sportsService,
        IFootballRulesetService footballRulesetService,
        IBasketballRulesetService basketballRulesetService,
        IRugbyRulesetService rugbyRulesetService,
        IGolfRulesetService golfRulesetService,
        ICombatSportsRulesetService combatSportsRulesetService,
        IAthleticsRulesetService athleticsRulesetService,
        ICricketT20RulesetService cricketT20RulesetService,
        SportsPlatformDbContext database,
        IFeatureEntitlementService featureEntitlements,
        IUsageLimitsService usageLimits,
        IAuditService audit)
    {
        _sportsService = sportsService;
        _database = database;
        _featureEntitlements = featureEntitlements;
        _usageLimits = usageLimits;
        _audit = audit;
        _footballRulesetService = footballRulesetService;
        _basketballRulesetService = basketballRulesetService;
        _rugbyRulesetService = rugbyRulesetService;
        _golfRulesetService = golfRulesetService;
        _combatSportsRulesetService = combatSportsRulesetService;
        _athleticsRulesetService = athleticsRulesetService;
        _cricketT20RulesetService = cricketT20RulesetService;
    }

    [HttpGet]
    [RequirePermission("sports.read")]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var sports = await _sportsService.GetAllSportsAsync(cancellationToken);
        return Ok(sports.Select(SportCatalogueItem.From));
    }

    [HttpGet("metrics/types")]
    [RequirePermission("sports.read")]
    public IActionResult GetMetricTypes() => Ok(Enum.GetNames<MetricDataType>());

    [HttpPost("metrics/validate")]
    [RequirePermission("sports.read")]
    public IActionResult ValidateMetric([FromBody] ValidateMetricValueRequest request)
    {
        try
        {
            var definition = MetricFramework.ParseDefinition(request.Definition);
            return Ok(MetricFramework.ValidateValue(definition, request.Value, request.Unit));
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { error = "InvalidMetric", message = exception.Message });
        }
    }

    [HttpPost("metrics/rank")]
    [RequirePermission("sports.read")]
    public IActionResult RankMetric([FromBody] RankMetricValuesRequest request)
    {
        if (request.Values is null or { Count: 0 })
            return BadRequest(new { error = "InvalidMetricRanking", message = "At least one metric value is required." });
        try
        {
            var definition = MetricFramework.ParseDefinition(request.Definition);
            var candidates = request.Values.Select(item =>
                new MetricRankCandidate(item.ParticipantId, item.Value, item.Unit));
            return Ok(MetricFramework.Rank(definition, candidates));
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            return BadRequest(new { error = "InvalidMetricRanking", message = exception.Message });
        }
    }

    [HttpGet("{id:guid}")]
    [RequirePermission("sports.read")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var sport = await _sportsService.GetSportByIdAsync(id);
        if (sport == null) return NotFound();
        return Ok(SportCatalogueItem.From(sport));
    }

    [HttpGet("by-code/{code}")]
    [RequirePermission("sports.read")]
    public async Task<IActionResult> GetByCode(string code)
    {
        var sport = await _sportsService.GetSportByCodeAsync(code);
        if (sport == null) return NotFound();
        return Ok(SportCatalogueItem.From(sport));
    }

    [HttpPost]
    [RequirePermission("sports.create")]
    public async Task<IActionResult> Create([FromBody] CreateSportRequest request, CancellationToken cancellationToken)
    {
        if (!User.IsPlatformOperator()) return Forbid();
        if (request.SupportedParticipantShapes is null or { Count: 0 })
            return BadRequest(new { error = "At least one supported participant shape is required." });
        var sport = await _sportsService.CreateSportAsync(
            request.Code, request.Name, request.Description, cancellationToken,
            request.Icon, request.SupportedParticipantShapes);
        await AuditAsync("SportCreated", sport.Id, null, SportCatalogueItem.From(sport), cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = sport.Id }, SportCatalogueItem.From(sport));
    }

    [HttpPut("{id:guid}")]
    [RequirePermission("sports.manage")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateSportRequest request, CancellationToken cancellationToken)
    {
        if (!User.IsPlatformOperator()) return Forbid();
        if (request.SupportedParticipantShapes is null or { Count: 0 })
            return BadRequest(new { error = "At least one supported participant shape is required." });
        var sport = await _sportsService.GetSportByIdAsync(id, cancellationToken);
        if (sport is null) return NotFound();
        var original = SportCatalogueItem.From(sport);
        if (!request.IsActive && sport.IsActive && await _database.TenantSports.AnyAsync(
                item => item.SportId == id && item.IsEnabled, cancellationToken))
            return Conflict(new { error = "SportInUse", message = "Disable this sport for every tenant before deactivating it." });
        sport.UpdateDetails(request.Name, request.Description, request.Icon, request.SupportedParticipantShapes);
        if (request.IsActive) sport.Activate(); else sport.Deactivate();
        await _sportsService.UpdateSportAsync(sport, cancellationToken);
        var updated = SportCatalogueItem.From(sport);
        await AuditAsync("SportUpdated", id, original, updated, cancellationToken);
        return Ok(updated);
    }

    [HttpGet("tenant/{tenantId:guid}")]
    [RequirePermission("sports.read")]
    public async Task<IActionResult> GetTenantCatalogue(Guid tenantId, CancellationToken cancellationToken)
    {
        if (!User.CanAccessTenant(tenantId)) return Forbid();
        if (!await _database.Tenants.AnyAsync(item => item.Id == tenantId, cancellationToken)) return NotFound();
        var sports = await _sportsService.GetAllSportsAsync(cancellationToken);
        var selections = (await _sportsService.GetTenantSportsAsync(tenantId, cancellationToken))
            .ToDictionary(item => item.SportId);
        return Ok(sports.Where(item => item.IsActive || User.IsPlatformOperator()).Select(item =>
        {
            selections.TryGetValue(item.Id, out var selection);
            return new TenantSportCatalogueItem(SportCatalogueItem.From(item), selection?.IsEnabled == true);
        }));
    }

    [HttpPut("tenant/{tenantId:guid}/{sportId:guid}")]
    [RequirePermission("sports.manage")]
    public async Task<IActionResult> SetTenantSport(
        Guid tenantId,
        Guid sportId,
        [FromBody] SetTenantSportRequest request,
        CancellationToken cancellationToken)
    {
        if (!User.CanAccessTenant(tenantId)) return Forbid();
        if (!await _database.Tenants.AnyAsync(item => item.Id == tenantId, cancellationToken)) return NotFound();
        var sport = await _sportsService.GetSportByIdAsync(sportId, cancellationToken);
        if (sport is null) return NotFound();
        var existing = await _database.TenantSports.SingleOrDefaultAsync(
            item => item.TenantId == tenantId && item.SportId == sportId, cancellationToken);
        if (request.Enabled && existing?.IsEnabled != true)
        {
            if (!await _featureEntitlements.HasFeatureAsync(tenantId, "sports.max_sports", cancellationToken))
                return StatusCode(StatusCodes.Status403Forbidden, new { error = "FeatureNotEntitled", feature = "sports.max_sports" });
            if (!await _usageLimits.CanCreateResourceAsync(tenantId, "max_sports", cancellationToken))
                return Conflict(new { error = "UsageLimitExceeded", resource = "max_sports" });
        }
        var selection = await _sportsService.SetTenantSportEnabledAsync(
            tenantId, sportId, request.Enabled, cancellationToken);
        await _audit.RecordAsync(
            request.Enabled ? "TenantSportEnabled" : "TenantSportDisabled",
            "TenantSport", selection.Id.ToString(), tenantId, User.UserId(),
            JsonSerializer.Serialize(new { enabled = !request.Enabled }),
            JsonSerializer.Serialize(new { request.Enabled, sportId }),
            "Tenant sport catalogue updated", cancellationToken);
        return Ok(new TenantSportCatalogueItem(SportCatalogueItem.From(sport), selection.IsEnabled));
    }

    [HttpPost("{id:guid}/versions")]
    [RequirePermission("sports.manage")]
    public async Task<IActionResult> CreateVersion(Guid id, [FromBody] CreateVersionRequest request, CancellationToken cancellationToken)
    {
        if (!User.IsPlatformOperator()) return Forbid();
        var version = await _sportsService.CreateSportVersionAsync(
            id, request.Name, Actor(), cancellationToken);
        var response = SportDefinitionVersionItem.From(version);
        await AuditVersionAsync("SportDefinitionVersionCreated", version, null, response, cancellationToken);
        return CreatedAtAction(nameof(GetVersion), new { versionId = version.Id }, response);
    }

    [HttpGet("versions/{versionId:guid}")]
    [RequirePermission("sports.read")]
    public async Task<IActionResult> GetVersion(Guid versionId, CancellationToken cancellationToken)
    {
        var version = await _sportsService.GetSportVersionAsync(versionId, cancellationToken);
        return version is null ? NotFound() : Ok(SportDefinitionVersionItem.From(version));
    }

    [HttpPost("{sportId:guid}/versions/{sourceVersionId:guid}/clone")]
    [RequirePermission("sports.manage")]
    public async Task<IActionResult> CloneVersion(
        Guid sportId,
        Guid sourceVersionId,
        [FromBody] CloneVersionRequest request,
        CancellationToken cancellationToken)
    {
        if (!User.IsPlatformOperator()) return Forbid();
        var clone = await _sportsService.CloneSportVersionAsync(
            sportId, sourceVersionId, request.Name, Actor(), cancellationToken);
        var response = SportDefinitionVersionItem.From(clone);
        await AuditVersionAsync("SportDefinitionVersionCloned", clone,
            new { sourceVersionId }, response, cancellationToken);
        return CreatedAtAction(nameof(GetVersion), new { versionId = clone.Id }, response);
    }

    [HttpPut("versions/{versionId:guid}")]
    [RequirePermission("sports.manage")]
    public async Task<IActionResult> ConfigureVersion(
        Guid versionId,
        [FromBody] ConfigureSportVersionRequest request,
        CancellationToken cancellationToken)
    {
        if (!User.IsPlatformOperator()) return Forbid();
        var version = await _sportsService.GetSportVersionAsync(versionId, cancellationToken);
        if (version is null) return NotFound();
        var original = SportDefinitionVersionItem.From(version);
        await _sportsService.ConfigureSportVersionAsync(versionId, request.ToConfiguration(), cancellationToken);
        var updated = await _sportsService.GetSportVersionAsync(versionId, cancellationToken);
        var response = SportDefinitionVersionItem.From(updated!);
        await AuditVersionAsync("SportDefinitionVersionConfigured", updated!, original, response, cancellationToken);
        return Ok(response);
    }

    [HttpPost("versions/{versionId:guid}/publish")]
    [RequirePermission("sports.manage")]
    public async Task<IActionResult> PublishVersion(Guid versionId, CancellationToken cancellationToken)
    {
        if (!User.IsPlatformOperator()) return Forbid();
        var version = await _sportsService.GetSportVersionAsync(versionId, cancellationToken);
        if (version is null) return NotFound();
        if (version.IsPublished) return NoContent();
        var original = SportDefinitionVersionItem.From(version);
        await _sportsService.PublishSportVersionAsync(versionId, cancellationToken);
        var published = await _sportsService.GetSportVersionAsync(versionId, cancellationToken);
        await AuditVersionAsync("SportDefinitionVersionPublished", published!, original,
            SportDefinitionVersionItem.From(published!), cancellationToken);
        return NoContent();
    }

    [HttpPost("football/configure")]
    [RequirePermission("sports.manage")]
    public async Task<IActionResult> ConfigureFootballRuleset([FromBody] ConfigureFootballRequest request)
    {
        var version = await _footballRulesetService.ConfigureFootballRulesetAsync(request.CreatedBy);
        return Ok(version);
    }

    [HttpGet("football/ruleset")]
    [RequirePermission("sports.read")]
    public async Task<IActionResult> GetFootballRuleset()
    {
        var version = await _footballRulesetService.GetFootballRulesetAsync();
        if (version == null) return NotFound(new { message = "Football ruleset not configured" });
        return Ok(version);
    }

    [HttpPost("basketball/configure")]
    [RequirePermission("sports.manage")]
    public async Task<IActionResult> ConfigureBasketballRuleset(
        [FromBody] ConfigureBasketballRequest request)
    {
        var version = await _basketballRulesetService
            .ConfigureBasketballRulesetAsync(request.CreatedBy);
        return Ok(version);
    }

    [HttpGet("basketball/ruleset")]
    [RequirePermission("sports.read")]
    public async Task<IActionResult> GetBasketballRuleset()
    {
        var version = await _basketballRulesetService.GetBasketballRulesetAsync();
        if (version == null)
            return NotFound(new { message = "Basketball ruleset not configured" });
        return Ok(version);
    }

    [HttpPost("rugby/configure")]
    [RequirePermission("sports.manage")]
    public async Task<IActionResult> ConfigureRugbyRuleset([FromBody] ConfigureRugbyRequest request)
    {
        SportDefinitionVersion version;
        try
        {
            version = await _rugbyRulesetService.ConfigureRugbyRulesetAsync(
                request.CreatedBy,
                request.Variant ?? RugbyVariants.Fifteens);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
        return Ok(version);
    }

    [HttpGet("rugby/ruleset")]
    [RequirePermission("sports.read")]
    public async Task<IActionResult> GetRugbyRuleset([FromQuery] string variant = RugbyVariants.Fifteens)
    {
        SportDefinitionVersion? version;
        try
        {
            version = await _rugbyRulesetService.GetRugbyRulesetAsync(variant);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
        if (version == null) return NotFound(new { message = "Rugby ruleset not configured" });
        return Ok(version);
    }

    [HttpPost("golf/configure")]
    [RequirePermission("sports.manage")]
    public async Task<IActionResult> ConfigureGolfRuleset([FromBody] ConfigureGolfRequest request)
    {
        var version = await _golfRulesetService.ConfigureGolfRulesetAsync(request.CreatedBy);
        return Ok(version);
    }

    [HttpGet("golf/ruleset")]
    [RequirePermission("sports.read")]
    public async Task<IActionResult> GetGolfRuleset()
    {
        var version = await _golfRulesetService.GetGolfRulesetAsync();
        if (version == null) return NotFound(new { message = "Golf ruleset not configured" });
        return Ok(version);
    }

    [HttpPost("combat/configure")]
    [RequirePermission("sports.manage")]
    public async Task<IActionResult> ConfigureCombatSportsRuleset(
        [FromBody] ConfigureCombatSportsRequest request)
    {
        var version = await _combatSportsRulesetService
            .ConfigureCombatSportsRulesetAsync(request.CreatedBy);
        return Ok(version);
    }

    [HttpGet("combat/ruleset")]
    [RequirePermission("sports.read")]
    public async Task<IActionResult> GetCombatSportsRuleset()
    {
        var version = await _combatSportsRulesetService.GetCombatSportsRulesetAsync();
        if (version == null)
            return NotFound(new { message = "Combat sports ruleset not configured" });
        return Ok(version);
    }

    [HttpPost("athletics/configure")]
    [RequirePermission("sports.manage")]
    public async Task<IActionResult> ConfigureAthleticsRuleset(
        [FromBody] ConfigureAthleticsRequest request)
    {
        var version = await _athleticsRulesetService
            .ConfigureAthleticsRulesetAsync(request.CreatedBy);
        return Ok(version);
    }

    [HttpGet("athletics/ruleset")]
    [RequirePermission("sports.read")]
    public async Task<IActionResult> GetAthleticsRuleset()
    {
        var version = await _athleticsRulesetService.GetAthleticsRulesetAsync();
        if (version == null)
            return NotFound(new { message = "Athletics ruleset not configured" });
        return Ok(version);
    }

    [HttpPost("cricket/configure")]
    [RequirePermission("sports.manage")]
    public async Task<IActionResult> ConfigureCricketRuleset(
        [FromBody] ConfigureCricketRequest request)
    {
        var version = await _cricketT20RulesetService
            .ConfigureCricketT20RulesetAsync(request.CreatedBy);
        return Ok(version);
    }

    [HttpGet("cricket/ruleset")]
    [RequirePermission("sports.read")]
    public async Task<IActionResult> GetCricketRuleset()
    {
        var version = await _cricketT20RulesetService.GetCricketT20RulesetAsync();
        if (version == null)
            return NotFound(new { message = "Cricket T20 ruleset not configured" });
        return Ok(version);
    }

    private Task AuditAsync(string action, Guid sportId, object? original, object value, CancellationToken cancellationToken) =>
        _audit.RecordAsync(action, "Sport", sportId.ToString(), null, User.UserId(),
            original is null ? null : JsonSerializer.Serialize(original), JsonSerializer.Serialize(value),
            "Sports catalogue updated", cancellationToken);

    private string Actor() => User.UserId()?.ToString() ?? "platform-operator";

    private Task AuditVersionAsync(
        string action,
        SportsPlatform.Modules.Sports.Entities.SportDefinitionVersion version,
        object? original,
        object value,
        CancellationToken cancellationToken) =>
        _audit.RecordAsync(action, "SportDefinitionVersion", version.Id.ToString(), null, User.UserId(),
            original is null ? null : JsonSerializer.Serialize(original), JsonSerializer.Serialize(value),
            "Sport definition version lifecycle updated", cancellationToken);
}

public record CreateSportRequest(string Code, string Name, string? Description, string? Icon, IReadOnlyCollection<string> SupportedParticipantShapes);
public record UpdateSportRequest(string Name, string? Description, string? Icon, IReadOnlyCollection<string> SupportedParticipantShapes, bool IsActive);
public record SetTenantSportRequest(bool Enabled);
public sealed record SportCatalogueItem(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    bool IsActive,
    string? Icon,
    int SportPackageVersion,
    IReadOnlyList<string> SupportedParticipantShapes,
    IReadOnlyList<SportVersionSummary> Versions)
{
    public static SportCatalogueItem From(SportsPlatform.Modules.Sports.Entities.Sport sport) => new(
        sport.Id, sport.Code, sport.Name, sport.Description, sport.IsActive, sport.Icon,
        sport.CurrentVersion, sport.GetSupportedParticipantShapes(),
        sport.Versions.OrderByDescending(item => item.VersionNumber)
            .Select(item => new SportVersionSummary(item.Id, item.VersionNumber, item.Name, item.IsPublished, item.CreatedAt, item.PublishedAt))
            .ToArray());
}
public sealed record SportVersionSummary(Guid Id, int VersionNumber, string Name, bool IsPublished, DateTime CreatedAt, DateTime? PublishedAt);
public sealed record TenantSportCatalogueItem(SportCatalogueItem Sport, bool Enabled);
public record CreateVersionRequest(string Name);
public record CloneVersionRequest(string Name);
public sealed record ConfigureSportVersionRequest(
    string ContestShape,
    string ParticipantType,
    string SegmentDefinitions,
    string EventDefinitions,
    string MetricDefinitions,
    string ResultMethods,
    string RankingRules,
    string DisplayConfiguration,
    string ValidationConfiguration)
{
    public SportDefinitionConfiguration ToConfiguration() => new(
        ContestShape, ParticipantType, SegmentDefinitions, EventDefinitions,
        MetricDefinitions, ResultMethods, RankingRules, DisplayConfiguration,
        ValidationConfiguration);
}
public sealed record SportDefinitionVersionItem(
    Guid Id,
    Guid SportId,
    int VersionNumber,
    string Name,
    bool IsPublished,
    string CreatedBy,
    DateTime CreatedAt,
    DateTime? PublishedAt,
    string ContestShape,
    string ParticipantType,
    string SegmentDefinitions,
    string EventDefinitions,
    string MetricDefinitions,
    string ResultMethods,
    string RankingRules,
    string DisplayConfiguration,
    string ValidationConfiguration)
{
    public static SportDefinitionVersionItem From(
        SportsPlatform.Modules.Sports.Entities.SportDefinitionVersion version) => new(
        version.Id, version.SportId, version.VersionNumber, version.Name,
        version.IsPublished, version.CreatedBy, version.CreatedAt, version.PublishedAt,
        version.ContestShape, version.ParticipantType, version.SegmentDefinitions,
        version.EventDefinitions, version.MetricDefinitions, version.ResultMethods,
        version.RankingRules, version.DisplayConfiguration, version.ValidationConfiguration);
}
public record ConfigureFootballRequest(string CreatedBy);
public record ConfigureBasketballRequest(string CreatedBy);
public record ConfigureRugbyRequest(string CreatedBy, string? Variant = null);
public record ConfigureGolfRequest(string CreatedBy);
public record ConfigureCombatSportsRequest(string CreatedBy);
public record ConfigureAthleticsRequest(string CreatedBy);
public record ConfigureCricketRequest(string CreatedBy);
public sealed record ValidateMetricValueRequest(JsonElement Definition, string Value, string? Unit);
public sealed record RankMetricValueRequest(Guid ParticipantId, string Value, string? Unit);
public sealed record RankMetricValuesRequest(
    JsonElement Definition,
    IReadOnlyList<RankMetricValueRequest> Values);

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SportsPlatform.Infrastructure.Services;
using SportsPlatform.Api.Authorization;
using SportsPlatform.Modules.Audit;
using SportsPlatform.Modules.Participants.Entities;
using System.Text.Json;

namespace SportsPlatform.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ParticipantsController : ControllerBase
{
    private readonly IParticipantsService _participantsService;
    private readonly IFeatureEntitlementService _featureEntitlements;
    private readonly IUsageLimitsService _usageLimits;
    private readonly IAuditService _audit;

    public ParticipantsController(
        IParticipantsService participantsService,
        IFeatureEntitlementService featureEntitlements,
        IUsageLimitsService usageLimits,
        IAuditService audit)
    {
        _participantsService = participantsService;
        _featureEntitlements = featureEntitlements;
        _usageLimits = usageLimits;
        _audit = audit;
    }

    [HttpGet]
    [RequirePermission("participants.read")]
    public async Task<IActionResult> GetAll([FromQuery] Guid? tenantId, [FromQuery] string? participantType)
    {
        var effectiveTenantId = tenantId ?? User.TenantId();
        if (effectiveTenantId.HasValue && !User.CanAccessTenant(effectiveTenantId.Value))
            return Forbid();
        if (effectiveTenantId.HasValue && !string.IsNullOrEmpty(participantType))
        {
            if (!Enum.TryParse<ParticipantType>(participantType, true, out var type)
                || !Enum.IsDefined(type))
                return BadRequest(new { error = $"Unknown participant type '{participantType}'." });
            return Ok((await _participantsService.GetParticipantsByTypeAsync(effectiveTenantId.Value, type))
                .Select(ToSafeProfile));
        }
        if (effectiveTenantId.HasValue)
            return Ok((await _participantsService.GetParticipantsByTenantAsync(effectiveTenantId.Value))
                .Select(ToSafeProfile));
        return Ok(Array.Empty<object>());
    }

    [HttpGet("{id:guid}")]
    [RequirePermission("participants.read")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var participant = await _participantsService.GetParticipantByIdAsync(id);
        if (participant == null) return NotFound();
        if (!User.CanAccessTenant(participant.TenantId)) return Forbid();
        return Ok(ToSafeProfile(participant));
    }

    [HttpPost("teams")]
    [RequirePermission("participants.create")]
    public async Task<IActionResult> CreateTeam([FromBody] CreateTeamRequest request)
    {
        if (!User.CanAccessTenant(request.TenantId)) return Forbid();
        var limitResult = await CheckParticipantLimitAsync(request.TenantId);
        if (limitResult is not null) return limitResult;
        try
        {
            var team = await _participantsService.CreateTeamAsync(request.Name, request.ShortName, request.TenantId);
            await AuditOrganizationAsync("OrganizationProfileCreated", team,
                new { team.Name, team.ShortName, Kind = team.Type.ToString() }, "Team profile created");
            return CreatedAtAction(nameof(GetById), new { id = team.Id }, ToSafeProfile(team));
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(new { error = exception.Message });
        }
    }

    [HttpPost("athletes")]
    [RequirePermission("participants.create")]
    public async Task<IActionResult> CreateAthlete([FromBody] CreateAthleteRequest request)
    {
        if (!User.CanAccessTenant(request.TenantId)) return Forbid();
        var limitResult = await CheckParticipantLimitAsync(request.TenantId);
        if (limitResult is not null) return limitResult;
        var athlete = await _participantsService.CreateAthleteAsync(request.Name, request.ShortName, request.TenantId);
        await AuditAsync("AthleteProfileCreated", athlete,
            new { athlete.Name, athlete.ShortName }, "Athlete profile created");
        return CreatedAtAction(nameof(GetById), new { id = athlete.Id }, ToSafeProfile(athlete));
    }

    [HttpPost]
    [RequirePermission("participants.create")]
    public async Task<IActionResult> Create([FromBody] CreateParticipantRequest request)
    {
        if (!User.CanAccessTenant(request.TenantId)) return Forbid();
        if (!Enum.TryParse<ParticipantType>(request.Type, true, out var type)
            || !Enum.IsDefined(type))
            return BadRequest(new { error = $"Unknown participant type '{request.Type}'." });
        var limitResult = await CheckParticipantLimitAsync(request.TenantId);
        if (limitResult is not null) return limitResult;
        try
        {
            var participant = await _participantsService.CreateParticipantAsync(
                request.Name, request.ShortName, request.TenantId, type);
            if (participant is OrganizationParticipant organization)
            {
                await AuditOrganizationAsync("OrganizationProfileCreated", organization,
                    new { organization.Name, organization.ShortName, Kind = organization.Type.ToString() },
                    $"{organization.Type} profile created");
            }
            return CreatedAtAction(nameof(GetById), new { id = participant.Id }, ToSafeProfile(participant));
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

    [HttpPut("{id:guid}/sports/{sportId:guid}/attributes")]
    [RequirePermission("participants.create")]
    public async Task<IActionResult> SetSportAttributes(
        Guid id, Guid sportId, [FromBody] SetSportAttributesRequest request)
    {
        var participant = await _participantsService.GetParticipantByIdAsync(id);
        if (participant is null) return NotFound();
        if (!User.CanAccessTenant(participant.TenantId)) return Forbid();
        try
        {
            return Ok(await _participantsService.SetSportAttributesAsync(
                id, sportId, request.Attributes.GetRawText()));
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
    }

    [HttpGet("athletes/{id:guid}/profile")]
    [RequirePermission("participants.read-sensitive")]
    public async Task<IActionResult> GetAthleteProfile(Guid id)
    {
        var athlete = await _participantsService.GetAthleteByIdAsync(id, HttpContext.RequestAborted);
        if (athlete is null) return NotFound();
        if (!User.CanAccessTenant(athlete.TenantId)) return Forbid();
        return Ok(ToSensitiveProfile(athlete));
    }

    [HttpPut("athletes/{id:guid}/profile")]
    [RequirePermission("participants.manage-sensitive")]
    public async Task<IActionResult> UpdateAthleteProfile(
        Guid id, [FromBody] UpdateAthleteProfileRequest request)
    {
        var athlete = await _participantsService.GetAthleteByIdAsync(id, HttpContext.RequestAborted);
        if (athlete is null) return NotFound();
        if (!User.CanAccessTenant(athlete.TenantId)) return Forbid();
        var original = ProfileAuditSummary(athlete);
        try
        {
            athlete.UpdateIdentity(request.Name, request.ShortName, request.PhotoUrl);
            athlete.UpdatePersonalInfo(
                request.DateOfBirth, request.Gender, request.CompetitionCategory, request.Nationality);
            athlete.UpdateContact(request.ContactEmail, request.ContactPhone, null);
            athlete.UpdateEmergencyContact(
                request.EmergencyContactName,
                request.EmergencyContactRelationship,
                request.EmergencyContactPhone);
            athlete.SetMedicalClearance(
                request.MedicalClearanceStatus, request.MedicalClearanceExpiresAt);
            athlete.SetIdentificationDocuments(request.IdentificationDocuments);
            await _participantsService.UpdateParticipantAsync(athlete, HttpContext.RequestAborted);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
        await AuditAsync(
            "AthleteProfileUpdated", athlete, ProfileAuditSummary(athlete),
            request.Reason ?? "Athlete profile updated", original);
        return Ok(ToSensitiveProfile(athlete));
    }

    [HttpPost("athletes/{id:guid}/sports/{sportId:guid}")]
    [RequirePermission("participants.manage-sensitive")]
    public async Task<IActionResult> RegisterAthleteForSport(Guid id, Guid sportId)
    {
        var athlete = await _participantsService.GetAthleteByIdAsync(id, HttpContext.RequestAborted);
        if (athlete is null) return NotFound();
        if (!User.CanAccessTenant(athlete.TenantId)) return Forbid();
        try
        {
            await _participantsService.RegisterAthleteForSportAsync(
                id, sportId, HttpContext.RequestAborted);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
        await AuditAsync("AthleteSportRegistered", athlete,
            new { sportId }, "Athlete registered for sport");
        return NoContent();
    }

    [HttpGet("athletes/{id:guid}/history")]
    [RequirePermission("participants.read-sensitive")]
    public async Task<IActionResult> GetAthleteHistory(Guid id)
    {
        var athlete = await _participantsService.GetAthleteByIdAsync(id, HttpContext.RequestAborted);
        if (athlete is null) return NotFound();
        if (!User.CanAccessTenant(athlete.TenantId)) return Forbid();
        return Ok(await _audit.GetByEntityAsync("Athlete", id.ToString(), HttpContext.RequestAborted));
    }

    [HttpGet("organizations/{id:guid}/profile")]
    [RequirePermission("participants.read-sensitive")]
    public async Task<IActionResult> GetOrganizationProfile(Guid id)
    {
        var organization = await _participantsService.GetOrganizationByIdAsync(
            id, HttpContext.RequestAborted);
        if (organization is null) return NotFound();
        if (!User.CanAccessTenant(organization.TenantId)) return Forbid();
        return Ok(await ToOrganizationProfileAsync(organization));
    }

    [HttpPut("organizations/{id:guid}/profile")]
    [RequirePermission("participants.manage-sensitive")]
    public async Task<IActionResult> UpdateOrganizationProfile(
        Guid id, [FromBody] UpdateOrganizationProfileRequest request)
    {
        var organization = await _participantsService.GetOrganizationByIdAsync(
            id, HttpContext.RequestAborted);
        if (organization is null) return NotFound();
        if (!User.CanAccessTenant(organization.TenantId)) return Forbid();
        if (await _participantsService.OrganizationDuplicateExistsAsync(
                organization.TenantId, request.Name, request.ShortName, organization.Id,
                HttpContext.RequestAborted))
            return Conflict(new { error = "A team or club with the same name or short name already exists." });
        var original = OrganizationAuditSummary(organization);
        try
        {
            organization.UpdateProfile(
                request.Name, request.ShortName, request.LogoUrl, request.HomeVenue,
                request.Coach, request.Manager, request.ContactEmail, request.ContactPhone);
            await _participantsService.UpdateParticipantAsync(organization, HttpContext.RequestAborted);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
        await AuditOrganizationAsync(
            "OrganizationProfileUpdated", organization, OrganizationAuditSummary(organization),
            request.Reason ?? "Organization profile updated", original);
        return Ok(await ToOrganizationProfileAsync(organization));
    }

    [HttpGet("organizations/duplicates")]
    [RequirePermission("participants.read")]
    public async Task<IActionResult> CheckOrganizationDuplicate(
        [FromQuery] Guid tenantId,
        [FromQuery] string name,
        [FromQuery] string shortName,
        [FromQuery] Guid? excludeId)
    {
        if (!User.CanAccessTenant(tenantId)) return Forbid();
        return Ok(new
        {
            duplicate = await _participantsService.OrganizationDuplicateExistsAsync(
                tenantId, name, shortName, excludeId, HttpContext.RequestAborted)
        });
    }

    [HttpPost("teams/{id:guid}/rosters")]
    [RequirePermission("participants.manage-sensitive")]
    public async Task<IActionResult> CreateSeasonalRoster(
        Guid id, [FromBody] CreateSeasonalRosterRequest request)
    {
        var organization = await _participantsService.GetOrganizationByIdAsync(
            id, HttpContext.RequestAborted);
        if (organization is null) return NotFound();
        if (organization is not Team) return BadRequest(new { error = "Rosters can only be created for teams." });
        if (!User.CanAccessTenant(organization.TenantId)) return Forbid();
        try
        {
            var roster = await _participantsService.CreateSeasonalRosterAsync(
                id, request.Name, request.Season, request.StartsAt, request.EndsAt,
                request.CompetitionId, HttpContext.RequestAborted);
            await AuditOrganizationAsync("TeamRosterCreated", organization,
                new { roster.Id, roster.Name, roster.Season, roster.StartsAt, roster.EndsAt, roster.CompetitionId },
                request.Reason ?? "Seasonal roster created");
            return Ok(await ToOrganizationProfileAsync(
                (await _participantsService.GetOrganizationByIdAsync(id, HttpContext.RequestAborted))!));
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

    [HttpPost("teams/{id:guid}/rosters/{rosterId:guid}/athletes/{athleteId:guid}/transfer")]
    [RequirePermission("participants.manage-sensitive")]
    public async Task<IActionResult> TransferRosterAthlete(
        Guid id, Guid rosterId, Guid athleteId, [FromBody] TransferRosterAthleteRequest request)
    {
        var organization = await _participantsService.GetOrganizationByIdAsync(
            id, HttpContext.RequestAborted);
        if (organization is null) return NotFound();
        if (organization is not Team) return BadRequest(new { error = "Rosters can only be managed for teams." });
        if (!User.CanAccessTenant(organization.TenantId)) return Forbid();
        try
        {
            await _participantsService.TransferAthleteAsync(
                id, rosterId, athleteId, request.JerseyNumber, request.Position,
                request.Reason, HttpContext.RequestAborted);
            await AuditOrganizationAsync("RosterAthleteTransferred", organization,
                new { athleteId, targetRosterId = rosterId, request.JerseyNumber, request.Position },
                request.Reason);
            return Ok(await ToOrganizationProfileAsync(
                (await _participantsService.GetOrganizationByIdAsync(id, HttpContext.RequestAborted))!));
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(new { error = exception.Message });
        }
        catch (KeyNotFoundException exception)
        {
            return NotFound(new { error = exception.Message });
        }
    }

    [HttpGet("organizations/{id:guid}/history")]
    [RequirePermission("participants.read-sensitive")]
    public async Task<IActionResult> GetOrganizationHistory(Guid id)
    {
        var organization = await _participantsService.GetOrganizationByIdAsync(
            id, HttpContext.RequestAborted);
        if (organization is null) return NotFound();
        if (!User.CanAccessTenant(organization.TenantId)) return Forbid();
        return Ok(await _audit.GetByEntityAsync(
            "Organization", id.ToString(), HttpContext.RequestAborted));
    }

    private Task AuditAsync(
        string action, Athlete athlete, object current, string reason, object? original = null) =>
        _audit.RecordAsync(
            action, "Athlete", athlete.Id.ToString(), athlete.TenantId, User.UserId(),
            original is null ? null : JsonSerializer.Serialize(original),
            JsonSerializer.Serialize(current), reason, HttpContext.RequestAborted);

    private Task AuditOrganizationAsync(
        string action,
        OrganizationParticipant organization,
        object current,
        string reason,
        object? original = null) =>
        _audit.RecordAsync(
            action, "Organization", organization.Id.ToString(), organization.TenantId,
            User.UserId(), original is null ? null : JsonSerializer.Serialize(original),
            JsonSerializer.Serialize(current), reason, HttpContext.RequestAborted);

    private static object ToSafeProfile(Participant participant) => new
    {
        participant.Id,
        participant.TenantId,
        participant.Name,
        participant.ShortName,
        participant.Logo,
        participant.Type,
        participant.Status,
        participant.CreatedAt,
        participant.UpdatedAt,
        participant.SportProfiles,
        Athlete = participant is Athlete athlete ? new
        {
            athlete.Nationality,
            PhotoUrl = athlete.Photo
        } : null,
        Organization = participant is OrganizationParticipant organization ? new
        {
            organization.HomeVenue,
            organization.Coach,
            organization.Manager
        } : null,
        Team = participant is Team team ? new
        {
            team.HomeVenue,
            team.Coach,
            team.Manager
        } : null
    };

    private async Task<OrganizationProfileResponse> ToOrganizationProfileAsync(
        OrganizationParticipant organization)
    {
        var registrations = await _participantsService.GetRegistrationHistoryAsync(
            organization.Id, HttpContext.RequestAborted);
        var rosters = organization is Team team
            ? team.Rosters.OrderByDescending(item => item.StartsAt ?? item.CreatedAt)
                .Select(item => new TeamRosterResponse(
                    item.Id, item.Name, item.Season, item.StartsAt, item.EndsAt,
                    item.CompetitionId, item.IsActive, item.CreatedAt,
                    item.Entries.OrderBy(entry => entry.JoinedAt).Select(entry =>
                        new RosterEntryResponse(
                            entry.Id, entry.AthleteId, entry.JerseyNumber, entry.Position,
                            entry.IsActive, entry.JoinedAt, entry.LeftAt, entry.MovementReason))
                        .ToArray())).ToArray()
            : Array.Empty<TeamRosterResponse>();
        return new OrganizationProfileResponse(
            organization.Id, organization.TenantId, organization.Type.ToString(),
            organization.Name, organization.ShortName, organization.Logo,
            organization.HomeVenue, organization.Coach, organization.Manager,
            organization.ContactEmail, organization.ContactPhone, rosters,
            registrations.Select(item => new OrganizationRegistrationResponse(
                item.Id, item.CompetitionId, item.ContestId, item.Type.ToString(),
                item.Status.ToString(), item.RegisteredAt, item.ConfirmedAt,
                item.CancelledAt, item.CancellationReason, item.CreatedAt)).ToArray(),
            organization.CreatedAt, organization.UpdatedAt);
    }

    private static object OrganizationAuditSummary(OrganizationParticipant organization) => new
    {
        organization.Name,
        organization.ShortName,
        Kind = organization.Type.ToString(),
        HasLogo = !string.IsNullOrWhiteSpace(organization.Logo),
        HasContactEmail = !string.IsNullOrWhiteSpace(organization.ContactEmail),
        HasContactPhone = !string.IsNullOrWhiteSpace(organization.ContactPhone),
        RosterCount = organization is Team team ? team.Rosters.Count : 0
    };

    private static AthleteProfileResponse ToSensitiveProfile(Athlete athlete) => new(
        athlete.Id, athlete.TenantId, athlete.Name, athlete.ShortName,
        athlete.DateOfBirth, athlete.Gender, athlete.CompetitionCategory,
        athlete.Nationality, athlete.Photo, athlete.ContactEmail, athlete.ContactPhone,
        athlete.EmergencyContactName, athlete.EmergencyContactRelationship,
        athlete.EmergencyContactPhone, athlete.MedicalClearanceStatus,
        athlete.MedicalClearance, athlete.MedicalClearanceExpiresAt,
        athlete.GetIdentificationDocuments(),
        athlete.SportRegistrations.Where(item => item.IsActive)
            .Select(item => item.SportId).ToArray(), athlete.CreatedAt, athlete.UpdatedAt);

    private static object ProfileAuditSummary(Athlete athlete) => new
    {
        athlete.Name,
        athlete.ShortName,
        HasDateOfBirth = athlete.DateOfBirth.HasValue,
        HasContactEmail = !string.IsNullOrWhiteSpace(athlete.ContactEmail),
        HasContactPhone = !string.IsNullOrWhiteSpace(athlete.ContactPhone),
        HasEmergencyContact = !string.IsNullOrWhiteSpace(athlete.EmergencyContactPhone),
        athlete.MedicalClearanceStatus,
        athlete.MedicalClearanceExpiresAt,
        IdentificationDocumentTypes = athlete.GetIdentificationDocuments()
            .Select(item => item.Type).ToArray(),
        ActiveSportRegistrations = athlete.SportRegistrations.Count(item => item.IsActive)
    };

    private async Task<IActionResult?> CheckParticipantLimitAsync(Guid tenantId)
    {
        if (!await _featureEntitlements.HasFeatureAsync(tenantId, "sports.max_participants"))
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                error = "FeatureNotEntitled",
                feature = "sports.max_participants",
                message = "The current subscription does not include participant creation."
            });
        if (!await _usageLimits.CanCreateResourceAsync(tenantId, "max_participants"))
            return Conflict(new
            {
                error = "UsageLimitExceeded",
                resource = "max_participants",
                message = "The subscription's participant limit has been reached."
            });
        return null;
    }
}

public record CreateTeamRequest(string Name, string ShortName, Guid TenantId);
public record CreateAthleteRequest(string Name, string ShortName, Guid TenantId);
public record CreateParticipantRequest(string Name, string ShortName, Guid TenantId, string Type);
public record SetSportAttributesRequest(System.Text.Json.JsonElement Attributes);
public sealed record UpdateAthleteProfileRequest(
    string Name,
    string ShortName,
    DateTime? DateOfBirth,
    string? Gender,
    string? CompetitionCategory,
    string? Nationality,
    string? PhotoUrl,
    string? ContactEmail,
    string? ContactPhone,
    string? EmergencyContactName,
    string? EmergencyContactRelationship,
    string? EmergencyContactPhone,
    string MedicalClearanceStatus,
    DateTime? MedicalClearanceExpiresAt,
    IReadOnlyList<AthleteIdentificationDocument>? IdentificationDocuments,
    string? Reason = null);

public sealed record AthleteProfileResponse(
    Guid Id,
    Guid TenantId,
    string Name,
    string ShortName,
    DateTime? DateOfBirth,
    string? Gender,
    string? CompetitionCategory,
    string? Nationality,
    string? PhotoUrl,
    string? ContactEmail,
    string? ContactPhone,
    string? EmergencyContactName,
    string? EmergencyContactRelationship,
    string? EmergencyContactPhone,
    string MedicalClearanceStatus,
    bool MedicalClearance,
    DateTime? MedicalClearanceExpiresAt,
    IReadOnlyList<AthleteIdentificationDocument> IdentificationDocuments,
    IReadOnlyList<Guid> SportIds,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed record UpdateOrganizationProfileRequest(
    string Name,
    string ShortName,
    string? LogoUrl,
    string? HomeVenue,
    string? Coach,
    string? Manager,
    string? ContactEmail,
    string? ContactPhone,
    string? Reason = null);

public sealed record CreateSeasonalRosterRequest(
    string Name,
    string Season,
    DateTime? StartsAt,
    DateTime? EndsAt,
    Guid? CompetitionId,
    string? Reason = null);

public sealed record TransferRosterAthleteRequest(
    string? JerseyNumber,
    string? Position,
    string Reason);

public sealed record OrganizationProfileResponse(
    Guid Id,
    Guid TenantId,
    string Kind,
    string Name,
    string ShortName,
    string? LogoUrl,
    string? HomeVenue,
    string? Coach,
    string? Manager,
    string? ContactEmail,
    string? ContactPhone,
    IReadOnlyList<TeamRosterResponse> Rosters,
    IReadOnlyList<OrganizationRegistrationResponse> Registrations,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed record TeamRosterResponse(
    Guid Id,
    string Name,
    string Season,
    DateTime? StartsAt,
    DateTime? EndsAt,
    Guid? CompetitionId,
    bool IsActive,
    DateTime CreatedAt,
    IReadOnlyList<RosterEntryResponse> Entries);

public sealed record RosterEntryResponse(
    Guid Id,
    Guid AthleteId,
    string? JerseyNumber,
    string? Position,
    bool IsActive,
    DateTime JoinedAt,
    DateTime? LeftAt,
    string? MovementReason);

public sealed record OrganizationRegistrationResponse(
    Guid Id,
    Guid? CompetitionId,
    Guid? ContestId,
    string Type,
    string Status,
    DateTime? RegisteredAt,
    DateTime? ConfirmedAt,
    DateTime? CancelledAt,
    string? CancellationReason,
    DateTime CreatedAt);

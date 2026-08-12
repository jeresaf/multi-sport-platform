using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SportsPlatform.Api.Authorization;
using SportsPlatform.Infrastructure.Persistence;
using SportsPlatform.Infrastructure.Services;
using SportsPlatform.Modules.Audit.Entities;
using SportsPlatform.Modules.Sponsorship.Entities;

namespace SportsPlatform.Api.Controllers;

[ApiController]
[Route("api/sponsors")]
[Authorize]
public sealed class SponsorsController : ControllerBase
{
    private readonly SportsPlatformDbContext _database;
    private readonly IFeatureEntitlementService _featureEntitlements;

    public SponsorsController(
        SportsPlatformDbContext database,
        IFeatureEntitlementService featureEntitlements)
    {
        _database = database;
        _featureEntitlements = featureEntitlements;
    }

    [HttpGet]
    [RequirePermission("sponsorships.read")]
    public async Task<IActionResult> List(
        [FromQuery] Guid? tenantId,
        [FromQuery] bool includeInactive = true,
        CancellationToken cancellationToken = default)
    {
        var tenant = ResolveTenant(tenantId);
        if (tenant.Error is not null) return tenant.Error;

        var sponsors = await _database.Sponsors.AsNoTracking()
            .Where(item => item.TenantId == tenant.Id
                && (includeInactive || item.IsActive))
            .OrderBy(item => item.Name)
            .ToArrayAsync(cancellationToken);
        var sponsorIds = sponsors.Select(item => item.Id).ToArray();
        var sponsorships = await _database.Sponsorships.AsNoTracking()
            .Where(item => item.TenantId == tenant.Id
                && sponsorIds.Contains(item.SponsorId))
            .OrderBy(item => item.DisplayOrder)
            .ThenByDescending(item => item.CreatedAt)
            .ToArrayAsync(cancellationToken);
        var now = DateTime.UtcNow;

        return Ok(new
        {
            tenantId = tenant.Id,
            placements = SponsorPlacements.All.OrderBy(item => item),
            items = sponsors.Select(sponsor => SponsorResponse(
                sponsor,
                sponsorships.Where(item => item.SponsorId == sponsor.Id),
                now))
        });
    }

    [HttpGet("{id:guid}")]
    [RequirePermission("sponsorships.read")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var sponsor = await _database.Sponsors.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (sponsor is null) return NotFound();
        if (!User.CanAccessTenant(sponsor.TenantId)) return Forbid();
        var sponsorships = await _database.Sponsorships.AsNoTracking()
            .Where(item => item.TenantId == sponsor.TenantId && item.SponsorId == sponsor.Id)
            .OrderBy(item => item.DisplayOrder)
            .ThenByDescending(item => item.CreatedAt)
            .ToArrayAsync(cancellationToken);
        return Ok(SponsorResponse(sponsor, sponsorships, DateTime.UtcNow));
    }

    [HttpPost]
    [RequirePermission("sponsorships.manage")]
    public async Task<IActionResult> Create(
        [FromBody] CreateSponsorRequest request,
        CancellationToken cancellationToken)
    {
        if (!User.CanAccessTenant(request.TenantId)) return Forbid();
        var error = ValidateProfile(request.Name, request.LogoUrl, request.Website);
        if (error is not null) return BadRequest(new { error });
        if (!await _database.Tenants.AnyAsync(item => item.Id == request.TenantId, cancellationToken))
            return BadRequest(new { error = "Tenant was not found." });

        var sponsor = new Sponsor(Guid.NewGuid(), request.TenantId, request.Name.Trim());
        ApplyProfile(sponsor, request.Description, request.LogoUrl, request.Website,
            request.ContactEmail, request.ContactPhone, request.Tier);
        _database.Sponsors.Add(sponsor);
        AddAudit(request.TenantId, "SponsorCreated", "Sponsor", sponsor.Id,
            new { sponsor.Name, sponsor.LogoUrl, sponsor.Website, sponsor.Tier });
        await _database.SaveChangesAsync(cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = sponsor.Id }, SponsorResponse(
            sponsor, Array.Empty<Sponsorship>(), DateTime.UtcNow));
    }

    [HttpPut("{id:guid}")]
    [RequirePermission("sponsorships.manage")]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateSponsorRequest request,
        CancellationToken cancellationToken)
    {
        var sponsor = await _database.Sponsors.SingleOrDefaultAsync(
            item => item.Id == id, cancellationToken);
        if (sponsor is null) return NotFound();
        if (!User.CanAccessTenant(sponsor.TenantId)) return Forbid();
        var error = ValidateProfile(request.Name, request.LogoUrl, request.Website);
        if (error is not null) return BadRequest(new { error });
        var original = ProfileSnapshot(sponsor);
        sponsor.Rename(request.Name.Trim());
        ApplyProfile(sponsor, request.Description, request.LogoUrl, request.Website,
            request.ContactEmail, request.ContactPhone, request.Tier);
        if (request.IsActive) sponsor.Activate(); else sponsor.Deactivate();
        AddAudit(sponsor.TenantId, "SponsorUpdated", "Sponsor", sponsor.Id,
            ProfileSnapshot(sponsor), original);
        await _database.SaveChangesAsync(cancellationToken);
        var sponsorships = await _database.Sponsorships.AsNoTracking()
            .Where(item => item.TenantId == sponsor.TenantId && item.SponsorId == sponsor.Id)
            .ToArrayAsync(cancellationToken);
        return Ok(SponsorResponse(sponsor, sponsorships, DateTime.UtcNow));
    }

    [HttpPost("{sponsorId:guid}/sponsorships")]
    [RequirePermission("sponsorships.manage")]
    public async Task<IActionResult> CreateSponsorship(
        Guid sponsorId,
        [FromBody] UpsertSponsorshipRequest request,
        CancellationToken cancellationToken)
    {
        var sponsor = await _database.Sponsors.SingleOrDefaultAsync(
            item => item.Id == sponsorId, cancellationToken);
        if (sponsor is null) return NotFound();
        if (!User.CanAccessTenant(sponsor.TenantId)) return Forbid();
        if (!await _featureEntitlements.HasFeatureAsync(
                sponsor.TenantId, "sponsorship.placements", cancellationToken))
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                error = "FeatureNotEntitled",
                feature = "sponsorship.placements",
                message = "The current subscription does not include sponsor placements."
            });
        var validation = await ValidateSponsorshipAsync(sponsor.TenantId, request, cancellationToken);
        if (validation is not null) return BadRequest(new { error = validation });

        var sponsorship = new Sponsorship(
            Guid.NewGuid(), sponsor.TenantId, sponsor.Id, request.Type,
            request.Name.Trim(), request.Value);
        ApplySponsorship(sponsorship, request);
        _database.Sponsorships.Add(sponsorship);
        AddAudit(sponsor.TenantId, "SponsorshipCreated", "Sponsorship", sponsorship.Id,
            SponsorshipSnapshot(sponsorship));
        await _database.SaveChangesAsync(cancellationToken);
        return Created($"/api/sponsors/{sponsor.Id}", SponsorshipResponse(
            sponsorship, DateTime.UtcNow));
    }

    [HttpPut("sponsorships/{id:guid}")]
    [RequirePermission("sponsorships.manage")]
    public async Task<IActionResult> UpdateSponsorship(
        Guid id,
        [FromBody] UpsertSponsorshipRequest request,
        CancellationToken cancellationToken)
    {
        var sponsorship = await _database.Sponsorships.SingleOrDefaultAsync(
            item => item.Id == id, cancellationToken);
        if (sponsorship is null) return NotFound();
        if (!User.CanAccessTenant(sponsorship.TenantId)) return Forbid();
        if (!await _featureEntitlements.HasFeatureAsync(
                sponsorship.TenantId, "sponsorship.placements", cancellationToken))
            return PlacementNotEntitled();
        var validation = await ValidateSponsorshipAsync(
            sponsorship.TenantId, request, cancellationToken);
        if (validation is not null) return BadRequest(new { error = validation });
        var original = SponsorshipSnapshot(sponsorship);
        sponsorship.UpdateTerms(request.Type, request.Name.Trim(), request.Value);
        ApplySponsorship(sponsorship, request);
        AddAudit(sponsorship.TenantId, "SponsorshipUpdated", "Sponsorship", id,
            SponsorshipSnapshot(sponsorship), original);
        await _database.SaveChangesAsync(cancellationToken);
        return Ok(SponsorshipResponse(sponsorship, DateTime.UtcNow));
    }

    [HttpPost("sponsorships/{id:guid}/activate")]
    [RequirePermission("sponsorships.manage")]
    public Task<IActionResult> Activate(Guid id, CancellationToken cancellationToken) =>
        ChangeStatus(id, SponsorshipStatus.Active, cancellationToken);

    [HttpPost("sponsorships/{id:guid}/terminate")]
    [RequirePermission("sponsorships.manage")]
    public Task<IActionResult> Terminate(Guid id, CancellationToken cancellationToken) =>
        ChangeStatus(id, SponsorshipStatus.Terminated, cancellationToken);

    private async Task<IActionResult> ChangeStatus(
        Guid id,
        SponsorshipStatus status,
        CancellationToken cancellationToken)
    {
        var sponsorship = await _database.Sponsorships.SingleOrDefaultAsync(
            item => item.Id == id, cancellationToken);
        if (sponsorship is null) return NotFound();
        if (!User.CanAccessTenant(sponsorship.TenantId)) return Forbid();
        if (!await _featureEntitlements.HasFeatureAsync(
                sponsorship.TenantId, "sponsorship.placements", cancellationToken))
            return PlacementNotEntitled();
        var original = sponsorship.Status;
        if (status == SponsorshipStatus.Active) sponsorship.Approve();
        else sponsorship.Terminate();
        AddAudit(sponsorship.TenantId, $"Sponsorship{status}", "Sponsorship", id,
            new { status = sponsorship.Status.ToString() }, new { status = original.ToString() });
        await _database.SaveChangesAsync(cancellationToken);
        return Ok(SponsorshipResponse(sponsorship, DateTime.UtcNow));
    }

    private async Task<string?> ValidateSponsorshipAsync(
        Guid tenantId,
        UpsertSponsorshipRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 200)
            return "A sponsorship name of at most 200 characters is required.";
        if (request.Value < 0) return "Sponsorship value cannot be negative.";
        if (request.Currency.Length != 3 || !request.Currency.All(char.IsLetter))
            return "Currency must be a three-letter code.";
        if (request.StartDate.HasValue && request.EndDate.HasValue
            && request.EndDate.Value < request.StartDate.Value)
            return "End date cannot be before start date.";
        if (!SponsorPlacements.All.Contains(request.Placement))
            return $"Placement must be one of: {string.Join(", ", SponsorPlacements.All.OrderBy(item => item))}.";
        if (request.DisplayOrder < 0) return "Display order cannot be negative.";
        if (request.CompetitionId.HasValue == request.ContestId.HasValue)
            return "Select exactly one competition or contest association.";
        if (request.CompetitionId.HasValue && !await _database.Competitions.AnyAsync(item =>
                item.Id == request.CompetitionId.Value && item.TenantId == tenantId,
                cancellationToken))
            return "Competition does not belong to this tenant.";
        if (request.ContestId.HasValue && !await _database.Contests.AnyAsync(item =>
                item.Id == request.ContestId.Value && item.TenantId == tenantId,
                cancellationToken))
            return "Contest does not belong to this tenant.";
        return null;
    }

    private ObjectResult PlacementNotEntitled() => StatusCode(
        StatusCodes.Status403Forbidden,
        new
        {
            error = "FeatureNotEntitled",
            feature = "sponsorship.placements",
            message = "The current subscription does not include sponsor placements."
        });

    private static void ApplyProfile(
        Sponsor sponsor,
        string? description,
        string? logoUrl,
        string? website,
        string? contactEmail,
        string? contactPhone,
        SponsorTier tier)
    {
        sponsor.SetDescription(TrimOrNull(description));
        sponsor.SetLogoUrl(TrimOrNull(logoUrl));
        sponsor.SetWebsite(TrimOrNull(website));
        sponsor.SetContact(TrimOrNull(contactEmail), TrimOrNull(contactPhone));
        sponsor.SetTier(tier);
    }

    private static void ApplySponsorship(
        Sponsorship sponsorship,
        UpsertSponsorshipRequest request)
    {
        sponsorship.SetDescription(TrimOrNull(request.Description));
        sponsorship.SetCurrency(request.Currency.Trim().ToUpperInvariant());
        sponsorship.SetDates(request.StartDate?.ToUniversalTime(), request.EndDate?.ToUniversalTime());
        sponsorship.SetBenefits(TrimOrNull(request.Benefits));
        sponsorship.SetContractUrl(TrimOrNull(request.ContractUrl));
        sponsorship.SetAssociation(request.CompetitionId, request.ContestId);
        sponsorship.SetPlacement(request.Placement, request.DisplayOrder);
    }

    private static string? ValidateProfile(string name, string? logoUrl, string? website)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 200)
            return "A sponsor name of at most 200 characters is required.";
        if (!ValidWebUrl(logoUrl)) return "Logo URL must be an absolute HTTP or HTTPS URL.";
        if (!ValidWebUrl(website)) return "Website must be an absolute HTTP or HTTPS URL.";
        return null;
    }

    private static bool ValidWebUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return true;
        return Uri.TryCreate(value, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }

    private (Guid Id, IActionResult? Error) ResolveTenant(Guid? requested)
    {
        var tenantId = requested ?? User.TenantId();
        if (!tenantId.HasValue)
            return (Guid.Empty, BadRequest(new { error = "tenantId is required." }));
        return User.CanAccessTenant(tenantId.Value)
            ? (tenantId.Value, null)
            : (Guid.Empty, Forbid());
    }

    private void AddAudit(
        Guid tenantId,
        string action,
        string entityType,
        Guid entityId,
        object value,
        object? original = null)
    {
        var audit = new AuditEntry(
            Guid.NewGuid(), tenantId, User.UserId(), action, entityType, entityId.ToString(),
            original is null ? null : JsonSerializer.Serialize(original),
            JsonSerializer.Serialize(value), null);
        audit.SetRequestContext(
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            Request.Headers.UserAgent.ToString());
        _database.AuditEntries.Add(audit);
    }

    private static object SponsorResponse(
        Sponsor sponsor,
        IEnumerable<Sponsorship> sponsorships,
        DateTime now) => new
    {
        sponsor.Id,
        sponsor.TenantId,
        sponsor.Name,
        sponsor.Description,
        sponsor.LogoUrl,
        sponsor.Website,
        sponsor.ContactEmail,
        sponsor.ContactPhone,
        tier = sponsor.Tier.ToString(),
        sponsor.IsActive,
        sponsor.CreatedAt,
        sponsor.UpdatedAt,
        sponsorships = sponsorships.Select(item => SponsorshipResponse(item, now))
    };

    private static object SponsorshipResponse(Sponsorship item, DateTime now) => new
    {
        item.Id,
        item.SponsorId,
        item.TenantId,
        item.CompetitionId,
        item.ContestId,
        type = item.Type.ToString(),
        item.Name,
        item.Description,
        item.Value,
        item.Currency,
        status = item.Status.ToString(),
        item.StartDate,
        item.EndDate,
        item.Benefits,
        item.ContractUrl,
        item.Placement,
        item.DisplayOrder,
        isCurrentlyVisible = item.Status == SponsorshipStatus.Active
            && (!item.StartDate.HasValue || item.StartDate <= now)
            && (!item.EndDate.HasValue || item.EndDate >= now),
        item.CreatedAt,
        item.UpdatedAt
    };

    private static object ProfileSnapshot(Sponsor item) => new
    {
        item.Name, item.Description, item.LogoUrl, item.Website,
        item.ContactEmail, item.ContactPhone, tier = item.Tier.ToString(), item.IsActive
    };

    private static object SponsorshipSnapshot(Sponsorship item) => new
    {
        item.Name, type = item.Type.ToString(), item.Value, item.Currency,
        item.CompetitionId, item.ContestId, item.StartDate, item.EndDate,
        item.Placement, item.DisplayOrder, status = item.Status.ToString()
    };

    private static string? TrimOrNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed record CreateSponsorRequest(
    Guid TenantId,
    string Name,
    string? Description,
    string? LogoUrl,
    string? Website,
    string? ContactEmail,
    string? ContactPhone,
    SponsorTier Tier = SponsorTier.Standard);

public sealed record UpdateSponsorRequest(
    string Name,
    string? Description,
    string? LogoUrl,
    string? Website,
    string? ContactEmail,
    string? ContactPhone,
    SponsorTier Tier,
    bool IsActive);

public sealed record UpsertSponsorshipRequest(
    SponsorshipType Type,
    string Name,
    string? Description,
    decimal Value,
    string Currency,
    DateTime? StartDate,
    DateTime? EndDate,
    string? Benefits,
    string? ContractUrl,
    Guid? CompetitionId,
    Guid? ContestId,
    string Placement,
    int DisplayOrder);

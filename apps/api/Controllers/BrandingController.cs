using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using SportsPlatform.Infrastructure.Services;
using SportsPlatform.Api.Authorization;
using SportsPlatform.Api.Services;
using SportsPlatform.Infrastructure.Persistence;
using SportsPlatform.Modules.Audit;

namespace SportsPlatform.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class BrandingController : ControllerBase
{
    private readonly IBrandingService _brandingService;
    private readonly IFeatureEntitlementService _featureEntitlements;
    private readonly IBrandingAssetStorage _assetStorage;
    private readonly IUsageLimitsService _usageLimits;
    private readonly SportsPlatformDbContext _database;
    private readonly IAuditService _audit;

    public BrandingController(
        IBrandingService brandingService,
        IFeatureEntitlementService featureEntitlements,
        IBrandingAssetStorage assetStorage,
        IUsageLimitsService usageLimits,
        SportsPlatformDbContext database,
        IAuditService audit)
    {
        _brandingService = brandingService;
        _featureEntitlements = featureEntitlements;
        _assetStorage = assetStorage;
        _usageLimits = usageLimits;
        _database = database;
        _audit = audit;
    }

    [HttpGet("tenant/{tenantId:guid}")]
    [RequirePermission("branding.read")]
    public async Task<IActionResult> GetByTenant(Guid tenantId)
    {
        if (!User.CanAccessTenant(tenantId)) return Forbid();
        var branding = await _brandingService.GetBrandingByTenantAsync(tenantId);
        if (branding == null) return NotFound();
        return Ok(branding);
    }

    [HttpPut("tenant/{tenantId:guid}")]
    [RequirePermission("branding.manage")]
    public async Task<IActionResult> CreateOrUpdate(Guid tenantId, [FromBody] UpdateBrandingRequest request)
    {
        if (!User.CanAccessTenant(tenantId)) return Forbid();
        if (!string.IsNullOrWhiteSpace(request.LogoUrl) &&
            !await _featureEntitlements.HasFeatureAsync(tenantId, "branding.custom_logo"))
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                error = "FeatureNotEntitled",
                feature = "branding.custom_logo",
                message = "The current subscription does not include a custom logo."
            });
        if (HasCustomColors(request) &&
            !await _featureEntitlements.HasFeatureAsync(tenantId, "branding.custom_colors"))
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                error = "FeatureNotEntitled",
                feature = "branding.custom_colors",
                message = "The current subscription does not include custom colors."
            });
        var tenantSlug = await _database.Tenants.AsNoTracking()
            .Where(item => item.Id == tenantId)
            .Select(item => item.Slug)
            .SingleOrDefaultAsync(HttpContext.RequestAborted);
        if (tenantSlug is null) return NotFound();
        if (!string.IsNullOrWhiteSpace(request.PublicSlug)
            && !string.Equals(request.PublicSlug.Trim(), tenantSlug, StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { error = "The public slug is immutable and must match the tenant slug." });
        var original = await _brandingService.GetBrandingByTenantAsync(tenantId, HttpContext.RequestAborted);
        var originalLogoUrl = original?.LogoUrl;
        var originalSnapshot = original is null ? null : new
        {
            original.TenantName, original.ShortName, original.LogoUrl,
            original.PrimaryColor, original.SecondaryColor, original.AccentColor,
            original.PublicSlug, original.ContactEmail, original.ContactPhone, original.Website
        };
        var branding = await _brandingService.CreateOrUpdateBrandingAsync(
            tenantId, request.TenantName, request.ShortName, request.LogoUrl,
            request.PrimaryColor, request.SecondaryColor, request.AccentColor,
            tenantSlug, request.ContactEmail, request.ContactPhone, request.Website,
            HttpContext.RequestAborted);
        await DeleteReplacedManagedLogoAsync(tenantId, originalLogoUrl, branding.LogoUrl, HttpContext.RequestAborted);
        await AuditAsync("TenantBrandingUpdated", tenantId, branding.Id, request,
            originalSnapshot);
        return Ok(branding);
    }

    [HttpPost("tenant/{tenantId:guid}/logo")]
    [RequirePermission("branding.manage")]
    [RequestSizeLimit(2_200_000)]
    public async Task<IActionResult> UploadLogo(Guid tenantId, IFormFile file, CancellationToken cancellationToken)
    {
        if (!User.CanAccessTenant(tenantId)) return Forbid();
        if (!await _featureEntitlements.HasFeatureAsync(tenantId, "branding.custom_logo", cancellationToken))
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                error = "FeatureNotEntitled", feature = "branding.custom_logo",
                message = "The current subscription does not include a custom logo."
            });
        if (!await _usageLimits.CanCreateResourceAsync(tenantId, "storage_mb", cancellationToken))
            return Conflict(new
            {
                error = "UsageLimitReached", metric = "storage_mb",
                message = "The tenant storage limit has been reached."
            });
        var validation = await ValidateLogoAsync(file, cancellationToken);
        if (validation.Error is not null) return BadRequest(new { error = validation.Error });
        var existing = await _brandingService.GetBrandingByTenantAsync(tenantId, cancellationToken);
        var existingLogoUrl = existing?.LogoUrl;
        var tenant = await _database.Tenants.AsNoTracking().SingleOrDefaultAsync(item => item.Id == tenantId, cancellationToken);
        if (tenant is null) return NotFound();

        var fileName = $"{Guid.NewGuid():N}{validation.Extension}";
        await using var stream = new MemoryStream(validation.Content!);
        await _assetStorage.StoreAsync(tenantId, fileName, stream, stream.Length, validation.ContentType!, cancellationToken);
        await _usageLimits.IncrementUsageAsync(
            tenantId, UsageLimitsService.StorageBytesMetric, stream.Length, cancellationToken);
        var logoUrl = $"{Request.Scheme}://{Request.Host}/api/branding/assets/{tenantId:D}/{fileName}";
        var branding = await _brandingService.CreateOrUpdateBrandingAsync(
            tenantId, existing?.TenantName ?? tenant.Name, existing?.ShortName ?? tenant.ShortName,
            logoUrl, existing?.PrimaryColor, existing?.SecondaryColor, existing?.AccentColor,
            tenant.Slug, existing?.ContactEmail, existing?.ContactPhone, existing?.Website,
            cancellationToken);
        await DeleteReplacedManagedLogoAsync(tenantId, existingLogoUrl, branding.LogoUrl, cancellationToken);
        await AuditAsync("TenantLogoUploaded", tenantId, branding.Id,
            new { fileName, validation.ContentType, size = validation.Content!.Length });
        return Ok(branding);
    }

    [HttpGet("assets/{tenantId:guid}/{fileName}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetAsset(Guid tenantId, string fileName, CancellationToken cancellationToken)
    {
        if (!System.Text.RegularExpressions.Regex.IsMatch(
                fileName, "^[0-9a-f]{32}\\.(png|jpg|webp)$", System.Text.RegularExpressions.RegexOptions.CultureInvariant))
            return NotFound();
        var asset = await _assetStorage.GetAsync(tenantId, fileName, cancellationToken);
        if (asset is null) return NotFound();
        Response.Headers.CacheControl = "public,max-age=31536000,immutable";
        return File(asset.Content, asset.ContentType);
    }

    private async Task AuditAsync(string action, Guid tenantId, Guid brandingId, object current, object? original = null) =>
        await _audit.RecordAsync(action, "TenantBranding", brandingId.ToString(), tenantId,
            User.UserId(), original is null ? null : JsonSerializer.Serialize(original),
            JsonSerializer.Serialize(current), "Tenant branding changed", HttpContext.RequestAborted);

    private async Task DeleteReplacedManagedLogoAsync(
        Guid tenantId,
        string? previousUrl,
        string? currentUrl,
        CancellationToken cancellationToken)
    {
        if (string.Equals(previousUrl, currentUrl, StringComparison.Ordinal)) return;
        var fileName = ManagedAssetFileName(tenantId, previousUrl);
        if (fileName is not null)
        {
            var previous = await _assetStorage.GetAsync(tenantId, fileName, cancellationToken);
            await _assetStorage.DeleteAsync(tenantId, fileName, cancellationToken);
            if (previous is not null)
                await _usageLimits.IncrementUsageAsync(
                    tenantId, UsageLimitsService.StorageBytesMetric,
                    -previous.Content.LongLength, cancellationToken);
        }
    }

    private static string? ManagedAssetFileName(Guid tenantId, string? value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)) return null;
        var prefix = $"/api/branding/assets/{tenantId:D}/";
        if (!uri.AbsolutePath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return null;
        var fileName = uri.AbsolutePath[prefix.Length..];
        return System.Text.RegularExpressions.Regex.IsMatch(
            fileName, "^[0-9a-f]{32}\\.(png|jpg|webp)$", System.Text.RegularExpressions.RegexOptions.CultureInvariant)
            ? fileName : null;
    }

    private static async Task<(string? Error, byte[]? Content, string? ContentType, string? Extension)> ValidateLogoAsync(
        IFormFile? file,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0) return ("A logo file is required.", null, null, null);
        if (file.Length > 2_000_000) return ("Logo files cannot exceed 2 MB.", null, null, null);
        await using var input = file.OpenReadStream();
        await using var output = new MemoryStream((int)file.Length);
        await input.CopyToAsync(output, cancellationToken);
        var bytes = output.ToArray();
        if (bytes.Length >= 8 && bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }))
            return (null, bytes, "image/png", ".png");
        if (bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
            return (null, bytes, "image/jpeg", ".jpg");
        if (bytes.Length >= 12 && bytes.AsSpan(0, 4).SequenceEqual("RIFF"u8)
            && bytes.AsSpan(8, 4).SequenceEqual("WEBP"u8))
            return (null, bytes, "image/webp", ".webp");
        return ("Only genuine PNG, JPEG, or WebP logo files are accepted.", null, null, null);
    }

    private static bool HasCustomColors(UpdateBrandingRequest request) =>
        !string.IsNullOrWhiteSpace(request.PrimaryColor) ||
        !string.IsNullOrWhiteSpace(request.SecondaryColor) ||
        !string.IsNullOrWhiteSpace(request.AccentColor);
}

public record UpdateBrandingRequest(
    string TenantName,
    string? ShortName,
    string? LogoUrl,
    string? PrimaryColor,
    string? SecondaryColor,
    string? AccentColor,
    string? PublicSlug = null,
    string? ContactEmail = null,
    string? ContactPhone = null,
    string? Website = null);

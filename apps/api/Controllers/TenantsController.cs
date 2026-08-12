using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SportsPlatform.Modules.Tenancy;
using SportsPlatform.Modules.Tenancy.Entities;
using SportsPlatform.Modules.Tenancy.Services;
using SportsPlatform.Api.Authorization;
using SportsPlatform.Infrastructure.Persistence;
using SportsPlatform.Modules.Audit;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using SportsPlatform.Modules.Identity.Entities;

namespace SportsPlatform.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TenantsController : ControllerBase
{
    private readonly ITenancyService _tenancyService;
    private readonly SportsPlatformDbContext _database;
    private readonly IAuditService _audit;

    public TenantsController(
        ITenancyService tenancyService,
        SportsPlatformDbContext database,
        IAuditService audit)
    {
        _tenancyService = tenancyService;
        _database = database;
        _audit = audit;
    }

    [HttpGet]
    [RequirePermission("tenants.read")]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var tenants = await _tenancyService.GetAllTenantsAsync(cancellationToken);
        return Ok(User.IsPlatformOperator()
            ? tenants
            : tenants.Where(tenant => User.CanAccessTenant(tenant.Id)));
    }

    [HttpGet("{id:guid}")]
    [RequirePermission("tenants.read")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        if (!User.CanAccessTenant(id)) return Forbid();

        var tenant = await _tenancyService.GetTenantByIdAsync(id, cancellationToken);
        if (tenant == null) return NotFound();
        return Ok(tenant);
    }

    [HttpGet("by-slug/{slug}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetBySlug(string slug, CancellationToken cancellationToken)
    {
        var tenant = await _tenancyService.GetTenantBySlugAsync(slug, cancellationToken);
        if (tenant == null) return NotFound();
        return Ok(tenant);
    }

    [HttpPost]
    [RequirePermission("tenants.manage")]
    public async Task<IActionResult> Create([FromBody] CreateTenantRequest request, CancellationToken cancellationToken)
    {
        if (!User.IsPlatformOperator()) return Forbid();

        var tenant = await _tenancyService.CreateTenantAsync(request.Name, request.ShortName, request.Slug, cancellationToken);
        _database.Roles.AddRange(SystemRoleCatalogue.CreateTenantRoles(tenant.Id));
        await _database.SaveChangesAsync(cancellationToken);
        await AuditAsync("TenantCreated", tenant.Id, "Tenant", tenant.Id,
            request, "Tenant created", cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = tenant.Id }, tenant);
    }

    [HttpPut("{id:guid}")]
    [RequirePermission("tenants.update")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateTenantRequest request, CancellationToken cancellationToken)
    {
        if (!User.CanAccessTenant(id)) return Forbid();

        var tenant = await _tenancyService.GetTenantByIdAsync(id, cancellationToken);
        if (tenant == null) return NotFound();

        var original = new { tenant.Name, tenant.ShortName, tenant.Description };
        tenant.UpdateDetails(request.Name, request.ShortName, request.Description);
        await _tenancyService.UpdateTenantAsync(tenant, cancellationToken);
        await AuditAsync("TenantUpdated", id, "Tenant", id, request,
            "Tenant profile updated", cancellationToken, original);
        return Ok(tenant);
    }

    [HttpPost("{id:guid}/activate")]
    [RequirePermission("tenants.manage")]
    public async Task<IActionResult> Activate(Guid id, CancellationToken cancellationToken)
    {
        if (!User.IsPlatformOperator()) return Forbid();

        await _tenancyService.ActivateTenantAsync(id, cancellationToken);
        await AuditAsync("TenantActivated", id, "Tenant", id,
            new { status = "Active" }, "Tenant activated", cancellationToken);
        return Ok(new { message = "Tenant activated successfully." });
    }

    [HttpPost("{id:guid}/suspend")]
    [RequirePermission("tenants.manage")]
    public async Task<IActionResult> Suspend(Guid id, CancellationToken cancellationToken)
    {
        if (!User.IsPlatformOperator()) return Forbid();

        await _tenancyService.SuspendTenantAsync(id, cancellationToken);
        await AuditAsync("TenantSuspended", id, "Tenant", id,
            new { status = "Suspended" }, "Tenant suspended", cancellationToken);
        return Ok(new { message = "Tenant suspended successfully." });
    }

    [HttpPost("{id:guid}/archive")]
    [RequirePermission("tenants.manage")]
    public async Task<IActionResult> Archive(Guid id, CancellationToken cancellationToken)
    {
        if (!User.IsPlatformOperator()) return Forbid();

        await _tenancyService.ArchiveTenantAsync(id, cancellationToken);
        await AuditAsync("TenantArchived", id, "Tenant", id,
            new { status = "Archived" }, "Tenant archived", cancellationToken);
        return Ok(new { message = "Tenant archived successfully." });
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission("tenants.delete")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        if (!User.IsPlatformOperator()) return Forbid();

        var tenant = await _tenancyService.GetTenantByIdAsync(id, cancellationToken);
        if (tenant == null) return NotFound();

        await _tenancyService.ArchiveTenantAsync(id, cancellationToken);
        await AuditAsync("TenantDeleted", id, "Tenant", id,
            new { status = "Archived" }, "Tenant delete converted to archive", cancellationToken);
        return NoContent();
    }

    // --- Contact Management ---

    [HttpGet("{id:guid}/contacts")]
    [RequirePermission("tenants.read")]
    public async Task<IActionResult> GetContacts(Guid id, CancellationToken cancellationToken)
    {
        if (!User.CanAccessTenant(id)) return Forbid();

        var contacts = await _tenancyService.GetContactsByTenantAsync(id, cancellationToken);
        return Ok(contacts);
    }

    [HttpPost("{id:guid}/contacts")]
    [RequirePermission("tenants.update")]
    public async Task<IActionResult> CreateContact(Guid id, [FromBody] CreateContactRequest request, CancellationToken cancellationToken)
    {
        if (!User.CanAccessTenant(id)) return Forbid();

        try
        {
            var contact = await _tenancyService.CreateContactAsync(
                id, request.Name, request.Email, request.Phone, request.Role, request.IsPrimary, cancellationToken);
            await AuditAsync("TenantContactCreated", id, "TenantContact", contact.Id,
                request, "Tenant contact created", cancellationToken);
            return CreatedAtAction(nameof(GetContacts), new { id }, contact);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("contacts/{contactId:guid}")]
    [RequirePermission("tenants.update")]
    public async Task<IActionResult> UpdateContact(Guid contactId, [FromBody] UpdateContactRequest request, CancellationToken cancellationToken)
    {
        var tenantId = await _database.TenantContacts.AsNoTracking()
            .Where(item => item.Id == contactId).Select(item => (Guid?)item.TenantId)
            .SingleOrDefaultAsync(cancellationToken);
        if (!await User.CanAccessTenantContactAsync(_database, contactId, cancellationToken))
            return Forbid();

        try
        {
            var contact = await _tenancyService.UpdateContactAsync(
                contactId, request.Name, request.Email, request.Phone, request.Role, request.IsPrimary, cancellationToken);
            if (tenantId.HasValue)
                await AuditAsync("TenantContactUpdated", tenantId.Value, "TenantContact", contactId,
                    request, "Tenant contact updated", cancellationToken);
            return Ok(contact);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete("contacts/{contactId:guid}")]
    [RequirePermission("tenants.update")]
    public async Task<IActionResult> DeleteContact(Guid contactId, CancellationToken cancellationToken)
    {
        var tenantId = await _database.TenantContacts.AsNoTracking()
            .Where(item => item.Id == contactId).Select(item => (Guid?)item.TenantId)
            .SingleOrDefaultAsync(cancellationToken);
        if (!await User.CanAccessTenantContactAsync(_database, contactId, cancellationToken))
            return Forbid();

        try
        {
            await _tenancyService.DeleteContactAsync(contactId, cancellationToken);
            if (tenantId.HasValue)
                await AuditAsync("TenantContactDeleted", tenantId.Value, "TenantContact", contactId,
                    new { deleted = true }, "Tenant contact deleted", cancellationToken);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // --- Localization ---

    [HttpPut("{id:guid}/localization")]
    [RequirePermission("tenants.update")]
    public async Task<IActionResult> UpdateLocalization(Guid id, [FromBody] UpdateLocalizationRequest request, CancellationToken cancellationToken)
    {
        if (!User.CanAccessTenant(id)) return Forbid();

        try
        {
            await _tenancyService.UpdateLocalizationAsync(
                id, request.Country, request.Timezone, request.Currency, request.DefaultLanguage, cancellationToken);
            await AuditAsync("TenantLocalizationUpdated", id, "Tenant", id,
                request, "Tenant localization updated", cancellationToken);
            return Ok(new { message = "Localization updated successfully." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    private Task AuditAsync(
        string action,
        Guid tenantId,
        string entityType,
        Guid entityId,
        object value,
        string reason,
        CancellationToken cancellationToken,
        object? original = null) => _audit.RecordAsync(
            action,
            entityType,
            entityId.ToString(),
            tenantId,
            User.UserId(),
            original is null ? null : JsonSerializer.Serialize(original),
            JsonSerializer.Serialize(value),
            reason,
            cancellationToken);
}

public record CreateTenantRequest(string Name, string ShortName, string Slug);
public record UpdateTenantRequest(string Name, string ShortName, string? Description);
public record CreateContactRequest(string Name, string Email, string? Phone, string? Role, bool IsPrimary);
public record UpdateContactRequest(string Name, string Email, string? Phone, string? Role, bool IsPrimary);
public record UpdateLocalizationRequest(string? Country, string? Timezone, string? Currency, string? DefaultLanguage);

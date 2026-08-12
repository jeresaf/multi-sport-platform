using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SportsPlatform.Modules.Identity.DTOs;
using SportsPlatform.Modules.Identity.Services;
using SportsPlatform.Modules.Identity.Entities;
using SportsPlatform.Api.Authorization;
using SportsPlatform.Infrastructure.Persistence;
using SportsPlatform.Modules.Audit;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace SportsPlatform.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class RolesController : ControllerBase
{
    private readonly IRoleService _roleService;
    private readonly SportsPlatformDbContext _database;
    private readonly IAuditService _audit;

    public RolesController(
        IRoleService roleService,
        SportsPlatformDbContext database,
        IAuditService audit)
    {
        _roleService = roleService;
        _database = database;
        _audit = audit;
    }

    /// <summary>
    /// Get all roles for the current tenant or system roles
    /// </summary>
    [HttpGet]
    [RequirePermission("roles.read")]
    public async Task<ActionResult<IReadOnlyList<RoleResponse>>> GetRoles(CancellationToken cancellationToken)
    {
        var tenantId = User.IsPlatformOperator() ? null : GetTenantId();
        var roles = await _roleService.GetRolesByTenantAsync(tenantId, cancellationToken);
        return Ok(roles);
    }

    [HttpGet("permissions")]
    [RequirePermission("roles.read")]
    public ActionResult<IReadOnlyList<string>> GetPermissionCatalogue()
    {
        var permissions = User.IsPlatformOperator()
            ? SystemPermissions.All
            : SystemPermissions.TenantAssignable;
        return Ok(permissions);
    }

    /// <summary>
    /// Get role by ID
    /// </summary>
    [HttpGet("{id:guid}")]
    [RequirePermission("roles.read")]
    public async Task<ActionResult<RoleResponse>> GetRole(Guid id, CancellationToken cancellationToken)
    {
        if (!await User.CanAccessRoleAsync(_database, id, cancellationToken))
            return Forbid();

        var role = await _roleService.GetRoleByIdAsync(id, cancellationToken);
        if (role == null)
            return NotFound();

        return Ok(role);
    }

    /// <summary>
    /// Create a new role
    /// </summary>
    [HttpPost]
    [RequirePermission("roles.create")]
    public async Task<ActionResult<RoleResponse>> CreateRole([FromBody] CreateRoleRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var tenantId = User.IsPlatformOperator() ? null : GetTenantId();
            var role = await _roleService.CreateRoleAsync(request, tenantId, cancellationToken);
            await AuditAsync("PermissionRoleCreated", tenantId, role.Id, request,
                "Role and permissions created", cancellationToken);
            return CreatedAtAction(nameof(GetRole), new { id = role.Id }, role);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Update a role
    /// </summary>
    [HttpPut("{id:guid}")]
    [RequirePermission("roles.update")]
    public async Task<ActionResult<RoleResponse>> UpdateRole(Guid id, [FromBody] UpdateRoleRequest request, CancellationToken cancellationToken)
    {
        if (!await User.CanAccessRoleAsync(_database, id, cancellationToken))
            return Forbid();

        try
        {
            var existing = await _database.Roles.AsNoTracking()
                .Where(item => item.Id == id)
                .Select(item => new
                {
                    item.TenantId, item.Name,
                    permissions = item.Permissions.Select(permission => permission.Permission)
                }).SingleAsync(cancellationToken);
            var role = await _roleService.UpdateRoleAsync(id, request, cancellationToken);
            await AuditAsync("PermissionRoleUpdated", existing.TenantId, id, request,
                "Role permissions updated", cancellationToken, existing);
            return Ok(role);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Delete a role
    /// </summary>
    [HttpDelete("{id:guid}")]
    [RequirePermission("roles.delete")]
    public async Task<ActionResult> DeleteRole(Guid id, CancellationToken cancellationToken)
    {
        if (!await User.CanAccessRoleAsync(_database, id, cancellationToken))
            return Forbid();

        try
        {
            var existing = await _database.Roles.AsNoTracking()
                .Where(item => item.Id == id)
                .Select(item => new { item.TenantId, item.Name })
                .SingleAsync(cancellationToken);
            await _roleService.DeleteRoleAsync(id, cancellationToken);
            await AuditAsync("PermissionRoleDeleted", existing.TenantId, id,
                new { deleted = true }, "Role deleted", cancellationToken, existing);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Assign a role to a user
    /// </summary>
    [HttpPost("assign")]
    [RequirePermission("roles.assign")]
    public async Task<ActionResult> AssignRole([FromBody] AssignRoleRequest request, CancellationToken cancellationToken)
    {
        if (!await User.CanAssignRoleAsync(
                _database,
                request.UserId,
                request.RoleId,
                cancellationToken))
            return Forbid();

        try
        {
            await _roleService.AssignRoleToUserAsync(request.UserId, request.RoleId, cancellationToken);
            var tenantId = await _database.Users.AsNoTracking()
                .Where(item => item.Id == request.UserId)
                .Select(item => item.TenantId).SingleAsync(cancellationToken);
            await AuditAsync("PermissionRoleAssigned", tenantId, request.RoleId,
                request, "Role assigned to user", cancellationToken);
            return Ok(new { message = "Role assigned successfully." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Remove a role from a user
    /// </summary>
    [HttpPost("remove")]
    [RequirePermission("roles.assign")]
    public async Task<ActionResult> RemoveRole([FromBody] AssignRoleRequest request, CancellationToken cancellationToken)
    {
        if (!await User.CanAssignRoleAsync(
                _database,
                request.UserId,
                request.RoleId,
                cancellationToken))
            return Forbid();

        try
        {
            await _roleService.RemoveRoleFromUserAsync(request.UserId, request.RoleId, cancellationToken);
            var tenantId = await _database.Users.AsNoTracking()
                .Where(item => item.Id == request.UserId)
                .Select(item => item.TenantId).SingleAsync(cancellationToken);
            await AuditAsync("PermissionRoleRemoved", tenantId, request.RoleId,
                request, "Role removed from user", cancellationToken);
            return Ok(new { message = "Role removed successfully." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    private Guid? GetTenantId()
    {
        var claim = User.FindFirst("tenant_id");
        return claim != null ? Guid.Parse(claim.Value) : null;
    }

    private Task AuditAsync(
        string action,
        Guid? tenantId,
        Guid roleId,
        object value,
        string reason,
        CancellationToken cancellationToken,
        object? original = null) => _audit.RecordAsync(
            action,
            "Role",
            roleId.ToString(),
            tenantId,
            User.UserId(),
            original is null ? null : JsonSerializer.Serialize(original),
            JsonSerializer.Serialize(value),
            reason,
            cancellationToken);
}

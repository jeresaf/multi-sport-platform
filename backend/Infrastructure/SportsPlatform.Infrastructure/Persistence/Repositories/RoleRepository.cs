using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using SportsPlatform.BuildingBlocks;
using SportsPlatform.Modules.Identity;
using SportsPlatform.Modules.Identity.Entities;

namespace SportsPlatform.Infrastructure.Persistence.Repositories;

public class RoleRepository : IRoleRepository
{
    private readonly SportsPlatformDbContext _context;

    public RoleRepository(SportsPlatformDbContext context)
    {
        _context = context;
    }

    public IUnitOfWork UnitOfWork => new UnitOfWork(_context);

    public async Task<Role?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Roles
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Role>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Roles.ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Role>> FindAsync(
        Expression<Func<Role, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        return await _context.Roles.Where(predicate).ToListAsync(cancellationToken);
    }

    public async Task<Role?> GetByNameAsync(string name, Guid? tenantId, CancellationToken cancellationToken = default)
    {
        return await _context.Roles
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.Name.ToLower() == name.Trim().ToLower()
                && r.TenantId == tenantId, cancellationToken);
    }

    public async Task<IReadOnlyList<Role>> GetByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await _context.Roles
            .Include(r => r.Permissions)
            .Where(r => r.TenantId == tenantId)
            .OrderBy(r => r.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Role>> GetSystemRolesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Roles
            .Include(r => r.Permissions)
            .Where(r => r.IsSystemRole)
            .OrderBy(r => r.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<Role> AddAsync(Role role, CancellationToken cancellationToken = default)
    {
        var entry = await _context.Roles.AddAsync(role, cancellationToken);
        return entry.Entity;
    }

    public async Task UpdateAsync(Role role, CancellationToken cancellationToken = default)
    {
        _context.Roles.Update(role);
        await Task.CompletedTask;
    }

    public async Task DeleteAsync(Role role, CancellationToken cancellationToken = default)
    {
        _context.Roles.Remove(role);
        await Task.CompletedTask;
    }

    public Task<bool> IsAssignedAsync(Guid roleId, CancellationToken cancellationToken = default) =>
        _context.UserRoles.AnyAsync(item => item.RoleId == roleId, cancellationToken);

    public Task ReplacePermissionsAsync(
        Role role,
        IReadOnlyCollection<string> permissions,
        CancellationToken cancellationToken = default)
    {
        var required = permissions.ToHashSet(StringComparer.Ordinal);
        foreach (var existing in role.Permissions
                     .Where(item => !required.Contains(item.Permission))
                     .ToArray())
            _context.RolePermissions.Remove(existing);
        foreach (var permission in required.Where(permission =>
                     role.Permissions.All(existing => existing.Permission != permission)))
            _context.RolePermissions.Add(
                new RolePermission(Guid.NewGuid(), role.Id, permission));
        return Task.CompletedTask;
    }
}

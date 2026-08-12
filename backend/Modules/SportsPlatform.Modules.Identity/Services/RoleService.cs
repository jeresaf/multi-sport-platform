using SportsPlatform.Modules.Identity.DTOs;
using SportsPlatform.Modules.Identity.Entities;

namespace SportsPlatform.Modules.Identity.Services;

public class RoleService : IRoleService
{
    private readonly IRoleRepository _roleRepository;
    private readonly IUserRepository _userRepository;

    public RoleService(IRoleRepository roleRepository, IUserRepository userRepository)
    {
        _roleRepository = roleRepository;
        _userRepository = userRepository;
    }

    public async Task<RoleResponse> CreateRoleAsync(CreateRoleRequest request, Guid? tenantId, CancellationToken cancellationToken = default)
    {
        var name = request.Name.Trim();
        var existing = await _roleRepository.GetByNameAsync(name, tenantId, cancellationToken);
        if (existing != null)
            throw new InvalidOperationException($"Role '{name}' already exists.");

        var role = new Role(Guid.NewGuid(), name, request.Description, isSystemRole: false, tenantId);

        foreach (var permission in ValidatePermissions(request.Permissions, tenantId))
            role.AddPermission(permission);

        await _roleRepository.AddAsync(role, cancellationToken);
        await _roleRepository.UnitOfWork.SaveEntitiesAsync(cancellationToken);

        return MapToRoleResponse(role);
    }

    public async Task<RoleResponse> UpdateRoleAsync(Guid roleId, UpdateRoleRequest request, CancellationToken cancellationToken = default)
    {
        var role = await _roleRepository.GetByIdAsync(roleId, cancellationToken);
        if (role == null)
            throw new InvalidOperationException("Role not found.");

        if (role.IsSystemRole)
            throw new InvalidOperationException("System roles cannot be modified.");

        var name = request.Name.Trim();
        var duplicate = await _roleRepository.GetByNameAsync(name, role.TenantId, cancellationToken);
        if (duplicate is not null && duplicate.Id != role.Id)
            throw new InvalidOperationException($"Role '{name}' already exists.");

        role.UpdateDetails(name, request.Description);

        var permissions = ValidatePermissions(request.Permissions, role.TenantId);
        await _roleRepository.ReplacePermissionsAsync(role, permissions, cancellationToken);

        await _roleRepository.UnitOfWork.SaveEntitiesAsync(cancellationToken);

        return MapToRoleResponse(role, permissions);
    }

    public async Task DeleteRoleAsync(Guid roleId, CancellationToken cancellationToken = default)
    {
        var role = await _roleRepository.GetByIdAsync(roleId, cancellationToken);
        if (role == null)
            throw new InvalidOperationException("Role not found.");

        if (role.IsSystemRole)
            throw new InvalidOperationException("System roles cannot be deleted.");

        if (await _roleRepository.IsAssignedAsync(roleId, cancellationToken))
            throw new InvalidOperationException("Assigned roles cannot be deleted. Remove all user assignments first.");

        await _roleRepository.DeleteAsync(role, cancellationToken);
        await _roleRepository.UnitOfWork.SaveEntitiesAsync(cancellationToken);
    }

    public async Task<RoleResponse?> GetRoleByIdAsync(Guid roleId, CancellationToken cancellationToken = default)
    {
        var role = await _roleRepository.GetByIdAsync(roleId, cancellationToken);
        return role != null ? MapToRoleResponse(role) : null;
    }

    public async Task<IReadOnlyList<RoleResponse>> GetRolesByTenantAsync(Guid? tenantId, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Role> roles;

        if (tenantId.HasValue)
        {
            roles = await _roleRepository.GetByTenantAsync(tenantId.Value, cancellationToken);
        }
        else
        {
            roles = await _roleRepository.GetSystemRolesAsync(cancellationToken);
        }

        return roles.Select(role => MapToRoleResponse(role)).ToList();
    }

    public async Task AssignRoleToUserAsync(Guid userId, Guid roleId, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user == null)
            throw new InvalidOperationException("User not found.");

        var role = await _roleRepository.GetByIdAsync(roleId, cancellationToken);
        if (role == null)
            throw new InvalidOperationException("Role not found.");

        await _userRepository.AddUserRoleAsync(userId, roleId, cancellationToken);
        await _userRepository.UnitOfWork.SaveEntitiesAsync(cancellationToken);
    }

    public async Task RemoveRoleFromUserAsync(Guid userId, Guid roleId, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user == null)
            throw new InvalidOperationException("User not found.");

        await _userRepository.RemoveUserRoleAsync(userId, roleId, cancellationToken);
        await _userRepository.UnitOfWork.SaveEntitiesAsync(cancellationToken);
    }

    private static RoleResponse MapToRoleResponse(
        Role role,
        IReadOnlyList<string>? effectivePermissions = null)
    {
        return new RoleResponse(
            role.Id,
            role.Name,
            role.Description,
            role.IsSystemRole,
            effectivePermissions ?? role.Permissions.Select(p => p.Permission)
                .OrderBy(permission => permission).ToList(),
            role.TenantId
        );
    }

    private static IReadOnlyList<string> ValidatePermissions(
        IEnumerable<string>? permissions,
        Guid? tenantId)
    {
        try
        {
            var normalized = (permissions ?? Array.Empty<string>())
                .Select(SystemPermissions.Normalize)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(permission => permission)
                .ToArray();
            if (tenantId.HasValue)
            {
                var forbidden = normalized.FirstOrDefault(permission =>
                    !SystemPermissions.IsTenantAssignable(permission));
                if (forbidden is not null)
                    throw new InvalidOperationException(
                        $"Permission '{forbidden}' can only be assigned to a platform role.");
            }
            return normalized;
        }
        catch (ArgumentException exception)
        {
            throw new InvalidOperationException(exception.Message, exception);
        }
    }
}

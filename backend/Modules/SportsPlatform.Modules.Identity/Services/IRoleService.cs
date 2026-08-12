using SportsPlatform.Modules.Identity.DTOs;

namespace SportsPlatform.Modules.Identity.Services;

public interface IRoleService
{
    Task<RoleResponse> CreateRoleAsync(CreateRoleRequest request, Guid? tenantId, CancellationToken cancellationToken = default);
    Task<RoleResponse> UpdateRoleAsync(Guid roleId, UpdateRoleRequest request, CancellationToken cancellationToken = default);
    Task DeleteRoleAsync(Guid roleId, CancellationToken cancellationToken = default);
    Task<RoleResponse?> GetRoleByIdAsync(Guid roleId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RoleResponse>> GetRolesByTenantAsync(Guid? tenantId, CancellationToken cancellationToken = default);
    Task AssignRoleToUserAsync(Guid userId, Guid roleId, CancellationToken cancellationToken = default);
    Task RemoveRoleFromUserAsync(Guid userId, Guid roleId, CancellationToken cancellationToken = default);
}
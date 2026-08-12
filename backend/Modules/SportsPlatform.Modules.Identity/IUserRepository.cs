using SportsPlatform.BuildingBlocks;
using SportsPlatform.Modules.Identity.Entities;

namespace SportsPlatform.Modules.Identity;

public interface IUserRepository : IRepository<User>
{
    new Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<User?> GetByRefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default);
    Task<DeviceSession?> GetSessionByRefreshTokenHashAsync(string refreshTokenHash, CancellationToken cancellationToken = default);
    Task<IdentityActionToken?> GetActionTokenByHashAsync(
        string tokenHash,
        IdentityActionTokenPurpose purpose,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<User>> GetByTenantAsync(Guid tenantId, int page = 1, int pageSize = 50, CancellationToken cancellationToken = default);
    new Task<User> AddAsync(User user, CancellationToken cancellationToken = default);
    Task AddSessionAsync(DeviceSession session, CancellationToken cancellationToken = default);
    Task AddActionTokenAsync(IdentityActionToken token, CancellationToken cancellationToken = default);
    Task RevokeActionTokensAsync(
        Guid userId,
        IdentityActionTokenPurpose purpose,
        DateTime revokedAt,
        CancellationToken cancellationToken = default);
    Task AddUserRoleAsync(Guid userId, Guid roleId, CancellationToken cancellationToken = default);
    Task RemoveUserRoleAsync(Guid userId, Guid roleId, CancellationToken cancellationToken = default);
    new Task UpdateAsync(User user, CancellationToken cancellationToken = default);
    Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default);
}

public interface IRoleRepository : IRepository<Role>
{
    new Task<Role?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Role?> GetByNameAsync(string name, Guid? tenantId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Role>> GetByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Role>> GetSystemRolesAsync(CancellationToken cancellationToken = default);
    new Task<Role> AddAsync(Role role, CancellationToken cancellationToken = default);
    new Task UpdateAsync(Role role, CancellationToken cancellationToken = default);
    new Task DeleteAsync(Role role, CancellationToken cancellationToken = default);
    Task<bool> IsAssignedAsync(Guid roleId, CancellationToken cancellationToken = default);
    Task ReplacePermissionsAsync(
        Role role,
        IReadOnlyCollection<string> permissions,
        CancellationToken cancellationToken = default);
}

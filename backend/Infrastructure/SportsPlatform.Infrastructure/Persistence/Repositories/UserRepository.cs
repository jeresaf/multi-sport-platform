using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using SportsPlatform.BuildingBlocks;
using SportsPlatform.Modules.Identity;
using SportsPlatform.Modules.Identity.Entities;

namespace SportsPlatform.Infrastructure.Persistence.Repositories;

public class UserRepository : IUserRepository
{
    private readonly SportsPlatformDbContext _context;

    public UserRepository(SportsPlatformDbContext context)
    {
        _context = context;
    }

    public IUnitOfWork UnitOfWork => new UnitOfWork(_context);

    public async Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Users
            .Include(u => u.Roles)
            .Include(u => u.Sessions)
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<User>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Users.ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<User>> FindAsync(
        Expression<Func<User, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        return await _context.Users.Where(predicate).ToListAsync(cancellationToken);
    }

    public async Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        return await _context.Users
            .Include(u => u.Roles)
            .Include(u => u.Sessions)
            .FirstOrDefaultAsync(u => u.Email == email.Trim().ToLower(), cancellationToken);
    }

    public async Task<User?> GetByRefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        return await _context.Users
            .Include(u => u.Roles)
            .Include(u => u.Sessions)
            .FirstOrDefaultAsync(u => u.RefreshToken == refreshToken, cancellationToken);
    }

    public Task<DeviceSession?> GetSessionByRefreshTokenHashAsync(
        string refreshTokenHash,
        CancellationToken cancellationToken = default) =>
        _context.DeviceSessions.FirstOrDefaultAsync(
            session => session.RefreshToken == refreshTokenHash,
            cancellationToken);

    public Task<IdentityActionToken?> GetActionTokenByHashAsync(
        string tokenHash,
        IdentityActionTokenPurpose purpose,
        CancellationToken cancellationToken = default) =>
        _context.IdentityActionTokens.FirstOrDefaultAsync(
            token => token.TokenHash == tokenHash && token.Purpose == purpose,
            cancellationToken);

    public async Task<IReadOnlyList<User>> GetByTenantAsync(Guid tenantId, int page = 1, int pageSize = 50, CancellationToken cancellationToken = default)
    {
        return await _context.Users
            .Include(u => u.Roles)
            .Where(u => u.TenantId == tenantId)
            .OrderByDescending(u => u.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
    }

    public async Task<User> AddAsync(User user, CancellationToken cancellationToken = default)
    {
        var entry = await _context.Users.AddAsync(user, cancellationToken);
        return entry.Entity;
    }

    public async Task AddSessionAsync(DeviceSession session, CancellationToken cancellationToken = default)
    {
        // DeviceSession uses an application-generated Guid, so adding it only
        // through a tracked navigation can make EF infer Modified. Explicitly
        // register it as Added to produce an INSERT.
        await _context.DeviceSessions.AddAsync(session, cancellationToken);
    }

    public async Task AddActionTokenAsync(
        IdentityActionToken token,
        CancellationToken cancellationToken = default) =>
        await _context.IdentityActionTokens.AddAsync(token, cancellationToken);

    public async Task RevokeActionTokensAsync(
        Guid userId,
        IdentityActionTokenPurpose purpose,
        DateTime revokedAt,
        CancellationToken cancellationToken = default)
    {
        var tokens = await _context.IdentityActionTokens
            .Where(token => token.UserId == userId
                && token.Purpose == purpose
                && token.ConsumedAt == null)
            .ToArrayAsync(cancellationToken);
        foreach (var token in tokens)
            token.Revoke(revokedAt);
    }

    public async Task AddUserRoleAsync(
        Guid userId,
        Guid roleId,
        CancellationToken cancellationToken = default)
    {
        if (!await _context.UserRoles.AnyAsync(
                item => item.UserId == userId && item.RoleId == roleId,
                cancellationToken))
            await _context.UserRoles.AddAsync(
                new UserRole(Guid.NewGuid(), userId, roleId), cancellationToken);
    }

    public async Task RemoveUserRoleAsync(
        Guid userId,
        Guid roleId,
        CancellationToken cancellationToken = default)
    {
        var assignment = await _context.UserRoles.FirstOrDefaultAsync(
            item => item.UserId == userId && item.RoleId == roleId,
            cancellationToken);
        if (assignment is not null)
            _context.UserRoles.Remove(assignment);
    }

    public async Task UpdateAsync(User user, CancellationToken cancellationToken = default)
    {
        _context.Users.Update(user);
        await Task.CompletedTask;
    }

    public async Task DeleteAsync(User user, CancellationToken cancellationToken = default)
    {
        _context.Users.Remove(user);
        await Task.CompletedTask;
    }

    public async Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = email.Trim().ToLower();
        return await _context.Users.AnyAsync(u => u.Email == normalizedEmail, cancellationToken);
    }
}

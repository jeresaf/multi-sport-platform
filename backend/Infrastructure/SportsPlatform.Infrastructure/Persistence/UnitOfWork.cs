using SportsPlatform.BuildingBlocks;

namespace SportsPlatform.Infrastructure.Persistence;

public class UnitOfWork : IUnitOfWork
{
    private readonly SportsPlatformDbContext _context;

    public UnitOfWork(SportsPlatformDbContext context)
    {
        _context = context;
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> SaveEntitiesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
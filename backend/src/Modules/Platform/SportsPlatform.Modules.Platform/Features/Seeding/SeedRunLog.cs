using Microsoft.EntityFrameworkCore;
using SportsPlatform.BuildingBlocks.Persistence.Seeding;
using SportsPlatform.Modules.Platform.Domain.Seeding;
using SportsPlatform.Modules.Platform.Infrastructure;

namespace SportsPlatform.Modules.Platform.Features.Seeding;

/// <summary>
/// <c>platform.seed_runs</c> as the seed runner's bookkeeping. Owner connection, like the REFERENCE seeds: seeding
/// happens in the CLI, which holds the <c>Migrator</c> credentials anyway.
/// </summary>
internal sealed class SeedRunLog(OwnerContextFactory ownerContexts) : ISeedRunLog
{
    public async Task<string?> LastChecksumAsync(string seederKey, CancellationToken cancellationToken)
    {
        await using var context = ownerContexts.Create<PlatformDbContext>();
        return await context.Set<SeedRun>()
            .Where(r => r.SeederKey == seederKey)
            .OrderByDescending(r => r.Id)
            .Select(r => r.Checksum)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task AppendAsync(SeedRunRecord run, CancellationToken cancellationToken)
    {
        await using var context = ownerContexts.Create<PlatformDbContext>();
        context.Add(SeedRun.From(run));
        await context.SaveChangesAsync(cancellationToken);
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SportsPlatform.BuildingBlocks.Actors;

namespace SportsPlatform.BuildingBlocks.Persistence;

/// <summary>Stamps <c>created_*</c> and <c>updated_*</c> from the current actor and the injected clock.</summary>
public sealed class AuditColumnsInterceptor(ICurrentActor actor, TimeProvider timeProvider) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Stamp(eventData.Context);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Stamp(eventData.Context);
        return ValueTask.FromResult(result);
    }

    private void Stamp(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var now = timeProvider.GetUtcNow();
        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry.Metadata.FindProperty(AuditColumns.CreatedAt) is null)
            {
                continue;
            }

            var hasUpdatedColumns = entry.Metadata.FindProperty(AuditColumns.UpdatedAt) is not null;
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Property(AuditColumns.CreatedAt).CurrentValue = now;
                    entry.Property(AuditColumns.CreatedBy).CurrentValue = actor.ActorId;
                    if (hasUpdatedColumns)
                    {
                        entry.Property(AuditColumns.UpdatedAt).CurrentValue = now;
                        entry.Property(AuditColumns.UpdatedBy).CurrentValue = actor.ActorId;
                    }

                    break;
                case EntityState.Modified:
                    // Who created a row never changes, whatever the entity's code did.
                    entry.Property(AuditColumns.CreatedAt).IsModified = false;
                    entry.Property(AuditColumns.CreatedBy).IsModified = false;
                    if (hasUpdatedColumns)
                    {
                        entry.Property(AuditColumns.UpdatedAt).CurrentValue = now;
                        entry.Property(AuditColumns.UpdatedBy).CurrentValue = actor.ActorId;
                    }

                    break;
                default:
                    break;
            }
        }
    }
}

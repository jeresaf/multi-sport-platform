using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using SportsPlatform.BuildingBlocks.Domain;
using SportsPlatform.BuildingBlocks.Messaging;
using SportsPlatform.BuildingBlocks.Persistence.Messaging;
using SportsPlatform.BuildingBlocks.Results;

namespace SportsPlatform.BuildingBlocks.Persistence;

/// <summary>
/// Base for each module's DbContext (the module's unit of work). Applies the data conventions: the module's schema,
/// app-generated UUIDv7 keys, audit columns, <c>xmin</c> concurrency for aggregates
/// (docs/architecture/data-conventions.md), and the module's <c>outbox_messages</c> and <c>inbox_messages</c>
/// (docs/architecture/events-and-jobs.md). snake_case names come from <see cref="ModuleDbContextOptions"/>.
/// </summary>
public abstract class ModuleDbContext : DbContext
{
    protected ModuleDbContext(DbContextOptions options, string schema)
        : base(options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(schema);
        Schema = schema;
    }

    public string Schema { get; }

    /// <summary>The module's <c>&lt;MODULE&gt;_CONCURRENCY_CONFLICT</c> error (409).</summary>
    protected abstract Error ConcurrencyConflictError { get; }

    /// <summary>Saves; a lost optimistic-concurrency race is an expected failure, not an exception.</summary>
    public async Task<Result> SaveChangesResultAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await SaveChangesAsync(cancellationToken);
            return Result.Success();
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure(ConcurrencyConflictError);
        }
    }

    /// <summary>
    /// Queues an integration event in this module's outbox; it is saved with the rest of the unit of work and
    /// delivered after commit. Call it from a use-case handler or a domain event subscriber, never after saving.
    /// </summary>
    public void Publish(IIntegrationEvent integrationEvent, string aggregateType, Guid aggregateId)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        var eventType = integrationEvent.GetType();
        var payload = JsonSerializer.Serialize(integrationEvent, eventType, MessageSerialization.Options);
        Add(new OutboxMessage(IntegrationEventName.Of(eventType), payload, aggregateType, aggregateId));
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        EnsureNewEntitiesHaveIds();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        EnsureNewEntitiesHaveIds();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    /// <summary>Configure the module's entities here; the conventions are applied afterwards.</summary>
    protected abstract void ConfigureModel(ModelBuilder modelBuilder);

    protected sealed override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        MessagingModel.Configure(modelBuilder);
        ConfigureModel(modelBuilder);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes().Where(IsTableRoot).ToList())
        {
            var entity = modelBuilder.Entity(entityType.ClrType);

            if (typeof(Entity).IsAssignableFrom(entityType.ClrType))
            {
                entity.Property(nameof(Entity.Id)).ValueGeneratedNever();
            }

            entity.Property<DateTimeOffset>(AuditColumns.CreatedAt);
            entity.Property<Guid>(AuditColumns.CreatedBy);
            if (!entityType.IsAppendOnly())
            {
                entity.Property<DateTimeOffset>(AuditColumns.UpdatedAt);
                entity.Property<Guid>(AuditColumns.UpdatedBy);
            }

            if (typeof(AggregateRoot).IsAssignableFrom(entityType.ClrType))
            {
                // PostgreSQL's xmin system column changes on every update; Npgsql never creates it in migrations.
                entity.Property<uint>(AuditColumns.Version).HasColumnName("xmin").HasColumnType("xid").IsRowVersion();
            }
        }
    }

    // Owned types and derived types share their owner's or base type's row, so only roots get the columns.
    private static bool IsTableRoot(IMutableEntityType entityType) =>
        !entityType.IsOwned() && entityType.BaseType is null && entityType.ClrType != typeof(Dictionary<string, object>);

    /// <summary>Also run by <see cref="MessagingInterceptor"/> for entities that domain event subscribers added.</summary>
    internal void EnsureNewEntitiesHaveIds()
    {
        var missing = ChangeTracker.Entries<Entity>()
            .FirstOrDefault(entry => entry.State == EntityState.Added && entry.Entity.Id == Guid.Empty);
        if (missing is not null)
        {
            throw new InvalidOperationException(
                $"A new {missing.Entity.GetType().Name} has no ID. Create entities with Entity(Guid) and NewId().");
        }
    }
}

using System.Linq.Expressions;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using SportsPlatform.BuildingBlocks.Auditing;
using SportsPlatform.BuildingBlocks.Domain;
using SportsPlatform.BuildingBlocks.Messaging;
using SportsPlatform.BuildingBlocks.Persistence.Messaging;
using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.BuildingBlocks.Tenancy;

namespace SportsPlatform.BuildingBlocks.Persistence;

/// <summary>
/// Base for each module's DbContext (the module's unit of work). Applies the data conventions: the module's schema,
/// app-generated UUIDv7 keys, audit columns, <c>xmin</c> concurrency for aggregates
/// (docs/architecture/data-conventions.md), and the module's <c>outbox_messages</c> and <c>inbox_messages</c>
/// (docs/architecture/events-and-jobs.md), and tenant isolation for <see cref="ITenantOwned"/> entities: a query filter
/// on the current tenant and composite foreign keys between tenant-owned tables (docs/architecture/multi-tenancy.md).
/// snake_case names come from <see cref="ModuleDbContextOptions"/>.
/// </summary>
public abstract class ModuleDbContext : DbContext
{
    /// <summary>The name of the query filter on every <see cref="ITenantOwned"/> entity.</summary>
    public const string TenantFilter = "TenantIsolation";

    private readonly ITenantContext _tenantContext;

    protected ModuleDbContext(DbContextOptions options, string schema, ITenantContext tenantContext)
        : base(options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(schema);
        ArgumentNullException.ThrowIfNull(tenantContext);
        Schema = schema;
        _tenantContext = tenantContext;
    }

    public string Schema { get; }

    /// <summary>Read by the tenant query filter on every query, so it follows the scope's tenant; null matches nothing.</summary>
    protected Guid? CurrentTenantId => _tenantContext.TenantId;

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
    public void Publish(IIntegrationEvent integrationEvent, string aggregateType, Guid aggregateId) =>
        AddOutboxMessage(integrationEvent, aggregateType, aggregateId);

    /// <summary>
    /// Queues an audit entry: it goes to this module's outbox when the context next saves, in the same transaction.
    /// For work that saves in its own scope, with a record from the request's <see cref="AuditScope"/>; within the
    /// request's scope the first save records the scope's entry by itself.
    /// </summary>
    public void RecordAudit(AuditRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        _pendingAudits.Add(record);
    }

    private readonly List<AuditRecord> _pendingAudits = [];

    /// <summary>The audit records queued with <see cref="RecordAudit"/>, handed over to the save that writes them.</summary>
    internal List<AuditRecord> TakePendingAudits()
    {
        var records = _pendingAudits.ToList();
        _pendingAudits.Clear();
        return records;
    }

    internal OutboxMessage AddOutboxMessage(IIntegrationEvent integrationEvent, string aggregateType, Guid aggregateId)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        var eventType = integrationEvent.GetType();
        var payload = JsonSerializer.Serialize(integrationEvent, eventType, MessageSerialization.Options);
        var message = new OutboxMessage(IntegrationEventName.Of(eventType), payload, aggregateType, aggregateId);
        Add(message);
        return message;
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

            if (typeof(ITenantOwned).IsAssignableFrom(entityType.ClrType))
            {
                entity.HasQueryFilter(TenantFilter, TenantFilterFor(entityType.ClrType));
            }
        }

        EnsureCompositeTenantForeignKeys(modelBuilder.Model);
    }

    // e => e.TenantId == this.CurrentTenantId; EF reads the context member per query, as a parameter.
    private LambdaExpression TenantFilterFor(Type clrType)
    {
        var entity = Expression.Parameter(clrType, "e");
        var tenantId = Expression.Convert(Expression.Property(entity, nameof(ITenantOwned.TenantId)), typeof(Guid?));
        var current = Expression.Property(Expression.Constant(this), nameof(CurrentTenantId));
        return Expression.Lambda(Expression.Equal(tenantId, current), entity);
    }

    /// <summary>
    /// PostgreSQL checks foreign keys without RLS, so only a key that includes <c>tenant_id</c> on both sides stops a
    /// row from referencing another tenant's row: <c>(tenant_id, parent_id) → (tenant_id, id)</c>.
    /// </summary>
    private static void EnsureCompositeTenantForeignKeys(IMutableModel model)
    {
        var singleColumn = model.GetEntityTypes()
            .SelectMany(entityType => entityType.GetDeclaredForeignKeys())
            .Where(fk => !fk.IsOwnership
                && typeof(ITenantOwned).IsAssignableFrom(fk.DeclaringEntityType.ClrType)
                && typeof(ITenantOwned).IsAssignableFrom(fk.PrincipalEntityType.ClrType)
                && !PairsTenantIds(fk))
            .Select(fk => $"{fk.DeclaringEntityType.ClrType.Name} → {fk.PrincipalEntityType.ClrType.Name}")
            .ToList();
        if (singleColumn.Count > 0)
        {
            throw new InvalidOperationException(
                "Foreign keys between tenant-owned entities must pair TenantId with TenantId " +
                $"(HasForeignKey(TenantId, ParentId).HasPrincipalKey(TenantId, Id)): {string.Join(", ", singleColumn)}.");
        }
    }

    // TenantId on both sides, at the same position, so tenant_id references tenant_id.
    private static bool PairsTenantIds(IMutableForeignKey foreignKey)
    {
        var position = TenantIdPosition(foreignKey.Properties);
        return position >= 0 && position == TenantIdPosition(foreignKey.PrincipalKey.Properties);
    }

    private static int TenantIdPosition(IReadOnlyList<IMutableProperty> properties)
    {
        for (var i = 0; i < properties.Count; i++)
        {
            if (properties[i].Name == nameof(ITenantOwned.TenantId))
            {
                return i;
            }
        }

        return -1;
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

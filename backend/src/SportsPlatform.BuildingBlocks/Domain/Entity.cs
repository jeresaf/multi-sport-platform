namespace SportsPlatform.BuildingBlocks.Domain;

/// <summary>Base for persisted entities. IDs are UUIDv7, generated in the application.</summary>
public abstract class Entity
{
    /// <summary>
    /// For EF Core materialisation only: leaves <see cref="Id"/> empty for EF to set from the stored value. Domain
    /// code creates entities with <see cref="Entity(Guid)"/> and <see cref="NewId"/>; saving a new entity with an
    /// empty ID throws.
    /// </summary>
    protected Entity()
    {
    }

    protected Entity(Guid id)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("An entity ID cannot be empty.", nameof(id));
        }

        Id = id;
    }

    public Guid Id { get; private set; }

    /// <summary>A new time-ordered UUIDv7.</summary>
    protected static Guid NewId() => Guid.CreateVersion7();
}

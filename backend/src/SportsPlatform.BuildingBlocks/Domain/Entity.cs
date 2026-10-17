namespace SportsPlatform.BuildingBlocks.Domain;

/// <summary>Base for persisted entities. IDs are UUIDv7, generated in the application.</summary>
public abstract class Entity
{
    // Also runs when EF Core materialises an entity: EF then overwrites Id with the stored value, so the generated
    // ID is discarded. T2 gives EF a constructor path that skips generation.
    protected Entity()
        : this(Guid.CreateVersion7())
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
}

using SportsPlatform.BuildingBlocks;

namespace SportsPlatform.Modules.Sports.Entities;

public class SportCategory : AggregateRoot<Guid>
{
    public Guid SportId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public int SortOrder { get; private set; }
    public bool IsActive { get; private set; } = true;
    public new DateTime CreatedAt { get; private set; }
    public new DateTime? UpdatedAt { get; private set; }

    private SportCategory() { }

    public SportCategory(Guid id, Guid sportId, string name, int sortOrder) : base(id)
    {
        SportId = sportId;
        Name = name;
        SortOrder = sortOrder;
        CreatedAt = DateTime.UtcNow;
    }

    public void UpdateDetails(string name, string? description, int sortOrder)
    {
        Name = name;
        Description = description;
        SortOrder = sortOrder;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Activate()
    {
        IsActive = true;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Deactivate()
    {
        IsActive = false;
        UpdatedAt = DateTime.UtcNow;
    }
}

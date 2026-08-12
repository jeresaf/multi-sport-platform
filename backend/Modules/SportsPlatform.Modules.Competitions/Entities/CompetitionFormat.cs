using SportsPlatform.BuildingBlocks;

namespace SportsPlatform.Modules.Competitions.Entities;

public class CompetitionFormat : AggregateRoot<Guid>
{
    public Guid SportId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Code { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public StageType DefaultStageType { get; private set; }
    public bool SupportsGroups { get; private set; }
    public bool SupportsRounds { get; private set; }
    public bool SupportsEditions { get; private set; } = true;
    public bool IsActive { get; private set; } = true;
    public new DateTime CreatedAt { get; private set; }
    public new DateTime? UpdatedAt { get; private set; }

    private CompetitionFormat() { }

    public CompetitionFormat(Guid id, Guid sportId, string name, string code, StageType defaultStageType)
        : base(id)
    {
        SportId = sportId;
        Name = name;
        Code = code;
        DefaultStageType = defaultStageType;
        CreatedAt = DateTime.UtcNow;
    }

    public void UpdateDetails(string name, string? description, StageType defaultStageType)
    {
        Name = name;
        Description = description;
        DefaultStageType = defaultStageType;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Configure(bool supportsGroups, bool supportsRounds, bool supportsEditions)
    {
        SupportsGroups = supportsGroups;
        SupportsRounds = supportsRounds;
        SupportsEditions = supportsEditions;
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

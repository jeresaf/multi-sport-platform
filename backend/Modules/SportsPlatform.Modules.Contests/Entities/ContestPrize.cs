using SportsPlatform.BuildingBlocks;

namespace SportsPlatform.Modules.Contests.Entities;

public class ContestPrize : Entity<Guid>
{
    public Guid ContestId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public int Position { get; private set; }
    public string? PrizeType { get; private set; }
    public decimal? MonetaryValue { get; private set; }
    public string? Currency { get; private set; }
    public string? TrophyName { get; private set; }
    public string? MedalType { get; private set; }
    public bool IsActive { get; private set; } = true;
    public new DateTime CreatedAt { get; private set; }
    public new DateTime? UpdatedAt { get; private set; }

    private ContestPrize() { }

    public ContestPrize(Guid id, Guid contestId, string name, int position) : base(id)
    {
        ContestId = contestId;
        Name = name;
        Position = position;
        CreatedAt = DateTime.UtcNow;
    }

    public void UpdateDetails(string name, string? description, int position)
    {
        Name = name;
        Description = description;
        Position = position;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetMonetaryPrize(decimal value, string currency)
    {
        PrizeType = "Monetary";
        MonetaryValue = value;
        Currency = currency;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetTrophy(string trophyName)
    {
        PrizeType = "Trophy";
        TrophyName = trophyName;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetMedal(string medalType)
    {
        PrizeType = "Medal";
        MedalType = medalType;
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

using SportsPlatform.BuildingBlocks;

namespace SportsPlatform.Modules.Leaderboards.Entities;

public class Leaderboard : AggregateRoot<Guid>
{
    public Guid ContestId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public LeaderboardType Type { get; private set; }
    public string? Scope { get; private set; }
    public bool IsActive { get; private set; } = true;
    public new DateTime CreatedAt { get; private set; }
    public new DateTime? UpdatedAt { get; private set; }

    private readonly List<LeaderboardEntry> _entries = new();
    public IReadOnlyCollection<LeaderboardEntry> Entries => _entries.AsReadOnly();

    private Leaderboard() { }

    public Leaderboard(Guid id, Guid contestId, string name, LeaderboardType type) : base(id)
    {
        ContestId = contestId;
        Name = name;
        Type = type;
        CreatedAt = DateTime.UtcNow;
    }

    public void SetDescription(string? description)
    {
        Description = description;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetScope(string? scope)
    {
        Scope = scope;
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

    public void AddEntry(LeaderboardEntry entry)
    {
        _entries.Add(entry);
        UpdatedAt = DateTime.UtcNow;
    }

    public void ClearEntries()
    {
        _entries.Clear();
        UpdatedAt = DateTime.UtcNow;
    }
}

public class LeaderboardEntry : Entity<Guid>
{
    public Guid LeaderboardId { get; private set; }
    public Guid ParticipantId { get; private set; }
    public int Rank { get; private set; }
    public decimal Score { get; private set; }
    public string? ScoreDisplay { get; private set; }
    public int? PreviousRank { get; private set; }
    public int RankChange => PreviousRank.HasValue ? PreviousRank.Value - Rank : 0;
    public new DateTime UpdatedAt { get; private set; }

    private LeaderboardEntry() { }

    public LeaderboardEntry(Guid id, Guid leaderboardId, Guid participantId, int rank, decimal score) : base(id)
    {
        LeaderboardId = leaderboardId;
        ParticipantId = participantId;
        Rank = rank;
        Score = score;
        PreviousRank = rank;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateRank(int newRank, decimal newScore, string? scoreDisplay = null)
    {
        PreviousRank = Rank;
        Rank = newRank;
        Score = newScore;
        ScoreDisplay = scoreDisplay;
        UpdatedAt = DateTime.UtcNow;
    }
}

public enum LeaderboardType
{
    Overall = 0,
    Event = 1,
    Category = 2,
    AgeGroup = 3,
    Custom = 4
}

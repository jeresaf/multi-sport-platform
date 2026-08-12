using SportsPlatform.BuildingBlocks;

namespace SportsPlatform.Modules.Standings.Entities;

public class Standing : AggregateRoot<Guid>
{
    public Guid CompetitionId { get; private set; }
    public Guid GroupId { get; private set; }
    public Guid ParticipantId { get; private set; }
    public int Position { get; private set; }
    public int Played { get; private set; }
    public int Won { get; private set; }
    public int Drawn { get; private set; }
    public int Lost { get; private set; }
    public int PointsFor { get; private set; }
    public int PointsAgainst { get; private set; }
    public int PointDifferential { get; private set; }
    public decimal Points { get; private set; }
    public string? Metadata { get; private set; }
    public new DateTime UpdatedAt { get; private set; }

    private Standing() { }

    public Standing(Guid id, Guid competitionId, Guid groupId, Guid participantId) : base(id)
    {
        CompetitionId = competitionId;
        GroupId = groupId;
        ParticipantId = participantId;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateStats(int played, int won, int drawn, int lost, int pointsFor, int pointsAgainst, decimal points)
    {
        Played = played;
        Won = won;
        Drawn = drawn;
        Lost = lost;
        PointsFor = pointsFor;
        PointsAgainst = pointsAgainst;
        PointDifferential = pointsFor - pointsAgainst;
        Points = points;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetPosition(int position)
    {
        Position = position;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetMetadata(string? metadata)
    {
        Metadata = metadata;
        UpdatedAt = DateTime.UtcNow;
    }
}

public class StandingEntry : Entity<Guid>
{
    public Guid StandingId { get; private set; }
    public string MetricName { get; private set; } = string.Empty;
    public string? Value { get; private set; }
    public int SortOrder { get; private set; }

    private StandingEntry() { }

    public StandingEntry(Guid id, Guid standingId, string metricName, string? value, int sortOrder) : base(id)
    {
        StandingId = standingId;
        MetricName = metricName;
        Value = value;
        SortOrder = sortOrder;
    }
}

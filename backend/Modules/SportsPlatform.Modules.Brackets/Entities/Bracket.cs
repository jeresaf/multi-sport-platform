using SportsPlatform.BuildingBlocks;

namespace SportsPlatform.Modules.Brackets.Entities;

public class Bracket : AggregateRoot<Guid>
{
    public Guid CompetitionId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public BracketType Type { get; private set; }
    public int Size { get; private set; }
    public bool IsSeeded { get; private set; }
    public int? DrawSeed { get; private set; }
    public BracketStatus Status { get; private set; } = BracketStatus.Draft;
    public Guid? ChampionId { get; private set; }
    public new DateTime CreatedAt { get; private set; }
    public new DateTime? UpdatedAt { get; private set; }

    private readonly List<BracketRound> _rounds = new();
    public IReadOnlyCollection<BracketRound> Rounds => _rounds.AsReadOnly();

    private Bracket() { }

    public Bracket(Guid id, Guid competitionId, string name, BracketType type, int size) : base(id)
    {
        CompetitionId = competitionId;
        Name = name;
        Type = type;
        Size = size;
        CreatedAt = DateTime.UtcNow;
    }

    public void SetSeeded(bool isSeeded)
    {
        IsSeeded = isSeeded;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetDrawSeed(int? drawSeed)
    {
        DrawSeed = drawSeed;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Publish()
    {
        if (Status != BracketStatus.Draft)
            throw new InvalidOperationException("Only draft brackets can be published.");
        
        Status = BracketStatus.Published;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Start()
    {
        if (Status != BracketStatus.Published)
            throw new InvalidOperationException("Only published brackets can be started.");
        
        Status = BracketStatus.InProgress;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Complete(Guid championId)
    {
        if (Status != BracketStatus.InProgress)
            throw new InvalidOperationException("Only in-progress brackets can be completed.");
        
        Status = BracketStatus.Completed;
        ChampionId = championId;
        UpdatedAt = DateTime.UtcNow;
    }

    public void AddRound(BracketRound round)
    {
        _rounds.Add(round);
        UpdatedAt = DateTime.UtcNow;
    }
}

public class BracketRound : Entity<Guid>
{
    public Guid BracketId { get; private set; }
    public int RoundNumber { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public int MatchCount { get; private set; }
    public DateTime? ScheduledAt { get; private set; }

    private readonly List<BracketMatch> _matches = new();
    public IReadOnlyCollection<BracketMatch> Matches => _matches.AsReadOnly();

    private BracketRound() { }

    public BracketRound(Guid id, Guid bracketId, int roundNumber, string name, int matchCount) : base(id)
    {
        BracketId = bracketId;
        RoundNumber = roundNumber;
        Name = name;
        MatchCount = matchCount;
    }

    public void SetScheduledAt(DateTime? scheduledAt)
    {
        ScheduledAt = scheduledAt;
    }

    public void AddMatch(BracketMatch match)
    {
        _matches.Add(match);
    }
}

public class BracketMatch : Entity<Guid>
{
    public Guid RoundId { get; private set; }
    public int MatchNumber { get; private set; }
    public Guid? Participant1Id { get; private set; }
    public Guid? Participant2Id { get; private set; }
    public Guid? WinnerId { get; private set; }
    public Guid? ContestId { get; private set; }
    public Guid? SeriesId { get; private set; }
    public int BestOf { get; private set; } = 1;
    public bool IsAggregateTie { get; private set; }
    public bool IsThirdPlace { get; private set; }
    public Guid? NextMatchId { get; private set; }
    public int? NextMatchSlot { get; private set; }
    public Guid? LoserNextMatchId { get; private set; }
    public int? LoserNextMatchSlot { get; private set; }
    public MatchStatus Status { get; private set; } = MatchStatus.Scheduled;
    public string? Score1 { get; private set; }
    public string? Score2 { get; private set; }
    public DateTime? ScheduledAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }

    private BracketMatch() { }

    public BracketMatch(Guid id, Guid roundId, int matchNumber) : base(id)
    {
        RoundId = roundId;
        MatchNumber = matchNumber;
    }

    public void SetParticipants(Guid? participant1Id, Guid? participant2Id)
    {
        Participant1Id = participant1Id;
        Participant2Id = participant2Id;
    }

    public void BindContest(Guid contestId)
    {
        ContestId = contestId;
    }

    public void BindSeries(Guid seriesId, Guid firstContestId, int bestOf)
    {
        if (bestOf < 1 || bestOf % 2 == 0)
            throw new ArgumentOutOfRangeException(
                nameof(bestOf),
                "A series length must be a positive odd number.");
        SeriesId = seriesId;
        ContestId = firstContestId;
        BestOf = bestOf;
    }

    public void BindAggregateTie(Guid tieId, Guid firstLegContestId)
    {
        SeriesId = tieId;
        ContestId = firstLegContestId;
        BestOf = 2;
        IsAggregateTie = true;
    }

    public void MarkAsThirdPlace()
    {
        IsThirdPlace = true;
    }

    public void AdvanceTo(Guid nextMatchId, int nextMatchSlot)
    {
        if (nextMatchSlot is not 1 and not 2)
            throw new ArgumentOutOfRangeException(
                nameof(nextMatchSlot),
                "The next match slot must be 1 or 2.");
        NextMatchId = nextMatchId;
        NextMatchSlot = nextMatchSlot;
    }

    public void AdvanceLoserTo(Guid nextMatchId, int nextMatchSlot)
    {
        if (nextMatchSlot is not 1 and not 2)
            throw new ArgumentOutOfRangeException(
                nameof(nextMatchSlot),
                "The next match slot must be 1 or 2.");
        LoserNextMatchId = nextMatchId;
        LoserNextMatchSlot = nextMatchSlot;
    }

    public void SetScore(string? score1, string? score2)
    {
        Score1 = score1;
        Score2 = score2;
    }

    public void SetWinner(Guid winnerId)
    {
        WinnerId = winnerId;
        Status = MatchStatus.Completed;
        CompletedAt = DateTime.UtcNow;
    }

    public void Start()
    {
        Status = MatchStatus.InProgress;
    }

    public void Cancel()
    {
        Status = MatchStatus.Cancelled;
    }
}

public enum BracketType
{
    SingleElimination = 0,
    DoubleElimination = 1,
    TripleElimination = 2,
    RoundRobin = 3,
    Swiss = 4
}

public enum BracketStatus
{
    Draft = 0,
    Published = 1,
    InProgress = 2,
    Completed = 3,
    Cancelled = 4
}

public enum MatchStatus
{
    Scheduled = 0,
    InProgress = 1,
    Completed = 2,
    Cancelled = 3,
    Postponed = 4
}

public sealed class BracketAdvancementOverride : Entity<Guid>
{
    public Guid BracketMatchId { get; private set; }
    public Guid WinnerId { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public Guid RequestedBy { get; private set; }
    public DateTime RequestedAt { get; private set; }
    public BracketOverrideStatus Status { get; private set; }
    public Guid? ReviewedBy { get; private set; }
    public DateTime? ReviewedAt { get; private set; }
    public string? ReviewNotes { get; private set; }

    private BracketAdvancementOverride() { }

    public BracketAdvancementOverride(
        Guid id,
        Guid bracketMatchId,
        Guid winnerId,
        string reason,
        Guid requestedBy) : base(id)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("An override reason is required.", nameof(reason));
        BracketMatchId = bracketMatchId;
        WinnerId = winnerId;
        Reason = reason.Trim();
        RequestedBy = requestedBy;
        RequestedAt = DateTime.UtcNow;
        Status = BracketOverrideStatus.Pending;
        CreatedAt = DateTime.UtcNow;
    }

    public void Approve(Guid reviewedBy, string? notes)
    {
        if (Status != BracketOverrideStatus.Pending)
            throw new InvalidOperationException("Only pending overrides can be approved.");
        if (reviewedBy == RequestedBy)
            throw new InvalidOperationException("An override requires approval by a different user.");
        Status = BracketOverrideStatus.Approved;
        ReviewedBy = reviewedBy;
        ReviewedAt = DateTime.UtcNow;
        ReviewNotes = notes;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Reject(Guid reviewedBy, string? notes)
    {
        if (Status != BracketOverrideStatus.Pending)
            throw new InvalidOperationException("Only pending overrides can be rejected.");
        Status = BracketOverrideStatus.Rejected;
        ReviewedBy = reviewedBy;
        ReviewedAt = DateTime.UtcNow;
        ReviewNotes = notes;
        UpdatedAt = DateTime.UtcNow;
    }
}

public enum BracketOverrideStatus
{
    Pending = 0,
    Approved = 1,
    Rejected = 2
}

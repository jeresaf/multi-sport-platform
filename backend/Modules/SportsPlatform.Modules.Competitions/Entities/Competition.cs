using SportsPlatform.BuildingBlocks;
using System.Text.Json;

namespace SportsPlatform.Modules.Competitions.Entities;

public class Competition : TenantAggregateRoot<Guid>
{
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public Guid SportId { get; private set; }
    public Guid? RulesetVersionId { get; private set; }
    public CompetitionStatus Status { get; private set; } = CompetitionStatus.Draft;
    public new DateTime CreatedAt { get; private set; }
    public new DateTime? UpdatedAt { get; private set; }
    public DateTime? PublishedAt { get; private set; }
    public string PresentationConfiguration { get; private set; } = "{}";

    private readonly List<Edition> _editions = new();
    public IReadOnlyCollection<Edition> Editions => _editions.AsReadOnly();

    private Competition() { }

    public Competition(Guid id, Guid tenantId, string name, Guid sportId) : base(id, tenantId)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("A tenant is required.", nameof(tenantId));
        if (sportId == Guid.Empty) throw new ArgumentException("A sport is required.", nameof(sportId));
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("A competition name is required.", nameof(name));
        Name = name.Trim();
        SportId = sportId;
        Status = CompetitionStatus.Draft;
        CreatedAt = DateTime.UtcNow;
    }

    public void AssignRulesetVersion(Guid rulesetVersionId)
    {
        if (rulesetVersionId == Guid.Empty)
            throw new ArgumentException("A ruleset version is required.", nameof(rulesetVersionId));
        if (RulesetVersionId.HasValue && RulesetVersionId != rulesetVersionId)
            throw new InvalidOperationException(
                "A competition's ruleset version cannot change after assignment.");
        RulesetVersionId = rulesetVersionId;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdatePresentationConfiguration(string configuration)
    {
        if (string.IsNullOrWhiteSpace(configuration)) configuration = "{}";
        using var document = JsonDocument.Parse(configuration);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
            throw new ArgumentException(
                "Presentation configuration must be a JSON object.", nameof(configuration));
        PresentationConfiguration = configuration.Trim();
        UpdatedAt = DateTime.UtcNow;
    }

    public Edition AddEdition(string name, DateTime startDate, DateTime? endDate)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("An edition name is required.", nameof(name));
        if (endDate.HasValue && endDate.Value < startDate)
            throw new ArgumentException("An edition cannot end before it starts.", nameof(endDate));
        var edition = new Edition(Guid.NewGuid(), Id, TenantId, name, startDate, endDate);
        _editions.Add(edition);
        UpdatedAt = DateTime.UtcNow;
        return edition;
    }

    public void Publish()
    {
        Status = CompetitionStatus.Published;
        PublishedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Close()
    {
        Status = CompetitionStatus.Closed;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Archive()
    {
        Status = CompetitionStatus.Archived;
        UpdatedAt = DateTime.UtcNow;
    }
}

public enum CompetitionStatus
{
    Draft,
    Published,
    InProgress,
    Closed,
    Archived
}

public class Edition : TenantAggregateRoot<Guid>
{
    public Guid CompetitionId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public DateTime StartDate { get; private set; }
    public DateTime? EndDate { get; private set; }
    public EditionStatus Status { get; private set; } = EditionStatus.Pending;
    public new DateTime CreatedAt { get; private set; }
    public new DateTime? UpdatedAt { get; private set; }

    private readonly List<Stage> _stages = new();
    public IReadOnlyCollection<Stage> Stages => _stages.AsReadOnly();

    private Edition() { }

    public Edition(Guid id, Guid competitionId, Guid tenantId, string name, DateTime startDate, DateTime? endDate)
        : base(id, tenantId)
    {
        CompetitionId = competitionId;
        if (competitionId == Guid.Empty) throw new ArgumentException("A competition is required.", nameof(competitionId));
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("An edition name is required.", nameof(name));
        if (endDate.HasValue && endDate.Value < startDate)
            throw new ArgumentException("An edition cannot end before it starts.", nameof(endDate));
        Name = name.Trim();
        StartDate = startDate;
        EndDate = endDate;
        CreatedAt = DateTime.UtcNow;
    }

    public Stage AddStage(string name, StageType type, int sortOrder)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("A stage name is required.", nameof(name));
        if (sortOrder < 1) throw new ArgumentOutOfRangeException(nameof(sortOrder));
        var stage = new Stage(Guid.NewGuid(), Id, TenantId, name, type, sortOrder);
        _stages.Add(stage);
        UpdatedAt = DateTime.UtcNow;
        return stage;
    }

    public void Open() { Status = EditionStatus.Open; UpdatedAt = DateTime.UtcNow; }
    public void Close() { Status = EditionStatus.Closed; UpdatedAt = DateTime.UtcNow; }
}

public enum EditionStatus { Pending, Open, Closed }

public enum StageType { Group, Knockout, GroupAndKnockout, League, Leaderboard, Heat, FightCard }

public class Stage : TenantAggregateRoot<Guid>
{
    public Guid EditionId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public StageType Type { get; private set; }
    public int SortOrder { get; private set; }
    public StageStatus Status { get; private set; } = StageStatus.Pending;
    public new DateTime CreatedAt { get; private set; }
    public new DateTime? UpdatedAt { get; private set; }
    public int? GroupsCount { get; private set; }
    public int? TeamsPerGroup { get; private set; }
    public int? QualifiersPerGroup { get; private set; }
    public int BestRunnerUpCount { get; private set; }
    public string? QualificationTieBreakers { get; private set; }
    public DateTime? QualificationAppliedAt { get; private set; }
    public int? FixtureGenerationSeed { get; private set; }
    public bool? FixtureHomeAndAway { get; private set; }
    public int? FixtureDaysBetweenRounds { get; private set; }
    public string? FixtureVenue { get; private set; }
    public DateTime? FixturesGeneratedAt { get; private set; }

    private readonly List<Group> _groups = new();
    private readonly List<Round> _rounds = new();
    public IReadOnlyCollection<Group> Groups => _groups.AsReadOnly();
    public IReadOnlyCollection<Round> Rounds => _rounds.AsReadOnly();

    private Stage() { }

    public Stage(Guid id, Guid editionId, Guid tenantId, string name, StageType type, int sortOrder)
        : base(id, tenantId)
    {
        EditionId = editionId;
        if (editionId == Guid.Empty) throw new ArgumentException("An edition is required.", nameof(editionId));
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("A stage name is required.", nameof(name));
        if (sortOrder < 1) throw new ArgumentOutOfRangeException(nameof(sortOrder));
        Name = name.Trim();
        Type = type;
        SortOrder = sortOrder;
        CreatedAt = DateTime.UtcNow;
    }

    public Group AddGroup(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("A group name is required.", nameof(name));
        var group = new Group(Guid.NewGuid(), Id, TenantId, name);
        _groups.Add(group);
        UpdatedAt = DateTime.UtcNow;
        return group;
    }

    public Round AddRound(string name, int sortOrder)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("A round name is required.", nameof(name));
        if (sortOrder < 1) throw new ArgumentOutOfRangeException(nameof(sortOrder));
        var round = new Round(Guid.NewGuid(), Id, TenantId, name, sortOrder);
        _rounds.Add(round);
        UpdatedAt = DateTime.UtcNow;
        return round;
    }

    public void Open() { Status = StageStatus.InProgress; UpdatedAt = DateTime.UtcNow; }
    public void Complete() { Status = StageStatus.Completed; UpdatedAt = DateTime.UtcNow; }

    public void ConfigureGroupQualification(
        int groupsCount,
        int teamsPerGroup,
        int qualifiersPerGroup,
        int bestRunnerUpCount,
        IEnumerable<string> tieBreakers)
    {
        if (groupsCount < 2) throw new ArgumentOutOfRangeException(nameof(groupsCount));
        if (teamsPerGroup < 2) throw new ArgumentOutOfRangeException(nameof(teamsPerGroup));
        if (qualifiersPerGroup < 1 || qualifiersPerGroup >= teamsPerGroup)
            throw new ArgumentOutOfRangeException(nameof(qualifiersPerGroup));
        if (bestRunnerUpCount < 0 || bestRunnerUpCount > groupsCount)
            throw new ArgumentOutOfRangeException(nameof(bestRunnerUpCount));
        var configured = tieBreakers.Select(item => item.Trim().ToLowerInvariant())
            .Where(item => item.Length > 0)
            .Distinct()
            .ToArray();
        if (configured.Length == 0)
            throw new ArgumentException("At least one qualification tie-breaker is required.", nameof(tieBreakers));

        GroupsCount = groupsCount;
        TeamsPerGroup = teamsPerGroup;
        QualifiersPerGroup = qualifiersPerGroup;
        BestRunnerUpCount = bestRunnerUpCount;
        QualificationTieBreakers = string.Join(',', configured);
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkQualificationApplied()
    {
        QualificationAppliedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void RecordFixtureGeneration(
        int seed,
        bool homeAndAway,
        int daysBetweenRounds,
        string venue)
    {
        if (daysBetweenRounds < 1)
            throw new ArgumentOutOfRangeException(nameof(daysBetweenRounds));
        if (string.IsNullOrWhiteSpace(venue))
            throw new ArgumentException("A fixture venue is required.", nameof(venue));
        if (FixtureGenerationSeed.HasValue)
            throw new InvalidOperationException("Fixture generation metadata has already been recorded.");

        FixtureGenerationSeed = seed;
        FixtureHomeAndAway = homeAndAway;
        FixtureDaysBetweenRounds = daysBetweenRounds;
        FixtureVenue = venue.Trim();
        FixturesGeneratedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }
}

public enum StageStatus { Pending, InProgress, Completed, Cancelled }

public class Group : TenantAggregateRoot<Guid>
{
    public Guid StageId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public GroupStatus Status { get; private set; } = GroupStatus.Pending;
    public new DateTime CreatedAt { get; private set; }
    public new DateTime? UpdatedAt { get; private set; }

    private Group() { }

    public Group(Guid id, Guid stageId, Guid tenantId, string name) : base(id, tenantId)
    {
        if (stageId == Guid.Empty) throw new ArgumentException("A stage is required.", nameof(stageId));
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("A group name is required.", nameof(name));
        StageId = stageId;
        Name = name.Trim();
        CreatedAt = DateTime.UtcNow;
    }
}

public enum GroupStatus { Pending, InProgress, Completed }

public class Round : TenantAggregateRoot<Guid>
{
    public Guid StageId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public int SortOrder { get; private set; }
    public RoundStatus Status { get; private set; } = RoundStatus.Pending;
    public new DateTime CreatedAt { get; private set; }
    public new DateTime? UpdatedAt { get; private set; }

    private Round() { }

    public Round(Guid id, Guid stageId, Guid tenantId, string name, int sortOrder) : base(id, tenantId)
    {
        if (stageId == Guid.Empty) throw new ArgumentException("A stage is required.", nameof(stageId));
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("A round name is required.", nameof(name));
        if (sortOrder < 1) throw new ArgumentOutOfRangeException(nameof(sortOrder));
        StageId = stageId;
        Name = name.Trim();
        SortOrder = sortOrder;
        CreatedAt = DateTime.UtcNow;
    }
}

public enum RoundStatus { Pending, InProgress, Completed }

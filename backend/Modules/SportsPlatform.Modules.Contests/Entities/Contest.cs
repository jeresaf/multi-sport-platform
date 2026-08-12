using SportsPlatform.BuildingBlocks;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SportsPlatform.Modules.Contests.Entities;

public class Contest : TenantAggregateRoot<Guid>
{
    private readonly List<ContestStatusTransition> _pendingTransitions = new();

    [NotMapped]
    [JsonIgnore]
    public IReadOnlyCollection<ContestStatusTransition> PendingTransitions =>
        _pendingTransitions.AsReadOnly();
    private static readonly IReadOnlyDictionary<ContestStatus, IReadOnlySet<ContestStatus>>
        DefaultTransitions = new Dictionary<ContestStatus, IReadOnlySet<ContestStatus>>
        {
            [ContestStatus.Draft] = Set(ContestStatus.Scheduled, ContestStatus.Cancelled),
            [ContestStatus.Scheduled] = Set(ContestStatus.CheckInOpen, ContestStatus.Ready,
                ContestStatus.InProgress, ContestStatus.Cancelled, ContestStatus.Abandoned),
            [ContestStatus.CheckInOpen] = Set(ContestStatus.Ready, ContestStatus.Scheduled,
                ContestStatus.Cancelled, ContestStatus.Abandoned),
            [ContestStatus.Ready] = Set(ContestStatus.CheckInOpen, ContestStatus.InProgress,
                ContestStatus.Cancelled, ContestStatus.Abandoned),
            [ContestStatus.InProgress] = Set(ContestStatus.Paused, ContestStatus.Suspended,
                ContestStatus.Completed, ContestStatus.Abandoned),
            [ContestStatus.Paused] = Set(ContestStatus.InProgress, ContestStatus.Suspended,
                ContestStatus.Completed, ContestStatus.Abandoned),
            [ContestStatus.Suspended] = Set(ContestStatus.Scheduled, ContestStatus.InProgress,
                ContestStatus.Cancelled, ContestStatus.Abandoned),
            [ContestStatus.Completed] = Set(ContestStatus.PendingApproval, ContestStatus.Corrected),
            [ContestStatus.PendingApproval] = Set(ContestStatus.Approved, ContestStatus.Completed,
                ContestStatus.Corrected),
            [ContestStatus.Approved] = Set(ContestStatus.Corrected, ContestStatus.PendingApproval,
                ContestStatus.Published),
            [ContestStatus.Corrected] = Set(ContestStatus.PendingApproval),
            [ContestStatus.Published] = Set(ContestStatus.Corrected),
            [ContestStatus.Cancelled] = Set(),
            [ContestStatus.Abandoned] = Set()
        };

    public Guid? StageId { get; private set; }
    public Guid? GroupId { get; private set; }
    public Guid? RoundId { get; private set; }
    public Guid SportId { get; private set; }
    public Guid? RulesetVersionId { get; private set; }
    public Guid? SeriesId { get; private set; }
    public int? SeriesGameNumber { get; private set; }
    public int? SeriesBestOf { get; private set; }
    public int? SeriesHomeParticipantSlot { get; private set; }
    public int? ParPerRound { get; private set; }
    public int? CutAfterRound { get; private set; }
    public int? CutToParticipants { get; private set; }
    public DateTime? CutAppliedAt { get; private set; }
    public string? CourseName { get; private set; }
    public string? CourseLocation { get; private set; }
    public string? HoleParsJson { get; private set; }
    public string? WeightClass { get; private set; }
    public string? CardSection { get; private set; }
    public int? ScheduledRounds { get; private set; }
    public string? ResultMethod { get; private set; }
    public int? ResultRound { get; private set; }
    public int? ResultTimeSeconds { get; private set; }
    public string? ResultNotes { get; private set; }
    public string? AllowedResultMethods { get; private set; }
    public string? AthleticsEventName { get; private set; }
    public int? DistanceMeters { get; private set; }
    public int? QualifyByPlace { get; private set; }
    public int? QualifyByTime { get; private set; }
    public bool? AdvanceQualificationTies { get; private set; }
    public bool? AdvancePlaceTies { get; private set; }
    public bool? AdvanceTimeTies { get; private set; }
    public int? AthleticsLanesPerHeat { get; private set; }
    public string? AthleticsLaneAssignment { get; private set; }
    public int? NextRoundHeatCount { get; private set; }
    public int? NextRoundQualifyByPlace { get; private set; }
    public int? NextRoundQualifyByTime { get; private set; }
    public DateTime? QualificationAppliedAt { get; private set; }
    public string? AthleticsMetricType { get; private set; }
    public int? AthleticsMaxAttempts { get; private set; }
    public decimal? AthleticsRecordThreshold { get; private set; }
    public string ContestShape { get; private set; } = string.Empty;
    [NotMapped]
    public ContestShapeKind ShapeKind => ContestShapeCatalog.Classify(ContestShape, SeriesBestOf);
    public string AllowedParticipantTypes { get; private set; } = string.Join('|', ParticipantTypeNames);
    public string? Name { get; private set; }
    public Guid? WinnerId { get; private set; }
    public ContestStatus Status { get; private set; } = ContestStatus.Draft;
    public string? Venue { get; private set; }
    public DateTime? StartTime { get; private set; }
    public DateTime? EndTime { get; private set; }
    public int ScheduledDurationMinutes { get; private set; }
    public int SortOrder { get; private set; }
    public new DateTime CreatedAt { get; private set; }
    public new DateTime? UpdatedAt { get; private set; }

    private readonly List<ContestParticipant> _participants = new();
    private readonly List<ContestSegment> _segments = new();
    private readonly List<ContestEvent> _events = new();
    private readonly List<ContestOfficial> _officials = new();
    private readonly List<ContestMetric> _metrics = new();

    public IReadOnlyCollection<ContestParticipant> Participants => _participants.AsReadOnly();
    public IReadOnlyCollection<ContestSegment> Segments => _segments.AsReadOnly();
    public IReadOnlyCollection<ContestEvent> Events => _events.AsReadOnly();
    public IReadOnlyCollection<ContestOfficial> Officials => _officials.AsReadOnly();
    public IReadOnlyCollection<ContestMetric> Metrics => _metrics.AsReadOnly();

    private Contest() { }

    public Contest(Guid id, Guid tenantId, Guid sportId, string contestShape) : base(id, tenantId)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("A tenant is required.", nameof(tenantId));
        if (sportId == Guid.Empty) throw new ArgumentException("A sport is required.", nameof(sportId));
        if (string.IsNullOrWhiteSpace(contestShape))
            throw new ArgumentException("A contest shape is required.", nameof(contestShape));
        SportId = sportId;
        ContestShape = contestShape.Trim();
        Status = ContestStatus.Draft;
        CreatedAt = DateTime.UtcNow;
    }

    private static readonly string[] ParticipantTypeNames =
    {
        "Team", "Athlete", "Pair", "RelayTeam", "Crew", "Club", "School", "Country", "CompositeTeam"
    };

    public IReadOnlyList<string> GetAllowedParticipantTypes() =>
        AllowedParticipantTypes.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    public void ConfigureParticipantTypes(IEnumerable<string> participantTypes)
    {
        var requested = participantTypes
            .Select(type => ParticipantTypeNames.SingleOrDefault(known =>
                known.Equals(type?.Trim(), StringComparison.OrdinalIgnoreCase)))
            .ToArray();
        if (requested.Length == 0)
            throw new ArgumentException("A contest must accept at least one participant type.", nameof(participantTypes));
        if (requested.Any(type => type is null))
            throw new ArgumentException("A contest contains an unknown participant type.", nameof(participantTypes));
        AllowedParticipantTypes = string.Join('|', requested!
            .Distinct(StringComparer.OrdinalIgnoreCase));
        UpdatedAt = DateTime.UtcNow;
    }

    public ContestParticipant AddParticipant(Guid participantId, string participantType, int? sortOrder = null)
    {
        var canonicalType = ParticipantTypeNames.SingleOrDefault(type =>
            type.Equals(participantType?.Trim(), StringComparison.OrdinalIgnoreCase))
            ?? throw new ArgumentException("The participant type is not supported.", nameof(participantType));
        if (!GetAllowedParticipantTypes().Contains(canonicalType, StringComparer.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                $"This contest does not accept participants of type {canonicalType}.");
        var participant = new ContestParticipant(
            Guid.NewGuid(), Id, TenantId, participantId, canonicalType, sortOrder ?? _participants.Count + 1);
        _participants.Add(participant);
        UpdatedAt = DateTime.UtcNow;
        return participant;
    }

    public void SetName(string name)
    {
        Name = name;
        UpdatedAt = DateTime.UtcNow;
    }

    public void AssignRulesetVersion(Guid rulesetVersionId)
    {
        if (rulesetVersionId == Guid.Empty)
            throw new ArgumentException("A ruleset version is required.", nameof(rulesetVersionId));
        if (RulesetVersionId.HasValue && RulesetVersionId != rulesetVersionId)
            throw new InvalidOperationException("A contest's ruleset version cannot change after it is assigned.");
        RulesetVersionId = rulesetVersionId;
        UpdatedAt = DateTime.UtcNow;
    }

    public void AssignToStage(Guid stageId, int sortOrder)
    {
        StageId = stageId;
        SortOrder = sortOrder;
        UpdatedAt = DateTime.UtcNow;
    }

    public void AssignToGroup(Guid groupId)
    {
        GroupId = groupId;
        UpdatedAt = DateTime.UtcNow;
    }

    public void AssignToRound(Guid roundId)
    {
        if (roundId == Guid.Empty) throw new ArgumentException("A round is required.", nameof(roundId));
        RoundId = roundId;
        UpdatedAt = DateTime.UtcNow;
    }

    public void AssignToHierarchy(Guid stageId, Guid? groupId, Guid? roundId, int sortOrder)
    {
        AssignToStage(stageId, sortOrder);
        if (groupId.HasValue) AssignToGroup(groupId.Value);
        if (roundId.HasValue) AssignToRound(roundId.Value);
    }

    public void AssignToSeries(
        Guid seriesId,
        int gameNumber,
        int bestOf,
        int homeParticipantSlot = 1)
    {
        if (bestOf < 1 || bestOf % 2 == 0)
            throw new ArgumentOutOfRangeException(
                nameof(bestOf),
                "A series length must be a positive odd number.");
        if (gameNumber < 1 || gameNumber > bestOf)
            throw new ArgumentOutOfRangeException(nameof(gameNumber));
        if (homeParticipantSlot is not 1 and not 2)
            throw new ArgumentOutOfRangeException(nameof(homeParticipantSlot));

        SeriesId = seriesId;
        SeriesGameNumber = gameNumber;
        SeriesBestOf = bestOf;
        SeriesHomeParticipantSlot = homeParticipantSlot;
        UpdatedAt = DateTime.UtcNow;
    }

    public void AssignToAggregateTie(Guid tieId, int legNumber)
    {
        if (legNumber is < 1 or > 2)
            throw new ArgumentOutOfRangeException(nameof(legNumber));

        SeriesId = tieId;
        SeriesGameNumber = legNumber;
        SeriesBestOf = 2;
        UpdatedAt = DateTime.UtcNow;
    }

    public void ConfigureLeaderboard(
        int parPerRound,
        int cutAfterRound,
        int cutToParticipants,
        string courseName = "Championship Course",
        string courseLocation = "Main Course",
        IReadOnlyList<int>? holePars = null)
    {
        if (parPerRound <= 0) throw new ArgumentOutOfRangeException(nameof(parPerRound));
        if (cutAfterRound <= 0) throw new ArgumentOutOfRangeException(nameof(cutAfterRound));
        if (cutToParticipants <= 0)
            throw new ArgumentOutOfRangeException(nameof(cutToParticipants));
        if (string.IsNullOrWhiteSpace(courseName))
            throw new ArgumentException("A course name is required.", nameof(courseName));
        if (string.IsNullOrWhiteSpace(courseLocation))
            throw new ArgumentException("A course location is required.", nameof(courseLocation));

        var resolvedHolePars = holePars?.ToArray()
            ?? new[] { 4, 4, 3, 5, 4, 4, 3, 5, 4, 4, 4, 3, 5, 4, 4, 3, 5, 4 };
        if (resolvedHolePars.Length != 18
            || resolvedHolePars.Any(par => par is < 3 or > 5))
            throw new ArgumentException(
                "Exactly 18 hole pars from 3 through 5 are required.", nameof(holePars));
        if (resolvedHolePars.Sum() != parPerRound)
            throw new ArgumentException(
                "Hole pars must add up to the configured par per round.", nameof(holePars));

        ParPerRound = parPerRound;
        CutAfterRound = cutAfterRound;
        CutToParticipants = cutToParticipants;
        CourseName = courseName.Trim();
        CourseLocation = courseLocation.Trim();
        HoleParsJson = JsonSerializer.Serialize(resolvedHolePars);
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkCutApplied()
    {
        CutAppliedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void ConfigureBout(
        string weightClass,
        string cardSection,
        int scheduledRounds,
        IEnumerable<string>? allowedResultMethods = null)
    {
        if (string.IsNullOrWhiteSpace(weightClass))
            throw new ArgumentException("A weight class is required.", nameof(weightClass));
        if (string.IsNullOrWhiteSpace(cardSection))
            throw new ArgumentException("A card section is required.", nameof(cardSection));
        if (scheduledRounds is not (3 or 5))
            throw new ArgumentOutOfRangeException(
                nameof(scheduledRounds),
                "Combat bouts must be scheduled for three or five rounds.");

        WeightClass = weightClass.Trim();
        CardSection = cardSection.Trim();
        ScheduledRounds = scheduledRounds;
        var methods = (allowedResultMethods ?? Array.Empty<string>())
            .Select(method => method.Trim())
            .Where(method => method.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        AllowedResultMethods = methods.Length == 0 ? null : string.Join('|', methods);
        UpdatedAt = DateTime.UtcNow;
    }

    public IReadOnlyList<string> GetAllowedResultMethods() =>
        string.IsNullOrWhiteSpace(AllowedResultMethods)
            ? Array.Empty<string>()
            : AllowedResultMethods.Split('|', StringSplitOptions.RemoveEmptyEntries
                | StringSplitOptions.TrimEntries);

    public bool AllowsResultMethod(string method) =>
        GetAllowedResultMethods().Count == 0
        || GetAllowedResultMethods().Contains(method, StringComparer.OrdinalIgnoreCase);

    public void RecordBoutResult(
        Guid? winnerId,
        string resultMethod,
        int? resultRound,
        int? resultTimeSeconds,
        string? notes)
    {
        if (string.IsNullOrWhiteSpace(resultMethod))
            throw new ArgumentException("A result method is required.", nameof(resultMethod));
        if (winnerId.HasValue
            && _participants.All(item => item.ParticipantId != winnerId.Value))
            throw new InvalidOperationException("The winner must be a participant in the bout.");

        WinnerId = winnerId;
        ResultMethod = resultMethod.Trim();
        ResultRound = resultRound;
        ResultTimeSeconds = resultTimeSeconds;
        ResultNotes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        UpdatedAt = DateTime.UtcNow;
    }

    public void ConfigureAthleticsRace(
        string eventName,
        int distanceMeters,
        int qualifyByPlace = 0,
        int qualifyByTime = 0,
        bool advanceTies = true,
        int lanesPerHeat = 8,
        string laneAssignment = "Standard",
        int nextRoundHeatCount = 0,
        int nextRoundQualifyByPlace = 0,
        int nextRoundQualifyByTime = 0,
        bool? advancePlaceTies = null,
        bool? advanceTimeTies = null)
    {
        if (string.IsNullOrWhiteSpace(eventName))
            throw new ArgumentException("An event name is required.", nameof(eventName));
        if (distanceMeters <= 0)
            throw new ArgumentOutOfRangeException(nameof(distanceMeters));
        if (qualifyByPlace < 0 || qualifyByTime < 0
            || nextRoundHeatCount < 0 || nextRoundQualifyByPlace < 0
            || nextRoundQualifyByTime < 0)
            throw new ArgumentOutOfRangeException(
                nameof(qualifyByPlace),
                "Qualification counts cannot be negative.");
        if (lanesPerHeat <= 0)
            throw new ArgumentOutOfRangeException(nameof(lanesPerHeat));
        if (!new[] { "Standard", "Seeded", "Random" }.Contains(
                laneAssignment,
                StringComparer.OrdinalIgnoreCase))
            throw new ArgumentException(
                "Lane assignment must be Standard, Seeded, or Random.",
                nameof(laneAssignment));

        AthleticsEventName = eventName.Trim();
        DistanceMeters = distanceMeters;
        QualifyByPlace = qualifyByPlace;
        QualifyByTime = qualifyByTime;
        AdvanceQualificationTies = advanceTies;
        AdvancePlaceTies = advancePlaceTies ?? advanceTies;
        AdvanceTimeTies = advanceTimeTies ?? advanceTies;
        AthleticsLanesPerHeat = lanesPerHeat;
        AthleticsLaneAssignment = laneAssignment;
        NextRoundHeatCount = nextRoundHeatCount;
        NextRoundQualifyByPlace = nextRoundQualifyByPlace;
        NextRoundQualifyByTime = nextRoundQualifyByTime;
        UpdatedAt = DateTime.UtcNow;
    }

    public void ConfigureAthleticsFieldEvent(
        string eventName,
        string metricType,
        int maxAttempts,
        decimal? recordThreshold)
    {
        if (string.IsNullOrWhiteSpace(eventName))
            throw new ArgumentException("An event name is required.", nameof(eventName));
        if (!new[] { "Distance", "Height" }.Contains(
                metricType, StringComparer.OrdinalIgnoreCase))
            throw new ArgumentException(
                "The field metric must be Distance or Height.", nameof(metricType));
        if (maxAttempts is < 1 or > 6)
            throw new ArgumentOutOfRangeException(
                nameof(maxAttempts), "Field events support one through six attempts.");
        if (recordThreshold is <= 0)
            throw new ArgumentOutOfRangeException(nameof(recordThreshold));

        AthleticsEventName = eventName.Trim();
        AthleticsMetricType = metricType.Equals("Height", StringComparison.OrdinalIgnoreCase)
            ? "Height"
            : "Distance";
        AthleticsMaxAttempts = maxAttempts;
        AthleticsRecordThreshold = recordThreshold;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkQualificationApplied()
    {
        QualificationAppliedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetWinner(Guid winnerId)
    {
        WinnerId = winnerId;
        UpdatedAt = DateTime.UtcNow;
    }

    public void ClearWinner()
    {
        WinnerId = null;
        UpdatedAt = DateTime.UtcNow;
    }

    public ContestSegment AddSegment(
        string name,
        string segmentType,
        int sortOrder,
        bool isOptional = false,
        string? activationCondition = null)
    {
        var segment = new ContestSegment(
            Guid.NewGuid(),
            Id,
            TenantId,
            name,
            segmentType,
            sortOrder,
            isOptional,
            activationCondition);
        _segments.Add(segment);
        UpdatedAt = DateTime.UtcNow;
        return segment;
    }

    public ContestEvent AddEvent(string eventType, Guid participantId, Guid? segmentId,
        string? metricValue, Guid? scorerId, string deviceId, long sequenceNumber, string idempotencyKey)
    {
        var contestEvent = new ContestEvent(
            Guid.NewGuid(), Id, TenantId, eventType, participantId, segmentId,
            metricValue, scorerId, deviceId, sequenceNumber, idempotencyKey);
        _events.Add(contestEvent);
        UpdatedAt = DateTime.UtcNow;
        return contestEvent;
    }

    public void AddOfficial(Guid officialId, string role, string? displayName = null)
    {
        var official = new ContestOfficial(
            Guid.NewGuid(), Id, TenantId, officialId, role, displayName);
        _officials.Add(official);
        UpdatedAt = DateTime.UtcNow;
    }

    public ContestMetric RecordMetric(
        string name,
        decimal? numericValue,
        string? textValue,
        string? unit = null,
        Guid? participantId = null,
        Guid? segmentId = null,
        string dataType = "Decimal",
        int precision = 6)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("A metric name is required.", nameof(name));
        if (!numericValue.HasValue && string.IsNullOrWhiteSpace(textValue))
            throw new ArgumentException("A numeric or text metric value is required.");
        if (string.IsNullOrWhiteSpace(dataType))
            throw new ArgumentException("A metric data type is required.", nameof(dataType));
        if (precision is < 0 or > 6)
            throw new ArgumentOutOfRangeException(nameof(precision), "Metric precision must be between 0 and 6.");
        if (participantId.HasValue
            && _participants.All(item => item.ParticipantId != participantId.Value))
            throw new InvalidOperationException("A participant metric must reference a contest participant.");
        if (segmentId.HasValue
            && _segments.All(item => item.Id != segmentId.Value
                && item.NestedSegments.All(nested => nested.Id != segmentId.Value)))
            throw new InvalidOperationException("A segment metric must reference a contest segment.");
        var metric = new ContestMetric(
            Guid.NewGuid(), Id, TenantId, name.Trim(), numericValue,
            string.IsNullOrWhiteSpace(textValue) ? null : textValue.Trim(),
            string.IsNullOrWhiteSpace(unit) ? null : unit.Trim(), participantId, segmentId,
            dataType.Trim(), precision);
        _metrics.Add(metric);
        UpdatedAt = DateTime.UtcNow;
        return metric;
    }

    public void Schedule(string venue, DateTime startTime, int durationMinutes = 0)
    {
        if (string.IsNullOrWhiteSpace(venue))
            throw new ArgumentException("A venue is required.", nameof(venue));
        if (durationMinutes is < 0 or > 1440)
            throw new ArgumentOutOfRangeException(nameof(durationMinutes));
        Venue = venue;
        StartTime = startTime;
        ScheduledDurationMinutes = durationMinutes;
        if (Status != ContestStatus.Scheduled)
            TransitionTo(ContestStatus.Scheduled);
        UpdatedAt = DateTime.UtcNow;
    }

    public void OpenCheckIn() => TransitionTo(ContestStatus.CheckInOpen);

    public void MarkReady() => TransitionTo(ContestStatus.Ready);

    public void Start()
    {
        if (Status == ContestStatus.InProgress) return;
        TransitionTo(ContestStatus.InProgress);
        UpdatedAt = DateTime.UtcNow;
    }

    public void Pause() => TransitionTo(ContestStatus.Paused);

    public void Resume() => TransitionTo(ContestStatus.InProgress);

    public void Suspend() => TransitionTo(ContestStatus.Suspended);

    public void Complete()
    {
        if (Status == ContestStatus.Completed) return;
        TransitionTo(ContestStatus.Completed);
        EndTime = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SubmitForApproval()
    {
        if (Status == ContestStatus.PendingApproval) return;
        TransitionTo(ContestStatus.PendingApproval);
        UpdatedAt = DateTime.UtcNow;
    }

    public void Approve()
    {
        TransitionTo(ContestStatus.Approved);
        UpdatedAt = DateTime.UtcNow;
    }

    public void Cancel()
    {
        TransitionTo(ContestStatus.Cancelled);
        UpdatedAt = DateTime.UtcNow;
    }

    public void Abandon()
    {
        TransitionTo(ContestStatus.Abandoned);
        EndTime = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkCorrected() => TransitionTo(ContestStatus.Corrected);

    public bool CanTransitionTo(
        ContestStatus target,
        IReadOnlyDictionary<ContestStatus, IReadOnlySet<ContestStatus>>? transitions = null) =>
        (transitions ?? DefaultTransitions).TryGetValue(Status, out var allowed)
        && allowed.Contains(target);

    public IReadOnlyCollection<ContestStatus> AllowedTransitions(
        IReadOnlyDictionary<ContestStatus, IReadOnlySet<ContestStatus>>? transitions = null) =>
        (transitions ?? DefaultTransitions).GetValueOrDefault(Status, Set()).ToArray();

    public void TransitionTo(
        ContestStatus target,
        IReadOnlyDictionary<ContestStatus, IReadOnlySet<ContestStatus>>? transitions = null)
    {
        if (!CanTransitionTo(target, transitions))
            throw new InvalidOperationException(
                $"Contest cannot transition from {Status} to {target}.");
        var original = Status;
        Status = target;
        _pendingTransitions.Add(new ContestStatusTransition(
            original, target, DateTime.UtcNow));
        if (target is ContestStatus.Completed or ContestStatus.Abandoned)
            EndTime = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    private static IReadOnlySet<ContestStatus> Set(params ContestStatus[] statuses) =>
        statuses.ToHashSet();

    public void AcceptPendingTransitions() => _pendingTransitions.Clear();
}

public enum ContestShapeKind
{
    HeadToHeadTeam,
    HeadToHeadIndividual,
    MultiParticipantRanked,
    Race,
    Heat,
    BestOfSeries,
    FightCardBout,
    Custom
}

public static class ContestShapeCatalog
{
    public static ContestShapeKind Classify(string contestShape, int? seriesBestOf = null)
    {
        if (seriesBestOf is > 1) return ContestShapeKind.BestOfSeries;
        var shape = contestShape?.Trim() ?? string.Empty;
        if (shape.Contains("bout", StringComparison.OrdinalIgnoreCase)
            || shape.Contains("fight", StringComparison.OrdinalIgnoreCase))
            return ContestShapeKind.FightCardBout;
        if (shape.Equals("BestOfSeries", StringComparison.OrdinalIgnoreCase))
            return ContestShapeKind.BestOfSeries;
        if (shape.Contains("heat", StringComparison.OrdinalIgnoreCase))
            return ContestShapeKind.Heat;
        if (shape.Contains("race", StringComparison.OrdinalIgnoreCase)
            || shape.Contains("athleticsfinal", StringComparison.OrdinalIgnoreCase)
            || shape.Contains("athleticssemifinal", StringComparison.OrdinalIgnoreCase))
            return ContestShapeKind.Race;
        if (shape.Contains("leaderboard", StringComparison.OrdinalIgnoreCase)
            || shape.Contains("multi", StringComparison.OrdinalIgnoreCase)
            || shape.Contains("strokeplay", StringComparison.OrdinalIgnoreCase)
            || shape.Contains("fieldevent", StringComparison.OrdinalIgnoreCase))
            return ContestShapeKind.MultiParticipantRanked;
        if (shape.Contains("individual", StringComparison.OrdinalIgnoreCase))
            return ContestShapeKind.HeadToHeadIndividual;
        if (shape.Contains("headtohead", StringComparison.OrdinalIgnoreCase)
            || shape.Contains("teamvsteam", StringComparison.OrdinalIgnoreCase)
            || shape.Contains("match", StringComparison.OrdinalIgnoreCase)
            || shape.Contains("semifinal", StringComparison.OrdinalIgnoreCase)
            || shape.Contains("final", StringComparison.OrdinalIgnoreCase))
            return ContestShapeKind.HeadToHeadTeam;
        return ContestShapeKind.Custom;
    }
}

public sealed class ContestMetric : TenantEntity<Guid>
{
    public Guid ContestId { get; private set; }
    public Guid? ParticipantId { get; private set; }
    public Guid? SegmentId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public decimal? NumericValue { get; private set; }
    public string? TextValue { get; private set; }
    public string? Unit { get; private set; }
    public string DataType { get; private set; } = "Decimal";
    public int Precision { get; private set; } = 6;
    public DateTime RecordedAt { get; private set; }

    private ContestMetric() { }

    public ContestMetric(
        Guid id, Guid contestId, Guid tenantId, string name,
        decimal? numericValue, string? textValue, string? unit,
        Guid? participantId, Guid? segmentId,
        string dataType = "Decimal", int precision = 6) : base(id, tenantId)
    {
        ContestId = contestId;
        ParticipantId = participantId;
        SegmentId = segmentId;
        Name = name;
        NumericValue = numericValue;
        TextValue = textValue;
        Unit = unit;
        DataType = dataType;
        Precision = precision;
        RecordedAt = DateTime.UtcNow;
    }
}

public sealed record ContestStatusTransition(
    ContestStatus From,
    ContestStatus To,
    DateTime OccurredAt);

public enum ContestStatus
{
    Draft,
    Scheduled,
    CheckInOpen,
    Ready,
    InProgress,
    Paused,
    Suspended,
    Completed,
    PendingApproval,
    Approved,
    Corrected,
    Published,
    Cancelled,
    Abandoned
}

public class ContestParticipant : TenantEntity<Guid>
{
    public Guid ContestId { get; private set; }
    public Guid ParticipantId { get; private set; }
    public string ParticipantType { get; private set; } = string.Empty;
    public int SortOrder { get; private set; }
    public ContestParticipantStatus Status { get; private set; } = ContestParticipantStatus.Entered;
    public string? Seed { get; private set; }

    private ContestParticipant() { }

    public ContestParticipant(Guid id, Guid contestId, Guid tenantId, Guid participantId,
        string participantType, int sortOrder) : base(id, tenantId)
    {
        ContestId = contestId;
        ParticipantId = participantId;
        ParticipantType = participantType;
        SortOrder = sortOrder;
    }

    public void CheckIn() { Status = ContestParticipantStatus.CheckedIn; }
    public void Withdraw() { Status = ContestParticipantStatus.Withdrawn; }
    public void Disqualify() { Status = ContestParticipantStatus.Disqualified; }
    public void SetCutStatus(bool madeCut) { MadeCut = madeCut; }
    public void SetQualification(string status, string reason)
    {
        QualificationStatus = status;
        QualificationReason = reason;
    }

    public bool? MadeCut { get; private set; }
    public string? QualificationStatus { get; private set; }
    public string? QualificationReason { get; private set; }
}

public enum ContestParticipantStatus { Entered, CheckedIn, Withdrawn, Disqualified }

public class ContestSegment : TenantEntity<Guid>
{
    public Guid ContestId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string SegmentType { get; private set; } = string.Empty;
    public int SortOrder { get; private set; }
    public SegmentStatus Status { get; private set; } = SegmentStatus.Pending;
    public bool IsOptional { get; private set; }
    public string? ActivationCondition { get; private set; }
    public DateTime? StartedAt { get; private set; }
    public DateTime? EndedAt { get; private set; }

    private readonly List<ContestSegment> _nestedSegments = new();
    public IReadOnlyCollection<ContestSegment> NestedSegments => _nestedSegments.AsReadOnly();

    private ContestSegment() { }

    public ContestSegment(
        Guid id,
        Guid contestId,
        Guid tenantId,
        string name,
        string segmentType,
        int sortOrder,
        bool isOptional = false,
        string? activationCondition = null)
        : base(id, tenantId)
    {
        ContestId = contestId;
        Name = name;
        SegmentType = segmentType;
        SortOrder = sortOrder;
        IsOptional = isOptional;
        ActivationCondition = activationCondition;
    }

    public ContestSegment AddNestedSegment(
        string name,
        string segmentType,
        int sortOrder,
        bool isOptional = false,
        string? activationCondition = null)
    {
        var segment = new ContestSegment(
            Guid.NewGuid(),
            ContestId,
            TenantId,
            name,
            segmentType,
            sortOrder,
            isOptional,
            activationCondition);
        _nestedSegments.Add(segment);
        return segment;
    }

    public void Configure(
        string name,
        string segmentType,
        bool isOptional = false,
        string? activationCondition = null)
    {
        Name = name;
        SegmentType = segmentType;
        IsOptional = isOptional;
        ActivationCondition = activationCondition;
    }

    public void Start() { Status = SegmentStatus.InProgress; StartedAt = DateTime.UtcNow; }
    public void End() { Status = SegmentStatus.Completed; EndedAt = DateTime.UtcNow; }
}

public enum SegmentStatus { Pending, InProgress, Completed }

public class ContestEvent : TenantEntity<Guid>
{
    public Guid ContestId { get; private set; }
    public string EventType { get; private set; } = string.Empty;
    public Guid ParticipantId { get; private set; }
    public Guid? SecondaryParticipantId { get; private set; }
    public Guid? SegmentId { get; private set; }
    public string? MetricValue { get; private set; }
    public Guid? ScorerId { get; private set; }
    public string DeviceId { get; private set; } = string.Empty;
    public long SequenceNumber { get; private set; }
    public string IdempotencyKey { get; private set; } = string.Empty;
    public DateTime DeviceTimestamp { get; private set; }
    public DateTime ServerTimestamp { get; private set; }
    public EventStatus Status { get; private set; } = EventStatus.Pending;
    public string? CorrectionReason { get; private set; }
    public Guid? SupersededByEventId { get; private set; }

    private ContestEvent() { }

    public ContestEvent(Guid id, Guid contestId, Guid tenantId, string eventType,
        Guid participantId, Guid? segmentId, string? metricValue, Guid? scorerId,
        string deviceId, long sequenceNumber, string idempotencyKey) : base(id, tenantId)
    {
        ContestId = contestId;
        EventType = eventType;
        ParticipantId = participantId;
        SegmentId = segmentId;
        MetricValue = metricValue;
        ScorerId = scorerId;
        DeviceId = deviceId;
        SequenceNumber = sequenceNumber;
        IdempotencyKey = idempotencyKey;
        DeviceTimestamp = DateTime.UtcNow;
        ServerTimestamp = DateTime.UtcNow;
    }

    public void Approve() { Status = EventStatus.Approved; }
    public void Reject(string reason) { Status = EventStatus.Rejected; }
    public void Correct(string reason, Guid supersededByEventId)
    {
        Status = EventStatus.Superseded;
        CorrectionReason = reason;
        SupersededByEventId = supersededByEventId;
    }

    public void RestoreAfterRejectedCorrection()
    {
        if (Status != EventStatus.Superseded)
            throw new InvalidOperationException("Only a superseded event can be restored.");
        Status = EventStatus.Approved;
        CorrectionReason = null;
        SupersededByEventId = null;
    }
}

public enum EventStatus { Pending, Approved, Rejected, Superseded }

public class ContestOfficial : TenantEntity<Guid>
{
    public Guid ContestId { get; private set; }
    public Guid OfficialId { get; private set; }
    public string Role { get; private set; } = string.Empty;
    public string? DisplayName { get; private set; }
    public bool IsConfirmed { get; private set; }

    private ContestOfficial() { }

    public ContestOfficial(
        Guid id,
        Guid contestId,
        Guid tenantId,
        Guid officialId,
        string role,
        string? displayName = null)
        : base(id, tenantId)
    {
        ContestId = contestId;
        OfficialId = officialId;
        Role = role;
        DisplayName = string.IsNullOrWhiteSpace(displayName)
            ? null
            : displayName.Trim();
    }

    public void Confirm() { IsConfirmed = true; }
}

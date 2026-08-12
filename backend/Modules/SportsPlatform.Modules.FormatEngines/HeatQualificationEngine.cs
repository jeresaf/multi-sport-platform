using SportsPlatform.Modules.Competitions.Entities;
using SportsPlatform.Modules.Contests.Entities;
using SportsPlatform.Modules.FormatEngines.Interfaces;

namespace SportsPlatform.Modules.FormatEngines;

/// <summary>
/// Implementation of the heat qualification format engine.
/// Used for track and field events where participants compete in heats
/// and qualify based on place and/or time.
/// </summary>
public class HeatQualificationEngine : IHeatQualificationEngine
{
    public string FormatType => "HeatQualification";

    public IEnumerable<ContestGenerationResult> GenerateContests(
        Stage stage,
        IEnumerable<ParticipantInfo> participants,
        FormatOptions? options = null)
    {
        var heatOptions = options as HeatQualificationOptions ?? new HeatQualificationOptions();
        return GenerateContests(stage, participants, heatOptions)
            .Select(result => new ContestGenerationResult(
                result.Heat,
                result.Round,
                MatchNumber: result.HeatNumber,
                HomeParticipantId: null,
                AwayParticipantId: null,
                GroupName: $"Heat {result.HeatNumber}"));
    }

    public IEnumerable<HeatGenerationResult> GenerateContests(
        Stage stage,
        IEnumerable<ParticipantInfo> participants,
        HeatQualificationOptions? options = null)
    {
        options ??= new HeatQualificationOptions();
        if (!options.SportId.HasValue)
            throw new ArgumentException("A sport ID is required for heat generation.");
        var participantList = participants.ToList();

        ValidateOptions(options);

        if (participantList.Count < 2)
        {
            throw new ArgumentException("At least 2 participants are required for heat qualification format.");
        }

        var results = new List<HeatGenerationResult>();
        var assignments = AssignToHeats(participantList, options).ToList();

        // Group by heat number
        var heats = assignments.GroupBy(a => a.HeatNumber).OrderBy(g => g.Key);

        foreach (var heat in heats)
        {
            var heatNumber = heat.Key;
            var heatParticipants = heat.ToList();
            var participantIds = heatParticipants.Select(a => a.ParticipantId).ToList();
            var participantTypes = heatParticipants
                .Select(a => participantList.First(p => p.ParticipantId == a.ParticipantId).ParticipantType)
                .ToList();

            var contest = CreateHeatContest(
                stage,
                options.SportId.Value,
                heatNumber,
                heatParticipants,
                participantTypes,
                options);

            results.Add(new HeatGenerationResult(
                Heat: contest,
                HeatNumber: heatNumber,
                Round: options.RoundNumber,
                LaneAssignment: options.LaneAssignment,
                AssignedParticipants: heatParticipants.Select(a => 
                    participantList.First(p => p.ParticipantId == a.ParticipantId))));
        }

        return results;
    }

    public IEnumerable<HeatAssignment> AssignToHeats(
        IEnumerable<ParticipantInfo> participants,
        HeatQualificationOptions? options = null)
    {
        options ??= new HeatQualificationOptions();
        var participantList = participants.ToList();

        if (participantList.Count == 0)
            return Enumerable.Empty<HeatAssignment>();
        ValidateOptions(options);

        // Calculate number of heats needed
        var participantsPerHeat = options.ParticipantsPerHeat;
        var numberOfHeats = (int)Math.Ceiling((double)participantList.Count / participantsPerHeat);

        // Sort participants for heat assignment
        IEnumerable<ParticipantInfo> sortedParticipants;
        if (options.UseSeeding && participantList.Any(p => p.Seed.HasValue))
        {
            sortedParticipants = participantList
                .Where(p => p.Seed.HasValue)
                .OrderBy(p => p.Seed)
                .ThenBy(p => p.ParticipantId)
                .Concat(participantList.Where(p => !p.Seed.HasValue)
                    .OrderBy(p => p.ParticipantId));
        }
        else
        {
            sortedParticipants = participantList.OrderBy(p => p.ParticipantId);
        }

        var participantArray = sortedParticipants.ToArray();
        var heatBuckets = Enumerable.Range(0, numberOfHeats)
            .Select(_ => new List<ParticipantInfo>())
            .ToArray();
        for (var index = 0; index < participantArray.Length; index++)
        {
            var block = index / numberOfHeats;
            var offset = index % numberOfHeats;
            var heatIndex = options.UseSeeding && options.UseSerpentineSeeding
                && block % 2 == 1
                    ? numberOfHeats - 1 - offset
                    : offset;
            heatBuckets[heatIndex].Add(participantArray[index]);
        }

        var assignments = new List<HeatAssignment>();
        for (var heatIndex = 0; heatIndex < heatBuckets.Length; heatIndex++)
        {
            var lanes = LaneOrder(options, heatIndex + 1).ToArray();
            for (var index = 0; index < heatBuckets[heatIndex].Count; index++)
            {
                assignments.Add(new HeatAssignment(
                    heatBuckets[heatIndex][index].ParticipantId,
                    heatIndex + 1,
                    lanes[index],
                    null));
            }
        }
        return assignments;
    }

    public QualificationResult CalculateQualification(
        IEnumerable<HeatResult> heatResults,
        HeatQualificationOptions? options = null)
    {
        options ??= new HeatQualificationOptions();
        ValidateOptions(options);
        var results = heatResults.ToList();

        if (results.Count == 0)
            return new QualificationResult(
                Enumerable.Empty<QualifiedParticipant>(),
                Enumerable.Empty<QualifiedParticipant>(),
                0);

        var resultRows = results
            .SelectMany(heat => heat.Results.Select(result => new
            {
                Result = result,
                heat.HeatNumber
            }))
            .ToList();
        var allResults = resultRows.Where(item => !item.Result.IsDisqualified).ToList();

        var qualified = new List<QualifiedParticipant>();
        var notQualified = new List<QualifiedParticipant>();

        // First, qualify by place
        var placeQualifiers = allResults
            .GroupBy(item => item.HeatNumber)
            .OrderBy(group => group.Key)
            .SelectMany(group =>
            {
                var candidates = group
                    .Where(item => item.Result.Place.HasValue
                        && item.Result.Place.Value <= options.QualifyByPlace)
                    .OrderBy(item => item.Result.Place)
                    .ThenBy(item => item.Result.Time)
                    .ThenBy(item => item.Result.Lane);
                return AdvancePlaceTies(options)
                    ? candidates
                    : candidates.Take(options.QualifyByPlace);
            })
            .ToList();

        foreach (var q in placeQualifiers)
        {
            qualified.Add(new QualifiedParticipant(
                ParticipantId: q.Result.ParticipantId,
                Place: q.Result.Place,
                Time: q.Result.Time,
                QualificationReason: $"Qualified by place ({q.Result.Place}) in heat {q.HeatNumber}",
                HeatNumber: q.HeatNumber));
        }

        // Then, qualify by time (fastest losers)
        var remainingSlots = options.TotalQualificationSlots - qualified.Count;
        if (remainingSlots > 0)
        {
            var orderedCandidates = allResults
                .Where(item => !qualified.Any(q => q.ParticipantId == item.Result.ParticipantId) && item.Result.Time.HasValue)
                .OrderBy(item => item.Result.Time)
                .ToList();
            var notYetQualified = orderedCandidates.Take(remainingSlots).ToList();
            if (AdvanceTimeTies(options)
                && notYetQualified.Count == remainingSlots
                && orderedCandidates.Count > remainingSlots)
            {
                var cutoff = notYetQualified[^1].Result.Time;
                notYetQualified.AddRange(orderedCandidates
                    .Skip(remainingSlots)
                    .TakeWhile(item => item.Result.Time == cutoff));
            }

            foreach (var q in notYetQualified)
            {
                qualified.Add(new QualifiedParticipant(
                    ParticipantId: q.Result.ParticipantId,
                    Place: q.Result.Place,
                    Time: q.Result.Time,
                    QualificationReason: "Qualified by time (fastest loser)",
                    HeatNumber: q.HeatNumber));
            }
        }

        // Mark remaining as not qualified
        var allQualifiedIds = qualified.Select(q => q.ParticipantId).ToHashSet();
        foreach (var r in allResults.Where(item => !allQualifiedIds.Contains(item.Result.ParticipantId)))
        {
            notQualified.Add(new QualifiedParticipant(
                ParticipantId: r.Result.ParticipantId,
                Place: r.Result.Place,
                Time: r.Result.Time,
                QualificationReason: "Did not qualify",
                HeatNumber: r.HeatNumber));
        }

        foreach (var r in resultRows.Where(item => item.Result.IsDisqualified))
        {
            notQualified.Add(new QualifiedParticipant(
                r.Result.ParticipantId,
                null,
                null,
                "Disqualified",
                r.HeatNumber));
        }

        return new QualificationResult(qualified, notQualified, options.TotalQualificationSlots);
    }

    private Contest CreateHeatContest(
        Stage stage,
        Guid sportId,
        int heatNumber,
        List<HeatAssignment> assignments,
        List<string> participantTypes,
        HeatQualificationOptions options)
    {
        var contest = new Contest(
            Guid.NewGuid(),
            stage.TenantId,
            sportId,
            options.ContestShape);

        contest.AssignToStage(stage.Id, options.ContestSortOrderOffset + heatNumber);
        contest.SetName($"{options.ContestNamePrefix} {heatNumber}");
        contest.AddSegment("Race", "Race", 1);

        for (var i = 0; i < assignments.Count; i++)
        {
            contest.AddParticipant(
                assignments[i].ParticipantId,
                i < participantTypes.Count ? participantTypes[i] : "Athlete",
                assignments[i].Lane);
        }

        return contest;
    }

    private static IEnumerable<int> LaneOrder(HeatQualificationOptions options, int heatNumber)
    {
        var lanes = Enumerable.Range(1, options.LanesPerHeat).ToList();
        if (options.LaneAssignment.Equals("Seeded", StringComparison.OrdinalIgnoreCase))
        {
            var centre = (options.LanesPerHeat + 1) / 2.0;
            return lanes.OrderBy(lane => Math.Abs(lane - centre)).ThenBy(lane => lane);
        }
        if (options.LaneAssignment.Equals("Random", StringComparison.OrdinalIgnoreCase))
        {
            var random = new Random(unchecked(options.RandomSeed + (heatNumber * 397)));
            return lanes.OrderBy(_ => random.Next());
        }
        return lanes;
    }

    private static bool AdvancePlaceTies(HeatQualificationOptions options) =>
        options.AdvancePlaceTies ?? options.AdvanceTies;

    private static bool AdvanceTimeTies(HeatQualificationOptions options) =>
        options.AdvanceTimeTies ?? options.AdvanceTies;

    private static void ValidateOptions(HeatQualificationOptions options)
    {
        if (options.ParticipantsPerHeat <= 0 || options.LanesPerHeat <= 0
            || options.ParticipantsPerHeat > options.LanesPerHeat)
            throw new ArgumentOutOfRangeException(nameof(options),
                "Participants per heat must fit within the configured lanes.");
        if (options.QualifyByPlace < 0 || options.TotalQualificationSlots < 0)
            throw new ArgumentOutOfRangeException(nameof(options),
                "Qualification counts cannot be negative.");
        if (!new[] { "Standard", "Seeded", "Random" }.Contains(
                options.LaneAssignment,
                StringComparer.OrdinalIgnoreCase))
            throw new ArgumentException("Lane assignment must be Standard, Seeded, or Random.", nameof(options));
        if (string.IsNullOrWhiteSpace(options.ContestShape)
            || string.IsNullOrWhiteSpace(options.ContestNamePrefix))
            throw new ArgumentException("Contest shape and name prefix are required.", nameof(options));
    }
}

/// <summary>
/// Options specific to heat qualification format.
/// </summary>
public class HeatQualificationOptions : FormatOptions
{
    /// <summary>
    /// Number of participants per heat.
    /// </summary>
    public int ParticipantsPerHeat { get; set; } = 8;

    /// <summary>
    /// Number of lanes available per heat.
    /// </summary>
    public int LanesPerHeat { get; set; } = 8;

    /// <summary>
    /// Round number for this heat stage.
    /// </summary>
    public int RoundNumber { get; set; } = 1;

    /// <summary>
    /// Number of participants to qualify for the next round.
    /// </summary>
    public int TotalQualificationSlots { get; set; } = 24;

    /// <summary>
    /// Number of participants to qualify by place per heat.
    /// </summary>
    public int QualifyByPlace { get; set; } = 3;

    /// <summary>
    /// Whether to use seeding for heat assignment.
    /// </summary>
    public bool UseSeeding { get; set; } = true;

    /// <summary>
    /// Whether to use serpentine seeding (fastest times in different heats).
    /// </summary>
    public bool UseSerpentineSeeding { get; set; } = true;

    /// <summary>
    /// Lane assignment method (e.g., "Standard", "Seeded", "Random").
    /// </summary>
    public string LaneAssignment { get; set; } = "Standard";

    public int RandomSeed { get; set; } = 1729;

    public string ContestShape { get; set; } = "Heat";

    public string ContestNamePrefix { get; set; } = "Heat";

    public int ContestSortOrderOffset { get; set; }

    /// <summary>
    /// Whether times are measured in hundredths of a second.
    /// </summary>
    public bool UseHundredths { get; set; } = true;

    /// <summary>
    /// Whether this is a timed final (no heats, just timed final).
    /// </summary>
    public bool IsTimedFinal { get; set; } = false;

    public bool AdvanceTies { get; set; } = true;

    public bool? AdvancePlaceTies { get; set; }

    public bool? AdvanceTimeTies { get; set; }
}

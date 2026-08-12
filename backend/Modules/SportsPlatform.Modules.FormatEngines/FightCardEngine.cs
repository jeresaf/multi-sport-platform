using SportsPlatform.Modules.Competitions.Entities;
using SportsPlatform.Modules.Contests.Entities;
using SportsPlatform.Modules.FormatEngines.Interfaces;

namespace SportsPlatform.Modules.FormatEngines;

/// <summary>
/// Implementation of the fight-card format engine.
/// Used for combat sports like MMA and boxing where fighters compete in bouts
/// organized by weight classes and ranked by main event hierarchy.
/// </summary>
public class FightCardEngine : IFightCardEngine
{
    public string FormatType => "FightCard";

    // Standard weight classes (boxing/MMA)
    private static readonly List<WeightClass> StandardWeightClasses = new()
    {
        new WeightClass { Name = "Heavyweight", WeightLimitLbs = 265, WeightLimitKg = 120, IsTitleClass = true, DefaultRounds = 3, TitleRounds = 5 },
        new WeightClass { Name = "Cruiserweight", WeightLimitLbs = 200, WeightLimitKg = 91, IsTitleClass = true, DefaultRounds = 3, TitleRounds = 5 },
        new WeightClass { Name = "Light Heavyweight", WeightLimitLbs = 175, WeightLimitKg = 79, IsTitleClass = true, DefaultRounds = 3, TitleRounds = 5 },
        new WeightClass { Name = "Super Middleweight", WeightLimitLbs = 168, WeightLimitKg = 76, IsTitleClass = true, DefaultRounds = 3, TitleRounds = 5 },
        new WeightClass { Name = "Middleweight", WeightLimitLbs = 160, WeightLimitKg = 73, IsTitleClass = true, DefaultRounds = 3, TitleRounds = 5 },
        new WeightClass { Name = "Super Welterweight", WeightLimitLbs = 154, WeightLimitKg = 70, IsTitleClass = true, DefaultRounds = 3, TitleRounds = 5 },
        new WeightClass { Name = "Welterweight", WeightLimitLbs = 147, WeightLimitKg = 67, IsTitleClass = true, DefaultRounds = 3, TitleRounds = 5 },
        new WeightClass { Name = "Super Lightweight", WeightLimitLbs = 140, WeightLimitKg = 64, IsTitleClass = true, DefaultRounds = 3, TitleRounds = 5 },
        new WeightClass { Name = "Lightweight", WeightLimitLbs = 135, WeightLimitKg = 61, IsTitleClass = true, DefaultRounds = 3, TitleRounds = 5 },
        new WeightClass { Name = "Super Featherweight", WeightLimitLbs = 130, WeightLimitKg = 59, IsTitleClass = true, DefaultRounds = 3, TitleRounds = 5 },
        new WeightClass { Name = "Featherweight", WeightLimitLbs = 126, WeightLimitKg = 57, IsTitleClass = true, DefaultRounds = 3, TitleRounds = 5 },
        new WeightClass { Name = "Super Bantamweight", WeightLimitLbs = 122, WeightLimitKg = 55, IsTitleClass = true, DefaultRounds = 3, TitleRounds = 5 },
        new WeightClass { Name = "Bantamweight", WeightLimitLbs = 118, WeightLimitKg = 54, IsTitleClass = true, DefaultRounds = 3, TitleRounds = 5 },
        new WeightClass { Name = "Super Flyweight", WeightLimitLbs = 115, WeightLimitKg = 52, IsTitleClass = true, DefaultRounds = 3, TitleRounds = 5 },
        new WeightClass { Name = "Flyweight", WeightLimitLbs = 112, WeightLimitKg = 51, IsTitleClass = true, DefaultRounds = 3, TitleRounds = 5 },
    };

    public IEnumerable<ContestGenerationResult> GenerateContests(
        Stage stage,
        IEnumerable<ParticipantInfo> participants,
        FormatOptions? options = null)
    {
        var fightCardOptions = options as FightCardOptions ?? new FightCardOptions();
        return GenerateContests(stage, participants, fightCardOptions)
            .Select(result => new ContestGenerationResult(
                result.Bout,
                Round: 1,
                MatchNumber: result.BoutNumber,
                HomeParticipantId: result.Fighter1?.ParticipantId,
                AwayParticipantId: result.Fighter2?.ParticipantId,
                GroupName: result.WeightClass));
    }

    public IEnumerable<FightCardGenerationResult> GenerateContests(
        Stage stage,
        IEnumerable<ParticipantInfo> participants,
        FightCardOptions? options = null)
    {
        options ??= new FightCardOptions();
        if (!options.SportId.HasValue)
            throw new ArgumentException("A sport ID is required for fight-card generation.");
        var participantList = participants.ToList();

        if (participantList.Count < 2)
        {
            throw new ArgumentException("At least 2 participants are required for fight-card format.");
        }

        if (participantList.Count % 2 != 0)
        {
            throw new ArgumentException("Fight-card format requires an even number of participants.");
        }

        var results = new List<FightCardGenerationResult>();
        var boutNumber = 1;

        // Group participants by weight class if specified
        var weightGroups = GroupByWeightClass(participantList, options);

        foreach (var group in weightGroups)
        {
            var weightClass = group.Key;
            var fighters = group.Value;
            var weightClassDef = StandardWeightClasses.FirstOrDefault(w => w.Name == weightClass) 
                ?? new WeightClass { Name = weightClass, DefaultRounds = 3, TitleRounds = 5 };

            // Pair fighters within weight class
            for (var i = 0; i < fighters.Count; i += 2)
            {
                var fighter1 = fighters[i];
                var fighter2 = i + 1 < fighters.Count ? fighters[i + 1] : null;

                var boutType = options.BoutTypes.ContainsKey(weightClass) 
                    ? options.BoutTypes[weightClass] 
                    : "Standard";

                var scheduledRounds = options.IsTitleCard && weightClassDef.IsTitleClass
                    ? weightClassDef.TitleRounds
                    : weightClassDef.DefaultRounds;

                var contest = CreateBoutContest(
                    stage,
                    options.SportId.Value,
                    boutNumber,
                    fighter1,
                    fighter2,
                    weightClass);
                contest.ConfigureBout(weightClass, boutType, scheduledRounds);
                for (var round = 1; round <= scheduledRounds; round++)
                    contest.AddSegment($"Round {round}", "Round", round);

                results.Add(new FightCardGenerationResult(
                    Bout: contest,
                    BoutNumber: boutNumber,
                    WeightClass: weightClass,
                    BoutType: boutType,
                    Fighter1: fighter1,
                    Fighter2: fighter2,
                    ScheduledRounds: scheduledRounds));

                boutNumber++;
            }
        }

        // Sort by bout order (main event last)
        if (options.BoutOrder == "CoMain")
        {
            // Main event at end, co-main before, then prelims
            results = results.OrderBy(r => r.BoutNumber).ToList();
        }

        return results;
    }

    public BoutOutcome DetermineBoutOutcome(BoutResult boutResult)
    {
        if (boutResult.IsDraw)
        {
            throw new InvalidOperationException("Cannot determine winner from a draw.");
        }

        if (!boutResult.WinnerId.HasValue || !boutResult.LoserId.HasValue)
        {
            throw new InvalidOperationException("Winner and loser IDs are required.");
        }

        var winMethod = NormalizeWinMethod(boutResult.ResultMethod);
        var isUnanimous = winMethod.Contains("Decision") && winMethod.Contains("Unanimous");
        var isTitleBout = false; // Would be determined from bout metadata

        return new BoutOutcome(
            WinnerId: boutResult.WinnerId.Value,
            LoserId: boutResult.LoserId.Value,
            WinMethod: winMethod,
            WinRound: boutResult.RoundFinished ?? 1,
            WinTime: boutResult.TimeInRound ?? TimeSpan.Zero,
            IsUnanimous: isUnanimous,
            IsTitleBout: isTitleBout);
    }

    public FightCardValidationResult ValidateFightCard(FightCard card)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        if (card.Bouts.Count == 0)
        {
            errors.Add("Fight card has no bouts.");
        }

        var activeOrders = card.Bouts.Where(bout => !bout.IsCancelled)
            .Select(bout => bout.BoutOrder)
            .Order()
            .ToArray();
        if (activeOrders.Distinct().Count() != activeOrders.Length
            || !activeOrders.SequenceEqual(Enumerable.Range(1, activeOrders.Length)))
            errors.Add("Active bout order must be unique and contiguous from one.");

        var sectionOrder = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["PreliminaryCard"] = 0,
            ["MainCard"] = 1,
            ["CoMainEvent"] = 2,
            ["MainEvent"] = 3
        };
        var orderedSections = card.Bouts.Where(bout => !bout.IsCancelled)
            .OrderBy(bout => bout.BoutOrder)
            .Where(bout => sectionOrder.ContainsKey(bout.BoutType))
            .Select(bout => sectionOrder[bout.BoutType])
            .ToArray();
        if (!orderedSections.SequenceEqual(orderedSections.Order()))
            errors.Add("Bout sections must remain in preliminary, main-card, co-main, main-event order.");

        // Check for duplicate fighters
        var allFighterIds = card.Bouts
            .Where(b => !b.IsCancelled)
            .SelectMany(b => new[] { b.Fighter1Id, b.Fighter2Id })
            .ToList();

        var duplicates = allFighterIds
            .GroupBy(id => id)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (duplicates.Any())
        {
            errors.Add($"Fighters appear in multiple bouts: {string.Join(", ", duplicates)}");
        }

        // Check weight class ordering
        var weightClassOrder = StandardWeightClasses.Select(w => w.Name).ToList();
        var boutsByClass = card.Bouts
            .Where(b => !b.IsCancelled)
            .GroupBy(b => b.WeightClass)
            .OrderBy(g => weightClassOrder.IndexOf(g.Key))
            .ToList();

        for (var i = 0; i < boutsByClass.Count - 1; i++)
        {
            var currentClass = boutsByClass[i].Key;
            var nextClass = boutsByClass[i + 1].Key;
            var currentIndex = weightClassOrder.IndexOf(currentClass);
            var nextIndex = weightClassOrder.IndexOf(nextClass);

            if (nextIndex < currentIndex)
            {
                warnings.Add($"Weight class '{nextClass}' appears before '{currentClass}' which is unusual.");
            }
        }

        // Check for title bout conflicts
        var titleBouts = card.Bouts.Count(b => b.IsTitleBout && !b.IsCancelled);
        if (titleBouts > 3)
        {
            warnings.Add($"Card has {titleBouts} title bouts which is unusual for a single event.");
        }

        // Check for cancelled bouts
        var cancelledBouts = card.Bouts.Count(b => b.IsCancelled);
        if (cancelledBouts > 0)
        {
            warnings.Add($"{cancelledBouts} bout(s) have been cancelled.");
        }

        return new FightCardValidationResult(
            IsValid: !errors.Any(),
            Errors: errors,
            Warnings: warnings);
    }

    public FightCard ReorderFightCard(FightCard card, IReadOnlyList<Guid> orderedBoutIds)
    {
        if (orderedBoutIds.Count != card.Bouts.Count
            || orderedBoutIds.Distinct().Count() != card.Bouts.Count
            || orderedBoutIds.Any(id => card.Bouts.All(bout => bout.Id != id)))
            throw new ArgumentException(
                "The new order must contain every bout exactly once.",
                nameof(orderedBoutIds));

        var byId = card.Bouts.ToDictionary(bout => bout.Id);
        var reordered = orderedBoutIds.Select((id, index) =>
        {
            var bout = byId[id];
            bout.BoutOrder = index + 1;
            return bout;
        }).ToList();
        card.Bouts = reordered;
        var validation = ValidateFightCard(card);
        if (!validation.IsValid)
            throw new InvalidOperationException(string.Join(" ", validation.Errors));
        return card;
    }

    private Dictionary<string, List<ParticipantInfo>> GroupByWeightClass(
        List<ParticipantInfo> participants,
        FightCardOptions options)
    {
        var groups = new Dictionary<string, List<ParticipantInfo>>();

        foreach (var participant in participants)
        {
            // Use group name as weight class if specified, otherwise use default
            var weightClass = !string.IsNullOrEmpty(participant.GroupName) 
                ? participant.GroupName 
                : options.DefaultWeightClass;

            if (!groups.ContainsKey(weightClass))
            {
                groups[weightClass] = new List<ParticipantInfo>();
            }
            groups[weightClass].Add(participant);
        }

        return groups;
    }

    private Contest CreateBoutContest(Stage stage, Guid sportId, int boutNumber,
        ParticipantInfo fighter1, ParticipantInfo? fighter2, string weightClass)
    {
        var contest = new Contest(
            Guid.NewGuid(),
            stage.TenantId,
            sportId,
            "Bout");

        contest.AssignToStage(stage.Id, boutNumber);
        contest.SetName($"Bout {boutNumber} - {weightClass}");

        contest.AddParticipant(fighter1.ParticipantId, "Athlete", 1);

        if (fighter2 != null)
        {
            contest.AddParticipant(fighter2.ParticipantId, "Athlete", 2);
        }

        return contest;
    }

    private string NormalizeWinMethod(string method)
    {
        var normalized = method.ToUpperInvariant().Trim();

        return normalized switch
        {
            "KO" or "K.O." or "KNOCKOUT" => "KO",
            "TKO" or "T.K.O." or "TECHNICAL KNOCKOUT" => "TKO",
            "UD" or "UNANIMOUS DECISION" => "Unanimous Decision",
            "SD" or "SPLIT DECISION" => "Split Decision",
            "MD" or "MAJORITY DECISION" => "Majority Decision",
            "DRAW" or "D" => "Draw",
            "NC" or "NO CONTEST" => "No Contest",
            "DQ" or "DISQUALIFICATION" => "Disqualification",
            "RET" or "RETIREMENT" => "Retirement",
            _ => normalized
        };
    }
}

/// <summary>
/// Options specific to fight-card format.
/// </summary>
public class FightCardOptions : FormatOptions
{
    /// <summary>
    /// Default weight class for unassigned fighters.
    /// </summary>
    public string DefaultWeightClass { get; set; } = "Middleweight";

    /// <summary>
    /// Whether this is a title card (main events are title fights).
    /// </summary>
    public bool IsTitleCard { get; set; } = false;

    /// <summary>
    /// Bout order preference: "Standard" (main last), "CoMain" (co-main before main).
    /// </summary>
    public string BoutOrder { get; set; } = "Standard";

    /// <summary>
    /// Bout types by weight class.
    /// </summary>
    public Dictionary<string, string> BoutTypes { get; set; } = new();

    /// <summary>
    /// Whether to use amateur scoring (10-point must system).
    /// </summary>
    public bool UseAmateurScoring { get; set; } = false;

    /// <summary>
    /// Whether to allow draws.
    /// </summary>
    public bool AllowDraws { get; set; } = true;

    /// <summary>
    /// Whether to use instant replay for controversial decisions.
    /// </summary>
    public bool UseInstantReplay { get; set; } = false;
}

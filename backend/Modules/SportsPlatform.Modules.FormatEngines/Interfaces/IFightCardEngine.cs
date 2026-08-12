using SportsPlatform.Modules.Competitions.Entities;
using SportsPlatform.Modules.Contests.Entities;
using SportsPlatform.Modules.FormatEngines.Interfaces;

namespace SportsPlatform.Modules.FormatEngines.Interfaces;

/// <summary>
/// Interface for fight-card format engine.
/// Used for combat sports like MMA and boxing where fighters compete in bouts
/// organized by weight classes and ranked by main event hierarchy.
/// </summary>
public interface IFightCardEngine : IFormatEngine
{
    /// <summary>
    /// Generates a fight card with bouts organized by weight class and event hierarchy.
    /// </summary>
    IEnumerable<FightCardGenerationResult> GenerateContests(
        Stage stage,
        IEnumerable<ParticipantInfo> participants,
        FightCardOptions? options = null);

    /// <summary>
    /// Determines the winner of a bout based on result method.
    /// </summary>
    /// <param name="boutResult">The result of the bout.</param>
    /// <returns>Winner and loser information.</returns>
    BoutOutcome DetermineBoutOutcome(BoutResult boutResult);

    /// <summary>
    /// Validates a fight card for scheduling conflicts and weight class issues.
    /// </summary>
    /// <param name="card">The fight card to validate.</param>
    /// <returns>Validation result with any issues found.</returns>
    FightCardValidationResult ValidateFightCard(FightCard card);

    /// <summary>
    /// Applies a complete new bout order after validating card hierarchy.
    /// </summary>
    FightCard ReorderFightCard(FightCard card, IReadOnlyList<Guid> orderedBoutIds);
}

/// <summary>
/// Result of fight card generation containing all bouts and their hierarchy.
/// </summary>
public record FightCardGenerationResult(
    Contest Bout,
    int BoutNumber,
    string WeightClass,
    string BoutType,
    ParticipantInfo? Fighter1,
    ParticipantInfo? Fighter2,
    int ScheduledRounds);

/// <summary>
/// Result of a completed bout.
/// </summary>
public record BoutResult(
    Guid BoutId,
    Guid? WinnerId,
    Guid? LoserId,
    string ResultMethod,
    int? RoundFinished,
    TimeSpan? TimeInRound,
    string? TechnicalDecisionReason,
    bool IsDraw = false);

/// <summary>
/// Outcome of a bout including winner and loser details.
/// </summary>
public record BoutOutcome(
    Guid WinnerId,
    Guid LoserId,
    string WinMethod,
    int WinRound,
    TimeSpan WinTime,
    bool IsUnanimous,
    bool IsTitleBout);

/// <summary>
/// A fight card containing multiple bouts.
/// </summary>
public class FightCard
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime EventDate { get; set; }
    public string? Venue { get; set; }
    public List<FightCardBout> Bouts { get; set; } = new();
}

/// <summary>
/// A bout within a fight card.
/// </summary>
public class FightCardBout
{
    public Guid Id { get; set; }
    public Guid Fighter1Id { get; set; }
    public Guid Fighter2Id { get; set; }
    public string WeightClass { get; set; } = string.Empty;
    public string BoutType { get; set; } = "Standard";
    public int ScheduledRounds { get; set; } = 3;
    public int BoutOrder { get; set; }
    public bool IsTitleBout { get; set; }
    public bool IsCancelled { get; set; }
    public string? CancellationReason { get; set; }
}

/// <summary>
/// Result of fight card validation.
/// </summary>
public record FightCardValidationResult(
    bool IsValid,
    List<string> Errors,
    List<string> Warnings);

/// <summary>
/// Weight class definition for combat sports.
/// </summary>
public class WeightClass
{
    public string Name { get; set; } = string.Empty;
    public int WeightLimitLbs { get; set; }
    public int WeightLimitKg { get; set; }
    public bool IsTitleClass { get; set; }
    public int DefaultRounds { get; set; } = 3;
    public int TitleRounds { get; set; } = 5;
}

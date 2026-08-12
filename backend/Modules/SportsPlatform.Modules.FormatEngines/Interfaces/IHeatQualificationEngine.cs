using SportsPlatform.Modules.Competitions.Entities;
using SportsPlatform.Modules.Contests.Entities;
using SportsPlatform.Modules.FormatEngines.Interfaces;

namespace SportsPlatform.Modules.FormatEngines.Interfaces;

/// <summary>
/// Interface for heat qualification format engine.
/// Used for track and field events where participants compete in heats
/// and qualify based on place and/or time.
/// </summary>
public interface IHeatQualificationEngine : IFormatEngine
{
    /// <summary>
    /// Generates heats for the given stage and participants.
    /// </summary>
    IEnumerable<HeatGenerationResult> GenerateContests(
        Stage stage,
        IEnumerable<ParticipantInfo> participants,
        HeatQualificationOptions? options = null);

    /// <summary>
    /// Calculates qualification from heat results.
    /// </summary>
    /// <param name="heatResults">Results from all heats.</param>
    /// <param name="options">Qualification options.</param>
    /// <returns>Qualified participants and their advancement details.</returns>
    QualificationResult CalculateQualification(
        IEnumerable<HeatResult> heatResults,
        HeatQualificationOptions? options = null);

    /// <summary>
    /// Assigns participants to heats with optional seeding.
    /// </summary>
    /// <param name="participants">Participants to assign.</param>
    /// <param name="options">Heat assignment options.</param>
    /// <returns>Heat assignments for each participant.</returns>
    IEnumerable<HeatAssignment> AssignToHeats(
        IEnumerable<ParticipantInfo> participants,
        HeatQualificationOptions? options = null);
}

/// <summary>
/// Result of heat generation containing the heat contest and its metadata.
/// </summary>
public record HeatGenerationResult(
    Contest Heat,
    int HeatNumber,
    int Round,
    string? LaneAssignment,
    IEnumerable<ParticipantInfo> AssignedParticipants);

/// <summary>
/// Result of a completed heat.
/// </summary>
public record HeatResult(
    Guid HeatId,
    int HeatNumber,
    IEnumerable<HeatParticipantResult> Results);

/// <summary>
/// Result for a participant in a heat.
/// </summary>
public record HeatParticipantResult(
    Guid ParticipantId,
    int Lane,
    int? Place,
    TimeSpan? Time,
    bool Qualified,
    string? QualificationReason,
    bool IsDisqualified = false);

/// <summary>
/// Assignment of a participant to a heat.
/// </summary>
public record HeatAssignment(
    Guid ParticipantId,
    int HeatNumber,
    int Lane,
    TimeSpan? SeedTime);

/// <summary>
/// Result of qualification calculation.
/// </summary>
public record QualificationResult(
    IEnumerable<QualifiedParticipant> QualifiedParticipants,
    IEnumerable<QualifiedParticipant> NotQualifiedParticipants,
    int TotalQualificationSlots);

/// <summary>
/// A participant who qualified for the next round.
/// </summary>
public record QualifiedParticipant(
    Guid ParticipantId,
    int? Place,
    TimeSpan? Time,
    string QualificationReason,
    int? HeatNumber);

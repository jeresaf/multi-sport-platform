using SportsPlatform.Modules.LiveScoring.Entities;

namespace SportsPlatform.Modules.LiveScoring;

public interface ILiveScoringService
{
    Task<LiveScoringOperationResult> SubmitEventAsync(
        LiveEventSubmission submission,
        CancellationToken cancellationToken = default);

    Task<LiveScoringOperationResult> UpdateEventAsync(
        Guid contestId,
        Guid eventId,
        decimal newValue,
        string? updatedBy,
        string? reason,
        CancellationToken cancellationToken = default);

    Task<LiveScoringOperationResult> ReverseEventAsync(
        Guid contestId,
        Guid eventId,
        string? reversedBy,
        string reason,
        CancellationToken cancellationToken = default);

    Task<LiveScoreCorrectionResult> RequestCorrectionAsync(
        Guid contestId,
        Guid eventId,
        LiveScoreCorrectionProposal proposal,
        Guid requestedBy,
        CancellationToken cancellationToken = default);

    Task<LiveScoreCorrectionResult> ReviewCorrectionAsync(
        Guid contestId,
        Guid correctionId,
        bool approve,
        Guid reviewedBy,
        string? reviewReason,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LiveScoreCorrectionView>> GetCorrectionsAsync(
        Guid contestId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LiveScore>> GetContestScoresAsync(
        Guid contestId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LiveScoreUpdate>> GetScoreHistoryAsync(
        Guid contestId,
        int skip,
        int take,
        CancellationToken cancellationToken = default);

    Task<LiveScoreSession?> GetActiveSessionAsync(
        Guid contestId,
        CancellationToken cancellationToken = default);

    Task<LiveScoreSession> StartSessionAsync(
        Guid contestId,
        Guid eventId,
        string name,
        CancellationToken cancellationToken = default);

    Task<LiveScoringOperationResult> PauseSessionAsync(
        Guid contestId,
        CancellationToken cancellationToken = default);

    Task<LiveScoringOperationResult> ResumeSessionAsync(
        Guid contestId,
        CancellationToken cancellationToken = default);

    Task<LiveScoringOperationResult> EndSessionAsync(
        Guid contestId,
        CancellationToken cancellationToken = default);

    Task<LiveScoringOperationResult> SetSessionContextAsync(
        Guid contestId,
        Guid segmentId,
        decimal clockSeconds,
        bool clockCountsDown,
        CancellationToken cancellationToken = default);

    Task<LiveScoringOperationResult> StartClockAsync(
        Guid contestId,
        CancellationToken cancellationToken = default);

    Task<LiveScoringOperationResult> PauseClockAsync(
        Guid contestId,
        CancellationToken cancellationToken = default);
}

public sealed record LiveEventSubmission(
    Guid TenantId,
    Guid ContestId,
    Guid EventId,
    string EventType,
    Guid ParticipantId,
    Guid? SecondaryParticipantId,
    Guid? SegmentId,
    string MetricName,
    decimal Value,
    decimal? ClockSeconds,
    DateTime DeviceTimestamp,
    Guid ScorerId,
    string DeviceId,
    int? SequenceNumber,
    string IdempotencyKey,
    Guid? RulesetVersionId,
    Guid? PrimaryPlayerId = null,
    Guid? SecondaryPlayerId = null);

public enum LiveScoringFailureKind { None, InvalidSequence, Validation }

public enum LiveScoreCorrectionKind { Correct, Reverse, Replace }

public sealed record LiveScoreCorrectionProposal(
    LiveScoreCorrectionKind Kind,
    string Reason,
    decimal? Value = null,
    string? EventType = null,
    Guid? ParticipantId = null,
    Guid? SecondaryParticipantId = null,
    Guid? SegmentId = null,
    string? MetricName = null,
    decimal? ClockSeconds = null);

public sealed record LiveScoreCorrectionView(
    Guid Id,
    Guid LiveScoreId,
    LiveScoreCorrectionKind Kind,
    string Reason,
    string Status,
    string? RequestedBy,
    DateTime RequestedAt,
    string? ReviewedBy,
    DateTime? ReviewedAt,
    string? ReviewReason,
    string? OldValue,
    string? NewValue);

public sealed record LiveScoreCorrectionResult(
    bool Success,
    string Message,
    LiveScoreCorrectionView? Correction = null,
    LiveScore? Event = null);

public record LiveScoringOperationResult(
    bool Success,
    string Message,
    LiveScore? Event = null,
    bool IsReplay = false,
    LiveScoringFailureKind FailureKind = LiveScoringFailureKind.None);

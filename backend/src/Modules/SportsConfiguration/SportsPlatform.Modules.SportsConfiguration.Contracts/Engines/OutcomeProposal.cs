namespace SportsPlatform.Modules.SportsConfiguration.Contracts.Engines;

/// <summary>
/// What the engine proposes from the score as it stands (sport-engine.md §5, "Outcome"); Results approves it. The
/// winner and the method are set only when <see cref="Kind"/> is <see cref="OutcomeKind.Winner"/>; the next period only
/// when the tie resolution needs one.
/// </summary>
public sealed record OutcomeProposal(OutcomeKind Kind)
{
    public static OutcomeProposal Draw { get; } = new(OutcomeKind.Draw);

    public Guid? WinnerParticipantId { get; init; }

    public OutcomeMethod? Method { get; init; }

    /// <summary>
    /// For <see cref="OutcomeKind.RequiresExtraTime"/> and <see cref="OutcomeKind.RequiresShootout"/>: the period of the
    /// rules' <c>contest.extra_period_sequence</c> to start next; null while the period it needs is under way (the
    /// shoot-out, or the sequence's last extra period).
    /// </summary>
    public string? NextPeriodCode { get; init; }

    /// <summary>The winner scored in a <c>sudden_death</c> period: the contest ends now (sport-engine.md's <c>REQUIRES_END</c>).</summary>
    public bool RequiresEnd { get; init; }

    public static OutcomeProposal Win(Guid participantId, OutcomeMethod method) =>
        new(OutcomeKind.Winner) { WinnerParticipantId = participantId, Method = method };

    public static OutcomeProposal Requires(OutcomeKind kind, string? nextPeriodCode) =>
        kind is OutcomeKind.RequiresExtraTime or OutcomeKind.RequiresShootout
            ? new(kind) { NextPeriodCode = nextPeriodCode }
            : throw new ArgumentOutOfRangeException(nameof(kind), kind, "Only a tie resolution's step requires a period.");
}

/// <summary>
/// A winner, a draw, or a level score whose tie resolution needs more play: extra periods, or a shoot-out (Contest
/// Operations then offers the next period instead of ending the contest).
/// </summary>
public enum OutcomeKind
{
    Winner,
    Draw,
    RequiresExtraTime,
    RequiresShootout,
}

/// <summary>
/// How the winner was decided (docs/domain/state-machines.md, result methods); the methods Results sets (walkover,
/// forfeit, awarded) are not the engine's.
/// </summary>
public enum OutcomeMethod
{
    Regulation,
    ExtraTime,
    Overtime,
    SuddenDeath,
    Shootout,
}

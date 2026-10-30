namespace SportsPlatform.Modules.SportsConfiguration.Contracts.Engines;

/// <summary>
/// What the engine proposes from the score (sport-engine.md §5, "Outcome"); Results approves it. The winner is set only
/// when <see cref="Kind"/> is <see cref="OutcomeKind.Winner"/>.
/// </summary>
public sealed record OutcomeProposal(OutcomeKind Kind)
{
    public static OutcomeProposal Draw { get; } = new(OutcomeKind.Draw);

    public Guid? WinnerParticipantId { get; init; }

    public OutcomeMethod? Method { get; init; }

    public static OutcomeProposal Win(Guid participantId, OutcomeMethod method) =>
        new(OutcomeKind.Winner) { WinnerParticipantId = participantId, Method = method };
}

public enum OutcomeKind
{
    Winner,
    Draw,
}

/// <summary>How the winner was decided; methods set by Results (walkover, forfeit) are not the engine's.</summary>
public enum OutcomeMethod
{
    Regulation,
}

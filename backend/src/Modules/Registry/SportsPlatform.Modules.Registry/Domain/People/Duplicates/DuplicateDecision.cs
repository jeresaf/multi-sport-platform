using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Domain;

namespace SportsPlatform.Modules.Registry.Domain.People.Duplicates;

/// <summary>What was decided about two people the duplicate check paired. Merges (S25) add their own.</summary>
public enum DuplicateDecisionKind
{
    NotSame,
}

/// <summary>
/// A remembered decision about two people of one tenant (<c>registry.duplicate_decisions</c>, tenant-owned, S06 D7):
/// "not the same person", with why, who and when. The pair is stored in one order (<see cref="PersonLowId"/> before
/// <see cref="PersonHighId"/> as PostgreSQL sorts UUIDs) and is unique, so the check finds it whichever person is the
/// candidate. Written by a create or an update that proceeds past candidates (S06-T2b, T3); undoing one comes with
/// merges (S25). The reason is free text kept permanently: never personal data.
/// </summary>
public sealed class DuplicateDecision : AggregateRoot, ITenantOwned
{
    public const int ReasonMaxLength = 500;

    private DuplicateDecision()
    {
    }

    private DuplicateDecision(Guid low, Guid high, string reason, ICurrentActor actor, DateTimeOffset decidedAt)
        : base(NewId())
    {
        PersonLowId = low;
        PersonHighId = high;
        Decision = DuplicateDecisionKind.NotSame;
        Reason = reason;
        ActorKind = actor.Kind;
        ActorId = actor.ActorId;
        DecidedAt = decidedAt;
    }

    /// <summary>Stamped from the tenant context when saved.</summary>
    public Guid TenantId { get; private set; }

    public Guid PersonLowId { get; private set; }

    public Guid PersonHighId { get; private set; }

    public DuplicateDecisionKind Decision { get; private set; }

    public string Reason { get; private set; } = string.Empty;

    public ActorKind ActorKind { get; private set; }

    public Guid ActorId { get; private set; }

    public DateTimeOffset DecidedAt { get; private set; }

    /// <summary>The two people are not the same person, in either order.</summary>
    public static DuplicateDecision NotSame(
        Guid personId, Guid otherPersonId, string reason, ICurrentActor actor, DateTimeOffset decidedAt)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        if (personId == Guid.Empty || otherPersonId == Guid.Empty || personId == otherPersonId)
        {
            throw new ArgumentException("A decision is about two different people.", nameof(otherPersonId));
        }

        var trimmed = reason.Trim();
        ArgumentOutOfRangeException.ThrowIfGreaterThan(trimmed.Length, ReasonMaxLength, nameof(reason));
        var (low, high) = Ordered(personId, otherPersonId);
        return new DuplicateDecision(low, high, trimmed, actor, decidedAt);
    }

    /// <summary>
    /// The pair in its stored order. PostgreSQL compares UUIDs byte by byte, which is the order of their hexadecimal
    /// text; <see cref="Guid.CompareTo(Guid)"/> is not relied on to agree.
    /// </summary>
    public static (Guid Low, Guid High) Ordered(Guid personId, Guid otherPersonId) =>
        string.CompareOrdinal(personId.ToString("N"), otherPersonId.ToString("N")) < 0
            ? (personId, otherPersonId)
            : (otherPersonId, personId);

    /// <summary>The other person of the pair.</summary>
    public Guid OtherThan(Guid personId) => personId == PersonLowId ? PersonHighId : PersonLowId;
}

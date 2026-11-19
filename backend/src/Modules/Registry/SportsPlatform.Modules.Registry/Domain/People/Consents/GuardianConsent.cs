using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Domain;

namespace SportsPlatform.Modules.Registry.Domain.People.Consents;

/// <summary>
/// A guardian's consent for a person who isn't known to be an adult (<c>registry.guardian_consents</c>, tenant-owned,
/// S06 D14, ADR-0015): who gave it, for which scopes, on which date, with an optional evidence file in Platform's media.
/// A record is never changed but to attach its evidence or to close it: withdrawing scopes closes it
/// (<c>ACTIVE → WITHDRAWN</c>) and, when scopes remain, <see cref="Person.WithdrawConsent"/> records a successor with
/// them that names this one, so the history stays.
/// </summary>
public sealed class GuardianConsent : Entity, ITenantOwned
{
    public const int GuardianNameMaxLength = 200;
    public const int ContactMaxLength = 200;
    public const int ReasonMaxLength = 500;

    private readonly List<ConsentScope> _scopes = [];

    internal GuardianConsent(
        Guid personId, GuardianConsentDetails details, Guid? evidenceAssetId, Guid? replacesConsentId, ICurrentActor actor, DateTimeOffset now)
        : base(NewId())
    {
        ArgumentNullException.ThrowIfNull(details);
        ArgumentNullException.ThrowIfNull(actor);
        if (personId == Guid.Empty)
        {
            throw new ArgumentException("A consent belongs to a person.", nameof(personId));
        }

        var scopes = details.Scopes.Distinct().Order().ToList();
        if (scopes.Count == 0 || scopes.Count != details.Scopes.Count)
        {
            throw new ArgumentException("A consent needs one or more distinct scopes.", nameof(details));
        }

        PersonId = personId;
        GuardianName = Person.Required(details.GuardianName, GuardianNameMaxLength, nameof(details));
        Relationship = details.Relationship;
        Contact = Person.Required(details.Contact, ContactMaxLength, nameof(details));
        _scopes.AddRange(scopes);
        GivenOn = details.GivenOn;
        EvidenceAssetId = evidenceAssetId;
        ReplacesConsentId = replacesConsentId;
        Status = ConsentStatus.Active;
        RecordedBy = actor.ActorId;
        RecordedAt = now;
    }

    private GuardianConsent()
    {
    }

    /// <summary>Stamped from the tenant context when saved.</summary>
    public Guid TenantId { get; private set; }

    public Guid PersonId { get; private set; }

    public string GuardianName { get; private set; } = string.Empty;

    public GuardianRelationship Relationship { get; private set; }

    /// <summary>A phone number or an email address, as given.</summary>
    public string Contact { get; private set; } = string.Empty;

    /// <summary>The scopes, in their declared order.</summary>
    public IReadOnlyList<ConsentScope> Scopes => _scopes;

    public DateOnly GivenOn { get; private set; }

    /// <summary>The evidence's media asset in Platform (no foreign key: another module's table); a successor keeps it.</summary>
    public Guid? EvidenceAssetId { get; private set; }

    /// <summary>The record a partial withdrawal closed to record this one.</summary>
    public Guid? ReplacesConsentId { get; private set; }

    public ConsentStatus Status { get; private set; }

    public Guid RecordedBy { get; private set; }

    public DateTimeOffset RecordedAt { get; private set; }

    public Guid? WithdrawnBy { get; private set; }

    public DateTimeOffset? WithdrawnAt { get; private set; }

    /// <summary>Free text kept permanently: never personal data.</summary>
    public string? WithdrawalReason { get; private set; }

    public bool IsActive => Status == ConsentStatus.Active;

    /// <summary>What a successor of this record carries over: the guardian and the date, with <paramref name="scopes"/>.</summary>
    internal GuardianConsentDetails DetailsWith(IReadOnlyCollection<ConsentScope> scopes) =>
        new(GuardianName, Relationship, Contact, scopes, GivenOn);

    /// <summary>Points the record at a new evidence asset and returns the one it replaces.</summary>
    internal Guid? AttachEvidence(Guid assetId)
    {
        if (!IsActive)
        {
            throw new InvalidOperationException("A withdrawn consent takes no evidence.");
        }

        var previous = EvidenceAssetId;
        EvidenceAssetId = assetId;
        return previous;
    }

    /// <summary>Closes the record; the person has checked it is active.</summary>
    internal void Withdraw(string reason, ICurrentActor actor, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(actor);
        if (!IsActive)
        {
            throw new InvalidOperationException("The consent is already withdrawn.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        var trimmed = reason.Trim();
        ArgumentOutOfRangeException.ThrowIfGreaterThan(trimmed.Length, ReasonMaxLength, nameof(reason));
        Status = ConsentStatus.Withdrawn;
        WithdrawalReason = trimmed;
        WithdrawnBy = actor.ActorId;
        WithdrawnAt = now;
    }
}

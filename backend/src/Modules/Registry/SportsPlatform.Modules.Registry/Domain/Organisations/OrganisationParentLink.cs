using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Domain;

namespace SportsPlatform.Modules.Registry.Domain.Organisations;

/// <summary>
/// One link from an organisation to its parent (<c>registry.organisation_parent_links</c>, tenant-owned, S07 D5): open
/// while <see cref="ValidTo"/> is null, from <see cref="ValidFrom"/> to <see cref="ValidTo"/> on the tenant's calendar
/// once ended. A link opened and ended on the same day keeps both dates (nothing is deleted). The reason says why the
/// parent was set: free text kept permanently, never personal data. A child of the organisation it belongs to: a new or
/// ended link moves that organisation's version, and a new one also moves its parent's when the parent is loaded
/// (S06 D11; owner, S07-T2c plan).
/// </summary>
public sealed class OrganisationParentLink : Entity, ITenantOwned
{
    public const int ReasonMaxLength = 500;

    /// <summary>An open link from <paramref name="organisationId"/> to <paramref name="parentId"/> from <paramref name="validFrom"/>.</summary>
    public OrganisationParentLink(
        Guid organisationId, Guid parentId, DateOnly validFrom, string reason, ICurrentActor actor, DateTimeOffset setAt)
        : base(NewId())
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(reason.Length, ReasonMaxLength, nameof(reason));
        if (organisationId == Guid.Empty || parentId == Guid.Empty)
        {
            throw new ArgumentException("A parent link joins two organisations.", nameof(parentId));
        }

        if (organisationId == parentId)
        {
            throw new ArgumentException("An organisation can't be its own parent.", nameof(parentId));
        }

        OrganisationId = organisationId;
        ParentId = parentId;
        ValidFrom = validFrom;
        Reason = reason;
        ActorKind = actor.Kind;
        ActorId = actor.ActorId;
        SetAt = setAt;
    }

    private OrganisationParentLink()
    {
    }

    /// <summary>Stamped from the tenant context when saved.</summary>
    public Guid TenantId { get; private set; }

    /// <summary>The child: the organisation whose parent this is.</summary>
    public Guid OrganisationId { get; private set; }

    public Guid ParentId { get; private set; }

    /// <summary>The tenant's date the link was opened.</summary>
    public DateOnly ValidFrom { get; private set; }

    /// <summary>The tenant's date the link ended; null while it is the organisation's current parent.</summary>
    public DateOnly? ValidTo { get; private set; }

    public string Reason { get; private set; } = string.Empty;

    /// <summary>Who set the parent.</summary>
    public ActorKind ActorKind { get; private set; }

    public Guid ActorId { get; private set; }

    /// <summary>When the parent was set.</summary>
    public DateTimeOffset SetAt { get; private set; }

    public bool IsOpen => ValidTo is null;

    /// <summary>Ends the open link on <paramref name="today"/> (the tenant's date; never before it opened).</summary>
    internal void End(DateOnly today)
    {
        if (!IsOpen)
        {
            throw new InvalidOperationException("The parent link has already ended.");
        }

        ValidTo = today < ValidFrom ? ValidFrom : today;
    }
}

using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Domain;
using SportsPlatform.Modules.Registry.Domain.People;

namespace SportsPlatform.Modules.Registry.Domain.Organisations;

/// <summary>
/// Another name an organisation is known by (<c>registry.organisation_aliases</c>, tenant-owned, S07 D10), with its own
/// name key: the directory search finds the organisation under it, and the duplicate warning compares it with the other
/// organisations' names and aliases (D13, owner, S07-T2d plan). Removing one stamps who, when and why, so the history
/// stays (<see cref="Organisation.RemoveAlias"/>).
/// </summary>
public sealed class OrganisationAlias : Entity, ITenantOwned
{
    /// <summary>As long as an organisation's display name.</summary>
    public const int NameMaxLength = OrganisationDetails.DisplayNameMaxLength;
    public const int ReasonMaxLength = 500;

    /// <summary>An alias of an organisation; commands add one through <see cref="Organisation.AddAlias"/>.</summary>
    public OrganisationAlias(Guid organisationId, string name, AliasKind kind)
        : base(NewId())
    {
        if (organisationId == Guid.Empty)
        {
            throw new ArgumentException("An alias belongs to an organisation.", nameof(organisationId));
        }

        Name = Person.Required(name, NameMaxLength, nameof(name));
        NameKey = Organisation.NameKeyOf(Name);
        if (NameKey.Length == 0)
        {
            throw new ArgumentException("An alias needs a letter or a digit.", nameof(name));
        }

        OrganisationId = organisationId;
        Kind = kind;
    }

    private OrganisationAlias()
    {
    }

    /// <summary>Stamped from the tenant context when saved.</summary>
    public Guid TenantId { get; private set; }

    public Guid OrganisationId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public AliasKind Kind { get; private set; }

    /// <summary>The sorted normalised tokens of <see cref="Name"/> (S06 D4).</summary>
    public string NameKey { get; private set; } = string.Empty;

    public DateTimeOffset? RemovedAt { get; private set; }

    public Guid? RemovedBy { get; private set; }

    /// <summary>Free text kept permanently: never personal data.</summary>
    public string? RemoveReason { get; private set; }

    public bool IsRemoved => RemovedAt is not null;

    /// <summary>Stamps the removal; the organisation has checked the alias isn't removed already.</summary>
    internal void Remove(string reason, ICurrentActor actor, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(actor);
        if (IsRemoved)
        {
            throw new InvalidOperationException("The alias is already removed.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        var trimmed = reason.Trim();
        ArgumentOutOfRangeException.ThrowIfGreaterThan(trimmed.Length, ReasonMaxLength, nameof(reason));
        RemoveReason = trimmed;
        RemovedBy = actor.ActorId;
        RemovedAt = now;
    }
}

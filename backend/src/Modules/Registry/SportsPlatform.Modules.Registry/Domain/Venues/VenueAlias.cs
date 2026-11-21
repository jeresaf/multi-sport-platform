using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Domain;
using SportsPlatform.Modules.Registry.Domain.People;

namespace SportsPlatform.Modules.Registry.Domain.Venues;

/// <summary>
/// Another name a venue is known by (<c>registry.venue_aliases</c>, tenant-owned, S07 D10), with its own name key: the
/// directory search finds the venue under it (the duplicate warning compares the venue's own name only; owner, S07-T1c
/// plan). Removing one stamps who, when and why, so the history stays (<see cref="Venue.RemoveAlias"/>).
/// </summary>
public sealed class VenueAlias : Entity, ITenantOwned
{
    /// <summary>As long as a venue's own name.</summary>
    public const int NameMaxLength = VenueDetails.NameMaxLength;
    public const int ReasonMaxLength = 500;

    /// <summary>An alias of a venue; commands add one through <see cref="Venue.AddAlias"/>.</summary>
    public VenueAlias(Guid venueId, string name, AliasKind kind)
        : base(NewId())
    {
        if (venueId == Guid.Empty)
        {
            throw new ArgumentException("An alias belongs to a venue.", nameof(venueId));
        }

        Name = Person.Required(name, NameMaxLength, nameof(name));
        NameKey = Venue.NameKeyOf(Name);
        if (NameKey.Length == 0)
        {
            throw new ArgumentException("An alias needs a letter or a digit.", nameof(name));
        }

        VenueId = venueId;
        Kind = kind;
    }

    private VenueAlias()
    {
    }

    /// <summary>Stamped from the tenant context when saved.</summary>
    public Guid TenantId { get; private set; }

    public Guid VenueId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public AliasKind Kind { get; private set; }

    /// <summary>The sorted normalised tokens of <see cref="Name"/> (S06 D4).</summary>
    public string NameKey { get; private set; } = string.Empty;

    public DateTimeOffset? RemovedAt { get; private set; }

    public Guid? RemovedBy { get; private set; }

    /// <summary>Free text kept permanently: never personal data.</summary>
    public string? RemoveReason { get; private set; }

    public bool IsRemoved => RemovedAt is not null;

    /// <summary>Stamps the removal; the venue has checked the alias isn't removed already.</summary>
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

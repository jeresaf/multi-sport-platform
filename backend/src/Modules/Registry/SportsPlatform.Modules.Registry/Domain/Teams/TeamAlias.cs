using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Domain;
using SportsPlatform.Modules.Registry.Domain.People;

namespace SportsPlatform.Modules.Registry.Domain.Teams;

/// <summary>
/// Another name a team is known by (<c>registry.team_aliases</c>, tenant-owned, S07 D8, D10), with its own name key: the
/// directory search finds the team under it. A <see cref="AliasKind.FormerName"/> comes only from a rename and keeps the
/// name's dates and the short name and code that went with it (owner, S07-T3c plan), so the name on a date can answer
/// all three (D18); it is never removed. A nickname or a spelling is added and removed by the user; removing one stamps
/// who, when and why, so the history stays (<see cref="Team.RemoveAlias"/>).
/// </summary>
public sealed class TeamAlias : Entity, ITenantOwned
{
    /// <summary>As long as a team's name.</summary>
    public const int NameMaxLength = TeamDetails.NameMaxLength;
    public const int ReasonMaxLength = 500;

    private TeamAlias(Guid teamId, string name, AliasKind kind)
        : base(NewId())
    {
        if (teamId == Guid.Empty)
        {
            throw new ArgumentException("An alias belongs to a team.", nameof(teamId));
        }

        Name = Person.Required(name, NameMaxLength, nameof(name));
        NameKey = Team.NameKeyOf(Name);
        if (NameKey.Length == 0)
        {
            throw new ArgumentException("An alias needs a letter or a digit.", nameof(name));
        }

        TeamId = teamId;
        Kind = kind;
    }

    private TeamAlias()
    {
    }

    /// <summary>Stamped from the tenant context when saved.</summary>
    public Guid TenantId { get; private set; }

    public Guid TeamId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public AliasKind Kind { get; private set; }

    /// <summary>The sorted normalised tokens of <see cref="Name"/> (S06 D4).</summary>
    public string NameKey { get; private set; } = string.Empty;

    /// <summary>A former name's short name, as it was with that name; null on other aliases.</summary>
    public string? ShortName { get; private set; }

    /// <summary>A former name's code, as it was with that name; null on other aliases.</summary>
    public string? Code { get; private set; }

    /// <summary>The first day of a former name (the team's <c>name_since</c> then); null on other aliases.</summary>
    public DateOnly? ValidFrom { get; private set; }

    /// <summary>The last day of a former name (the day before the rename's <c>effectiveOn</c>); null on other aliases.</summary>
    public DateOnly? ValidTo { get; private set; }

    public DateTimeOffset? RemovedAt { get; private set; }

    public Guid? RemovedBy { get; private set; }

    /// <summary>Free text kept permanently: never personal data.</summary>
    public string? RemoveReason { get; private set; }

    public bool IsRemoved => RemovedAt is not null;

    public bool IsFormerName => Kind == AliasKind.FormerName;

    /// <summary>Whether this former name was the team's name on <paramref name="date"/>.</summary>
    public bool Covers(DateOnly date) => IsFormerName && ValidFrom <= date && date <= ValidTo;

    /// <summary>A former name, written by <see cref="Team.Rename"/>: the name, short name and code it had from <paramref name="from"/> to <paramref name="to"/>.</summary>
    public static TeamAlias FormerName(Guid teamId, string name, string? shortName, string? code, DateOnly from, DateOnly to)
    {
        if (to < from)
        {
            throw new ArgumentOutOfRangeException(nameof(to), "A former name ends on or after its first day.");
        }

        // The name exactly as the team had it (Name keeps it as saved, not re-collapsed).
        return new TeamAlias(teamId, name, AliasKind.FormerName)
        {
            Name = name,
            ShortName = shortName,
            Code = code,
            ValidFrom = from,
            ValidTo = to,
        };
    }

    /// <summary>A nickname or a spelling of a team; commands add one through <see cref="Team.AddAlias"/>.</summary>
    public static TeamAlias Other(Guid teamId, string name, AliasKind kind)
    {
        if (kind == AliasKind.FormerName)
        {
            throw new ArgumentException("A former name comes only from a rename.", nameof(kind));
        }

        return new TeamAlias(teamId, name, kind);
    }

    /// <summary>Stamps the removal; the team has checked the alias is a nickname or spelling that isn't removed already.</summary>
    internal void Remove(string reason, ICurrentActor actor, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(actor);
        if (IsRemoved || IsFormerName)
        {
            throw new InvalidOperationException("Only a current nickname or spelling can be removed.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        var trimmed = reason.Trim();
        ArgumentOutOfRangeException.ThrowIfGreaterThan(trimmed.Length, ReasonMaxLength, nameof(reason));
        RemoveReason = trimmed;
        RemovedBy = actor.ActorId;
        RemovedAt = now;
    }
}

/// <summary>A team's name, short name and code on a date (S07 D8, D18).</summary>
public sealed record TeamNameOnDate(string Name, string? ShortName, string? Code);

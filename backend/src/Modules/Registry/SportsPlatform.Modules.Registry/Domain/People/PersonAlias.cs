using SportsPlatform.BuildingBlocks.Domain;

namespace SportsPlatform.Modules.Registry.Domain.People;

/// <summary>
/// Another name a person is known by (<c>registry.person_aliases</c>, tenant-owned, S06 D10), with its own name key.
/// Removing one stamps who, when and why, so the history stays; the commands come with S06-T3.
/// </summary>
public sealed class PersonAlias : Entity, ITenantOwned
{
    public const int NameMaxLength = 200;
    public const int ReasonMaxLength = 500;

    /// <summary>An alias of a person; commands add one through <see cref="Person.AddAlias"/>.</summary>
    public PersonAlias(Guid personId, string name, AliasKind kind)
        : base(NewId())
    {
        if (personId == Guid.Empty)
        {
            throw new ArgumentException("An alias belongs to a person.", nameof(personId));
        }

        PersonId = personId;
        Name = Person.Required(name, NameMaxLength, nameof(name));
        NameKey = NameNormaliser.NameKey(Name);
        if (NameKey.Length == 0)
        {
            throw new ArgumentException("An alias needs a letter or a digit.", nameof(name));
        }

        Kind = kind;
    }

    private PersonAlias()
    {
    }

    /// <summary>Stamped from the tenant context when saved.</summary>
    public Guid TenantId { get; private set; }

    public Guid PersonId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public AliasKind Kind { get; private set; }

    /// <summary>The sorted normalised tokens of <see cref="Name"/> (S06 D4).</summary>
    public string NameKey { get; private set; } = string.Empty;

    public DateTimeOffset? RemovedAt { get; private set; }

    public Guid? RemovedBy { get; private set; }

    /// <summary>Free text kept permanently: never personal data.</summary>
    public string? RemoveReason { get; private set; }

    public bool IsRemoved => RemovedAt is not null;
}

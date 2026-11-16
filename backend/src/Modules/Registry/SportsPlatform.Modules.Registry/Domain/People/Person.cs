using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Domain;

namespace SportsPlatform.Modules.Registry.Domain.People;

/// <summary>
/// One real person in a tenant's registry (<c>registry.persons</c>, tenant-owned, S06 D1, D2): the canonical record that
/// role profiles, memberships and registrations point at. Its ID never changes (AR-23). Names are kept as entered and as
/// normalised keys (<see cref="NameNormaliser"/>) for search and the duplicate check. Whether the person is a minor is
/// computed from the date of birth on every read, never stored (ADR-0015). Aliases and the status history are its
/// children; a change to one changes the person's version (S06 D11).
/// </summary>
public sealed class Person : AggregateRoot, ITenantOwned
{
    private readonly List<PersonAlias> _aliases = [];
    private readonly List<PersonStatusChange> _statusHistory = [];

    private Person()
    {
    }

    private Person(PersonSource source)
        : base(NewId())
    {
        Source = source;
        Status = PersonStatus.Active;
    }

    /// <summary>Stamped from the tenant context when saved.</summary>
    public Guid TenantId { get; private set; }

    public string FirstName { get; private set; } = string.Empty;

    public string? MiddleName { get; private set; }

    public string LastName { get; private set; } = string.Empty;

    /// <summary>Free text, e.g. a clan name.</summary>
    public string? OtherNames { get; private set; }

    public string DisplayName { get; private set; } = string.Empty;

    /// <summary>The normalised first name: the directory's second sort key.</summary>
    public string FirstNameKey { get; private set; } = string.Empty;

    /// <summary>The normalised last name: the directory's first sort key.</summary>
    public string LastNameKey { get; private set; } = string.Empty;

    /// <summary>The sorted normalised tokens of first, middle, last and other names (S06 D4).</summary>
    public string NameKey { get; private set; } = string.Empty;

    public Gender? Gender { get; private set; }

    public DateOnly? DateOfBirth { get; private set; }

    public string? Nationality { get; private set; }

    public string? SecondaryNationality { get; private set; }

    public int? HeightCm { get; private set; }

    public decimal? WeightKg { get; private set; }

    /// <summary>The photo's media asset in Platform (no foreign key: another module's table). Set with photos (S06-T5).</summary>
    public Guid? PhotoAssetId { get; private set; }

    /// <summary>Off for everyone until turned on (S06 D15).</summary>
    public bool PublicProfileEnabled { get; private set; }

    public PersonStatus Status { get; private set; }

    public PersonSource Source { get; private set; }

    /// <summary>The import that created the record, when <see cref="Source"/> is an import (AR-22).</summary>
    public Guid? ImportJobId { get; private set; }

    /// <summary>Every alias, removed ones included.</summary>
    public IReadOnlyList<PersonAlias> Aliases => _aliases;

    public IReadOnlyList<PersonStatusChange> StatusHistory => _statusHistory;

    /// <summary>
    /// A new <c>ACTIVE</c> person in the scope's tenant (stamped on save), with the first row of its status history.
    /// The use case has validated <paramref name="biodata"/> and run the duplicate check; a value out of bounds here is
    /// a bug.
    /// </summary>
    public static Person Create(PersonBiodata biodata, PersonSource source, ICurrentActor actor, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(biodata);
        ArgumentNullException.ThrowIfNull(actor);

        var person = new Person(source);
        person.Apply(biodata);
        person._statusHistory.Add(new PersonStatusChange(person.Id, null, person.Status, null, actor, now));
        return person;
    }

    /// <summary>Adds an alias with its own name key, so the person is found and matched under that name too (S06 D10).</summary>
    public PersonAlias AddAlias(string name, AliasKind kind)
    {
        var alias = new PersonAlias(Id, name, kind);
        _aliases.Add(alias);
        return alias;
    }

    private void Apply(PersonBiodata biodata)
    {
        FirstName = Required(biodata.FirstName, PersonBiodata.NameMaxLength, nameof(biodata.FirstName));
        LastName = Required(biodata.LastName, PersonBiodata.NameMaxLength, nameof(biodata.LastName));
        MiddleName = Optional(biodata.MiddleName, PersonBiodata.NameMaxLength, nameof(biodata.MiddleName));
        OtherNames = Optional(biodata.OtherNames, PersonBiodata.OtherNamesMaxLength, nameof(biodata.OtherNames));
        DisplayName = Optional(biodata.DisplayName, PersonBiodata.DisplayNameMaxLength, nameof(biodata.DisplayName))
            ?? $"{FirstName} {LastName}";

        FirstNameKey = NameNormaliser.Normalise(FirstName);
        LastNameKey = NameNormaliser.Normalise(LastName);
        NameKey = NameNormaliser.NameKey(FirstName, MiddleName, LastName, OtherNames);
        if (FirstNameKey.Length == 0 || LastNameKey.Length == 0)
        {
            throw new ArgumentException("A first and a last name need a letter or a digit each.", nameof(biodata));
        }

        Gender = biodata.Gender;
        DateOfBirth = biodata.DateOfBirth;
        Nationality = CountryCode(biodata.Nationality, nameof(biodata.Nationality));
        SecondaryNationality = CountryCode(biodata.SecondaryNationality, nameof(biodata.SecondaryNationality));

        if (biodata.HeightCm is { } height)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(height, PersonBiodata.MinHeightCm, nameof(biodata.HeightCm));
            ArgumentOutOfRangeException.ThrowIfGreaterThan(height, PersonBiodata.MaxHeightCm, nameof(biodata.HeightCm));
        }

        if (biodata.WeightKg is { } weight)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(weight, PersonBiodata.MinWeightKg, nameof(biodata.WeightKg));
            ArgumentOutOfRangeException.ThrowIfGreaterThan(weight, PersonBiodata.MaxWeightKg, nameof(biodata.WeightKg));
        }

        HeightCm = biodata.HeightCm;
        WeightKg = biodata.WeightKg;
    }

    internal static string Required(string? value, int maxLength, string name) =>
        Optional(value, maxLength, name) ?? throw new ArgumentException("A value is required.", name);

    // Trimmed, inner whitespace collapsed; null for blank.
    internal static string? Optional(string? value, int maxLength, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var text = string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(text.Length, maxLength, name);
        return text;
    }

    private static string? CountryCode(string? value, string name)
    {
        if (value is null)
        {
            return null;
        }

        if (value.Length != 2 || !char.IsAsciiLetterUpper(value[0]) || !char.IsAsciiLetterUpper(value[1]))
        {
            throw new ArgumentException("A nationality is an ISO 3166-1 alpha-2 code.", name);
        }

        return value;
    }
}

using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Domain;
using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.Modules.Registry.Contracts.People;
using SportsPlatform.Modules.Registry.Domain.People.Consents;
using SportsPlatform.Modules.Registry.Domain.People.Profiles;

namespace SportsPlatform.Modules.Registry.Domain.People;

/// <summary>
/// One real person in a tenant's registry (<c>registry.persons</c>, tenant-owned, S06 D1, D2): the canonical record that
/// role profiles, memberships and registrations point at. Its ID never changes (AR-23). Names are kept as entered and as
/// normalised keys (<see cref="NameNormaliser"/>) for search and the duplicate check. Whether the person is a minor is
/// computed from the date of birth on every read, never stored (ADR-0015). Aliases, role profiles and the status
/// histories are its children; a change to one changes the person's version (S06 D11; owner, S06-T4 plan).
/// </summary>
public sealed class Person : AggregateRoot, ITenantOwned
{
    private readonly List<PersonAlias> _aliases = [];
    private readonly List<PersonStatusChange> _statusHistory = [];
    private readonly List<AthleteProfile> _athleteProfiles = [];
    private readonly List<StaffProfile> _staffProfiles = [];
    private readonly List<OfficialProfile> _officialProfiles = [];
    private readonly List<ProfileStatusChange> _profileStatusHistory = [];
    private readonly List<GuardianConsent> _consents = [];

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

    /// <summary>Every athlete profile, archived ones included; load them before a profile command.</summary>
    public IReadOnlyList<AthleteProfile> AthleteProfiles => _athleteProfiles;

    /// <summary>Every staff profile, archived ones included; load them (with their capabilities) before a profile command.</summary>
    public IReadOnlyList<StaffProfile> StaffProfiles => _staffProfiles;

    /// <summary>Every official profile, archived ones included; load them (with their capabilities) before a profile command.</summary>
    public IReadOnlyList<OfficialProfile> OfficialProfiles => _officialProfiles;

    /// <summary>The status history of every profile; commands only add to it, so it needn't be loaded.</summary>
    public IReadOnlyList<ProfileStatusChange> ProfileStatusHistory => _profileStatusHistory;

    /// <summary>Every guardian consent, withdrawn ones included; load them before a consent command.</summary>
    public IReadOnlyList<GuardianConsent> Consents => _consents;

    /// <summary>The scopes of the active consents together (S06 D14): what the public read checks (D15, D18).</summary>
    public IReadOnlySet<ConsentScope> ActiveConsentScopes =>
        _consents.Where(c => c.IsActive).SelectMany(c => c.Scopes).ToHashSet();

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

    /// <summary>
    /// Replaces the biodata (S06 D8). The use case has validated it and, when the names, date of birth or nationalities
    /// changed (<see cref="ChangesIdentity"/>), run the duplicate check. The status, source and profile flag stay.
    /// </summary>
    public void Update(PersonBiodata biodata)
    {
        ArgumentNullException.ThrowIfNull(biodata);
        Apply(biodata);
    }

    /// <summary>
    /// Whether <paramref name="biodata"/> changes what the duplicate check compares (S06 D5, D8): the name key, the
    /// normalised last name, the date of birth or a nationality. A change of case, accents or punctuation in a name
    /// leaves the keys as they are and needs no check; the display name, gender, height and weight never do.
    /// </summary>
    public bool ChangesIdentity(PersonBiodata biodata)
    {
        ArgumentNullException.ThrowIfNull(biodata);
        return NameNormaliser.NameKey(biodata.FirstName, biodata.MiddleName, biodata.LastName, biodata.OtherNames) != NameKey
            || NameNormaliser.Normalise(biodata.LastName) != LastNameKey
            || biodata.DateOfBirth != DateOfBirth
            || biodata.Nationality != Nationality
            || biodata.SecondaryNationality != SecondaryNationality;
    }

    /// <summary>
    /// Moves the person along the lifecycle (S06 D9; docs/domain/state-machines.md, Registry) and writes a history row.
    /// A transition the current status doesn't allow is <c>PERSON_STATUS_INVALID</c>. <paramref name="reason"/> is
    /// required where <see cref="PersonTransitions.NeedsReason"/> says so and optional otherwise (the endpoint has
    /// validated it; a blank one is none).
    /// </summary>
    public Result ChangeStatus(PersonTransition transition, string? reason, ICurrentActor actor, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(actor);
        var trimmed = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        if (PersonTransitions.NeedsReason(transition) && trimmed is null)
        {
            throw new ArgumentException("This transition needs a reason.", nameof(reason));
        }

        if (PersonTransitions.Target(transition, Status) is not { } to)
        {
            return Result.Failure(RegistryErrors.StatusInvalid);
        }

        var from = Status;
        Status = to;
        _statusHistory.Add(new PersonStatusChange(Id, from, to, trimmed, actor, now));
        return Result.Success();
    }

    /// <summary>
    /// Whether the person takes a new profile, alias, identifier, consent or photo (S06 D9): not when <c>ARCHIVED</c>
    /// (<c>PERSON_ARCHIVED</c>) or <c>DECEASED</c> (<c>PERSON_DECEASED</c>). Corrections (biodata, removing an alias,
    /// ending an identifier) stay allowed in every status (owner, Q45).
    /// </summary>
    public Result EnsureAcceptsNewChildren() => Status switch
    {
        PersonStatus.Archived => Result.Failure(RegistryErrors.PersonArchived),
        PersonStatus.Deceased => Result.Failure(RegistryErrors.PersonDeceased),
        _ => Result.Success(),
    };

    /// <summary>
    /// Points the person at a newly stored photo asset (S06 D19) and returns the one it replaces, which the use case marks
    /// <c>REMOVED</c> after its save. Refused on an archived or deceased person (S06 D9).
    /// </summary>
    public Result<Guid?> SetPhoto(Guid assetId)
    {
        var accepts = EnsureAcceptsNewChildren();
        if (accepts.IsFailure)
        {
            return accepts.Error;
        }

        var previous = PhotoAssetId;
        PhotoAssetId = assetId;
        return Result.Success(previous);
    }

    /// <summary>
    /// Clears the photo in any status (a correction, as removing an alias) and returns the asset it named:
    /// <c>PERSON_PHOTO_NOT_FOUND</c> when there is none.
    /// </summary>
    public Result<Guid> RemovePhoto()
    {
        if (PhotoAssetId is not { } assetId)
        {
            return RegistryErrors.PhotoNotFound;
        }

        PhotoAssetId = null;
        return Result.Success(assetId);
    }

    /// <summary>
    /// Adds an alias with its own name key, so the person is found and matched under that name too (S06 D10). Refused
    /// on an archived or deceased person, and when the alias's key is already the person's own or a current alias's
    /// (<c>PERSON_ALIAS_EXISTS</c>, owner, S06-T3b plan): such an alias would find nothing new. The use case has checked
    /// that the name has a letter or a digit.
    /// </summary>
    public Result<PersonAlias> AddAlias(string name, AliasKind kind)
    {
        var accepts = EnsureAcceptsNewChildren();
        if (accepts.IsFailure)
        {
            return accepts.Error;
        }

        var alias = new PersonAlias(Id, name, kind);
        if (alias.NameKey == NameKey || _aliases.Any(existing => !existing.IsRemoved && existing.NameKey == alias.NameKey))
        {
            return RegistryErrors.AliasExists;
        }

        _aliases.Add(alias);
        return Result.Success(alias);
    }

    /// <summary>
    /// Removes an alias, keeping it with who, when and why (S06 D10); allowed in every status (Q45). An alias of
    /// another person is <c>PERSON_ALIAS_NOT_FOUND</c>, one already removed <c>PERSON_ALIAS_REMOVED</c>.
    /// </summary>
    public Result RemoveAlias(Guid aliasId, string reason, ICurrentActor actor, DateTimeOffset now)
    {
        var alias = _aliases.SingleOrDefault(existing => existing.Id == aliasId);
        if (alias is null)
        {
            return Result.Failure(RegistryErrors.AliasNotFound);
        }

        if (alias.IsRemoved)
        {
            return Result.Failure(RegistryErrors.AliasRemoved);
        }

        alias.Remove(reason, actor, now);
        return Result.Success();
    }

    /// <summary>
    /// Records a guardian's consent (S06 D14) for a person whose age status on the tenant's date isn't <c>ADULT</c>
    /// (<c>PERSON_CONSENT_NOT_NEEDED</c> otherwise; owner, S06-T6a plan). Several may be active at once, one per guardian.
    /// Refused on an archived or deceased person (D9). The use case has validated the details and checked that the date
    /// given is neither after the tenant's date nor before the date of birth.
    /// </summary>
    public Result<GuardianConsent> RecordConsent(
        GuardianConsentDetails details, AgeStatus ageStatus, ICurrentActor actor, DateTimeOffset now)
    {
        var accepts = EnsureAcceptsNewChildren();
        if (accepts.IsFailure)
        {
            return accepts.Error;
        }

        if (ageStatus == AgeStatus.Adult)
        {
            return RegistryErrors.ConsentNotNeeded;
        }

        var consent = new GuardianConsent(Id, details, evidenceAssetId: null, replacesConsentId: null, actor, now);
        _consents.Add(consent);
        return Result.Success(consent);
    }

    /// <summary>
    /// Withdraws <paramref name="scopes"/> of an active consent with a reason (S06 D14): the record is closed and, when
    /// scopes remain, a successor with them is recorded (the same guardian, date and evidence) and returned. Allowed in
    /// every status and at every age (a correction, as removing an alias). <c>PERSON_CONSENT_NOT_FOUND</c>,
    /// <c>PERSON_CONSENT_WITHDRAWN</c>; the use case has checked the scopes are the record's.
    /// </summary>
    public Result<GuardianConsent?> WithdrawConsent(
        Guid consentId, IReadOnlyCollection<ConsentScope> scopes, string reason, ICurrentActor actor, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(scopes);
        var found = FindActiveConsent(consentId);
        if (found.IsFailure)
        {
            return found.Error;
        }

        var consent = found.Value;
        if (scopes.Count == 0 || scopes.Any(scope => !consent.Scopes.Contains(scope)))
        {
            throw new ArgumentException("Withdraw one or more of the consent's own scopes.", nameof(scopes));
        }

        consent.Withdraw(reason, actor, now);
        var remaining = consent.Scopes.Except(scopes).ToList();
        if (remaining.Count == 0)
        {
            return Result.Success<GuardianConsent?>(null);
        }

        var successor = new GuardianConsent(Id, consent.DetailsWith(remaining), consent.EvidenceAssetId, consent.Id, actor, now);
        _consents.Add(successor);
        return Result.Success<GuardianConsent?>(successor);
    }

    /// <summary>
    /// Points an active consent at a newly stored evidence asset (S06 D19) and returns the one it replaces, which the use
    /// case marks <c>REMOVED</c> after its save unless another record still names it (owner, S06-T6a plan). Refused on an
    /// archived or deceased person (D9).
    /// </summary>
    public Result<Guid?> AttachConsentEvidence(Guid consentId, Guid assetId)
    {
        var accepts = EnsureAcceptsNewChildren();
        if (accepts.IsFailure)
        {
            return accepts.Error;
        }

        var found = FindActiveConsent(consentId);
        return found.IsFailure ? found.Error : Result.Success(found.Value.AttachEvidence(assetId));
    }

    /// <summary>An active consent of the person: <c>PERSON_CONSENT_NOT_FOUND</c> or <c>PERSON_CONSENT_WITHDRAWN</c>.</summary>
    public Result<GuardianConsent> FindActiveConsent(Guid consentId)
    {
        var consent = _consents.SingleOrDefault(existing => existing.Id == consentId);
        if (consent is null)
        {
            return RegistryErrors.ConsentNotFound;
        }

        return consent.IsActive ? Result.Success(consent) : RegistryErrors.ConsentWithdrawn;
    }

    /// <summary>
    /// Adds an <c>ACTIVE</c> athlete profile for <paramref name="variant"/> with the first row of its history (S06 D12).
    /// Refused on an archived or deceased person (D9) and when the person already holds an open profile for the variant
    /// (<c>PERSON_ATHLETE_PROFILE_EXISTS</c>: a retired one is reactivated, not doubled). The use case has checked that
    /// the variant is enabled and the codes are the definition's.
    /// </summary>
    public Result<AthleteProfile> AddAthleteProfile(
        string variant, AthleteProfileDetails details, ICurrentActor actor, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(actor);
        var accepts = EnsureAcceptsNewChildren();
        if (accepts.IsFailure)
        {
            return accepts.Error;
        }

        if (_athleteProfiles.Any(existing => existing.Variant == variant && ProfileTransitions.IsOpen(existing.Status)))
        {
            return RegistryErrors.AthleteProfileExists;
        }

        var profile = new AthleteProfile(Id, variant, details);
        _athleteProfiles.Add(profile);
        _profileStatusHistory.Add(
            new ProfileStatusChange(Id, ProfileKind.Athlete, profile.Id, null, profile.Status, null, actor, now));
        return Result.Success(profile);
    }

    /// <summary>
    /// Replaces an athlete profile's details (S06 D12): a correction, allowed whatever the person's status (D9; owner,
    /// S06-T3 plan) but not on an archived profile (<c>PERSON_PROFILE_ARCHIVED</c>). The use case has checked the codes.
    /// </summary>
    public Result UpdateAthleteProfile(Guid profileId, AthleteProfileDetails details)
    {
        var profile = _athleteProfiles.SingleOrDefault(existing => existing.Id == profileId);
        if (profile is null)
        {
            return Result.Failure(RegistryErrors.ProfileNotFound);
        }

        if (!ProfileTransitions.IsOpen(profile.Status))
        {
            return Result.Failure(RegistryErrors.ProfileArchived);
        }

        profile.Update(details);
        return Result.Success();
    }

    /// <summary>
    /// Moves an athlete profile to <paramref name="to"/> with a reason and a history row (S06 D12,
    /// <see cref="ProfileTransitions"/>); see <see cref="ChangeProfileStatus"/>.
    /// </summary>
    public Result ChangeAthleteProfileStatus(
        Guid profileId, ProfileStatus to, string reason, ICurrentActor actor, DateTimeOffset now) =>
        ChangeProfileStatus(
            ProfileKind.Athlete, _athleteProfiles.SingleOrDefault(existing => existing.Id == profileId), to, reason, actor, now);

    /// <summary>
    /// Adds an <c>ACTIVE</c> staff profile with its capabilities and the first row of its history (S06 D12). Refused on
    /// an archived or deceased person (D9) and when the person already holds an open staff profile
    /// (<c>PERSON_STAFF_PROFILE_EXISTS</c>: a retired one is reactivated, not doubled). The use case has checked the
    /// variants are enabled and the roles are the definitions'.
    /// </summary>
    public Result<StaffProfile> AddStaffProfile(
        IReadOnlyList<StaffCapabilityDetails> capabilities, ICurrentActor actor, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(actor);
        var accepts = EnsureAcceptsNewChildren();
        if (accepts.IsFailure)
        {
            return accepts.Error;
        }

        if (_staffProfiles.Any(existing => ProfileTransitions.IsOpen(existing.Status)))
        {
            return RegistryErrors.StaffProfileExists;
        }

        var profile = new StaffProfile(Id, capabilities);
        _staffProfiles.Add(profile);
        _profileStatusHistory.Add(
            new ProfileStatusChange(Id, ProfileKind.Staff, profile.Id, null, profile.Status, null, actor, now));
        return Result.Success(profile);
    }

    /// <summary>
    /// Replaces a staff profile's capabilities (S06 D12), but not on an archived profile (<c>PERSON_PROFILE_ARCHIVED</c>).
    /// On an archived or deceased person (D9) held capabilities may be kept, removed or re-qualified, but a role the
    /// profile doesn't hold is new data and refused (owner, review #102 R2). The use case has checked the codes, and that
    /// an added capability's variant is enabled.
    /// </summary>
    public Result UpdateStaffProfile(Guid profileId, IReadOnlyList<StaffCapabilityDetails> capabilities)
    {
        var profile = _staffProfiles.SingleOrDefault(existing => existing.Id == profileId);
        if (profile is null)
        {
            return Result.Failure(RegistryErrors.ProfileNotFound);
        }

        if (!ProfileTransitions.IsOpen(profile.Status))
        {
            return Result.Failure(RegistryErrors.ProfileArchived);
        }

        if (capabilities.Any(c => !profile.Holds(c.Variant, c.Role)) && EnsureAcceptsNewChildren() is { IsFailure: true } refused)
        {
            return refused;
        }

        profile.Replace(capabilities);
        return Result.Success();
    }

    /// <summary>Moves a staff profile; see <see cref="ChangeProfileStatus"/>.</summary>
    public Result ChangeStaffProfileStatus(
        Guid profileId, ProfileStatus to, string reason, ICurrentActor actor, DateTimeOffset now) =>
        ChangeProfileStatus(
            ProfileKind.Staff, _staffProfiles.SingleOrDefault(existing => existing.Id == profileId), to, reason, actor, now);

    /// <summary>
    /// Adds an <c>ACTIVE</c> official profile with its capabilities and the first row of its history (S06 D12). Refused on
    /// an archived or deceased person (D9) and when the person already holds an open official profile
    /// (<c>PERSON_OFFICIAL_PROFILE_EXISTS</c>: a retired one is reactivated, not doubled). The use case has checked the
    /// variants are enabled and the roles are the definitions'.
    /// </summary>
    public Result<OfficialProfile> AddOfficialProfile(
        IReadOnlyList<OfficialCapabilityDetails> capabilities, ICurrentActor actor, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(actor);
        var accepts = EnsureAcceptsNewChildren();
        if (accepts.IsFailure)
        {
            return accepts.Error;
        }

        if (_officialProfiles.Any(existing => ProfileTransitions.IsOpen(existing.Status)))
        {
            return RegistryErrors.OfficialProfileExists;
        }

        var profile = new OfficialProfile(Id, capabilities);
        _officialProfiles.Add(profile);
        _profileStatusHistory.Add(
            new ProfileStatusChange(Id, ProfileKind.Official, profile.Id, null, profile.Status, null, actor, now));
        return Result.Success(profile);
    }

    /// <summary>
    /// Replaces an official profile's capabilities (S06 D12), but not on an archived profile
    /// (<c>PERSON_PROFILE_ARCHIVED</c>). On an archived or deceased person (D9) held capabilities may be kept, removed or
    /// have their grade and licence changed, but a role the profile doesn't hold is new data and refused (as staff
    /// profiles, owner, review #102 R2). The use case has checked the codes, and that an added capability's variant is
    /// enabled.
    /// </summary>
    public Result UpdateOfficialProfile(Guid profileId, IReadOnlyList<OfficialCapabilityDetails> capabilities)
    {
        var profile = _officialProfiles.SingleOrDefault(existing => existing.Id == profileId);
        if (profile is null)
        {
            return Result.Failure(RegistryErrors.ProfileNotFound);
        }

        if (!ProfileTransitions.IsOpen(profile.Status))
        {
            return Result.Failure(RegistryErrors.ProfileArchived);
        }

        if (capabilities.Any(c => !profile.Holds(c.Variant, c.Role)) && EnsureAcceptsNewChildren() is { IsFailure: true } refused)
        {
            return refused;
        }

        profile.Replace(capabilities);
        return Result.Success();
    }

    /// <summary>Moves an official profile; see <see cref="ChangeProfileStatus"/>.</summary>
    public Result ChangeOfficialProfileStatus(
        Guid profileId, ProfileStatus to, string reason, ICurrentActor actor, DateTimeOffset now) =>
        ChangeProfileStatus(
            ProfileKind.Official, _officialProfiles.SingleOrDefault(existing => existing.Id == profileId), to, reason, actor, now);

    /// <summary>
    /// Moves a profile to <paramref name="to"/> with a reason and a history row (S06 D12,
    /// <see cref="ProfileTransitions"/>). A move the current status doesn't allow is <c>PERSON_PROFILE_STATUS_INVALID</c>;
    /// a move to <c>ACTIVE</c> is refused on an archived or deceased person (D9), every other move is allowed.
    /// </summary>
    private Result ChangeProfileStatus(
        ProfileKind kind, IRoleProfile? profile, ProfileStatus to, string reason, ICurrentActor actor, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        if (profile is null)
        {
            return Result.Failure(RegistryErrors.ProfileNotFound);
        }

        if (!ProfileTransitions.IsAllowed(profile.Status, to))
        {
            return Result.Failure(RegistryErrors.ProfileStatusInvalid);
        }

        if (to == ProfileStatus.Active && EnsureAcceptsNewChildren() is { IsFailure: true } refused)
        {
            return refused;
        }

        var from = profile.Status;
        profile.MoveTo(to);
        _profileStatusHistory.Add(new ProfileStatusChange(Id, kind, profile.Id, from, to, reason.Trim(), actor, now));
        return Result.Success();
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
            if (decimal.Round(weight, 1) != weight)
            {
                throw new ArgumentException("A weight has at most one decimal.", nameof(biodata));
            }
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

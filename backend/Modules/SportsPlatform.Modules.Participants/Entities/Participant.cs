using SportsPlatform.BuildingBlocks;
using System.Text.Json;

namespace SportsPlatform.Modules.Participants.Entities;

public abstract class Participant : TenantAggregateRoot<Guid>
{
    public string Name { get; protected set; } = string.Empty;
    public string ShortName { get; protected set; } = string.Empty;
    public string? Logo { get; protected set; }
    public ParticipantType Type { get; protected set; }
    public ParticipantStatus Status { get; protected set; } = ParticipantStatus.Active;
    public new DateTime CreatedAt { get; protected set; }
    public new DateTime? UpdatedAt { get; protected set; }

    private readonly List<ParticipantSportProfile> _sportProfiles = new();
    public IReadOnlyCollection<ParticipantSportProfile> SportProfiles =>
        _sportProfiles.AsReadOnly();

    protected Participant() { }

    protected Participant(Guid id, Guid tenantId, string name, string shortName, ParticipantType type)
        : base(id, tenantId)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("A tenant is required.", nameof(tenantId));
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("A name is required.", nameof(name));
        if (string.IsNullOrWhiteSpace(shortName)) throw new ArgumentException("A short name is required.", nameof(shortName));
        Name = name.Trim();
        ShortName = shortName.Trim();
        Type = type;
        CreatedAt = DateTime.UtcNow;
    }

    public ParticipantSportProfile SetSportAttributes(Guid sportId, string attributesJson)
    {
        if (sportId == Guid.Empty) throw new ArgumentException("A sport is required.", nameof(sportId));
        var canonicalJson = ValidateAttributes(attributesJson);
        var profile = _sportProfiles.SingleOrDefault(item => item.SportId == sportId);
        if (profile is null)
        {
            profile = new ParticipantSportProfile(Guid.NewGuid(), Id, TenantId, sportId, canonicalJson);
            _sportProfiles.Add(profile);
        }
        else
        {
            profile.Update(canonicalJson);
        }
        UpdatedAt = DateTime.UtcNow;
        return profile;
    }

    private static string ValidateAttributes(string attributesJson)
    {
        if (string.IsNullOrWhiteSpace(attributesJson))
            throw new ArgumentException("Sport attributes must be a JSON object.", nameof(attributesJson));
        try
        {
            using var document = JsonDocument.Parse(attributesJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                throw new ArgumentException("Sport attributes must be a JSON object.", nameof(attributesJson));
            return document.RootElement.GetRawText();
        }
        catch (JsonException exception)
        {
            throw new ArgumentException("Sport attributes must contain valid JSON.", nameof(attributesJson), exception);
        }
    }
}

public enum ParticipantType
{
    Team,
    Athlete,
    Pair,
    RelayTeam,
    Crew,
    Club,
    School,
    Country,
    CompositeTeam
}

public enum ParticipantStatus { Active, Inactive, Suspended }

public class Athlete : Participant
{
    public DateTime? DateOfBirth { get; private set; }
    public string? Gender { get; private set; }
    public string? CompetitionCategory { get; private set; }
    public string? Nationality { get; private set; }
    public string? Photo { get; private set; }
    public string? ContactEmail { get; private set; }
    public string? ContactPhone { get; private set; }
    public string? EmergencyContact { get; private set; }
    public string? EmergencyContactName { get; private set; }
    public string? EmergencyContactRelationship { get; private set; }
    public string? EmergencyContactPhone { get; private set; }
    public bool MedicalClearance { get; private set; }
    public string MedicalClearanceStatus { get; private set; } = "NotProvided";
    public DateTime? MedicalClearanceExpiresAt { get; private set; }
    public string IdentificationDocumentsJson { get; private set; } = "[]";

    private readonly List<SportRegistration> _sportRegistrations = new();
    public IReadOnlyCollection<SportRegistration> SportRegistrations => _sportRegistrations.AsReadOnly();

    private Athlete() { }

    public Athlete(Guid id, Guid tenantId, string name, string shortName)
        : base(id, tenantId, name, shortName, ParticipantType.Athlete) { }

    public void UpdatePersonalInfo(DateTime? dateOfBirth, string? gender, string? nationality)
    {
        UpdatePersonalInfo(dateOfBirth, gender, null, nationality);
    }

    public void UpdatePersonalInfo(
        DateTime? dateOfBirth,
        string? gender,
        string? competitionCategory,
        string? nationality)
    {
        var today = DateTime.UtcNow.Date;
        if (dateOfBirth?.Date > today)
            throw new ArgumentException("Date of birth cannot be in the future.", nameof(dateOfBirth));
        if (dateOfBirth?.Date < today.AddYears(-120))
            throw new ArgumentException("Date of birth is outside the supported range.", nameof(dateOfBirth));
        DateOfBirth = dateOfBirth;
        Gender = Optional(gender, nameof(gender), 50);
        CompetitionCategory = Optional(competitionCategory, nameof(competitionCategory), 100);
        Nationality = Optional(nationality, nameof(nationality), 100);
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateContact(string? email, string? phone, string? emergencyContact)
    {
        ContactEmail = Email(email);
        ContactPhone = Optional(phone, nameof(phone), 50);
        EmergencyContact = Optional(emergencyContact, nameof(emergencyContact), 300);
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateIdentity(string name, string shortName, string? photo)
    {
        Name = Required(name, nameof(name), 200);
        ShortName = Required(shortName, nameof(shortName), 50);
        if (!string.IsNullOrWhiteSpace(photo)
            && (!Uri.TryCreate(photo.Trim(), UriKind.Absolute, out var uri)
                || uri.Scheme is not ("http" or "https")))
            throw new ArgumentException("Photo must be an absolute HTTP(S) URL.", nameof(photo));
        Photo = Optional(photo, nameof(photo), 500);
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateEmergencyContact(string? name, string? relationship, string? phone)
    {
        EmergencyContactName = Optional(name, nameof(name), 200);
        EmergencyContactRelationship = Optional(relationship, nameof(relationship), 100);
        EmergencyContactPhone = Optional(phone, nameof(phone), 50);
        EmergencyContact = string.Join(" · ", new[]
            { EmergencyContactName, EmergencyContactRelationship, EmergencyContactPhone }
            .Where(value => !string.IsNullOrWhiteSpace(value)));
        if (EmergencyContact.Length == 0) EmergencyContact = null;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetMedicalClearance(bool cleared)
    {
        SetMedicalClearance(cleared ? "Cleared" : "NotProvided", null);
    }

    public void SetMedicalClearance(string status, DateTime? expiresAt)
    {
        var normalized = Required(status, nameof(status), 30);
        var allowed = new[] { "NotProvided", "Pending", "Cleared", "Expired", "Rejected" };
        var canonical = allowed.FirstOrDefault(value =>
            value.Equals(normalized, StringComparison.OrdinalIgnoreCase))
            ?? throw new ArgumentException($"Medical clearance status '{status}' is not supported.", nameof(status));
        if (expiresAt.HasValue && expiresAt.Value.Kind == DateTimeKind.Unspecified)
            expiresAt = DateTime.SpecifyKind(expiresAt.Value, DateTimeKind.Utc);
        MedicalClearanceStatus = canonical;
        MedicalClearanceExpiresAt = expiresAt?.ToUniversalTime();
        MedicalClearance = canonical == "Cleared"
            && (!MedicalClearanceExpiresAt.HasValue || MedicalClearanceExpiresAt > DateTime.UtcNow);
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetIdentificationDocuments(IEnumerable<AthleteIdentificationDocument>? documents)
    {
        var normalized = (documents ?? Array.Empty<AthleteIdentificationDocument>())
            .Select(document => new AthleteIdentificationDocument(
                Required(document.Type, "document type", 100),
                Required(document.Number, "document number", 200),
                Optional(document.IssuingCountry, "issuing country", 100),
                document.ExpiresAt?.ToUniversalTime(),
                OptionalHttpUrl(document.FileUrl, "document file URL")))
            .ToArray();
        if (normalized.Length > 20)
            throw new ArgumentException("An athlete cannot have more than 20 identification documents.", nameof(documents));
        IdentificationDocumentsJson = JsonSerializer.Serialize(normalized);
        UpdatedAt = DateTime.UtcNow;
    }

    public IReadOnlyList<AthleteIdentificationDocument> GetIdentificationDocuments()
    {
        try
        {
            return JsonSerializer.Deserialize<AthleteIdentificationDocument[]>(IdentificationDocumentsJson)
                ?? Array.Empty<AthleteIdentificationDocument>();
        }
        catch (JsonException)
        {
            return Array.Empty<AthleteIdentificationDocument>();
        }
    }

    public void RegisterForSport(Guid sportId)
    {
        if (sportId == Guid.Empty) throw new ArgumentException("A sport is required.", nameof(sportId));
        if (_sportRegistrations.Any(r => r.SportId == sportId && r.IsActive)) return;
        _sportRegistrations.Add(new SportRegistration(Guid.NewGuid(), Id, TenantId, sportId));
        UpdatedAt = DateTime.UtcNow;
    }

    private static string Required(string? value, string name, int maximumLength)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length == 0) throw new ArgumentException($"{name} is required.", name);
        if (normalized.Length > maximumLength) throw new ArgumentException($"{name} cannot exceed {maximumLength} characters.", name);
        return normalized;
    }

    private static string? Optional(string? value, string name, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var normalized = value.Trim();
        if (normalized.Length > maximumLength) throw new ArgumentException($"{name} cannot exceed {maximumLength} characters.", name);
        return normalized;
    }

    private static string? Email(string? value)
    {
        var normalized = Optional(value, "email", 254);
        if (normalized is not null && !System.Net.Mail.MailAddress.TryCreate(normalized, out _))
            throw new ArgumentException("Contact email is invalid.", nameof(value));
        return normalized;
    }

    private static string? OptionalHttpUrl(string? value, string name)
    {
        var normalized = Optional(value, name, 1000);
        if (normalized is not null
            && (!Uri.TryCreate(normalized, UriKind.Absolute, out var uri)
                || uri.Scheme is not ("http" or "https")))
            throw new ArgumentException($"{name} must be an absolute HTTP(S) URL.", name);
        return normalized;
    }
}

public sealed record AthleteIdentificationDocument(
    string Type,
    string Number,
    string? IssuingCountry = null,
    DateTime? ExpiresAt = null,
    string? FileUrl = null);

public abstract class OrganizationParticipant : Participant
{
    public string? HomeVenue { get; private set; }
    public string? Coach { get; private set; }
    public string? Manager { get; private set; }
    public string? ContactEmail { get; private set; }
    public string? ContactPhone { get; private set; }

    protected OrganizationParticipant() { }

    protected OrganizationParticipant(
        Guid id, Guid tenantId, string name, string shortName, ParticipantType type)
        : base(id, tenantId, name, shortName, type) { }

    public void UpdateProfile(
        string name,
        string shortName,
        string? logo,
        string? homeVenue,
        string? coach,
        string? manager,
        string? contactEmail,
        string? contactPhone)
    {
        Name = Required(name, nameof(name), 200);
        ShortName = Required(shortName, nameof(shortName), 50);
        Logo = OptionalHttpUrl(logo, nameof(logo), 500);
        HomeVenue = Optional(homeVenue, nameof(homeVenue), 200);
        Coach = Optional(coach, nameof(coach), 200);
        Manager = Optional(manager, nameof(manager), 200);
        ContactEmail = Email(contactEmail);
        ContactPhone = Optional(contactPhone, nameof(contactPhone), 50);
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateDetails(string? homeVenue, string? coach, string? manager)
    {
        HomeVenue = Optional(homeVenue, nameof(homeVenue), 200);
        Coach = Optional(coach, nameof(coach), 200);
        Manager = Optional(manager, nameof(manager), 200);
        UpdatedAt = DateTime.UtcNow;
    }

    private static string Required(string? value, string name, int maximumLength)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length == 0) throw new ArgumentException($"{name} is required.", name);
        if (normalized.Length > maximumLength) throw new ArgumentException($"{name} cannot exceed {maximumLength} characters.", name);
        return normalized;
    }

    private static string? Optional(string? value, string name, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var normalized = value.Trim();
        if (normalized.Length > maximumLength) throw new ArgumentException($"{name} cannot exceed {maximumLength} characters.", name);
        return normalized;
    }

    private static string? OptionalHttpUrl(string? value, string name, int maximumLength)
    {
        var normalized = Optional(value, name, maximumLength);
        if (normalized is not null
            && (!Uri.TryCreate(normalized, UriKind.Absolute, out var uri)
                || uri.Scheme is not ("http" or "https")))
            throw new ArgumentException($"{name} must be an absolute HTTP(S) URL.", name);
        return normalized;
    }

    private static string? Email(string? value)
    {
        var normalized = Optional(value, "contact email", 254);
        if (normalized is not null && !System.Net.Mail.MailAddress.TryCreate(normalized, out _))
            throw new ArgumentException("Contact email is invalid.", nameof(value));
        return normalized;
    }
}

public class Team : OrganizationParticipant
{

    private readonly List<TeamRoster> _rosters = new();
    public IReadOnlyCollection<TeamRoster> Rosters => _rosters.AsReadOnly();

    private Team() { }

    public Team(Guid id, Guid tenantId, string name, string shortName)
        : base(id, tenantId, name, shortName, ParticipantType.Team) { }

    public TeamRoster AddRoster(string name, Guid? competitionId)
        => AddRoster(name, name, null, null, competitionId);

    public TeamRoster AddRoster(
        string name,
        string season,
        DateTime? startsAt,
        DateTime? endsAt,
        Guid? competitionId)
    {
        if (_rosters.Any(item => item.IsActive
            && item.Name.Equals(name.Trim(), StringComparison.OrdinalIgnoreCase)
            && item.Season.Equals(season.Trim(), StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("An active roster already exists for this name and season.");
        var roster = new TeamRoster(
            Guid.NewGuid(), Id, TenantId, name, season, startsAt, endsAt, competitionId);
        _rosters.Add(roster);
        UpdatedAt = DateTime.UtcNow;
        return roster;
    }
}

public sealed class Pair : Participant
{
    private Pair() { }
    public Pair(Guid id, Guid tenantId, string name, string shortName)
        : base(id, tenantId, name, shortName, ParticipantType.Pair) { }
}

public sealed class RelayTeam : Participant
{
    private RelayTeam() { }
    public RelayTeam(Guid id, Guid tenantId, string name, string shortName)
        : base(id, tenantId, name, shortName, ParticipantType.RelayTeam) { }
}

public sealed class Crew : Participant
{
    private Crew() { }
    public Crew(Guid id, Guid tenantId, string name, string shortName)
        : base(id, tenantId, name, shortName, ParticipantType.Crew) { }
}

public sealed class Club : OrganizationParticipant
{
    private Club() { }
    public Club(Guid id, Guid tenantId, string name, string shortName)
        : base(id, tenantId, name, shortName, ParticipantType.Club) { }
}

public sealed class School : Participant
{
    private School() { }
    public School(Guid id, Guid tenantId, string name, string shortName)
        : base(id, tenantId, name, shortName, ParticipantType.School) { }
}

public sealed class Country : Participant
{
    private Country() { }
    public Country(Guid id, Guid tenantId, string name, string shortName)
        : base(id, tenantId, name, shortName, ParticipantType.Country) { }
}

public sealed class CompositeTeam : Participant
{
    private CompositeTeam() { }
    public CompositeTeam(Guid id, Guid tenantId, string name, string shortName)
        : base(id, tenantId, name, shortName, ParticipantType.CompositeTeam) { }
}

public sealed class ParticipantSportProfile : TenantEntity<Guid>
{
    public Guid ParticipantId { get; private set; }
    public Guid SportId { get; private set; }
    public string AttributesJson { get; private set; } = "{}";

    private ParticipantSportProfile() { }

    public ParticipantSportProfile(
        Guid id, Guid participantId, Guid tenantId, Guid sportId, string attributesJson)
        : base(id, tenantId)
    {
        ParticipantId = participantId;
        SportId = sportId;
        AttributesJson = attributesJson;
    }

    internal void Update(string attributesJson) => AttributesJson = attributesJson;
}

public class SportRegistration : TenantEntity<Guid>
{
    public Guid AthleteId { get; private set; }
    public Guid SportId { get; private set; }
    public bool IsActive { get; private set; } = true;
    public DateTime RegisteredAt { get; private set; }

    private SportRegistration() { }

    public SportRegistration(Guid id, Guid athleteId, Guid tenantId, Guid sportId)
        : base(id, tenantId)
    {
        AthleteId = athleteId;
        SportId = sportId;
        RegisteredAt = DateTime.UtcNow;
    }

    public void Deactivate() { IsActive = false; }
}

public class TeamRoster : TenantAggregateRoot<Guid>
{
    public Guid TeamId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public Guid? CompetitionId { get; private set; }
    public string Season { get; private set; } = string.Empty;
    public DateTime? StartsAt { get; private set; }
    public DateTime? EndsAt { get; private set; }
    public bool IsActive { get; private set; } = true;
    public new DateTime CreatedAt { get; private set; }

    private readonly List<RosterEntry> _entries = new();
    public IReadOnlyCollection<RosterEntry> Entries => _entries.AsReadOnly();

    private TeamRoster() { }

    public TeamRoster(Guid id, Guid teamId, Guid tenantId, string name, Guid? competitionId)
        : this(id, teamId, tenantId, name, name, null, null, competitionId) { }

    public TeamRoster(
        Guid id,
        Guid teamId,
        Guid tenantId,
        string name,
        string season,
        DateTime? startsAt,
        DateTime? endsAt,
        Guid? competitionId)
        : base(id, tenantId)
    {
        if (teamId == Guid.Empty) throw new ArgumentException("A team is required.", nameof(teamId));
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("A roster name is required.", nameof(name));
        if (string.IsNullOrWhiteSpace(season)) throw new ArgumentException("A season is required.", nameof(season));
        if (startsAt.HasValue && endsAt.HasValue && endsAt.Value < startsAt.Value)
            throw new ArgumentException("The roster end date cannot precede its start date.", nameof(endsAt));
        TeamId = teamId;
        Name = name.Trim();
        Season = season.Trim();
        StartsAt = startsAt?.ToUniversalTime();
        EndsAt = endsAt?.ToUniversalTime();
        CompetitionId = competitionId;
        CreatedAt = DateTime.UtcNow;
    }

    public RosterEntry AddAthlete(Guid athleteId, string? jerseyNumber, string? position)
    {
        if (!IsActive) throw new InvalidOperationException("Athletes cannot be added to an inactive roster.");
        if (athleteId == Guid.Empty) throw new ArgumentException("An athlete is required.", nameof(athleteId));
        if (_entries.Any(item => item.AthleteId == athleteId && item.IsActive))
            throw new InvalidOperationException("The athlete is already active on this roster.");
        var normalizedNumber = string.IsNullOrWhiteSpace(jerseyNumber) ? null : jerseyNumber.Trim();
        if (normalizedNumber?.Length > 30)
            throw new ArgumentException("A squad number cannot exceed 30 characters.", nameof(jerseyNumber));
        if (normalizedNumber is not null && _entries.Any(item => item.IsActive
            && string.Equals(item.JerseyNumber, normalizedNumber, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"Squad number '{normalizedNumber}' is already assigned on this roster.");
        var entry = new RosterEntry(Guid.NewGuid(), Id, TenantId, athleteId, normalizedNumber, position);
        _entries.Add(entry);
        return entry;
    }

    public RosterEntry RemoveAthlete(Guid athleteId, string reason)
    {
        var entry = _entries.SingleOrDefault(item => item.AthleteId == athleteId && item.IsActive)
            ?? throw new InvalidOperationException("The athlete is not active on this roster.");
        entry.Deactivate(reason);
        return entry;
    }

    public void Deactivate(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("A reason is required.", nameof(reason));
        foreach (var entry in _entries.Where(item => item.IsActive)) entry.Deactivate(reason);
        IsActive = false;
        UpdatedAt = DateTime.UtcNow;
    }
}

public class RosterEntry : TenantEntity<Guid>
{
    public Guid RosterId { get; private set; }
    public Guid AthleteId { get; private set; }
    public string? JerseyNumber { get; private set; }
    public string? Position { get; private set; }
    public bool IsActive { get; private set; } = true;
    public DateTime JoinedAt { get; private set; }
    public DateTime? LeftAt { get; private set; }
    public string? MovementReason { get; private set; }

    private RosterEntry() { }

    public RosterEntry(Guid id, Guid rosterId, Guid tenantId, Guid athleteId,
        string? jerseyNumber, string? position) : base(id, tenantId)
    {
        RosterId = rosterId;
        AthleteId = athleteId;
        JerseyNumber = string.IsNullOrWhiteSpace(jerseyNumber) ? null : jerseyNumber.Trim();
        Position = string.IsNullOrWhiteSpace(position) ? null : position.Trim();
        JoinedAt = DateTime.UtcNow;
    }

    public void Deactivate() => Deactivate("Removed from roster");

    public void Deactivate(string reason)
    {
        if (!IsActive) return;
        if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("A movement reason is required.", nameof(reason));
        IsActive = false;
        LeftAt = DateTime.UtcNow;
        MovementReason = reason.Trim();
        UpdatedAt = DateTime.UtcNow;
    }
}

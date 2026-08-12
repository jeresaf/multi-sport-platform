using SportsPlatform.BuildingBlocks;

namespace SportsPlatform.Modules.Registrations.Entities;

public class RegistrationForm : AggregateRoot<Guid>
{
    public Guid TenantId { get; private set; }
    public Guid? CompetitionId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public string FormSchema { get; private set; } = "[]";
    public bool IsActive { get; private set; }
    public new DateTime CreatedAt { get; private set; }
    public new DateTime? UpdatedAt { get; private set; }
    public string CreatedBy { get; private set; } = string.Empty;
    public int Version { get; private set; } = 1;

    private RegistrationForm() { }

    public RegistrationForm(
        Guid id,
        Guid tenantId,
        string name,
        string createdBy,
        Guid? competitionId = null,
        string? description = null) : base(id)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("A tenant is required.", nameof(tenantId));
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("A form name is required.", nameof(name));
        if (string.IsNullOrWhiteSpace(createdBy)) throw new ArgumentException("A creator is required.", nameof(createdBy));
        TenantId = tenantId;
        CompetitionId = competitionId;
        Name = name.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        CreatedBy = createdBy.Trim();
        IsActive = true;
        CreatedAt = DateTime.UtcNow;
    }

    public void UpdateSchema(string formSchema)
    {
        if (string.IsNullOrWhiteSpace(formSchema)) throw new ArgumentException("A form schema is required.", nameof(formSchema));
        FormSchema = formSchema;
        UpdatedAt = DateTime.UtcNow;
        Version++;
    }

    public void UpdateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("A form name is required.", nameof(name));
        Name = name.Trim();
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateDescription(string? description)
    {
        Description = description;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Activate()
    {
        IsActive = true;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Deactivate()
    {
        IsActive = false;
        UpdatedAt = DateTime.UtcNow;
    }
}

public class RegistrationFormField
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string FieldType { get; set; } = "Text"; // Text, Number, Date, Select, Checkbox, File, Consent
    public string Label { get; set; } = string.Empty;
    public string? Placeholder { get; set; }
    public bool IsRequired { get; set; }
    public string? ValidationRegex { get; set; }
    public string? ValidationMessage { get; set; }
    public int? MinLength { get; set; }
    public int? MaxLength { get; set; }
    public decimal? MinValue { get; set; }
    public decimal? MaxValue { get; set; }
    public List<SelectOption>? Options { get; set; } // For Select fields
    public string? ConditionalFieldId { get; set; } // For conditional fields
    public string? ConditionalValue { get; set; }
    public int Order { get; set; }
    public bool ShowInList { get; set; } = true;
    public string? HelpText { get; set; }
    public string? ConsentText { get; set; }
    public List<string>? AllowedContentTypes { get; set; }
    public long? MaxFileSizeBytes { get; set; }
}

public class SelectOption
{
    public string Value { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
}

public class RegistrationFormSubmission : AggregateRoot<Guid>
{
    public Guid FormId { get; private set; }
    public Guid ParticipantId { get; private set; }
    public Guid? ContestId { get; private set; }
    public string FormData { get; private set; } = "{}";
    public RegistrationFormSubmissionStatus Status { get; private set; } = RegistrationFormSubmissionStatus.Draft;
    public string? ReviewNotes { get; private set; }
    public Guid? ReviewedBy { get; private set; }
    public DateTime? ReviewedAt { get; private set; }
    public new DateTime CreatedAt { get; private set; }
    public new DateTime? UpdatedAt { get; private set; }
    public DateTime? SubmittedAt { get; private set; }
    public int FormVersion { get; private set; } = 1;
    public string FormSchemaSnapshot { get; private set; } = "[]";
    public int Revision { get; private set; } = 1;

    private RegistrationFormSubmission() { }

    public RegistrationFormSubmission(
        Guid id,
        Guid formId,
        Guid participantId,
        Guid? contestId = null) : base(id)
        => Initialize(formId, participantId, contestId, 1, "[]");

    public RegistrationFormSubmission(
        Guid id,
        Guid formId,
        Guid participantId,
        int formVersion,
        string formSchemaSnapshot,
        Guid? contestId = null) : base(id)
        => Initialize(formId, participantId, contestId, formVersion, formSchemaSnapshot);

    private void Initialize(
        Guid formId,
        Guid participantId,
        Guid? contestId,
        int formVersion,
        string formSchemaSnapshot)
    {
        if (formId == Guid.Empty) throw new ArgumentException("A form is required.", nameof(formId));
        if (participantId == Guid.Empty) throw new ArgumentException("A participant is required.", nameof(participantId));
        if (formVersion < 1) throw new ArgumentOutOfRangeException(nameof(formVersion));
        if (string.IsNullOrWhiteSpace(formSchemaSnapshot)) throw new ArgumentException("A schema snapshot is required.", nameof(formSchemaSnapshot));
        FormId = formId;
        ParticipantId = participantId;
        ContestId = contestId;
        FormVersion = formVersion;
        FormSchemaSnapshot = formSchemaSnapshot;
        CreatedAt = DateTime.UtcNow;
    }

    public void UpdateFormData(string formData)
    {
        FormData = formData;
        Revision++;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Submit()
    {
        if (Status != RegistrationFormSubmissionStatus.Draft)
            throw new InvalidOperationException("Only draft submissions can be submitted.");

        Status = RegistrationFormSubmissionStatus.Submitted;
        SubmittedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UnderReview()
    {
        if (Status != RegistrationFormSubmissionStatus.Submitted)
            throw new InvalidOperationException("Only submitted forms can be under review.");

        Status = RegistrationFormSubmissionStatus.UnderReview;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Approve()
    {
        if (Status != RegistrationFormSubmissionStatus.UnderReview)
            throw new InvalidOperationException("Only forms under review can be approved.");

        Status = RegistrationFormSubmissionStatus.Approved;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Reject(string? notes = null)
    {
        if (Status != RegistrationFormSubmissionStatus.UnderReview)
            throw new InvalidOperationException("Only forms under review can be rejected.");

        Status = RegistrationFormSubmissionStatus.Rejected;
        ReviewNotes = notes;
        UpdatedAt = DateTime.UtcNow;
    }

    public void RequestChanges(string? notes = null)
    {
        if (Status != RegistrationFormSubmissionStatus.UnderReview)
            throw new InvalidOperationException("Only forms under review can request changes.");

        Status = RegistrationFormSubmissionStatus.ChangesRequested;
        ReviewNotes = notes;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetReviewer(Guid reviewerId)
    {
        ReviewedBy = reviewerId;
        ReviewedAt = DateTime.UtcNow;
    }
}

public enum RegistrationFormSubmissionStatus
{
    Draft = 0,
    Submitted = 1,
    UnderReview = 2,
    Approved = 3,
    Rejected = 4,
    ChangesRequested = 5
}

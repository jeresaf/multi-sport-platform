using SportsPlatform.BuildingBlocks;

namespace SportsPlatform.Modules.Reporting.Entities;

public class Report : AggregateRoot<Guid>
{
    public Guid TenantId { get; private set; }
    public Guid CreatedBy { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public ReportType Type { get; private set; }
    public ReportFormat Format { get; private set; } = ReportFormat.Pdf;
    public string Parameters { get; private set; } = "{}";
    public ReportStatus Status { get; private set; } = ReportStatus.Pending;
    public string? GeneratedUrl { get; private set; }
    public DateTime? GeneratedAt { get; private set; }
    public string? ErrorMessage { get; private set; }
    public byte[]? GeneratedContent { get; private set; }
    public string? GeneratedContentType { get; private set; }
    public string? GeneratedFileName { get; private set; }
    public new DateTime CreatedAt { get; private set; }
    public new DateTime? UpdatedAt { get; private set; }

    private Report() { }

    public Report(Guid id, Guid tenantId, Guid createdBy, string name, ReportType type) : base(id)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("Tenant is required.", nameof(tenantId));
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Name is required.", nameof(name));
        TenantId = tenantId;
        CreatedBy = createdBy;
        Name = name.Trim();
        Type = type;
        CreatedAt = DateTime.UtcNow;
    }

    public void SetDescription(string? description)
    {
        Description = description;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetFormat(ReportFormat format)
    {
        Format = format;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetParameters(string parameters)
    {
        Parameters = parameters;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkAsProcessing()
    {
        Status = ReportStatus.Processing;
        UpdatedAt = DateTime.UtcNow;
    }

    public void ResetForGeneration()
    {
        Status = ReportStatus.Pending;
        GeneratedUrl = null;
        GeneratedAt = null;
        ErrorMessage = null;
        GeneratedContent = null;
        GeneratedContentType = null;
        GeneratedFileName = null;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkAsCompleted(string generatedUrl)
    {
        Status = ReportStatus.Completed;
        GeneratedUrl = generatedUrl;
        GeneratedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkAsCompleted(string fileName, string contentType, byte[] content)
    {
        if (content.Length == 0) throw new ArgumentException("Generated content is empty.", nameof(content));
        Status = ReportStatus.Completed;
        GeneratedUrl = $"/api/reports/{Id}/download";
        GeneratedFileName = fileName;
        GeneratedContentType = contentType;
        GeneratedContent = content;
        GeneratedAt = DateTime.UtcNow;
        ErrorMessage = null;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkAsFailed(string errorMessage)
    {
        Status = ReportStatus.Failed;
        ErrorMessage = errorMessage;
        UpdatedAt = DateTime.UtcNow;
    }
}

public class ReportSchedule : Entity<Guid>
{
    public Guid ReportId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public ScheduleFrequency Frequency { get; private set; }
    public string CronExpression { get; private set; } = string.Empty;
    public DateTime? NextRunAt { get; private set; }
    public DateTime? LastRunAt { get; private set; }
    public bool IsActive { get; private set; } = true;
    public string? Recipients { get; private set; }
    public new DateTime CreatedAt { get; private set; }

    private ReportSchedule() { }

    public ReportSchedule(Guid id, Guid reportId, string name, ScheduleFrequency frequency) : base(id)
    {
        ReportId = reportId;
        Name = name;
        Frequency = frequency;
        CreatedAt = DateTime.UtcNow;
    }

    public void SetFrequency(ScheduleFrequency frequency)
    {
        Frequency = frequency;
    }

    public void SetCronExpression(string cronExpression)
    {
        CronExpression = cronExpression;
    }

    public void SetNextRunAt(DateTime? nextRunAt)
    {
        NextRunAt = nextRunAt;
    }

    public void SetLastRunAt(DateTime lastRunAt)
    {
        LastRunAt = lastRunAt;
    }

    public void SetRecipients(string? recipients)
    {
        Recipients = recipients;
    }

    public void Pause()
    {
        IsActive = false;
    }

    public void Resume()
    {
        IsActive = true;
    }
}

public enum ReportType
{
    RegistrationList = 0,
    Results = 1,
    CompetitionSummary = 2,
    AthletePerformance = 3,
    FixtureSchedule = 4,
    AuditHistory = 5,
    TeamRoster = 6,
    OfficialAssignment = 7,
    MatchSheet = 8,
    Standings = 9,
    Discipline = 10,
    ContestSheet = 11,
    TenantDataExport = 12,
    // Compatibility aliases for prototype callers created before the
    // authoritative report catalogue was defined.
    Participation = RegistrationList,
    Financial = CompetitionSummary,
    Performance = AthletePerformance,
    Attendance = FixtureSchedule,
    Custom = AuditHistory
}

public enum ReportFormat
{
    Pdf = 0,
    Excel = 1,
    Csv = 2,
    Html = 3,
    Zip = 4
}

public enum ReportStatus
{
    Pending = 0,
    Processing = 1,
    Completed = 2,
    Failed = 3
}

public enum ScheduleFrequency
{
    Daily = 0,
    Weekly = 1,
    Monthly = 2,
    Quarterly = 3,
    Yearly = 4,
    Custom = 5
}

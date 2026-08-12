using System.Text.Json;
using SportsPlatform.BuildingBlocks;
using SportsPlatform.Modules.Participants.Services;

namespace SportsPlatform.Modules.Participants.Entities;

public sealed class BulkImportJob : TenantAggregateRoot<Guid>
{
    public string Kind { get; private set; } = string.Empty;
    public string FileName { get; private set; } = string.Empty;
    public BulkImportJobStatus Status { get; private set; }
    public DuplicateHandling DuplicateHandling { get; private set; }
    public Guid CreatedBy { get; private set; }
    public Guid? RosterId { get; private set; }
    public int TotalRows { get; private set; }
    public int ImportedRows { get; private set; }
    public int DuplicateRows { get; private set; }
    public int FailedRows { get; private set; }
    public string InputJson { get; private set; } = "[]";
    public string ErrorsJson { get; private set; } = "[]";
    public DateTime? StartedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }

    private BulkImportJob() { }

    public BulkImportJob(Guid id, Guid tenantId, string kind, string fileName,
        DuplicateHandling duplicateHandling, Guid createdBy, Guid? rosterId,
        int totalRows, string inputJson, IEnumerable<ImportError>? parseErrors = null)
        : base(id, tenantId)
    {
        Kind = Required(kind, nameof(kind), 30);
        FileName = Required(fileName, nameof(fileName), 255);
        if (createdBy == Guid.Empty) throw new ArgumentException("An import actor is required.", nameof(createdBy));
        CreatedBy = createdBy;
        RosterId = rosterId;
        TotalRows = totalRows;
        InputJson = inputJson;
        ErrorsJson = JsonSerializer.Serialize(parseErrors ?? []);
        Status = BulkImportJobStatus.Queued;
        CreatedAt = DateTime.UtcNow;
    }

    public void Start() { Status = BulkImportJobStatus.Running; StartedAt = DateTime.UtcNow; UpdatedAt = DateTime.UtcNow; }

    public void Complete(BulkImportResult result)
    {
        ImportedRows = result.Imported;
        DuplicateRows = result.Duplicates;
        var errors = GetErrors().Concat(result.Errors).OrderBy(item => item.Row).ToArray();
        ErrorsJson = JsonSerializer.Serialize(errors);
        FailedRows = errors.Length;
        Status = BulkImportJobStatus.Completed;
        CompletedAt = DateTime.UtcNow;
        InputJson = "[]";
        UpdatedAt = DateTime.UtcNow;
    }

    public void Fail(string message)
    {
        var errors = GetErrors().Append(new ImportError { Row = 0, Message = message }).ToArray();
        ErrorsJson = JsonSerializer.Serialize(errors);
        FailedRows = errors.Length;
        Status = BulkImportJobStatus.Failed;
        CompletedAt = DateTime.UtcNow;
        InputJson = "[]";
        UpdatedAt = DateTime.UtcNow;
    }

    public IReadOnlyList<ImportError> GetErrors() =>
        JsonSerializer.Deserialize<ImportError[]>(ErrorsJson) ?? [];

    private static string Required(string? value, string name, int max)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length == 0) throw new ArgumentException($"{name} is required.", name);
        if (normalized.Length > max) throw new ArgumentException($"{name} cannot exceed {max} characters.", name);
        return normalized;
    }
}

public enum BulkImportJobStatus { Queued, Running, Completed, Failed }
public enum DuplicateHandling { Skip, Reject }

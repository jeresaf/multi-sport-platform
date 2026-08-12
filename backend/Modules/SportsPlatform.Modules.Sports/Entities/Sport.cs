using SportsPlatform.BuildingBlocks;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SportsPlatform.Modules.Sports.Entities;

public class Sport : AggregateRoot<Guid>
{
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public bool IsActive { get; private set; } = true;
    public string? Icon { get; private set; }
    public string SupportedParticipantShapes { get; private set; } = "[]";
    public int CurrentVersion { get; private set; } = 1;
    public new DateTime CreatedAt { get; private set; }
    public new DateTime? UpdatedAt { get; private set; }

    private readonly List<SportDefinitionVersion> _versions = new();
    public IReadOnlyCollection<SportDefinitionVersion> Versions => _versions.AsReadOnly();

    private Sport() { }

    public Sport(Guid id, string code, string name) : base(id)
    {
        Code = NormalizeCode(code);
        Name = ValidateRequired(name, nameof(name), 200);
        CreatedAt = DateTime.UtcNow;
    }

    public void UpdateDetails(
        string name,
        string? description,
        string? icon,
        IReadOnlyCollection<string>? supportedParticipantShapes = null)
    {
        Name = ValidateRequired(name, nameof(name), 200);
        Description = ValidateOptional(description, nameof(description), 1000);
        Icon = ValidateOptional(icon, nameof(icon), 200);
        if (supportedParticipantShapes is not null)
        {
            var normalized = supportedParticipantShapes
                .Select(shape => ValidateRequired(shape, "participant shape", 50).ToLowerInvariant())
                .Distinct(StringComparer.Ordinal)
                .OrderBy(shape => shape, StringComparer.Ordinal)
                .ToArray();
            if (normalized.Length == 0)
                throw new ArgumentException("At least one supported participant shape is required.", nameof(supportedParticipantShapes));
            SupportedParticipantShapes = JsonSerializer.Serialize(normalized);
        }
        UpdatedAt = DateTime.UtcNow;
    }

    public IReadOnlyList<string> GetSupportedParticipantShapes()
    {
        try
        {
            return JsonSerializer.Deserialize<string[]>(SupportedParticipantShapes) ?? Array.Empty<string>();
        }
        catch (JsonException)
        {
            return Array.Empty<string>();
        }
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

    public SportDefinitionVersion CreateNewVersion(string name, string createdBy)
    {
        var nextVersion = _versions.Count == 0
            ? Math.Max(1, CurrentVersion)
            : Math.Max(CurrentVersion, _versions.Max(item => item.VersionNumber)) + 1;
        var version = new SportDefinitionVersion(
            Guid.NewGuid(),
            Id,
            nextVersion,
            name,
            createdBy);
        _versions.Add(version);
        CurrentVersion = nextVersion;
        UpdatedAt = DateTime.UtcNow;
        return version;
    }

    public SportDefinitionVersion CloneVersion(
        Guid sourceVersionId,
        string name,
        string createdBy)
    {
        var source = _versions.SingleOrDefault(item => item.Id == sourceVersionId)
            ?? throw new InvalidOperationException(
                $"Sport definition version with ID {sourceVersionId} was not found for this sport.");
        var clone = CreateNewVersion(name, createdBy);
        clone.ConfigureContestShape(
            source.ContestShape,
            source.ParticipantType,
            source.SegmentDefinitions,
            source.EventDefinitions,
            source.MetricDefinitions,
            source.ResultMethods,
            source.RankingRules,
            source.DisplayConfiguration,
            source.ValidationConfiguration);
        return clone;
    }

    private static string NormalizeCode(string value)
    {
        var code = ValidateRequired(value, nameof(value), 50).ToUpperInvariant();
        if (!Regex.IsMatch(code, "^[A-Z0-9]+(?:-[A-Z0-9]+)*$"))
            throw new ArgumentException("Sport code must contain letters, numbers, or single hyphens.", nameof(value));
        return code;
    }

    private static string ValidateRequired(string value, string parameterName, int maxLength)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length == 0)
            throw new ArgumentException($"{parameterName} is required.", parameterName);
        if (normalized.Length > maxLength)
            throw new ArgumentException($"{parameterName} cannot exceed {maxLength} characters.", parameterName);
        return normalized;
    }

    private static string? ValidateOptional(string? value, string parameterName, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var normalized = value.Trim();
        if (normalized.Length > maxLength)
            throw new ArgumentException($"{parameterName} cannot exceed {maxLength} characters.", parameterName);
        return normalized;
    }
}

public class SportDefinitionVersion : AggregateRoot<Guid>
{
    public Guid SportId { get; private set; }
    public int VersionNumber { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public bool IsPublished { get; private set; }
    public string CreatedBy { get; private set; } = string.Empty;
    public new DateTime CreatedAt { get; private set; }
    public DateTime? PublishedAt { get; private set; }

    // Contest shape configuration
    public string ContestShape { get; private set; } = string.Empty;
    public string ParticipantType { get; private set; } = string.Empty;

    // JSON-serialized configuration
    public string SegmentDefinitions { get; private set; } = "[]";
    public string EventDefinitions { get; private set; } = "[]";
    public string MetricDefinitions { get; private set; } = "[]";
    public string ResultMethods { get; private set; } = "[]";
    public string RankingRules { get; private set; } = "[]";
    public string DisplayConfiguration { get; private set; } = "{}";
    public string ValidationConfiguration { get; private set; } = "{}";

    private SportDefinitionVersion() { }

    public SportDefinitionVersion(
        Guid id,
        Guid sportId,
        int versionNumber,
        string name,
        string createdBy) : base(id)
    {
        if (sportId == Guid.Empty) throw new ArgumentException("Sport is required.", nameof(sportId));
        if (versionNumber < 1) throw new ArgumentOutOfRangeException(nameof(versionNumber));
        SportId = sportId;
        VersionNumber = versionNumber;
        Name = Required(name, nameof(name), 200);
        CreatedBy = Required(createdBy, nameof(createdBy), 200);
        CreatedAt = DateTime.UtcNow;
    }

    public void ConfigureContestShape(
        string contestShape,
        string participantType,
        string segmentDefinitions,
        string eventDefinitions,
        string metricDefinitions,
        string resultMethods,
        string rankingRules,
        string displayConfiguration,
        string validationConfiguration)
    {
        if (IsPublished)
            throw new InvalidOperationException(
                "A published sport definition version cannot be changed. Create a new version instead.");

        ContestShape = Required(contestShape, nameof(contestShape), 100);
        ParticipantType = Required(participantType, nameof(participantType), 100);
        SegmentDefinitions = ValidJson(segmentDefinitions, nameof(segmentDefinitions), JsonValueKind.Array);
        EventDefinitions = EventFramework.NormalizeDefinitions(eventDefinitions);
        MetricDefinitions = MetricFramework.NormalizeDefinitions(metricDefinitions);
        ResultMethods = ValidJson(resultMethods, nameof(resultMethods), JsonValueKind.Array);
        RankingRules = ValidJson(rankingRules, nameof(rankingRules), JsonValueKind.Array);
        DisplayConfiguration = ValidJson(displayConfiguration, nameof(displayConfiguration), JsonValueKind.Object);
        ValidationConfiguration = ValidJson(validationConfiguration, nameof(validationConfiguration), JsonValueKind.Object);
    }

    public void Publish()
    {
        if (IsPublished) return;
        if (string.IsNullOrWhiteSpace(ContestShape) || string.IsNullOrWhiteSpace(ParticipantType))
            throw new InvalidOperationException(
                "Configure every required sport definition field before publishing.");
        IsPublished = true;
        PublishedAt = DateTime.UtcNow;
    }

    private static string Required(string value, string parameterName, int maxLength)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length == 0)
            throw new ArgumentException($"{parameterName} is required.", parameterName);
        if (normalized.Length > maxLength)
            throw new ArgumentException($"{parameterName} cannot exceed {maxLength} characters.", parameterName);
        return normalized;
    }

    private static string ValidJson(string value, string parameterName, JsonValueKind expectedKind)
    {
        try
        {
            using var document = JsonDocument.Parse(value);
            if (document.RootElement.ValueKind != expectedKind)
                throw new ArgumentException(
                    $"{parameterName} must be a JSON {expectedKind.ToString().ToLowerInvariant()}.",
                    parameterName);
            return document.RootElement.GetRawText();
        }
        catch (JsonException exception)
        {
            throw new ArgumentException($"{parameterName} must contain valid JSON.", parameterName, exception);
        }
    }
}

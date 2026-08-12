using System.Text.Json;
using System.ComponentModel.DataAnnotations.Schema;
using SportsPlatform.BuildingBlocks;

namespace SportsPlatform.Modules.Registrations.Entities;

public sealed class CompetitionEligibilityPolicy : TenantAggregateRoot<Guid>
{
    public Guid CompetitionId { get; private set; }
    public int? MinimumSquadSize { get; private set; }
    public int? MaximumSquadSize { get; private set; }
    public int? MinimumAge { get; private set; }
    public int? MaximumAge { get; private set; }
    public decimal? MinimumWeightKg { get; private set; }
    public decimal? MaximumWeightKg { get; private set; }
    public DateTime? RegistrationDeadline { get; private set; }
    public string RequiredDocumentTypesJson { get; private set; } = "[]";
    public bool RequireSquadNumbers { get; private set; }
    public bool RequireUniqueSquadNumbers { get; private set; } = true;

    private CompetitionEligibilityPolicy() { }

    public CompetitionEligibilityPolicy(Guid id, Guid tenantId, Guid competitionId)
        : base(id, tenantId)
    {
        if (competitionId == Guid.Empty) throw new ArgumentException("A competition is required.", nameof(competitionId));
        CompetitionId = competitionId;
        CreatedAt = DateTime.UtcNow;
    }

    public void Configure(
        int? minimumSquadSize, int? maximumSquadSize,
        int? minimumAge, int? maximumAge,
        decimal? minimumWeightKg, decimal? maximumWeightKg,
        DateTime? registrationDeadline, IEnumerable<string>? requiredDocumentTypes,
        bool requireSquadNumbers, bool requireUniqueSquadNumbers)
    {
        ValidateRange(minimumSquadSize, maximumSquadSize, "squad size");
        ValidateRange(minimumAge, maximumAge, "age");
        ValidateRange(minimumWeightKg, maximumWeightKg, "weight");
        if (minimumAge is < 0 || maximumAge is > 120) throw new ArgumentException("Age limits must be between 0 and 120.");
        if (minimumWeightKg is < 0 || maximumWeightKg is < 0) throw new ArgumentException("Weight limits cannot be negative.");

        MinimumSquadSize = minimumSquadSize;
        MaximumSquadSize = maximumSquadSize;
        MinimumAge = minimumAge;
        MaximumAge = maximumAge;
        MinimumWeightKg = minimumWeightKg;
        MaximumWeightKg = maximumWeightKg;
        RegistrationDeadline = registrationDeadline?.ToUniversalTime();
        RequiredDocumentTypesJson = JsonSerializer.Serialize((requiredDocumentTypes ?? [])
            .Select(value => value.Trim()).Where(value => value.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(value => value).ToArray());
        RequireSquadNumbers = requireSquadNumbers;
        RequireUniqueSquadNumbers = requireUniqueSquadNumbers;
        UpdatedAt = DateTime.UtcNow;
    }

    [NotMapped]
    public IReadOnlyList<string> RequiredDocumentTypes =>
        JsonSerializer.Deserialize<string[]>(RequiredDocumentTypesJson) ?? [];

    private static void ValidateRange<T>(T? minimum, T? maximum, string name) where T : struct, IComparable<T>
    {
        if (minimum.HasValue && maximum.HasValue && minimum.Value.CompareTo(maximum.Value) > 0)
            throw new ArgumentException($"Minimum {name} cannot exceed maximum {name}.");
    }
}

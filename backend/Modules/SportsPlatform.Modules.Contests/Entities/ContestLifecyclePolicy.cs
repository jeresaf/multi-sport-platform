using System.Text.Json;
using SportsPlatform.BuildingBlocks;

namespace SportsPlatform.Modules.Contests.Entities;

public sealed record ContestLifecycleTransition(ContestStatus From, ContestStatus To);

public sealed class ContestLifecyclePolicy : TenantAggregateRoot<Guid>
{
    public Guid SportId { get; private set; }
    public string AllowedTransitionsJson { get; private set; } = "[]";
    public new DateTime CreatedAt { get; private set; }
    public new DateTime? UpdatedAt { get; private set; }

    private ContestLifecyclePolicy() { }

    public ContestLifecyclePolicy(Guid id, Guid tenantId, Guid sportId) : base(id, tenantId)
    {
        SportId = sportId;
        CreatedAt = DateTime.UtcNow;
    }

    public void Configure(IEnumerable<ContestLifecycleTransition> transitions)
    {
        var configured = transitions.Distinct().ToArray();
        if (configured.Length == 0)
            throw new ArgumentException("At least one lifecycle transition is required.", nameof(transitions));
        if (configured.Any(item => item.From == item.To))
            throw new ArgumentException("Lifecycle transitions cannot target their current status.", nameof(transitions));
        AllowedTransitionsJson = JsonSerializer.Serialize(configured);
        UpdatedAt = DateTime.UtcNow;
    }

    public IReadOnlyDictionary<ContestStatus, IReadOnlySet<ContestStatus>> GetTransitions() =>
        (JsonSerializer.Deserialize<ContestLifecycleTransition[]>(AllowedTransitionsJson)
            ?? Array.Empty<ContestLifecycleTransition>())
        .GroupBy(item => item.From)
        .ToDictionary(
            group => group.Key,
            group => (IReadOnlySet<ContestStatus>)group.Select(item => item.To).ToHashSet());
}

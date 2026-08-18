using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SportsPlatform.Infrastructure.Persistence;
using SportsPlatform.Modules.Audit.Entities;
using SportsPlatform.Modules.Contests.Entities;

namespace SportsPlatform.Infrastructure.Services;

public sealed record ContestLifecycleState(
    Guid ContestId,
    ContestStatus Status,
    CanonicalContestStatus CanonicalStatus,
    string CanonicalStatusCode,
    string? StatusReason,
    DateTime LastChangedAt,
    IReadOnlyCollection<ContestStatus> AllowedTransitions,
    bool UsesSportPolicy);

public interface IContestLifecycleService
{
    Task<ContestLifecycleState?> GetAsync(Guid contestId, CancellationToken cancellationToken = default);
    Task<ContestLifecycleState?> TransitionAsync(Guid contestId, ContestStatus target,
        Guid actorId, string? reason, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AuditEntry>> HistoryAsync(Guid contestId, CancellationToken cancellationToken = default);
    Task<ContestLifecyclePolicy> ConfigurePolicyAsync(Guid tenantId, Guid sportId,
        IEnumerable<ContestLifecycleTransition> transitions, Guid actorId,
        CancellationToken cancellationToken = default);
    Task<ContestLifecyclePolicy?> GetPolicyAsync(Guid tenantId, Guid sportId,
        CancellationToken cancellationToken = default);
}

public sealed class ContestLifecycleService : IContestLifecycleService
{
    private static readonly IReadOnlySet<ContestStatus> ReasonRequired = new HashSet<ContestStatus>
    {
        ContestStatus.Suspended,
        ContestStatus.Corrected,
        ContestStatus.Cancelled,
        ContestStatus.Abandoned
    };

    private readonly SportsPlatformDbContext _database;
    private readonly IContestCheckInWorkflowService _checkInWorkflow;

    public ContestLifecycleService(
        SportsPlatformDbContext database,
        IContestCheckInWorkflowService checkInWorkflow)
    {
        _database = database;
        _checkInWorkflow = checkInWorkflow;
    }

    public async Task<ContestLifecycleState?> GetAsync(
        Guid contestId,
        CancellationToken cancellationToken = default)
    {
        var contest = await _database.Contests.FindAsync(new object[] { contestId }, cancellationToken);
        if (contest is null) return null;
        var policy = await GetPolicyAsync(contest.TenantId, contest.SportId, cancellationToken);
        return State(contest, policy);
    }

    public async Task<ContestLifecycleState?> TransitionAsync(
        Guid contestId,
        ContestStatus target,
        Guid actorId,
        string? reason,
        CancellationToken cancellationToken = default)
    {
        var contest = await _database.Contests.FindAsync(new object[] { contestId }, cancellationToken);
        if (contest is null) return null;
        if (ReasonRequired.Contains(target) && string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException($"A reason is required when transitioning to {target}.", nameof(reason));
        if (target == ContestStatus.Scheduled
            && (string.IsNullOrWhiteSpace(contest.Venue) || !contest.StartTime.HasValue))
            throw new InvalidOperationException("Schedule the contest with a venue and start time first.");
        if (target == ContestStatus.InProgress)
            await _checkInWorkflow.EnsureCanStartAsync(contestId, cancellationToken);

        var policy = await GetPolicyAsync(contest.TenantId, contest.SportId, cancellationToken);
        var original = contest.Status;
        contest.TransitionTo(target, policy?.GetTransitions(), actorId, reason);
        _database.AuditEntries.Add(new AuditEntry(
            Guid.NewGuid(), contest.TenantId, actorId,
            "ContestStatusChanged", "Contest", contest.Id.ToString(),
            JsonSerializer.Serialize(new { status = original.ToString() }),
            JsonSerializer.Serialize(new { status = target.ToString() }),
            string.IsNullOrWhiteSpace(reason) ? null : reason.Trim()));
        await _database.SaveChangesAsync(cancellationToken);
        return State(contest, policy);
    }

    public async Task<IReadOnlyList<AuditEntry>> HistoryAsync(
        Guid contestId,
        CancellationToken cancellationToken = default) =>
        await _database.AuditEntries
            .Where(item => item.EntityType == "Contest"
                && item.EntityId == contestId.ToString()
                && item.Action == "ContestStatusChanged")
            .OrderBy(item => item.Timestamp)
            .ToArrayAsync(cancellationToken);

    public async Task<ContestLifecyclePolicy> ConfigurePolicyAsync(
        Guid tenantId,
        Guid sportId,
        IEnumerable<ContestLifecycleTransition> transitions,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        if (!await _database.Sports.AnyAsync(item => item.Id == sportId, cancellationToken))
            throw new KeyNotFoundException("The sport was not found.");
        var policy = await GetPolicyAsync(tenantId, sportId, cancellationToken);
        if (policy is null)
        {
            policy = new ContestLifecyclePolicy(Guid.NewGuid(), tenantId, sportId);
            _database.ContestLifecyclePolicies.Add(policy);
        }
        policy.Configure(transitions);
        _database.AuditEntries.Add(new AuditEntry(
            Guid.NewGuid(), tenantId, actorId,
            "ContestLifecyclePolicyConfigured", "Sport", sportId.ToString(),
            null, policy.AllowedTransitionsJson, null));
        await _database.SaveChangesAsync(cancellationToken);
        return policy;
    }

    public Task<ContestLifecyclePolicy?> GetPolicyAsync(
        Guid tenantId,
        Guid sportId,
        CancellationToken cancellationToken = default) =>
        _database.ContestLifecyclePolicies.SingleOrDefaultAsync(
            item => item.TenantId == tenantId && item.SportId == sportId,
            cancellationToken);

    private static ContestLifecycleState State(
        Contest contest,
        ContestLifecyclePolicy? policy) => new(
        contest.Id,
        contest.Status,
        contest.CanonicalStatus,
        contest.CanonicalStatus.ToCode(),
        contest.StatusReason,
        contest.CanonicalStatusChangedAt,
        contest.AllowedTransitions(policy?.GetTransitions()),
        policy is not null);
}

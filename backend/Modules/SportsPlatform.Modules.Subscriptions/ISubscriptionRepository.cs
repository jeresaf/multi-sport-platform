using SportsPlatform.Modules.Subscriptions.Entities;

namespace SportsPlatform.Modules.Subscriptions;

public interface ISubscriptionRepository
{
    Task<Subscription?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Subscription?> GetByTenantIdAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Subscription>> GetExpiringSubscriptionsAsync(DateTime threshold, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Subscription>> GetGracePeriodExpiredSubscriptionsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Subscription>> GetActiveSubscriptionsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Subscription>> GetSubscriptionsByStatusAsync(SubscriptionStatus status, CancellationToken cancellationToken = default);
    Task<SubscriptionPlan?> GetPlanByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<SubscriptionPlan?> GetPlanByCodeAsync(string code, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SubscriptionPlan>> GetActivePlansAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SubscriptionPlan>> GetAllPlansAsync(CancellationToken cancellationToken = default);
    void Add(Subscription subscription);
    void Update(Subscription subscription);
    void AddPlan(SubscriptionPlan plan);
    void UpdatePlan(SubscriptionPlan plan);
    Task<bool> SaveChangesAsync(CancellationToken cancellationToken = default);
}
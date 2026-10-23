using System.Text.Json.Serialization;
using SportsPlatform.Modules.Tenancy.Domain.Subscriptions;

namespace SportsPlatform.Modules.Tenancy.Features.Subscriptions.AssignPeriod;

/// <summary>
/// A new period: <c>planCode</c> from <c>startsOn</c> to <c>endsOn</c> (both included, the tenant's dates). The windows
/// default to 30 expiring and 14 grace days.
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AssignPeriodRequest(
    string? PlanCode,
    SubscriptionKind? Kind,
    DateOnly? StartsOn,
    DateOnly? EndsOn,
    int? ExpiringWindowDays = null,
    int? GraceDays = null);

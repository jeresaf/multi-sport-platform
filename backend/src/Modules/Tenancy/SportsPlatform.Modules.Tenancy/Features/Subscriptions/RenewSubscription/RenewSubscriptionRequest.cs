using System.Text.Json.Serialization;
using SportsPlatform.Modules.Tenancy.Contracts.Subscriptions;

namespace SportsPlatform.Modules.Tenancy.Features.Subscriptions.RenewSubscription;

/// <summary>
/// A renewal up to <c>endsOn</c> (included). The plan, kind and windows not sent are the latest period's; a lower plan
/// is chosen here.
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RenewSubscriptionRequest(
    DateOnly? EndsOn,
    string? PlanCode = null,
    SubscriptionKind? Kind = null,
    int? ExpiringWindowDays = null,
    int? GraceDays = null);

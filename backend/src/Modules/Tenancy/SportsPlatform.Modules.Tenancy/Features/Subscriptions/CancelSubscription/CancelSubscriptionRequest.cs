using System.Text.Json.Serialization;

namespace SportsPlatform.Modules.Tenancy.Features.Subscriptions.CancelSubscription;

/// <summary>
/// The body of <c>subscription.cancel</c> (audit <c>REQUIRED_WITH_REASON</c>). The reason is kept permanently on the
/// cancelled periods and in the audit trail and is not redacted: it must not contain personal data.
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CancelSubscriptionRequest(string? Reason);

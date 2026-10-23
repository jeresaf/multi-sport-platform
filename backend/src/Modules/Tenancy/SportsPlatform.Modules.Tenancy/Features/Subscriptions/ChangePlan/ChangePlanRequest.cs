using System.Text.Json.Serialization;

namespace SportsPlatform.Modules.Tenancy.Features.Subscriptions.ChangePlan;

/// <summary>The higher plan to move to now.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ChangePlanRequest(string? PlanCode);

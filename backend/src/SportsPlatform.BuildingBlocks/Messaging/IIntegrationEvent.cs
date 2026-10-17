namespace SportsPlatform.BuildingBlocks.Messaging;

/// <summary>
/// A fact another module may react to, delivered through the producer's outbox (docs/architecture/events-and-jobs.md).
/// Declare it in the producer's Contracts as a versioned record nested in a static class named in the past tense:
/// <c>public static class ResultApproved { public sealed record V1(...) : IIntegrationEvent; }</c>. Payloads carry IDs and
/// display names only; changes are additive, and a breaking change is a new version.
/// </summary>
public interface IIntegrationEvent;

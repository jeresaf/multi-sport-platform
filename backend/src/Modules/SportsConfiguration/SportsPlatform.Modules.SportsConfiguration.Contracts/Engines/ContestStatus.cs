namespace SportsPlatform.Modules.SportsConfiguration.Contracts.Engines;

/// <summary>
/// The stored contest status (docs/domain/state-machines.md, "Contest status"), which Match Operations owns; engines
/// read it to decide whether an event may be recorded. It is never combined with the phase.
/// </summary>
public enum ContestStatus
{
    NotStarted,
    Delayed,
    Postponed,
    Cancelled,
    Live,
    Interrupted,
    Suspended,
    Abandoned,
    Ended,
    Closed,
}

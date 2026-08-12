using Microsoft.EntityFrameworkCore;
using SportsPlatform.Infrastructure.Persistence;
using SportsPlatform.Modules.Contests.Entities;
using SportsPlatform.Modules.LiveScoring;
using SportsPlatform.Modules.LiveScoring.Entities;

namespace SportsPlatform.Infrastructure.Services;

public sealed record CheckInParticipantState(
    Guid ParticipantId,
    string ParticipantType,
    ContestParticipantStatus Status);

public sealed record CheckInOfficialState(
    Guid OfficialId,
    string Role,
    string? DisplayName,
    bool IsConfirmed);

public sealed record ContestReadinessState(
    Guid ContestId,
    ContestStatus ContestStatus,
    bool IsOpen,
    int TotalParticipants,
    int CheckedIn,
    int Withdrawn,
    int Disqualified,
    int Pending,
    bool ParticipantCheckInPassed,
    bool RosterConfirmationPassed,
    bool OfficialConfirmationPassed,
    bool VenueConfirmationPassed,
    bool CanStart,
    bool IsOverridden,
    string? OverrideReason,
    IReadOnlyList<string> BlockingChecks,
    IReadOnlyList<CheckInParticipantState> Participants,
    IReadOnlyList<CheckInOfficialState> Officials);

public interface IContestCheckInWorkflowService
{
    Task<ContestReadinessState?> GetAsync(Guid contestId, CancellationToken cancellationToken = default);
    Task<ContestReadinessState> OpenAsync(Guid contestId, Guid actorId, CancellationToken cancellationToken = default);
    Task<ContestReadinessState> CheckInParticipantAsync(Guid contestId, Guid participantId, Guid actorId, CancellationToken cancellationToken = default);
    Task<ContestReadinessState> WithdrawParticipantAsync(Guid contestId, Guid participantId, Guid actorId, string reason, CancellationToken cancellationToken = default);
    Task<ContestReadinessState> DisqualifyParticipantAsync(Guid contestId, Guid participantId, Guid actorId, string reason, CancellationToken cancellationToken = default);
    Task<ContestReadinessState> ConfirmRosterAsync(Guid contestId, Guid actorId, CancellationToken cancellationToken = default);
    Task<ContestReadinessState> ConfirmOfficialAsync(Guid contestId, Guid officialId, Guid actorId, CancellationToken cancellationToken = default);
    Task<ContestReadinessState> ConfirmVenueAsync(Guid contestId, Guid actorId, CancellationToken cancellationToken = default);
    Task<ContestReadinessState> CompleteAsync(Guid contestId, Guid actorId, CancellationToken cancellationToken = default);
    Task<ContestReadinessState> OverrideAsync(Guid contestId, Guid actorId, string reason, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ContestCheckInHistory>> HistoryAsync(Guid contestId, CancellationToken cancellationToken = default);
    Task EnsureCanStartAsync(Guid contestId, CancellationToken cancellationToken = default);
}

public sealed class ContestCheckInWorkflowService : IContestCheckInWorkflowService
{
    private readonly SportsPlatformDbContext _database;
    private readonly ContestCheckInService _checkIn;

    public ContestCheckInWorkflowService(
        SportsPlatformDbContext database,
        ContestCheckInService checkIn)
    {
        _database = database;
        _checkIn = checkIn;
    }

    public async Task<ContestReadinessState?> GetAsync(
        Guid contestId,
        CancellationToken cancellationToken = default)
    {
        var contest = await LoadContestAsync(contestId, cancellationToken);
        if (contest is null) return null;
        var readiness = await _database.ContestReadiness
            .SingleOrDefaultAsync(item => item.ContestId == contestId, cancellationToken);
        return State(contest, readiness);
    }

    public async Task<ContestReadinessState> OpenAsync(
        Guid contestId,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        var contest = await RequiredContestAsync(contestId, cancellationToken);
        var readiness = await GetOrCreateReadinessAsync(contest, cancellationToken);
        if (contest.Status == ContestStatus.Scheduled)
        {
            _checkIn.OpenCheckIn(contest);
            AddHistory(contest, "CheckInOpened", "Contest", contest.Id,
                ContestStatus.Scheduled.ToString(), ContestStatus.CheckInOpen.ToString(), actorId);
            await _database.SaveChangesAsync(cancellationToken);
        }
        else if (contest.Status != ContestStatus.CheckInOpen)
        {
            throw new InvalidOperationException(
                $"Check-in cannot be opened while the contest is {contest.Status}.");
        }
        return State(contest, readiness);
    }

    public async Task<ContestReadinessState> CheckInParticipantAsync(
        Guid contestId,
        Guid participantId,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        var (contest, readiness) = await LoadForCheckInAsync(
            contestId, actorId, cancellationToken);
        var participant = RequiredParticipant(contest, participantId);
        var previous = participant.Status;
        var result = _checkIn.CheckInParticipant(contest, participantId, actorId);
        if (!result.Success) throw new InvalidOperationException(result.Message);
        if (participant.Status != previous)
        {
            AddHistory(contest, "ParticipantCheckedIn", participant.ParticipantType,
                participantId, previous.ToString(), participant.Status.ToString(), actorId);
            await _database.SaveChangesAsync(cancellationToken);
        }
        return State(contest, readiness);
    }

    public Task<ContestReadinessState> WithdrawParticipantAsync(
        Guid contestId, Guid participantId, Guid actorId, string reason,
        CancellationToken cancellationToken = default) =>
        ChangeParticipantAsync(contestId, participantId, actorId, reason,
            "ParticipantWithdrawn", (contest, id) => _checkIn.WithdrawParticipant(contest, id, reason),
            cancellationToken);

    public Task<ContestReadinessState> DisqualifyParticipantAsync(
        Guid contestId, Guid participantId, Guid actorId, string reason,
        CancellationToken cancellationToken = default) =>
        ChangeParticipantAsync(contestId, participantId, actorId, reason,
            "ParticipantDisqualified", (contest, id) => _checkIn.DisqualifyParticipant(contest, id, reason),
            cancellationToken);

    public async Task<ContestReadinessState> ConfirmRosterAsync(
        Guid contestId, Guid actorId, CancellationToken cancellationToken = default)
    {
        var (contest, readiness) = await OpenForActionAsync(contestId, actorId, cancellationToken);
        if (!readiness.RosterConfirmed)
        {
            readiness.ConfirmRoster();
            AddHistory(contest, "RosterConfirmed", "Roster", null, "Pending", "Confirmed", actorId);
            await _database.SaveChangesAsync(cancellationToken);
        }
        return State(contest, readiness);
    }

    public async Task<ContestReadinessState> ConfirmOfficialAsync(
        Guid contestId, Guid officialId, Guid actorId,
        CancellationToken cancellationToken = default)
    {
        var (contest, readiness) = await OpenForActionAsync(contestId, actorId, cancellationToken);
        var official = contest.Officials.SingleOrDefault(item => item.OfficialId == officialId)
            ?? throw new KeyNotFoundException("The official is not assigned to this contest.");
        if (!official.IsConfirmed)
        {
            official.Confirm();
            AddHistory(contest, "OfficialConfirmed", "Official", officialId,
                "Pending", "Confirmed", actorId);
            await _database.SaveChangesAsync(cancellationToken);
        }
        return State(contest, readiness);
    }

    public async Task<ContestReadinessState> ConfirmVenueAsync(
        Guid contestId, Guid actorId, CancellationToken cancellationToken = default)
    {
        var (contest, readiness) = await OpenForActionAsync(contestId, actorId, cancellationToken);
        if (string.IsNullOrWhiteSpace(contest.Venue))
            throw new InvalidOperationException("The contest does not have a scheduled venue.");
        if (!readiness.VenueConfirmed)
        {
            readiness.ConfirmVenue();
            AddHistory(contest, "VenueConfirmed", "Venue", null,
                "Pending", contest.Venue, actorId);
            await _database.SaveChangesAsync(cancellationToken);
        }
        return State(contest, readiness);
    }

    public async Task<ContestReadinessState> CompleteAsync(
        Guid contestId, Guid actorId, CancellationToken cancellationToken = default)
    {
        var contest = await RequiredContestAsync(contestId, cancellationToken);
        var readiness = await RequiredReadinessAsync(contestId, cancellationToken);
        var state = State(contest, readiness);
        if (!state.CanStart)
            throw new InvalidOperationException(
                $"Readiness checks are incomplete: {string.Join(", ", state.BlockingChecks)}.");
        if (contest.Status == ContestStatus.CheckInOpen)
        {
            contest.MarkReady();
            AddHistory(contest, "CheckInCompleted", "Contest", contest.Id,
                ContestStatus.CheckInOpen.ToString(), ContestStatus.Ready.ToString(), actorId,
                readiness.OverrideReason, readiness.OverrideBy.HasValue);
            await _database.SaveChangesAsync(cancellationToken);
        }
        return State(contest, readiness);
    }

    public async Task<ContestReadinessState> OverrideAsync(
        Guid contestId, Guid actorId, string reason,
        CancellationToken cancellationToken = default)
    {
        var (contest, readiness) = await OpenForActionAsync(contestId, actorId, cancellationToken);
        var before = State(contest, readiness);
        readiness.Override(actorId, reason);
        AddHistory(contest, "ReadinessOverridden", "Contest", contest.Id,
            string.Join(", ", before.BlockingChecks), "Override approved", actorId, reason, true);
        if (contest.Status == ContestStatus.CheckInOpen)
        {
            contest.MarkReady();
            AddHistory(contest, "CheckInCompleted", "Contest", contest.Id,
                ContestStatus.CheckInOpen.ToString(), ContestStatus.Ready.ToString(), actorId,
                reason, true);
        }
        await _database.SaveChangesAsync(cancellationToken);
        return State(contest, readiness);
    }

    public async Task<IReadOnlyList<ContestCheckInHistory>> HistoryAsync(
        Guid contestId, CancellationToken cancellationToken = default) =>
        await _database.ContestCheckInHistory
            .Where(item => item.ContestId == contestId)
            .OrderBy(item => item.OccurredAt)
            .ThenBy(item => item.Id)
            .ToArrayAsync(cancellationToken);

    public async Task EnsureCanStartAsync(
        Guid contestId, CancellationToken cancellationToken = default)
    {
        var readiness = await _database.ContestReadiness
            .SingleOrDefaultAsync(item => item.ContestId == contestId, cancellationToken);
        if (readiness is null) return;
        var contest = await RequiredContestAsync(contestId, cancellationToken);
        var state = State(contest, readiness);
        if (!state.CanStart || contest.Status != ContestStatus.Ready)
            throw new InvalidOperationException(
                $"Contest check-in must be completed before start: {string.Join(", ", state.BlockingChecks)}.");
    }

    private async Task<ContestReadinessState> ChangeParticipantAsync(
        Guid contestId,
        Guid participantId,
        Guid actorId,
        string reason,
        string action,
        Func<Contest, Guid, CheckInResult> change,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("A reason is required.", nameof(reason));
        var (contest, readiness) = await LoadForParticipantStatusChangeAsync(
            contestId, actorId, cancellationToken);
        var participant = RequiredParticipant(contest, participantId);
        var previous = participant.Status;
        var result = change(contest, participantId);
        if (!result.Success) throw new InvalidOperationException(result.Message);
        if (participant.Status != previous)
        {
            AddHistory(contest, action, participant.ParticipantType, participantId,
                previous.ToString(), participant.Status.ToString(), actorId, reason);
            await _database.SaveChangesAsync(cancellationToken);
        }
        return State(contest, readiness);
    }

    private async Task<(Contest Contest, ContestReadiness Readiness)> OpenForActionAsync(
        Guid contestId, Guid actorId, CancellationToken cancellationToken)
    {
        var contest = await RequiredContestAsync(contestId, cancellationToken);
        var readiness = await GetOrCreateReadinessAsync(contest, cancellationToken);
        if (contest.Status == ContestStatus.Scheduled)
        {
            _checkIn.OpenCheckIn(contest);
            AddHistory(contest, "CheckInOpened", "Contest", contest.Id,
                ContestStatus.Scheduled.ToString(), ContestStatus.CheckInOpen.ToString(), actorId);
            await _database.SaveChangesAsync(cancellationToken);
        }
        if (contest.Status != ContestStatus.CheckInOpen)
            throw new InvalidOperationException(
                $"Check-in changes are not allowed while the contest is {contest.Status}.");
        return (contest, readiness);
    }

    private async Task<(Contest Contest, ContestReadiness? Readiness)>
        LoadForParticipantStatusChangeAsync(
            Guid contestId, Guid actorId, CancellationToken cancellationToken)
    {
        var contest = await RequiredContestAsync(contestId, cancellationToken);
        if (contest.Status is ContestStatus.Completed or ContestStatus.PendingApproval
            or ContestStatus.Approved or ContestStatus.Published
            or ContestStatus.Cancelled or ContestStatus.Abandoned)
            throw new InvalidOperationException(
                $"Participant status cannot change while the contest is {contest.Status}.");
        var readiness = await _database.ContestReadiness
            .SingleOrDefaultAsync(item => item.ContestId == contestId, cancellationToken);
        return (contest, readiness);
    }

    private async Task<(Contest Contest, ContestReadiness? Readiness)> LoadForCheckInAsync(
        Guid contestId, Guid actorId, CancellationToken cancellationToken)
    {
        var contest = await RequiredContestAsync(contestId, cancellationToken);
        if (contest.Status == ContestStatus.Draft)
            return (contest, null); // Backward-compatible pre-schedule registration check-in.
        var readiness = await GetOrCreateReadinessAsync(contest, cancellationToken);
        if (contest.Status == ContestStatus.Scheduled)
        {
            _checkIn.OpenCheckIn(contest);
            AddHistory(contest, "CheckInOpened", "Contest", contest.Id,
                ContestStatus.Scheduled.ToString(), ContestStatus.CheckInOpen.ToString(), actorId);
            await _database.SaveChangesAsync(cancellationToken);
        }
        if (contest.Status != ContestStatus.CheckInOpen)
            throw new InvalidOperationException(
                $"Check-in changes are not allowed while the contest is {contest.Status}.");
        return (contest, readiness);
    }

    private async Task<ContestReadiness> GetOrCreateReadinessAsync(
        Contest contest, CancellationToken cancellationToken)
    {
        var readiness = await _database.ContestReadiness
            .SingleOrDefaultAsync(item => item.ContestId == contest.Id, cancellationToken);
        if (readiness is not null) return readiness;
        readiness = new ContestReadiness(
            Guid.NewGuid(), contest.TenantId, contest.Id,
            requireParticipantCheckIn: true,
            requireRosterConfirmation: contest.Participants.Any(item =>
                item.ParticipantType.Equals("Team", StringComparison.OrdinalIgnoreCase)),
            requireOfficialConfirmation: contest.Officials.Count > 0,
            requireVenueConfirmation: true);
        _database.ContestReadiness.Add(readiness);
        return readiness;
    }

    private async Task<ContestReadiness> RequiredReadinessAsync(
        Guid contestId, CancellationToken cancellationToken) =>
        await _database.ContestReadiness.SingleOrDefaultAsync(
            item => item.ContestId == contestId, cancellationToken)
        ?? throw new InvalidOperationException("Contest check-in has not been opened.");

    private Task<Contest?> LoadContestAsync(Guid contestId, CancellationToken cancellationToken) =>
        _database.Contests
            .Include(item => item.Participants)
            .Include(item => item.Officials)
            .SingleOrDefaultAsync(item => item.Id == contestId, cancellationToken);

    private async Task<Contest> RequiredContestAsync(
        Guid contestId, CancellationToken cancellationToken) =>
        await LoadContestAsync(contestId, cancellationToken)
            ?? throw new KeyNotFoundException("The contest was not found.");

    private static ContestParticipant RequiredParticipant(Contest contest, Guid participantId) =>
        contest.Participants.SingleOrDefault(item => item.ParticipantId == participantId)
            ?? throw new KeyNotFoundException("The participant is not entered in this contest.");

    private void AddHistory(
        Contest contest, string action, string subjectType, Guid? subjectId,
        string? previous, string? next, Guid actorId, string? reason = null,
        bool isOverride = false) =>
        _database.ContestCheckInHistory.Add(new ContestCheckInHistory(
            Guid.NewGuid(), contest.TenantId, contest.Id, action, subjectType,
            subjectId, previous, next, actorId, reason, isOverride));

    private static ContestReadinessState State(Contest contest, ContestReadiness? readiness)
    {
        var participants = contest.Participants.OrderBy(item => item.SortOrder).ToArray();
        var checkedIn = participants.Count(item => item.Status == ContestParticipantStatus.CheckedIn);
        var withdrawn = participants.Count(item => item.Status == ContestParticipantStatus.Withdrawn);
        var disqualified = participants.Count(item => item.Status == ContestParticipantStatus.Disqualified);
        var pending = participants.Count(item => item.Status == ContestParticipantStatus.Entered);
        var participantPassed = readiness is null || !readiness.RequireParticipantCheckIn
            || (pending == 0 && checkedIn > 0);
        var rosterPassed = readiness is null || !readiness.RequireRosterConfirmation
            || readiness.RosterConfirmed;
        var officialPassed = readiness is null || !readiness.RequireOfficialConfirmation
            || (contest.Officials.Count > 0 && contest.Officials.All(item => item.IsConfirmed));
        var venuePassed = readiness is null || !readiness.RequireVenueConfirmation
            || (readiness.VenueConfirmed && !string.IsNullOrWhiteSpace(contest.Venue));
        var blockers = new List<string>();
        if (!participantPassed) blockers.Add("participant check-in");
        if (!rosterPassed) blockers.Add("roster confirmation");
        if (!officialPassed) blockers.Add("official confirmation");
        if (!venuePassed) blockers.Add("venue confirmation");
        var overridden = readiness?.OverrideBy.HasValue == true;
        return new ContestReadinessState(
            contest.Id, contest.Status, readiness is not null, participants.Length, checkedIn, withdrawn,
            disqualified, pending, participantPassed, rosterPassed, officialPassed,
            venuePassed, readiness is not null && (blockers.Count == 0 || overridden), overridden,
            readiness?.OverrideReason, blockers,
            participants.Select(item => new CheckInParticipantState(
                item.ParticipantId, item.ParticipantType, item.Status)).ToArray(),
            contest.Officials.Select(item => new CheckInOfficialState(
                item.OfficialId, item.Role, item.DisplayName, item.IsConfirmed)).ToArray());
    }
}

using SportsPlatform.Modules.Contests.Entities;

namespace SportsPlatform.Modules.LiveScoring;

/// <summary>
/// Service for managing contest check-in operations.
/// </summary>
public class ContestCheckInService
{
    /// <summary>
    /// Opens check-in for a contest.
    /// </summary>
    public void OpenCheckIn(Contest contest)
    {
        if (contest.Status != ContestStatus.Scheduled)
        {
            throw new InvalidOperationException(
                $"Cannot open check-in for contest in status: {contest.Status}");
        }

        contest.OpenCheckIn();
    }

    /// <summary>
    /// Checks in a participant to a contest.
    /// </summary>
    public CheckInResult CheckInParticipant(Contest contest, Guid participantId, Guid? checkedInBy = null)
    {
        var participant = contest.Participants.FirstOrDefault(p => p.ParticipantId == participantId);
        
        if (participant == null)
        {
            return new CheckInResult(false, "Participant not found in contest", null);
        }

        if (participant.Status == ContestParticipantStatus.CheckedIn)
        {
            return new CheckInResult(true, "Participant is already checked in", participant);
        }

        if (participant.Status == ContestParticipantStatus.Withdrawn)
        {
            return new CheckInResult(false, "Participant has withdrawn from contest", participant);
        }

        if (participant.Status == ContestParticipantStatus.Disqualified)
        {
            return new CheckInResult(false, "Participant is disqualified", participant);
        }

        participant.CheckIn();
        return new CheckInResult(true, "Participant checked in successfully", participant);
    }

    /// <summary>
    /// Withdraws a participant from a contest.
    /// </summary>
    public CheckInResult WithdrawParticipant(Contest contest, Guid participantId, string? reason = null)
    {
        var participant = contest.Participants.FirstOrDefault(p => p.ParticipantId == participantId);
        
        if (participant == null)
        {
            return new CheckInResult(false, "Participant not found in contest", null);
        }

        if (participant.Status == ContestParticipantStatus.Withdrawn)
        {
            return new CheckInResult(false, "Participant is already withdrawn", participant);
        }

        if (participant.Status == ContestParticipantStatus.Disqualified)
        {
            return new CheckInResult(false, "Cannot withdraw a disqualified participant", participant);
        }

        participant.Withdraw();
        return new CheckInResult(true, $"Participant withdrawn: {reason ?? "No reason provided"}", participant);
    }

    /// <summary>
    /// Disqualifies a participant from a contest.
    /// </summary>
    public CheckInResult DisqualifyParticipant(Contest contest, Guid participantId, string? reason = null)
    {
        var participant = contest.Participants.FirstOrDefault(p => p.ParticipantId == participantId);
        
        if (participant == null)
        {
            return new CheckInResult(false, "Participant not found in contest", null);
        }

        if (participant.Status == ContestParticipantStatus.Disqualified)
        {
            return new CheckInResult(false, "Participant is already disqualified", participant);
        }

        participant.Disqualify();
        return new CheckInResult(true, $"Participant disqualified: {reason ?? "No reason provided"}", participant);
    }

    /// <summary>
    /// Gets the check-in status for a contest.
    /// </summary>
    public ContestCheckInStatus GetCheckInStatus(Contest contest)
    {
        var participants = contest.Participants.ToList();
        var checkedIn = participants.Count(p => p.Status == ContestParticipantStatus.CheckedIn);
        var withdrawn = participants.Count(p => p.Status == ContestParticipantStatus.Withdrawn);
        var disqualified = participants.Count(p => p.Status == ContestParticipantStatus.Disqualified);
        var entered = participants.Count(p => p.Status == ContestParticipantStatus.Entered);

        var allCheckedIn = entered == 0 && checkedIn > 0;
        var minimumMet = checkedIn >= 2; // At least 2 participants needed

        return new ContestCheckInStatus(
            TotalParticipants: participants.Count,
            CheckedIn: checkedIn,
            Withdrawn: withdrawn,
            Disqualified: disqualified,
            Pending: entered,
            AllCheckedIn: allCheckedIn,
            MinimumMet: minimumMet,
            CanStart: allCheckedIn && minimumMet);
    }

    /// <summary>
    /// Validates if a contest is ready to start based on check-in status.
    /// </summary>
    public (bool IsReady, string? Message) ValidateReadyToStart(Contest contest)
    {
        var status = GetCheckInStatus(contest);

        if (status.TotalParticipants == 0)
        {
            return (false, "No participants in contest");
        }

        if (status.Pending > 0)
        {
            return (false, $"{status.Pending} participant(s) have not checked in");
        }

        if (!status.MinimumMet)
        {
            return (false, "Not enough participants checked in to start");
        }

        if (status.Disqualified == status.TotalParticipants)
        {
            return (false, "All participants have been disqualified");
        }

        return (true, "Contest is ready to start");
    }

    /// <summary>
    /// Closes check-in and transitions contest to ready status.
    /// </summary>
    public void CloseCheckIn(Contest contest)
    {
        if (contest.Status != ContestStatus.CheckInOpen)
        {
            throw new InvalidOperationException(
                $"Cannot close check-in for contest in status: {contest.Status}");
        }

        var readiness = ValidateReadyToStart(contest);
        if (!readiness.IsReady)
            throw new InvalidOperationException(readiness.Message);
        contest.MarkReady();
    }
}

/// <summary>
/// Result of a check-in operation.
/// </summary>
public record CheckInResult(
    bool Success,
    string Message,
    ContestParticipant? Participant);

/// <summary>
/// Summary of check-in status for a contest.
/// </summary>
public record ContestCheckInStatus(
    int TotalParticipants,
    int CheckedIn,
    int Withdrawn,
    int Disqualified,
    int Pending,
    bool AllCheckedIn,
    bool MinimumMet,
    bool CanStart);

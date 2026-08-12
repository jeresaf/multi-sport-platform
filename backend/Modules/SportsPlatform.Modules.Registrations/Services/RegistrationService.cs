using SportsPlatform.BuildingBlocks;
using SportsPlatform.Modules.Registrations.Entities;

namespace SportsPlatform.Modules.Registrations.Services;

public interface IRegistrationService
{
    // Basic CRUD
    Task<Registration> CreateRegistrationAsync(Guid contestId, Guid participantId, RegistrationType type, CancellationToken cancellationToken = default);
    Task<Registration> CreateCompetitionRegistrationAsync(Guid competitionId, Guid participantId, RegistrationType type, CancellationToken cancellationToken = default);
    Task<Registration?> GetRegistrationAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<Registration>> GetRegistrationsByContestAsync(Guid contestId, CancellationToken cancellationToken = default);
    Task<IEnumerable<Registration>> GetRegistrationsByCompetitionAsync(Guid competitionId, CancellationToken cancellationToken = default);
    Task<IEnumerable<Registration>> GetRegistrationsByParticipantAsync(Guid participantId, CancellationToken cancellationToken = default);
    
    // Workflow operations
    Task<Registration> SubmitRegistrationAsync(Guid id, Guid? submittedBy = null, CancellationToken cancellationToken = default);
    Task<Registration> StartReviewAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Registration> ApproveRegistrationAsync(Guid id, Guid reviewerId, string? notes = null, CancellationToken cancellationToken = default);
    Task<Registration> RejectRegistrationAsync(Guid id, Guid reviewerId, string? reason = null, CancellationToken cancellationToken = default);
    Task<Registration> WaitlistRegistrationAsync(Guid id, Guid reviewerId, string? notes = null, CancellationToken cancellationToken = default);
    Task<Registration> ConfirmRegistrationAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Registration> WithdrawRegistrationAsync(Guid id, string? reason = null, CancellationToken cancellationToken = default);
    Task<Registration> CancelRegistrationAsync(Guid id, string? reason = null, CancellationToken cancellationToken = default);
    Task<Registration> MarkAsNoShowAsync(Guid id, CancellationToken cancellationToken = default);
    
    // Statistics
    Task<int> GetConfirmedCountAsync(Guid contestId, CancellationToken cancellationToken = default);
    Task<int> GetPendingCountAsync(Guid contestId, CancellationToken cancellationToken = default);
    Task<int> GetSubmittedCountAsync(Guid contestId, CancellationToken cancellationToken = default);
    Task<RegistrationStats> GetRegistrationStatsAsync(Guid contestId, CancellationToken cancellationToken = default);
}

public class RegistrationStats
{
    public int Total { get; set; }
    public int Draft { get; set; }
    public int Submitted { get; set; }
    public int UnderReview { get; set; }
    public int Approved { get; set; }
    public int Rejected { get; set; }
    public int Waitlisted { get; set; }
    public int Withdrawn { get; set; }
    public int Confirmed { get; set; }
    public int Cancelled { get; set; }
    public int NoShow { get; set; }
}

public class RegistrationService : IRegistrationService
{
    private readonly IRepository<Registration> _registrationRepository;
    private readonly IUnitOfWork _unitOfWork;

    public RegistrationService(IRepository<Registration> registrationRepository, IUnitOfWork unitOfWork)
    {
        _registrationRepository = registrationRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Registration> CreateRegistrationAsync(
        Guid contestId, 
        Guid participantId, 
        RegistrationType type, 
        CancellationToken cancellationToken = default)
    {
        var registration = new Registration(
            Guid.NewGuid(),
            contestId,
            participantId,
            type
        );

        await _registrationRepository.AddAsync(registration, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return registration;
    }

    public async Task<Registration?> GetRegistrationAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _registrationRepository.GetByIdAsync(id, cancellationToken);
    }

    public async Task<Registration> CreateCompetitionRegistrationAsync(
        Guid competitionId,
        Guid participantId,
        RegistrationType type,
        CancellationToken cancellationToken = default)
    {
        var existing = (await _registrationRepository.FindAsync(
            registration => registration.CompetitionId == competitionId
                && registration.ParticipantId == participantId,
            cancellationToken)).SingleOrDefault();
        if (existing is not null) return existing;

        var registration = Registration.CreateForCompetition(
            Guid.NewGuid(),
            competitionId,
            participantId,
            type);
        await _registrationRepository.AddAsync(registration, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return registration;
    }

    public async Task<IEnumerable<Registration>> GetRegistrationsByContestAsync(Guid contestId, CancellationToken cancellationToken = default)
    {
        return await _registrationRepository.FindAsync(
            r => r.ContestId == contestId,
            cancellationToken);
    }

    public async Task<IEnumerable<Registration>> GetRegistrationsByCompetitionAsync(
        Guid competitionId,
        CancellationToken cancellationToken = default)
    {
        return await _registrationRepository.FindAsync(
            registration => registration.CompetitionId == competitionId,
            cancellationToken);
    }

    public async Task<IEnumerable<Registration>> GetRegistrationsByParticipantAsync(Guid participantId, CancellationToken cancellationToken = default)
    {
        return await _registrationRepository.FindAsync(
            r => r.ParticipantId == participantId,
            cancellationToken);
    }

    // Workflow methods
    public async Task<Registration> SubmitRegistrationAsync(
        Guid id,
        Guid? submittedBy = null,
        CancellationToken cancellationToken = default)
    {
        var registration = await _registrationRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new InvalidOperationException($"Registration with ID {id} not found.");

        registration.Submit(submittedBy);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return registration;
    }

    public async Task<Registration> StartReviewAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var registration = await _registrationRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new InvalidOperationException($"Registration with ID {id} not found.");

        registration.StartReview();
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return registration;
    }

    public async Task<Registration> ApproveRegistrationAsync(Guid id, Guid reviewerId, string? notes = null, CancellationToken cancellationToken = default)
    {
        var registration = await _registrationRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new InvalidOperationException($"Registration with ID {id} not found.");

        registration.Approve(reviewerId, notes);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return registration;
    }

    public async Task<Registration> RejectRegistrationAsync(Guid id, Guid reviewerId, string? reason = null, CancellationToken cancellationToken = default)
    {
        var registration = await _registrationRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new InvalidOperationException($"Registration with ID {id} not found.");

        registration.Reject(reviewerId, reason);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return registration;
    }

    public async Task<Registration> WaitlistRegistrationAsync(Guid id, Guid reviewerId, string? notes = null, CancellationToken cancellationToken = default)
    {
        var registration = await _registrationRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new InvalidOperationException($"Registration with ID {id} not found.");

        registration.AddToWaitlist(reviewerId, notes);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return registration;
    }

    public async Task<Registration> ConfirmRegistrationAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var registration = await _registrationRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new InvalidOperationException($"Registration with ID {id} not found.");

        registration.Confirm();
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return registration;
    }

    public async Task<Registration> WithdrawRegistrationAsync(Guid id, string? reason = null, CancellationToken cancellationToken = default)
    {
        var registration = await _registrationRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new InvalidOperationException($"Registration with ID {id} not found.");

        registration.Withdraw(reason);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return registration;
    }

    public async Task<Registration> CancelRegistrationAsync(Guid id, string? reason = null, CancellationToken cancellationToken = default)
    {
        var registration = await _registrationRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new InvalidOperationException($"Registration with ID {id} not found.");

        registration.Cancel(reason);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return registration;
    }

    public async Task<Registration> MarkAsNoShowAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var registration = await _registrationRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new InvalidOperationException($"Registration with ID {id} not found.");

        registration.MarkAsNoShow();
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return registration;
    }

    // Statistics
    public async Task<int> GetConfirmedCountAsync(Guid contestId, CancellationToken cancellationToken = default)
    {
        var registrations = await GetRegistrationsByContestAsync(contestId, cancellationToken);
        return registrations.Count(r => r.Status == RegistrationStatus.Confirmed);
    }

    public async Task<int> GetPendingCountAsync(Guid contestId, CancellationToken cancellationToken = default)
    {
        var registrations = await GetRegistrationsByContestAsync(contestId, cancellationToken);
        return registrations.Count(r => r.Status == RegistrationStatus.Submitted);
    }

    public async Task<int> GetSubmittedCountAsync(Guid contestId, CancellationToken cancellationToken = default)
    {
        var registrations = await GetRegistrationsByContestAsync(contestId, cancellationToken);
        return registrations.Count(r => r.Status == RegistrationStatus.Submitted);
    }

    public async Task<RegistrationStats> GetRegistrationStatsAsync(Guid contestId, CancellationToken cancellationToken = default)
    {
        var registrations = await GetRegistrationsByContestAsync(contestId, cancellationToken);
        var list = registrations.ToList();
        
        return new RegistrationStats
        {
            Total = list.Count,
            Draft = list.Count(r => r.Status == RegistrationStatus.Draft),
            Submitted = list.Count(r => r.Status == RegistrationStatus.Submitted),
            UnderReview = list.Count(r => r.Status == RegistrationStatus.UnderReview),
            Approved = list.Count(r => r.Status == RegistrationStatus.Approved),
            Rejected = list.Count(r => r.Status == RegistrationStatus.Rejected),
            Waitlisted = list.Count(r => r.Status == RegistrationStatus.Waitlisted),
            Withdrawn = list.Count(r => r.Status == RegistrationStatus.Withdrawn),
            Confirmed = list.Count(r => r.Status == RegistrationStatus.Confirmed),
            Cancelled = list.Count(r => r.Status == RegistrationStatus.Cancelled),
            NoShow = list.Count(r => r.Status == RegistrationStatus.NoShow)
        };
    }
}

using SportsPlatform.Modules.Results.Entities;

namespace SportsPlatform.Modules.Results.Services;

public interface IResultService
{
    Task<ContestResult> CreateResultAsync(Guid contestId, Guid eventId, Guid participantId);
    Task<ContestResult?> GetResultByIdAsync(Guid id);
    Task<IReadOnlyList<ContestResult>> GetResultsByContestAsync(Guid contestId);
    Task<IReadOnlyList<ContestResult>> GetResultsByEventAsync(Guid eventId);
    Task<IReadOnlyList<ContestResult>> GetResultsByParticipantAsync(Guid participantId);
    Task<ContestResult> UpdateScoreAsync(Guid resultId, decimal? score, string? scoreDisplay, string? time);
    Task<ContestResult> UpdateRankAsync(Guid resultId, int? rank, string? position);
    Task<ContestResult> UpdatePerformanceDataAsync(Guid resultId, string? performanceData, string? statistics);
    Task<ContestResult> SubmitResultAsync(Guid resultId);
    Task<ContestResult> BeginReviewAsync(Guid resultId);
    Task<ContestResult> ReopenForCorrectionAsync(Guid resultId, string reason);
    Task<ContestResult> ApproveResultAsync(Guid resultId, Guid approvedBy);
    Task<ContestResult> RejectResultAsync(Guid resultId, string? reason);
    Task<ContestResult> PublishResultAsync(Guid resultId);
    Task<ContestResult> MarkAsPersonalBestAsync(Guid resultId);
    Task<ContestResult> MarkAsSeasonBestAsync(Guid resultId);
    Task<ContestResult> MarkAsCompetitionRecordAsync(Guid resultId);
}

public class ResultService : IResultService
{
    private readonly IResultRepository _resultRepository;

    public ResultService(IResultRepository resultRepository)
    {
        _resultRepository = resultRepository;
    }

    public async Task<ContestResult> CreateResultAsync(Guid contestId, Guid eventId, Guid participantId)
    {
        var existingResult = await _resultRepository.GetByContestAndEventAndParticipantAsync(contestId, eventId, participantId);
        if (existingResult != null)
            throw new InvalidOperationException("Result already exists for this contest, event, and participant.");

        var result = new ContestResult(Guid.NewGuid(), contestId, eventId, participantId);
        _resultRepository.Add(result);
        await _resultRepository.SaveChangesAsync();
        return result;
    }

    public async Task<ContestResult?> GetResultByIdAsync(Guid id)
    {
        return await _resultRepository.GetByIdAsync(id);
    }

    public async Task<IReadOnlyList<ContestResult>> GetResultsByContestAsync(Guid contestId)
    {
        return await _resultRepository.GetByContestIdAsync(contestId);
    }

    public async Task<IReadOnlyList<ContestResult>> GetResultsByEventAsync(Guid eventId)
    {
        return await _resultRepository.GetByEventIdAsync(eventId);
    }

    public async Task<IReadOnlyList<ContestResult>> GetResultsByParticipantAsync(Guid participantId)
    {
        return await _resultRepository.GetByParticipantIdAsync(participantId);
    }

    public async Task<ContestResult> UpdateScoreAsync(Guid resultId, decimal? score, string? scoreDisplay, string? time)
    {
        var result = await _resultRepository.GetByIdAsync(resultId)
            ?? throw new InvalidOperationException("Result not found.");

        result.SetScore(score, scoreDisplay, time);
        _resultRepository.Update(result);
        await _resultRepository.SaveChangesAsync();
        return result;
    }

    public async Task<ContestResult> UpdateRankAsync(Guid resultId, int? rank, string? position)
    {
        var result = await _resultRepository.GetByIdAsync(resultId)
            ?? throw new InvalidOperationException("Result not found.");

        result.SetRank(rank, position);
        _resultRepository.Update(result);
        await _resultRepository.SaveChangesAsync();
        return result;
    }

    public async Task<ContestResult> UpdatePerformanceDataAsync(Guid resultId, string? performanceData, string? statistics)
    {
        var result = await _resultRepository.GetByIdAsync(resultId)
            ?? throw new InvalidOperationException("Result not found.");

        result.SetPerformanceData(performanceData, statistics);
        _resultRepository.Update(result);
        await _resultRepository.SaveChangesAsync();
        return result;
    }

    public async Task<ContestResult> SubmitResultAsync(Guid resultId)
    {
        var result = await _resultRepository.GetByIdAsync(resultId)
            ?? throw new InvalidOperationException("Result not found.");

        result.Submit();
        _resultRepository.Update(result);
        await _resultRepository.SaveChangesAsync();
        return result;
    }

    public async Task<ContestResult> BeginReviewAsync(Guid resultId)
    {
        var result = await _resultRepository.GetByIdAsync(resultId)
            ?? throw new InvalidOperationException("Result not found.");
        result.BeginReview();
        _resultRepository.Update(result);
        await _resultRepository.SaveChangesAsync();
        return result;
    }

    public async Task<ContestResult> ReopenForCorrectionAsync(Guid resultId, string reason)
    {
        var result = await _resultRepository.GetByIdAsync(resultId)
            ?? throw new InvalidOperationException("Result not found.");
        result.ReopenForCorrection(reason);
        _resultRepository.Update(result);
        await _resultRepository.SaveChangesAsync();
        return result;
    }

    public async Task<ContestResult> ApproveResultAsync(Guid resultId, Guid approvedBy)
    {
        var result = await _resultRepository.GetByIdAsync(resultId)
            ?? throw new InvalidOperationException("Result not found.");

        result.Approve(approvedBy);
        _resultRepository.Update(result);
        await _resultRepository.SaveChangesAsync();
        return result;
    }

    public async Task<ContestResult> RejectResultAsync(Guid resultId, string? reason)
    {
        var result = await _resultRepository.GetByIdAsync(resultId)
            ?? throw new InvalidOperationException("Result not found.");

        result.Reject(reason);
        _resultRepository.Update(result);
        await _resultRepository.SaveChangesAsync();
        return result;
    }

    public async Task<ContestResult> PublishResultAsync(Guid resultId)
    {
        var result = await _resultRepository.GetByIdAsync(resultId)
            ?? throw new InvalidOperationException("Result not found.");

        result.Publish();
        _resultRepository.Update(result);
        await _resultRepository.SaveChangesAsync();
        return result;
    }

    public async Task<ContestResult> MarkAsPersonalBestAsync(Guid resultId)
    {
        var result = await _resultRepository.GetByIdAsync(resultId)
            ?? throw new InvalidOperationException("Result not found.");

        result.MarkAsPersonalBest();
        _resultRepository.Update(result);
        await _resultRepository.SaveChangesAsync();
        return result;
    }

    public async Task<ContestResult> MarkAsSeasonBestAsync(Guid resultId)
    {
        var result = await _resultRepository.GetByIdAsync(resultId)
            ?? throw new InvalidOperationException("Result not found.");

        result.MarkAsSeasonBest();
        _resultRepository.Update(result);
        await _resultRepository.SaveChangesAsync();
        return result;
    }

    public async Task<ContestResult> MarkAsCompetitionRecordAsync(Guid resultId)
    {
        var result = await _resultRepository.GetByIdAsync(resultId)
            ?? throw new InvalidOperationException("Result not found.");

        result.MarkAsCompetitionRecord();
        _resultRepository.Update(result);
        await _resultRepository.SaveChangesAsync();
        return result;
    }
}

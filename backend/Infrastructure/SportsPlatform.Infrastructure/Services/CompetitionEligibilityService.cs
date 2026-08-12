using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SportsPlatform.Infrastructure.Persistence;
using SportsPlatform.Modules.Participants.Entities;
using SportsPlatform.Modules.Registrations.Entities;

namespace SportsPlatform.Infrastructure.Services;

public sealed record EligibilityEvaluation(bool IsEligible, IReadOnlyList<string> Issues);

public interface ICompetitionEligibilityService
{
    Task<EligibilityEvaluation> EvaluateAsync(Registration registration, CancellationToken cancellationToken = default);
}

public sealed class CompetitionEligibilityService(SportsPlatformDbContext database)
    : ICompetitionEligibilityService
{
    public async Task<EligibilityEvaluation> EvaluateAsync(
        Registration registration, CancellationToken cancellationToken = default)
    {
        if (!registration.CompetitionId.HasValue)
            return new(true, []);

        var competition = await database.Competitions.AsNoTracking()
            .Include(item => item.Editions)
            .SingleAsync(item => item.Id == registration.CompetitionId.Value, cancellationToken);
        var policy = await database.CompetitionEligibilityPolicies.AsNoTracking()
            .SingleOrDefaultAsync(item => item.CompetitionId == competition.Id, cancellationToken);
        var issues = new List<string>();

        var duplicateCount = await database.Registrations.AsNoTracking().CountAsync(item =>
            item.CompetitionId == competition.Id && item.ParticipantId == registration.ParticipantId
            && item.Id != registration.Id && item.Status != RegistrationStatus.Withdrawn
            && item.Status != RegistrationStatus.Cancelled, cancellationToken);
        if (duplicateCount > 0) issues.Add("The participant already has an active registration for this competition.");
        if (policy is null) return new(issues.Count == 0, issues);

        var submittedAt = registration.RegisteredAt ?? registration.CreatedAt;
        if (policy.RegistrationDeadline.HasValue && submittedAt > policy.RegistrationDeadline.Value)
            issues.Add($"Registration was submitted after the {policy.RegistrationDeadline.Value:u} deadline.");

        var participant = await database.Participants.AsNoTracking()
            .Include(item => item.SportProfiles)
            .SingleAsync(item => item.Id == registration.ParticipantId, cancellationToken);
        var referenceDate = competition.Editions.OrderBy(item => item.StartDate)
            .Select(item => (DateTime?)item.StartDate).FirstOrDefault() ?? DateTime.UtcNow;

        if (participant is Team)
        {
            var roster = await database.Set<TeamRoster>().AsNoTracking()
                .Include(item => item.Entries)
                .Where(item => item.TeamId == participant.Id && item.IsActive
                    && item.CompetitionId == competition.Id)
                .OrderByDescending(item => item.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);
            if (roster is null)
            {
                issues.Add("An active roster assigned to this competition is required.");
            }
            else
            {
                var entries = roster.Entries.Where(item => item.IsActive).ToArray();
                if (policy.MinimumSquadSize.HasValue && entries.Length < policy.MinimumSquadSize)
                    issues.Add($"Squad has {entries.Length} athletes; at least {policy.MinimumSquadSize} are required.");
                if (policy.MaximumSquadSize.HasValue && entries.Length > policy.MaximumSquadSize)
                    issues.Add($"Squad has {entries.Length} athletes; no more than {policy.MaximumSquadSize} are allowed.");
                if (policy.RequireSquadNumbers && entries.Any(item => string.IsNullOrWhiteSpace(item.JerseyNumber)))
                    issues.Add("Every active squad member must have a squad number.");
                if (policy.RequireUniqueSquadNumbers && entries.Where(item => !string.IsNullOrWhiteSpace(item.JerseyNumber))
                    .GroupBy(item => item.JerseyNumber!, StringComparer.OrdinalIgnoreCase).Any(group => group.Count() > 1))
                    issues.Add("Active squad numbers must be unique.");

                var athletes = await database.Participants.OfType<Athlete>().AsNoTracking()
                    .Include(item => item.SportProfiles)
                    .Where(item => entries.Select(entry => entry.AthleteId).Contains(item.Id))
                    .ToArrayAsync(cancellationToken);
                foreach (var athlete in athletes) ValidateAthlete(athlete, competition.SportId, referenceDate, policy, issues);
                if (athletes.Length != entries.Length) issues.Add("Every roster entry must reference a valid athlete.");
            }
        }
        else if (participant is Athlete athlete)
        {
            ValidateAthlete(athlete, competition.SportId, referenceDate, policy, issues);
        }

        return new(issues.Count == 0, issues.Distinct().ToArray());
    }

    private static void ValidateAthlete(Athlete athlete, Guid sportId, DateTime referenceDate,
        CompetitionEligibilityPolicy policy, List<string> issues)
    {
        if (policy.MinimumAge.HasValue || policy.MaximumAge.HasValue)
        {
            if (!athlete.DateOfBirth.HasValue) issues.Add($"{athlete.Name}: date of birth is required.");
            else
            {
                var age = referenceDate.Year - athlete.DateOfBirth.Value.Year;
                if (athlete.DateOfBirth.Value.Date > referenceDate.Date.AddYears(-age)) age--;
                if (policy.MinimumAge.HasValue && age < policy.MinimumAge) issues.Add($"{athlete.Name}: age {age} is below the minimum of {policy.MinimumAge}.");
                if (policy.MaximumAge.HasValue && age > policy.MaximumAge) issues.Add($"{athlete.Name}: age {age} exceeds the maximum of {policy.MaximumAge}.");
            }
        }

        if (policy.MinimumWeightKg.HasValue || policy.MaximumWeightKg.HasValue)
        {
            var profile = athlete.SportProfiles.SingleOrDefault(item => item.SportId == sportId);
            var weight = ReadWeight(profile?.AttributesJson);
            if (!weight.HasValue) issues.Add($"{athlete.Name}: weightKg sport attribute is required.");
            else
            {
                if (policy.MinimumWeightKg.HasValue && weight < policy.MinimumWeightKg) issues.Add($"{athlete.Name}: weight {weight:0.##} kg is below the minimum of {policy.MinimumWeightKg:0.##} kg.");
                if (policy.MaximumWeightKg.HasValue && weight > policy.MaximumWeightKg) issues.Add($"{athlete.Name}: weight {weight:0.##} kg exceeds the maximum of {policy.MaximumWeightKg:0.##} kg.");
            }
        }

        var documents = athlete.GetIdentificationDocuments();
        foreach (var required in policy.RequiredDocumentTypes)
            if (!documents.Any(document => document.Type.Equals(required, StringComparison.OrdinalIgnoreCase)
                && (!document.ExpiresAt.HasValue || document.ExpiresAt.Value >= referenceDate)))
                issues.Add($"{athlete.Name}: a valid {required} document is required.");
    }

    private static decimal? ReadWeight(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.TryGetProperty("weightKg", out var value) && value.TryGetDecimal(out var weight)
                ? weight : null;
        }
        catch (JsonException) { return null; }
    }
}

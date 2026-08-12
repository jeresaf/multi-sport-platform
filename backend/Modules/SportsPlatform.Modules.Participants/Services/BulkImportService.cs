using SportsPlatform.Modules.Participants.Entities;
using SportsPlatform.BuildingBlocks;

namespace SportsPlatform.Modules.Participants.Services;

public class BulkImportService
{
    private readonly IParticipantRepository _participantRepository;

    public BulkImportService(IParticipantRepository participantRepository)
    {
        _participantRepository = participantRepository;
    }

    public async Task<BulkImportResult> ImportAthletesAsync(Guid tenantId, List<AthleteImportDto> athletes,
        DuplicateHandling duplicateHandling = DuplicateHandling.Reject, CancellationToken cancellationToken = default)
    {
        var result = new BulkImportResult();
        
        foreach (var dto in athletes)
        {
            try
            {
                var existingAthlete = await _participantRepository.GetByNameAsync(tenantId, dto.Name, cancellationToken);
                if (existingAthlete != null)
                {
                    result.Duplicates++;
                    if (duplicateHandling == DuplicateHandling.Skip) continue;
                    result.Errors.Add(new ImportError { Row = dto.RowNumber, Message = $"Athlete '{dto.Name}' already exists" });
                    continue;
                }

                var athlete = new Athlete(Guid.NewGuid(), tenantId, dto.Name, dto.ShortName ?? dto.Name[..Math.Min(10, dto.Name.Length)]);
                
                if (dto.DateOfBirth.HasValue)
                    athlete.UpdatePersonalInfo(dto.DateOfBirth, dto.Gender, dto.Nationality);
                
                if (!string.IsNullOrEmpty(dto.Email) || !string.IsNullOrEmpty(dto.Phone))
                    athlete.UpdateContact(dto.Email, dto.Phone, dto.EmergencyContact);

                if (dto.MedicalClearance)
                    athlete.SetMedicalClearance(true);

                _participantRepository.Add(athlete);
                await _participantRepository.SaveChangesAsync(cancellationToken);
                result.Imported++;
            }
            catch (Exception ex)
            {
                result.Errors.Add(new ImportError { Row = dto.RowNumber, Message = ex.Message });
            }
        }

        result.Total = athletes.Count;
        return result;
    }

    public async Task<BulkImportResult> ImportTeamsAsync(Guid tenantId, List<TeamImportDto> teams,
        DuplicateHandling duplicateHandling = DuplicateHandling.Reject, CancellationToken cancellationToken = default)
    {
        var result = new BulkImportResult();
        
        foreach (var dto in teams)
        {
            try
            {
                var existingTeam = await _participantRepository.GetByNameAsync(tenantId, dto.Name, cancellationToken);
                if (existingTeam != null)
                {
                    result.Duplicates++;
                    if (duplicateHandling == DuplicateHandling.Skip) continue;
                    result.Errors.Add(new ImportError { Row = dto.RowNumber, Message = $"Team '{dto.Name}' already exists" });
                    continue;
                }

                var team = new Team(Guid.NewGuid(), tenantId, dto.Name, dto.ShortName ?? dto.Name[..Math.Min(10, dto.Name.Length)]);
                team.UpdateDetails(dto.HomeVenue, dto.Coach, dto.Manager);

                _participantRepository.Add(team);
                await _participantRepository.SaveChangesAsync(cancellationToken);
                result.Imported++;
            }
            catch (Exception ex)
            {
                result.Errors.Add(new ImportError { Row = dto.RowNumber, Message = ex.Message });
            }
        }

        result.Total = teams.Count;
        return result;
    }

    public async Task<BulkImportResult> ImportRosterEntriesAsync(Guid tenantId, Guid teamId, Guid rosterId,
        List<RosterEntryImportDto> entries, DuplicateHandling duplicateHandling = DuplicateHandling.Reject,
        CancellationToken cancellationToken = default)
    {
        var result = new BulkImportResult();
        
        foreach (var dto in entries)
        {
            try
            {
                var athlete = await _participantRepository.GetByNameAsync(tenantId, dto.AthleteName, cancellationToken);
                if (athlete == null)
                {
                    result.Errors.Add(new ImportError { Row = dto.RowNumber, Message = $"Athlete '{dto.AthleteName}' not found" });
                    continue;
                }

                var roster = await _participantRepository.GetRosterByIdAsync(rosterId, cancellationToken);
                if (roster == null)
                {
                    result.Errors.Add(new ImportError { Row = dto.RowNumber, Message = "Roster not found" });
                    continue;
                }
                if (roster.TenantId != tenantId)
                {
                    result.Errors.Add(new ImportError { Row = dto.RowNumber, Message = "Roster not found" });
                    continue;
                }
                if (roster.Entries.Any(item => item.IsActive && item.AthleteId == athlete.Id))
                {
                    result.Duplicates++;
                    if (duplicateHandling == DuplicateHandling.Skip) continue;
                }

                roster.AddAthlete(athlete.Id, dto.JerseyNumber, dto.Position);
                _participantRepository.UpdateRoster(roster);
                await _participantRepository.SaveChangesAsync(cancellationToken);
                result.Imported++;
            }
            catch (Exception ex)
            {
                result.Errors.Add(new ImportError { Row = dto.RowNumber, Message = ex.Message });
            }
        }

        result.Total = entries.Count;
        return result;
    }
}

public class AthleteImportDto
{
    public int RowNumber { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? ShortName { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public string? Gender { get; set; }
    public string? Nationality { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? EmergencyContact { get; set; }
    public bool MedicalClearance { get; set; }
}

public class TeamImportDto
{
    public int RowNumber { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? ShortName { get; set; }
    public string? HomeVenue { get; set; }
    public string? Coach { get; set; }
    public string? Manager { get; set; }
}

public class RosterEntryImportDto
{
    public int RowNumber { get; set; }
    public string AthleteName { get; set; } = string.Empty;
    public string? JerseyNumber { get; set; }
    public string? Position { get; set; }
}

public class BulkImportResult
{
    public int Imported { get; set; }
    public int Total { get; set; }
    public int Duplicates { get; set; }
    public List<ImportError> Errors { get; set; } = new();
    public bool Success => Errors.Count == 0;
}

public class ImportError
{
    public int Row { get; set; }
    public string Message { get; set; } = string.Empty;
}

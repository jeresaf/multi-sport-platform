using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;

namespace SportsPlatform.Modules.Participants.Services;

public sealed record CsvParseResult<T>(IReadOnlyList<T> Records, IReadOnlyList<ImportError> Errors);

public class CsvImportParser
{
    private static CsvReader Reader(Stream stream) => new(new StreamReader(stream),
        new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true, MissingFieldFound = null, HeaderValidated = null,
            BadDataFound = null, TrimOptions = TrimOptions.Trim
        });

    public List<AthleteImportDto> ParseAthletes(Stream stream) => ParseAthletesWithErrors(stream).Records.ToList();
    public List<TeamImportDto> ParseTeams(Stream stream) => ParseTeamsWithErrors(stream).Records.ToList();
    public List<RosterEntryImportDto> ParseRosterEntries(Stream stream) => ParseRosterEntriesWithErrors(stream).Records.ToList();

    public CsvParseResult<AthleteImportDto> ParseAthletesWithErrors(Stream stream)
    {
        using var csv = Reader(stream);
        var records = new List<AthleteImportDto>(); var errors = new List<ImportError>();
        csv.Read(); csv.ReadHeader();
        while (csv.Read())
        {
            var row = csv.Context.Parser.Row;
            try
            {
                var name = Field(csv, "Name", "FullName");
                var rawDate = Field(csv, "DateOfBirth", "DOB");
                if (string.IsNullOrWhiteSpace(name)) { errors.Add(Error(row, "Name is required.")); continue; }
                var date = ParseDate(rawDate);
                if (!string.IsNullOrWhiteSpace(rawDate) && !date.HasValue)
                { errors.Add(Error(row, $"DateOfBirth '{rawDate}' is invalid; use yyyy-MM-dd.")); continue; }
                records.Add(new AthleteImportDto
                {
                    RowNumber = row, Name = name, ShortName = Field(csv, "ShortName", "Nickname"),
                    DateOfBirth = date, Gender = Field(csv, "Gender"),
                    Nationality = Field(csv, "Nationality", "Country"), Email = Field(csv, "Email", "ContactEmail"),
                    Phone = Field(csv, "Phone", "ContactPhone"), EmergencyContact = Field(csv, "EmergencyContact"),
                    MedicalClearance = ParseBool(Field(csv, "MedicalClearance", "Cleared"))
                });
            }
            catch (Exception exception) { errors.Add(Error(row, exception.Message)); }
        }
        return new(records, errors);
    }

    public CsvParseResult<TeamImportDto> ParseTeamsWithErrors(Stream stream)
    {
        using var csv = Reader(stream);
        var records = new List<TeamImportDto>(); var errors = new List<ImportError>();
        csv.Read(); csv.ReadHeader();
        while (csv.Read())
        {
            var row = csv.Context.Parser.Row;
            try
            {
                var name = Field(csv, "Name", "TeamName");
                if (string.IsNullOrWhiteSpace(name)) { errors.Add(Error(row, "Name is required.")); continue; }
                records.Add(new TeamImportDto { RowNumber = row, Name = name,
                    ShortName = Field(csv, "ShortName", "Abbreviation"), HomeVenue = Field(csv, "HomeVenue", "Stadium", "Arena"),
                    Coach = Field(csv, "Coach"), Manager = Field(csv, "Manager") });
            }
            catch (Exception exception) { errors.Add(Error(row, exception.Message)); }
        }
        return new(records, errors);
    }

    public CsvParseResult<RosterEntryImportDto> ParseRosterEntriesWithErrors(Stream stream)
    {
        using var csv = Reader(stream);
        var records = new List<RosterEntryImportDto>(); var errors = new List<ImportError>();
        csv.Read(); csv.ReadHeader();
        while (csv.Read())
        {
            var row = csv.Context.Parser.Row;
            try
            {
                var name = Field(csv, "AthleteName", "Name", "PlayerName");
                if (string.IsNullOrWhiteSpace(name)) { errors.Add(Error(row, "AthleteName is required.")); continue; }
                records.Add(new RosterEntryImportDto { RowNumber = row, AthleteName = name,
                    JerseyNumber = Field(csv, "JerseyNumber", "Number"), Position = Field(csv, "Position", "Role") });
            }
            catch (Exception exception) { errors.Add(Error(row, exception.Message)); }
        }
        return new(records, errors);
    }

    private static string? Field(CsvReader csv, params string[] names)
    {
        foreach (var name in names)
            if (csv.TryGetField<string>(name, out var value) && !string.IsNullOrWhiteSpace(value)) return value.Trim();
        return null;
    }

    private static DateTime? ParseDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        string[] formats = ["yyyy-MM-dd", "dd/MM/yyyy", "MM/dd/yyyy", "dd-MM-yyyy", "yyyy/MM/dd"];
        return DateTime.TryParseExact(value, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? DateTime.SpecifyKind(date, DateTimeKind.Utc) : null;
    }

    private static bool ParseBool(string? value) => !string.IsNullOrWhiteSpace(value)
        && value.ToLowerInvariant() is "true" or "yes" or "y" or "1" or "cleared" or "approved";
    private static ImportError Error(int row, string message) => new() { Row = row, Message = message };
}

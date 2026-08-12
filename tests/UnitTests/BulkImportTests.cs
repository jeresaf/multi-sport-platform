using System.Text;
using FluentAssertions;
using SportsPlatform.Modules.Participants.Entities;
using SportsPlatform.Modules.Participants.Services;
using Xunit;

namespace SportsPlatform.UnitTests;

public sealed class BulkImportTests
{
    [Fact]
    public void Parser_RetainsValidRowsAndReportsMalformedRows()
    {
        const string csv = "Name,ShortName,DateOfBirth,Gender,Nationality,Email,Phone,EmergencyContact,MedicalClearance\n"
            + "Valid Athlete,VA,2000-01-02,Female,Kenya,,,,true\n"
            + "Invalid Athlete,IA,not-a-date,Male,Kenya,,,,false\n"
            + ",NA,2001-01-01,Female,Kenya,,,,false\n";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        var result = new CsvImportParser().ParseAthletesWithErrors(stream);

        result.Records.Should().ContainSingle().Which.Name.Should().Be("Valid Athlete");
        result.Records.Single().DateOfBirth!.Value.Kind.Should().Be(DateTimeKind.Utc);
        result.Errors.Should().HaveCount(2);
        result.Errors.Select(item => item.Row).Should().BeEquivalentTo(new[] { 3, 4 });
    }

    [Fact]
    public void CompletedJob_RetainsReportEvidenceAndDiscardsSourceRows()
    {
        var job = new BulkImportJob(Guid.NewGuid(), Guid.NewGuid(), "athletes", "athletes.csv",
            DuplicateHandling.Skip, Guid.NewGuid(), null, 3, "[{\"name\":\"private\"}]",
            [new ImportError { Row = 3, Message = "Invalid date" }]);
        job.Start();
        job.Complete(new BulkImportResult
        {
            Total = 2, Imported = 1, Duplicates = 1,
            Errors = [new ImportError { Row = 4, Message = "Duplicate rejected" }]
        });

        job.Status.Should().Be(BulkImportJobStatus.Completed);
        job.ImportedRows.Should().Be(1);
        job.DuplicateRows.Should().Be(1);
        job.FailedRows.Should().Be(2);
        job.GetErrors().Should().HaveCount(2);
        job.InputJson.Should().Be("[]");
    }
}

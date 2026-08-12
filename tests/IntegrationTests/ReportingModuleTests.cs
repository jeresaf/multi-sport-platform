using FluentAssertions;
using Xunit;
using SportsPlatform.Modules.Reporting.Entities;
using SportsPlatform.Modules.Reporting.Services;

namespace SportsPlatform.IntegrationTests;

public class ReportingModuleTests
{
    private readonly ReportService _reportService;

    public ReportingModuleTests()
    {
        _reportService = new ReportService();
    }

    #region Report Tests

    [Fact]
    public async Task CreateReportAsync_ShouldCreateReportWithCorrectProperties()
    {
        // Arrange
        var createdBy = Guid.NewGuid();
        var name = "Monthly Performance Report";
        var type = ReportType.Performance;

        // Act
        var report = await _reportService.CreateReportAsync(createdBy, name, type);

        // Assert
        report.Should().NotBeNull();
        report.Id.Should().NotBeEmpty();
        report.CreatedBy.Should().Be(createdBy);
        report.Name.Should().Be(name);
        report.Type.Should().Be(type);
        report.Status.Should().Be(ReportStatus.Pending);
        report.Format.Should().Be(ReportFormat.Pdf);
    }

    [Fact]
    public async Task CreateReportAsync_WithDescription_ShouldSetDescription()
    {
        // Arrange
        var createdBy = Guid.NewGuid();
        var name = "Financial Report";
        var description = "Q4 Financial Summary";
        var type = ReportType.Financial;

        // Act
        var report = await _reportService.CreateReportAsync(createdBy, name, type, description);

        // Assert
        report.Description.Should().Be(description);
    }

    [Fact]
    public async Task GetReportByIdAsync_WithExistingReport_ShouldReturnReport()
    {
        // Arrange
        var createdBy = Guid.NewGuid();
        var report = await _reportService.CreateReportAsync(createdBy, "Test Report", ReportType.Participation);

        // Act
        var retrieved = await _reportService.GetReportByIdAsync(report.Id);

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.Id.Should().Be(report.Id);
    }

    [Fact]
    public async Task GetReportByIdAsync_WithNonExistingReport_ShouldReturnNull()
    {
        // Act
        var result = await _reportService.GetReportByIdAsync(Guid.NewGuid());

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdateReportAsync_ShouldUpdateDescriptionAndFormat()
    {
        // Arrange
        var createdBy = Guid.NewGuid();
        var report = await _reportService.CreateReportAsync(createdBy, "Test Report", ReportType.Results);
        var newDescription = "Updated Description";
        var newFormat = ReportFormat.Excel;

        // Act
        var updated = await _reportService.UpdateReportAsync(report.Id, null, newDescription, newFormat);

        // Assert
        updated.Description.Should().Be(newDescription);
        updated.Format.Should().Be(newFormat);
    }

    [Fact]
    public async Task StartReportGenerationAsync_ShouldSetStatusToProcessing()
    {
        // Arrange
        var createdBy = Guid.NewGuid();
        var report = await _reportService.CreateReportAsync(createdBy, "Test Report", ReportType.Attendance);

        // Act
        var processing = await _reportService.StartReportGenerationAsync(report.Id);

        // Assert
        processing.Status.Should().Be(ReportStatus.Processing);
    }

    [Fact]
    public async Task CompleteReportGenerationAsync_ShouldSetStatusToCompleted()
    {
        // Arrange
        var createdBy = Guid.NewGuid();
        var report = await _reportService.CreateReportAsync(createdBy, "Test Report", ReportType.Financial);
        var generatedUrl = "https://storage.example.com/reports/report.pdf";

        // Act
        var completed = await _reportService.CompleteReportGenerationAsync(report.Id, generatedUrl);

        // Assert
        completed.Status.Should().Be(ReportStatus.Completed);
        completed.GeneratedUrl.Should().Be(generatedUrl);
        completed.GeneratedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task FailReportGenerationAsync_ShouldSetStatusToFailed()
    {
        // Arrange
        var createdBy = Guid.NewGuid();
        var report = await _reportService.CreateReportAsync(createdBy, "Test Report", ReportType.Performance);
        var errorMessage = "Report generation failed due to timeout";

        // Act
        var failed = await _reportService.FailReportGenerationAsync(report.Id, errorMessage);

        // Assert
        failed.Status.Should().Be(ReportStatus.Failed);
        failed.ErrorMessage.Should().Be(errorMessage);
    }

    [Fact]
    public async Task DeleteReportAsync_ShouldRemoveReport()
    {
        // Arrange
        var createdBy = Guid.NewGuid();
        var report = await _reportService.CreateReportAsync(createdBy, "Test Report", ReportType.Custom);

        // Act
        await _reportService.DeleteReportAsync(report.Id);
        var retrieved = await _reportService.GetReportByIdAsync(report.Id);

        // Assert
        retrieved.Should().BeNull();
    }

    #endregion

    #region Scheduled Report Tests

    [Fact]
    public async Task CreateScheduledReportAsync_ShouldCreateScheduleWithCorrectProperties()
    {
        // Arrange
        var createdBy = Guid.NewGuid();
        var report = await _reportService.CreateReportAsync(createdBy, "Scheduled Report", ReportType.Financial);
        var scheduleName = "Weekly Financial Summary";
        var frequency = ScheduleFrequency.Weekly;

        // Act
        var schedule = await _reportService.CreateScheduledReportAsync(report.Id, scheduleName, frequency);

        // Assert
        schedule.Should().NotBeNull();
        schedule.ReportId.Should().Be(report.Id);
        schedule.Name.Should().Be(scheduleName);
        schedule.Frequency.Should().Be(frequency);
        schedule.NextRunAt.Should().NotBeNull();
        schedule.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task CreateScheduledReportAsync_WithRecipients_ShouldSetRecipients()
    {
        // Arrange
        var createdBy = Guid.NewGuid();
        var report = await _reportService.CreateReportAsync(createdBy, "Scheduled Report", ReportType.Attendance);
        var recipients = "admin@example.com,finance@example.com";

        // Act
        var schedule = await _reportService.CreateScheduledReportAsync(
            report.Id, 
            "Weekly Attendance Report", 
            ScheduleFrequency.Daily, 
            recipients);

        // Assert
        schedule.Recipients.Should().Be(recipients);
    }

    [Fact]
    public async Task GetSchedulesByReportAsync_ShouldReturnSchedulesForReport()
    {
        // Arrange
        var createdBy = Guid.NewGuid();
        var report = await _reportService.CreateReportAsync(createdBy, "Multi-Schedule Report", ReportType.Performance);
        await _reportService.CreateScheduledReportAsync(report.Id, "Daily", ScheduleFrequency.Daily);
        await _reportService.CreateScheduledReportAsync(report.Id, "Weekly", ScheduleFrequency.Weekly);

        // Act
        var schedules = await _reportService.GetSchedulesByReportAsync(report.Id);

        // Assert
        schedules.Should().HaveCount(2);
    }

    [Fact]
    public async Task UpdateScheduleAsync_ShouldUpdateFrequencyAndRecipients()
    {
        // Arrange
        var createdBy = Guid.NewGuid();
        var report = await _reportService.CreateReportAsync(createdBy, "Test Report", ReportType.Financial);
        var schedule = await _reportService.CreateScheduledReportAsync(
            report.Id, 
            "Test Schedule", 
            ScheduleFrequency.Daily);
        var newRecipients = "new@example.com";

        // Act
        var updated = await _reportService.UpdateScheduleAsync(
            schedule.Id, 
            ScheduleFrequency.Monthly, 
            newRecipients);

        // Assert
        updated.Frequency.Should().Be(ScheduleFrequency.Monthly);
        updated.Recipients.Should().Be(newRecipients);
    }

    [Fact]
    public async Task PauseScheduleAsync_ShouldSetIsActiveToFalse()
    {
        // Arrange
        var createdBy = Guid.NewGuid();
        var report = await _reportService.CreateReportAsync(createdBy, "Test Report", ReportType.Attendance);
        var schedule = await _reportService.CreateScheduledReportAsync(
            report.Id, 
            "Test Schedule", 
            ScheduleFrequency.Weekly);

        // Act
        var paused = await _reportService.PauseScheduleAsync(schedule.Id);

        // Assert
        paused.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task ResumeScheduleAsync_ShouldSetIsActiveToTrue()
    {
        // Arrange
        var createdBy = Guid.NewGuid();
        var report = await _reportService.CreateReportAsync(createdBy, "Test Report", ReportType.Financial);
        var schedule = await _reportService.CreateScheduledReportAsync(
            report.Id, 
            "Test Schedule", 
            ScheduleFrequency.Monthly);
        await _reportService.PauseScheduleAsync(schedule.Id);

        // Act
        var resumed = await _reportService.ResumeScheduleAsync(schedule.Id);

        // Assert
        resumed.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteScheduleAsync_ShouldRemoveSchedule()
    {
        // Arrange
        var createdBy = Guid.NewGuid();
        var report = await _reportService.CreateReportAsync(createdBy, "Test Report", ReportType.Performance);
        var schedule = await _reportService.CreateScheduledReportAsync(
            report.Id, 
            "Test Schedule", 
            ScheduleFrequency.Quarterly);

        // Act
        await _reportService.DeleteScheduleAsync(schedule.Id);
        var schedules = await _reportService.GetSchedulesByReportAsync(report.Id);

        // Assert
        schedules.Should().BeEmpty();
    }

    #endregion

}

using SportsPlatform.Modules.Reporting.Entities;

namespace SportsPlatform.Modules.Reporting.Services;

public interface IReportService
{
    Task<Report> CreateReportAsync(Guid tenantId, Guid createdBy, string name, ReportType type, string? description = null);
    Task<Report> CreateReportAsync(Guid createdBy, string name, ReportType type, string? description = null);
    Task<Report?> GetReportByIdAsync(Guid reportId);
    Task<IEnumerable<Report>> GetReportsByTenantAsync(Guid tenantId, int page = 1, int pageSize = 20);
    Task<Report> UpdateReportAsync(Guid reportId, string? name, string? description, ReportFormat? format);
    Task<Report> SetReportParametersAsync(Guid reportId, string parameters);
    Task<Report> StartReportGenerationAsync(Guid reportId);
    Task<Report> CompleteReportGenerationAsync(Guid reportId, string generatedUrl);
    Task<Report> FailReportGenerationAsync(Guid reportId, string errorMessage);
    Task DeleteReportAsync(Guid reportId);
    
    // Scheduled reports
    Task<ReportSchedule> CreateScheduledReportAsync(Guid reportId, string name, ScheduleFrequency frequency, string? recipients = null);
    Task<IEnumerable<ReportSchedule>> GetSchedulesByReportAsync(Guid reportId);
    Task<ReportSchedule> UpdateScheduleAsync(Guid scheduleId, ScheduleFrequency? frequency, string? recipients);
    Task<ReportSchedule> PauseScheduleAsync(Guid scheduleId);
    Task<ReportSchedule> ResumeScheduleAsync(Guid scheduleId);
    Task DeleteScheduleAsync(Guid scheduleId);
}

public class ReportService : IReportService
{
    private readonly Dictionary<Guid, Report> _reports = new();
    private readonly Dictionary<Guid, ReportSchedule> _schedules = new();
    private readonly Dictionary<Guid, List<Guid>> _tenantReports = new();

    public Task<Report> CreateReportAsync(
        Guid createdBy,
        string name,
        ReportType type,
        string? description = null) =>
        CreateReportAsync(createdBy, createdBy, name, type, description);

    public Task<Report> CreateReportAsync(Guid tenantId, Guid createdBy, string name, ReportType type, string? description = null)
    {
        var report = new Report(
            Guid.NewGuid(),
            tenantId,
            createdBy,
            name,
            type
        );
        
        if (description != null)
        {
            report.SetDescription(description);
        }
        
        _reports[report.Id] = report;
        if (!_tenantReports.TryGetValue(tenantId, out var reportIds))
        {
            reportIds = new List<Guid>();
            _tenantReports[tenantId] = reportIds;
        }
        reportIds.Add(report.Id);
        return Task.FromResult(report);
    }

    public Task<Report?> GetReportByIdAsync(Guid reportId)
    {
        _reports.TryGetValue(reportId, out var report);
        return Task.FromResult(report);
    }

    public Task<IEnumerable<Report>> GetReportsByTenantAsync(Guid tenantId, int page = 1, int pageSize = 20)
    {
        if (!_tenantReports.TryGetValue(tenantId, out var reportIds))
        {
            return Task.FromResult(Enumerable.Empty<Report>());
        }

        var reports = reportIds
            .Where(id => _reports.ContainsKey(id))
            .Select(id => _reports[id])
            .OrderByDescending(r => r.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize);

        return Task.FromResult(reports);
    }

    public Task<Report> UpdateReportAsync(Guid reportId, string? name, string? description, ReportFormat? format)
    {
        if (!_reports.TryGetValue(reportId, out var report))
        {
            throw new InvalidOperationException($"Report {reportId} not found");
        }

        if (name != null)
        {
            // Reports are immutable by design, but we can update description and format
        }
        
        if (description != null)
        {
            report.SetDescription(description);
        }
        
        if (format.HasValue)
        {
            report.SetFormat(format.Value);
        }

        return Task.FromResult(report);
    }

    public Task<Report> SetReportParametersAsync(Guid reportId, string parameters)
    {
        if (!_reports.TryGetValue(reportId, out var report))
        {
            throw new InvalidOperationException($"Report {reportId} not found");
        }

        report.SetParameters(parameters);
        return Task.FromResult(report);
    }

    public Task<Report> StartReportGenerationAsync(Guid reportId)
    {
        if (!_reports.TryGetValue(reportId, out var report))
        {
            throw new InvalidOperationException($"Report {reportId} not found");
        }

        report.MarkAsProcessing();
        return Task.FromResult(report);
    }

    public Task<Report> CompleteReportGenerationAsync(Guid reportId, string generatedUrl)
    {
        if (!_reports.TryGetValue(reportId, out var report))
        {
            throw new InvalidOperationException($"Report {reportId} not found");
        }

        report.MarkAsCompleted(generatedUrl);
        return Task.FromResult(report);
    }

    public Task<Report> FailReportGenerationAsync(Guid reportId, string errorMessage)
    {
        if (!_reports.TryGetValue(reportId, out var report))
        {
            throw new InvalidOperationException($"Report {reportId} not found");
        }

        report.MarkAsFailed(errorMessage);
        return Task.FromResult(report);
    }

    public Task DeleteReportAsync(Guid reportId)
    {
        _reports.Remove(reportId);
        return Task.CompletedTask;
    }

    public Task<ReportSchedule> CreateScheduledReportAsync(Guid reportId, string name, ScheduleFrequency frequency, string? recipients = null)
    {
        var schedule = new ReportSchedule(
            Guid.NewGuid(),
            reportId,
            name,
            frequency
        );

        if (recipients != null)
        {
            schedule.SetRecipients(recipients);
        }

        // Calculate next run time based on frequency
        var nextRun = CalculateNextRunTime(frequency);
        schedule.SetNextRunAt(nextRun);

        _schedules[schedule.Id] = schedule;
        return Task.FromResult(schedule);
    }

    public Task<IEnumerable<ReportSchedule>> GetSchedulesByReportAsync(Guid reportId)
    {
        var schedules = _schedules.Values
            .Where(s => s.ReportId == reportId)
            .OrderBy(s => s.NextRunAt)
            .ToList();

        return Task.FromResult<IEnumerable<ReportSchedule>>(schedules);
    }

    public Task<ReportSchedule> UpdateScheduleAsync(Guid scheduleId, ScheduleFrequency? frequency, string? recipients)
    {
        if (!_schedules.TryGetValue(scheduleId, out var schedule))
        {
            throw new InvalidOperationException($"Schedule {scheduleId} not found");
        }

        if (frequency.HasValue)
        {
            schedule.SetFrequency(frequency.Value);
            schedule.SetCronExpression(GetCronExpression(frequency.Value));
            schedule.SetNextRunAt(CalculateNextRunTime(frequency.Value));
        }

        if (recipients != null)
        {
            schedule.SetRecipients(recipients);
        }

        return Task.FromResult(schedule);
    }

    public Task<ReportSchedule> PauseScheduleAsync(Guid scheduleId)
    {
        if (!_schedules.TryGetValue(scheduleId, out var schedule))
        {
            throw new InvalidOperationException($"Schedule {scheduleId} not found");
        }

        schedule.Pause();
        return Task.FromResult(schedule);
    }

    public Task<ReportSchedule> ResumeScheduleAsync(Guid scheduleId)
    {
        if (!_schedules.TryGetValue(scheduleId, out var schedule))
        {
            throw new InvalidOperationException($"Schedule {scheduleId} not found");
        }

        schedule.Resume();
        return Task.FromResult(schedule);
    }

    public Task DeleteScheduleAsync(Guid scheduleId)
    {
        _schedules.Remove(scheduleId);
        return Task.CompletedTask;
    }

    private static DateTime CalculateNextRunTime(ScheduleFrequency frequency)
    {
        var now = DateTime.UtcNow;
        return frequency switch
        {
            ScheduleFrequency.Daily => now.AddDays(1).Date,
            ScheduleFrequency.Weekly => now.AddDays(7),
            ScheduleFrequency.Monthly => now.AddMonths(1),
            ScheduleFrequency.Quarterly => now.AddMonths(3),
            ScheduleFrequency.Yearly => now.AddYears(1),
            _ => now.AddDays(1).Date
        };
    }

    private static string GetCronExpression(ScheduleFrequency frequency)
    {
        return frequency switch
        {
            ScheduleFrequency.Daily => "0 0 * * *",
            ScheduleFrequency.Weekly => "0 0 * * 1",
            ScheduleFrequency.Monthly => "0 0 1 * *",
            ScheduleFrequency.Quarterly => "0 0 1 */3 *",
            ScheduleFrequency.Yearly => "0 0 1 1 *",
            _ => "0 0 * * *"
        };
    }
}

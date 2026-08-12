using System.Globalization;
using System.IO.Compression;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Channels;
using ClosedXML.Excel;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;
using Microsoft.EntityFrameworkCore;
using PdfSharp.Fonts;
using SportsPlatform.Infrastructure.Persistence;
using SportsPlatform.Modules.Audit.Entities;
using SportsPlatform.Modules.Participants.Entities;
using SportsPlatform.Modules.Reporting.Entities;

namespace SportsPlatform.Api.Services;

public sealed record ReportParameters(
    Guid? CompetitionId,
    DateTime? FromUtc,
    DateTime? ToUtc,
    Guid? ContestId = null);

public sealed record GeneratedReportFile(
    string FileName,
    string ContentType,
    byte[] Content);

public interface IReportGenerationQueue
{
    ValueTask EnqueueAsync(Guid reportId, CancellationToken cancellationToken = default);
    IAsyncEnumerable<Guid> ReadAllAsync(CancellationToken cancellationToken);
}

public sealed class ReportGenerationQueue : IReportGenerationQueue
{
    private readonly Channel<Guid> _channel = Channel.CreateUnbounded<Guid>(
        new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });

    public ValueTask EnqueueAsync(Guid reportId, CancellationToken cancellationToken = default) =>
        _channel.Writer.WriteAsync(reportId, cancellationToken);

    public IAsyncEnumerable<Guid> ReadAllAsync(CancellationToken cancellationToken) =>
        _channel.Reader.ReadAllAsync(cancellationToken);
}

public sealed class ReportGenerationWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IReportGenerationQueue _queue;
    private readonly ILogger<ReportGenerationWorker> _logger;

    public ReportGenerationWorker(
        IServiceScopeFactory scopeFactory,
        IReportGenerationQueue queue,
        ILogger<ReportGenerationWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _queue = queue;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RequeuePendingAsync(stoppingToken);
        await foreach (var reportId in _queue.ReadAllAsync(stoppingToken))
            await GenerateAsync(reportId, stoppingToken);
    }

    private async Task RequeuePendingAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<SportsPlatformDbContext>();
        var pending = await database.Reports.AsNoTracking()
            .Where(item => item.Status == ReportStatus.Pending
                || item.Status == ReportStatus.Processing)
            .Select(item => item.Id)
            .ToArrayAsync(cancellationToken);
        foreach (var reportId in pending)
            await _queue.EnqueueAsync(reportId, cancellationToken);
    }

    private async Task GenerateAsync(Guid reportId, CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<SportsPlatformDbContext>();
        var builder = scope.ServiceProvider.GetRequiredService<ReportDocumentBuilder>();
        var report = await database.Reports.SingleOrDefaultAsync(
            item => item.Id == reportId, cancellationToken);
        if (report is null || report.Status == ReportStatus.Completed) return;

        try
        {
            report.MarkAsProcessing();
            await database.SaveChangesAsync(cancellationToken);
            var generated = await builder.BuildAsync(report, cancellationToken);
            report.MarkAsCompleted(
                generated.FileName, generated.ContentType, generated.Content);
            if (report.Type == ReportType.TenantDataExport)
                database.AuditEntries.Add(new AuditEntry(
                    Guid.NewGuid(), report.TenantId, report.CreatedBy,
                    "TenantDataExportCompleted", "Report", report.Id.ToString(),
                    null,
                    JsonSerializer.Serialize(new
                    {
                        report.GeneratedFileName,
                        bytes = generated.Content.Length
                    }),
                    "Authorized tenant data export completed."));
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            report.ResetForGeneration();
            await database.SaveChangesAsync(CancellationToken.None);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Report {ReportId} generation failed.", reportId);
            report.MarkAsFailed("Report generation failed. Review the selected filters and try again.");
            if (report.Type == ReportType.TenantDataExport)
                database.AuditEntries.Add(new AuditEntry(
                    Guid.NewGuid(), report.TenantId, report.CreatedBy,
                    "TenantDataExportFailed", "Report", report.Id.ToString(),
                    null, null, "Tenant data export generation failed."));
            await database.SaveChangesAsync(CancellationToken.None);
        }
    }
}

public sealed class ReportDocumentBuilder
{
    private readonly SportsPlatformDbContext _database;

    public ReportDocumentBuilder(SportsPlatformDbContext database) => _database = database;

    public async Task<GeneratedReportFile> BuildAsync(
        Report report,
        CancellationToken cancellationToken)
    {
        var parameters = JsonSerializer.Deserialize<ReportParameters>(report.Parameters)
            ?? new ReportParameters(null, null, null);
        if (report.Type == ReportType.ContestSheet)
            return await BuildContestSheetAsync(report, parameters, cancellationToken);
        if (report.Type == ReportType.TenantDataExport)
            return await BuildTenantDataExportAsync(report, cancellationToken);
        var document = report.Type switch
        {
            ReportType.RegistrationList => await BuildRegistrationListAsync(
                report.TenantId, parameters, cancellationToken),
            ReportType.TeamRoster => await BuildTeamRosterAsync(
                report.TenantId, parameters, cancellationToken),
            ReportType.OfficialAssignment => await BuildOfficialAssignmentsAsync(
                report.TenantId, parameters, cancellationToken),
            ReportType.MatchSheet => await BuildMatchSheetsAsync(
                report.TenantId, parameters, cancellationToken),
            ReportType.Standings => await BuildStandingsAsync(
                report.TenantId, parameters, cancellationToken),
            ReportType.CompetitionSummary => await BuildCompetitionSummaryAsync(
                report.TenantId, parameters, cancellationToken),
            ReportType.Discipline => await BuildDisciplineAsync(
                report.TenantId, parameters, cancellationToken),
            ReportType.AthletePerformance => await BuildAthletePerformanceAsync(
                report.TenantId, parameters, cancellationToken),
            ReportType.AuditHistory => await BuildAuditHistoryAsync(
                report.TenantId, parameters, cancellationToken),
            ReportType.FixtureSchedule => await BuildFixtureScheduleAsync(
                report.TenantId, parameters, cancellationToken),
            ReportType.Results => await BuildResultsAsync(
                report.TenantId, parameters, cancellationToken),
            _ => throw new InvalidOperationException(
                $"Report type {report.Type} is not available yet.")
        };
        var safeName = string.Concat(report.Name.Select(character =>
            Path.GetInvalidFileNameChars().Contains(character) ? '-' : character)).Trim();
        if (string.IsNullOrWhiteSpace(safeName)) safeName = "report";

        return report.Format switch
        {
            ReportFormat.Pdf => new GeneratedReportFile(
                $"{safeName}.pdf", "application/pdf", ToPdf(document)),
            ReportFormat.Csv => new GeneratedReportFile(
                $"{safeName}.csv", "text/csv; charset=utf-8", ToCsv(document)),
            ReportFormat.Excel => new GeneratedReportFile(
                $"{safeName}.xlsx",
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                ToSpreadsheet(document)),
            ReportFormat.Html => new GeneratedReportFile(
                $"{safeName}.html", "text/html; charset=utf-8", ToHtml(document)),
            _ => throw new InvalidOperationException(
                $"Report format {report.Format} is not available for this report.")
        };
    }

    private async Task<GeneratedReportFile> BuildContestSheetAsync(
        Report report,
        ReportParameters parameters,
        CancellationToken cancellationToken)
    {
        if (!parameters.ContestId.HasValue)
            throw new InvalidOperationException("A contest is required for a contest sheet.");

        var contest = await _database.Contests.AsNoTracking()
            .Include(item => item.Participants)
            .Include(item => item.Segments)
            .Include(item => item.Officials)
            .AsSplitQuery()
            .SingleOrDefaultAsync(item => item.Id == parameters.ContestId.Value
                && item.TenantId == report.TenantId, cancellationToken)
            ?? throw new InvalidOperationException("The contest is unavailable for this tenant.");
        var sport = await _database.Sports.AsNoTracking()
            .SingleAsync(item => item.Id == contest.SportId, cancellationToken);
        var participantIds = contest.Participants.Select(item => item.ParticipantId).ToArray();
        var participantNames = await _database.Participants.AsNoTracking()
            .Where(item => item.TenantId == report.TenantId
                && participantIds.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, item => item.Name, cancellationToken);
        var branding = await _database.TenantBrandings.AsNoTracking()
            .SingleOrDefaultAsync(item => item.TenantId == report.TenantId, cancellationToken);
        var tenantName = branding?.TenantName
            ?? await _database.Tenants.AsNoTracking()
                .Where(item => item.Id == report.TenantId)
                .Select(item => item.Name)
                .SingleAsync(cancellationToken);

        Guid? competitionId = null;
        string? competitionName = null;
        if (contest.StageId.HasValue)
        {
            var competitions = await _database.Competitions.AsNoTracking()
                .Include(item => item.Editions).ThenInclude(item => item.Stages)
                .Where(item => item.TenantId == report.TenantId)
                .Select(item => new
                {
                    item.Id,
                    item.Name,
                    StageIds = item.Editions.SelectMany(edition => edition.Stages)
                        .Select(stage => stage.Id)
                })
                .ToArrayAsync(cancellationToken);
            var competition = competitions.FirstOrDefault(
                item => item.StageIds.Contains(contest.StageId.Value));
            competitionId = competition?.Id;
            competitionName = competition?.Name;
        }

        var sponsorships = await _database.Sponsorships.AsNoTracking()
            .Where(item => item.TenantId == report.TenantId
                && item.Status == SportsPlatform.Modules.Sponsorship.Entities.SponsorshipStatus.Active
                && item.Placement == SportsPlatform.Modules.Sponsorship.Entities.SponsorPlacements.ContestSheet
                && (!item.StartDate.HasValue || item.StartDate <= DateTime.UtcNow)
                && (!item.EndDate.HasValue || item.EndDate >= DateTime.UtcNow)
                && (item.ContestId == contest.Id
                    || (competitionId.HasValue && item.CompetitionId == competitionId)))
            .OrderBy(item => item.DisplayOrder)
            .ThenBy(item => item.Type)
            .ToArrayAsync(cancellationToken);
        var sponsorIds = sponsorships.Select(item => item.SponsorId).Distinct().ToArray();
        var sponsorMap = await _database.Sponsors.AsNoTracking()
            .Where(item => item.TenantId == report.TenantId
                && sponsorIds.Contains(item.Id) && item.IsActive)
            .ToDictionaryAsync(item => item.Id, cancellationToken);
        var sponsors = sponsorships.Where(item => sponsorMap.ContainsKey(item.SponsorId))
            .Select(item => new ContestSheetSponsor(
                sponsorMap[item.SponsorId].Name,
                item.Type.ToString(),
                sponsorMap[item.SponsorId].LogoUrl))
            .ToArray();
        var results = await _database.ContestResults.AsNoTracking()
            .Where(item => item.ContestId == contest.Id)
            .OrderBy(item => item.Rank)
            .ToArrayAsync(cancellationToken);
        var events = await _database.LiveScores.AsNoTracking()
            .Where(item => item.TenantId == report.TenantId && item.ContestId == contest.Id)
            .OrderBy(item => item.Sequence)
            .ToArrayAsync(cancellationToken);

        var snapshot = new ContestSheetSnapshot(
            tenantName,
            branding?.ShortName,
            branding?.LogoUrl,
            branding?.PrimaryColor,
            branding?.SecondaryColor,
            competitionName,
            sport.Code,
            sport.Name,
            ContestSheetTemplate(sport.Code),
            contest,
            contest.Participants.OrderBy(item => item.SortOrder)
                .Select(item => new ContestSheetParticipant(
                    item.SortOrder,
                    participantNames.GetValueOrDefault(item.ParticipantId, "Unknown"),
                    item.Status.ToString(),
                    item.Seed,
                    item.QualificationStatus,
                    item.MadeCut))
                .ToArray(),
            contest.Segments.OrderBy(item => item.SortOrder)
                .Select(item => new ContestSheetSegment(item.Name, item.SegmentType, item.Status.ToString()))
                .ToArray(),
            contest.Officials.OrderBy(item => item.Role)
                .Select(item => new ContestSheetOfficial(
                    item.Role, item.DisplayName ?? "Unassigned", item.IsConfirmed))
                .ToArray(),
            sponsors,
            results.Select(item => new ContestSheetResult(
                    participantNames.GetValueOrDefault(item.ParticipantId, "Unknown"),
                    item.Rank,
                    item.ScoreDisplay ?? item.Score?.ToString(CultureInfo.InvariantCulture) ?? item.Time ?? "",
                    item.Status.ToString()))
                .ToArray(),
            events.Select(item => new ContestSheetEvent(
                    item.Sequence,
                    participantNames.GetValueOrDefault(item.ParticipantId, "Unknown"),
                    item.EventType,
                    item.DisplayValue ?? item.Value.ToString(CultureInfo.InvariantCulture),
                    item.ClockSeconds))
                .ToArray());

        var safeName = string.Concat(report.Name.Select(character =>
            Path.GetInvalidFileNameChars().Contains(character) ? '-' : character)).Trim();
        if (string.IsNullOrWhiteSpace(safeName)) safeName = "contest-sheet";
        return new GeneratedReportFile(
            $"{safeName}.pdf",
            "application/pdf",
            ToContestSheetPdf(snapshot, report.Id));
    }

    internal static string ContestSheetTemplate(string sportCode) =>
        sportCode.Trim().ToLowerInvariant() switch
        {
            "football" or "soccer" => "Football match sheet",
            "rugby" or "rugby-union" or "rugby-league" => "Rugby match sheet",
            "basketball" => "Basketball scoresheet",
            "cricket" or "cricket-t20" or "t20-cricket" => "Cricket scorecard",
            "golf" => "Golf scorecard",
            "mma" or "boxing" or "combat" or "combat-sports" => "Combat bout sheet",
            "athletics" or "track-field" => "Athletics event sheet",
            _ => "Contest sheet"
        };

    private async Task<GeneratedReportFile> BuildTenantDataExportAsync(
        Report report,
        CancellationToken cancellationToken)
    {
        var tenant = await _database.Tenants.AsNoTracking()
            .SingleAsync(item => item.Id == report.TenantId, cancellationToken);
        var branding = await _database.TenantBrandings.AsNoTracking()
            .Where(item => item.TenantId == report.TenantId)
            .Select(item => new
            {
                item.TenantId, item.TenantName, item.ShortName, item.LogoUrl,
                item.PrimaryColor, item.SecondaryColor, item.AccentColor,
                item.PublicSlug, item.ContactEmail, item.ContactPhone, item.Website,
                item.CreatedAt, item.UpdatedAt
            }).ToArrayAsync(cancellationToken);
        var users = await _database.Users.AsNoTracking()
            .Where(item => item.TenantId == report.TenantId)
            .Select(item => new
            {
                item.Id, item.TenantId, item.Email, item.FirstName, item.LastName,
                item.PhoneNumber, item.EmailVerified, item.PhoneVerified,
                item.IsActive, item.IsSuspended, item.SuspendedAt,
                item.SuspensionReason, item.LastLoginAt, item.CreatedAt, item.UpdatedAt
            }).ToArrayAsync(cancellationToken);
        var userIds = users.Select(item => item.Id).ToArray();
        var participants = await _database.Participants.AsNoTracking()
            .Where(item => item.TenantId == report.TenantId)
            .Select(item => new
            {
                item.Id, item.TenantId, item.Name, item.ShortName, item.Logo,
                item.Type, item.Status, item.CreatedAt, item.UpdatedAt
            }).ToArrayAsync(cancellationToken);
        var participantIds = participants.Select(item => item.Id).ToArray();
        var competitions = await _database.Competitions.AsNoTracking()
            .Include(item => item.Editions).ThenInclude(item => item.Stages)
                .ThenInclude(item => item.Groups)
            .Include(item => item.Editions).ThenInclude(item => item.Stages)
                .ThenInclude(item => item.Rounds)
            .AsSplitQuery()
            .Where(item => item.TenantId == report.TenantId)
            .ToArrayAsync(cancellationToken);
        var competitionIds = competitions.Select(item => item.Id).ToArray();
        var stageIds = competitions.SelectMany(item => item.Editions)
            .SelectMany(item => item.Stages).Select(item => item.Id).ToArray();
        var contests = await _database.Contests.AsNoTracking()
            .Include(item => item.Participants)
            .Include(item => item.Segments)
            .Include(item => item.Officials)
            .AsSplitQuery()
            .Where(item => item.TenantId == report.TenantId)
            .ToArrayAsync(cancellationToken);
        var contestIds = contests.Select(item => item.Id).ToArray();
        var results = await _database.ContestResults.AsNoTracking()
            .Where(item => contestIds.Contains(item.ContestId))
            .Select(item => new
            {
                item.Id, item.ContestId, item.EventId, item.ParticipantId,
                item.Status, item.Rank, item.Position, item.Score, item.ScoreDisplay,
                item.Time, item.PerformanceData, item.Statistics,
                item.IsPersonalBest, item.IsSeasonBest, item.IsCompetitionRecord,
                item.Remarks, item.ApprovedBy, item.ApprovedAt,
                item.CreatedAt, item.UpdatedAt
            }).ToArrayAsync(cancellationToken);
        var registrations = await _database.Registrations.AsNoTracking()
            .Where(item => participantIds.Contains(item.ParticipantId)
                && ((item.CompetitionId.HasValue
                        && competitionIds.Contains(item.CompetitionId.Value))
                    || (item.ContestId.HasValue && contestIds.Contains(item.ContestId.Value))))
            .Select(item => new
            {
                item.Id, item.ContestId, item.CompetitionId, item.ParticipantId,
                item.Type, item.Status, item.RegisteredAt, item.ConfirmedAt,
                item.CancelledAt, item.CancellationReason, item.Notes, item.EntryData,
                item.EntryFee, item.IsEntryFeePaid, item.ReviewedBy, item.ReviewedAt,
                item.ReviewNotes, item.CreatedAt, item.UpdatedAt
            }).ToArrayAsync(cancellationToken);
        var standings = await _database.Standings.AsNoTracking()
            .Where(item => competitionIds.Contains(item.CompetitionId))
            .Select(item => new
            {
                item.Id, item.CompetitionId, item.GroupId, item.ParticipantId,
                item.Position, item.Played, item.Won, item.Drawn, item.Lost,
                item.PointsFor, item.PointsAgainst, item.PointDifferential,
                item.Points, item.Metadata, item.UpdatedAt
            }).ToArrayAsync(cancellationToken);
        var liveScores = await _database.LiveScores.AsNoTracking()
            .Where(item => item.TenantId == report.TenantId
                && contestIds.Contains(item.ContestId))
            .Select(item => new
            {
                item.Id, item.TenantId, item.ContestId, item.EventId, item.EventType,
                item.ParticipantId, item.SecondaryParticipantId, item.SegmentId,
                item.ClockSeconds, item.MetricName, item.Value, item.DisplayValue,
                item.Sequence, item.Metadata, item.DeviceTimestamp,
                item.ServerTimestamp, item.ScoredAt
            }).ToArrayAsync(cancellationToken);
        var notifications = await _database.Notifications.AsNoTracking()
            .Where(item => userIds.Contains(item.UserId))
            .Select(item => new
            {
                item.Id, item.UserId, item.Type, item.Channel, item.Title,
                item.Content, item.Metadata, item.Status, item.SentAt, item.ReadAt,
                item.CreatedAt, item.UpdatedAt
            }).ToArrayAsync(cancellationToken);
        var officials = await _database.Officials.AsNoTracking()
            .Where(item => userIds.Contains(item.UserId))
            .Select(item => new
            {
                item.Id, item.UserId, item.FirstName, item.MiddleName, item.LastName,
                item.DisplayName, item.Type, item.LicenseNumber, item.LicenseExpiry,
                item.Status, item.Specializations, item.Certifications,
                item.YearsOfExperience, item.Bio, item.PhotoUrl,
                item.CreatedAt, item.UpdatedAt
            }).ToArrayAsync(cancellationToken);
        var brackets = await _database.Brackets.AsNoTracking()
            .Include(item => item.Rounds).ThenInclude(item => item.Matches)
            .AsSplitQuery()
            .Where(item => competitionIds.Contains(item.CompetitionId))
            .ToArrayAsync(cancellationToken);
        var leaderboards = await _database.Leaderboards.AsNoTracking()
            .Include(item => item.Entries)
            .AsSplitQuery()
            .Where(item => contestIds.Contains(item.ContestId))
            .ToArrayAsync(cancellationToken);
        var subscriptions = await _database.Subscriptions.AsNoTracking()
            .Where(item => item.TenantId == report.TenantId)
            .Select(item => new
            {
                item.Id, item.TenantId, item.Plan, item.StartDate, item.EndDate,
                item.GracePeriodEnd, item.Status, item.AutoRenew,
                item.FeatureEntitlements, item.UsageLimits,
                item.CreatedAt, item.UpdatedAt
            }).ToArrayAsync(cancellationToken);
        var reportArchives = await _database.Reports.AsNoTracking()
            .Where(item => item.TenantId == report.TenantId)
            .Select(item => new
            {
                item.Id, item.TenantId, item.CreatedBy, item.Name, item.Description,
                item.Type, item.Format, item.Parameters, item.Status,
                item.GeneratedAt, item.GeneratedContentType, item.GeneratedFileName,
                generatedBytes = item.GeneratedContent == null
                    ? 0
                    : item.GeneratedContent.Length,
                item.CreatedAt, item.UpdatedAt
            }).ToArrayAsync(cancellationToken);
        var auditEntries = await _database.AuditEntries.AsNoTracking()
            .Where(item => item.TenantId == report.TenantId)
            .Select(item => new
            {
                item.Id, item.TenantId, item.UserId, item.Action, item.EntityType,
                item.EntityId, item.OriginalValue, item.NewValue, item.Reason,
                item.IpAddress, item.UserAgent, item.Timestamp
            }).ToArrayAsync(cancellationToken);
        var sponsorships = await _database.Sponsorships.AsNoTracking()
            .Where(item => item.TenantId == report.TenantId
                && ((item.ContestId.HasValue && contestIds.Contains(item.ContestId.Value))
                    || (item.CompetitionId.HasValue
                        && competitionIds.Contains(item.CompetitionId.Value))))
            .ToArrayAsync(cancellationToken);
        var sponsorIds = sponsorships.Select(item => item.SponsorId).Distinct().ToArray();
        var sponsors = await _database.Sponsors.AsNoTracking()
            .Where(item => item.TenantId == report.TenantId && sponsorIds.Contains(item.Id))
            .Select(item => new
            {
                item.Id, item.TenantId, item.Name, item.Description, item.LogoUrl, item.Website,
                item.ContactEmail, item.ContactPhone, item.Tier, item.IsActive,
                item.CreatedAt, item.UpdatedAt
            }).ToArrayAsync(cancellationToken);
        var referencedSportIds = competitions.Select(item => item.SportId)
            .Concat(contests.Select(item => item.SportId)).Distinct().ToArray();
        var sports = await _database.Sports.AsNoTracking()
            .Where(item => referencedSportIds.Contains(item.Id))
            .Select(item => new
            {
                item.Id, item.Code, item.Name, item.Description,
                item.IsActive, item.Icon, item.CurrentVersion
            }).ToArrayAsync(cancellationToken);

        using var output = new MemoryStream();
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            AddJsonEntry(archive, "manifest.json", new
            {
                schema = "sports-platform-tenant-export/v1",
                exportId = report.Id,
                tenantId = report.TenantId,
                generatedAtUtc = DateTime.UtcNow,
                datasets = new Dictionary<string, int>
                {
                    ["branding"] = branding.Length,
                    ["users"] = users.Length,
                    ["sports"] = sports.Length,
                    ["participants"] = participants.Length,
                    ["competitions"] = competitions.Length,
                    ["contests"] = contests.Length,
                    ["results"] = results.Length,
                    ["registrations"] = registrations.Length,
                    ["standings"] = standings.Length,
                    ["live-scores"] = liveScores.Length,
                    ["notifications"] = notifications.Length,
                    ["officials"] = officials.Length,
                    ["brackets"] = brackets.Length,
                    ["leaderboards"] = leaderboards.Length,
                    ["subscriptions"] = subscriptions.Length,
                    ["report-archives"] = reportArchives.Length,
                    ["sponsors"] = sponsors.Length,
                    ["sponsorships"] = sponsorships.Length,
                    ["audit-entries"] = auditEntries.Length
                },
                credentialFieldsExcluded = new[]
                {
                    "passwordHash", "refreshToken", "deviceSession.refreshToken"
                }
            });
            AddJsonEntry(archive, "tenant.json", new
            {
                tenant.Id, tenant.Name, tenant.ShortName, tenant.Slug,
                tenant.Description, tenant.Country, tenant.Timezone, tenant.Currency,
                tenant.DefaultLanguage, tenant.ContactEmail, tenant.ContactPhone,
                tenant.Status, tenant.CreatedAt, tenant.UpdatedAt
            });
            AddJsonEntry(archive, "branding.json", branding);
            AddJsonEntry(archive, "users.json", users);
            AddJsonEntry(archive, "sports.json", sports);
            AddJsonEntry(archive, "participants.json", participants);
            AddJsonEntry(archive, "competitions.json", competitions.Select(item => new
            {
                item.Id, item.TenantId, item.Name, item.Description, item.SportId,
                item.RulesetVersionId, item.Status, item.CreatedAt, item.UpdatedAt,
                item.PublishedAt,
                editions = item.Editions.Select(edition => new
                {
                    edition.Id, edition.TenantId, edition.Name, edition.StartDate,
                    edition.EndDate, edition.Status, edition.CreatedAt, edition.UpdatedAt,
                    stages = edition.Stages.Select(stage => new
                    {
                        stage.Id, stage.TenantId, stage.Name, stage.Type, stage.SortOrder,
                        stage.Status, stage.GroupsCount, stage.TeamsPerGroup,
                        stage.QualifiersPerGroup, stage.BestRunnerUpCount,
                        stage.QualificationTieBreakers, stage.QualificationAppliedAt,
                        groups = stage.Groups.Select(group => new
                            { group.Id, group.Name, group.Status }),
                        rounds = stage.Rounds.Select(round => new
                            { round.Id, round.Name, round.SortOrder, round.Status })
                    })
                })
            }));
            AddJsonEntry(archive, "contests.json", contests.Select(item => new
            {
                item.Id, item.TenantId, item.StageId, item.GroupId, item.RoundId,
                item.SportId, item.RulesetVersionId, item.ContestShape, item.Name,
                item.Status, item.Venue, item.StartTime, item.EndTime,
                item.ScheduledDurationMinutes, item.SortOrder,
                participants = item.Participants.Select(entry => new
                {
                    entry.Id, entry.ParticipantId, entry.ParticipantType,
                    entry.SortOrder, entry.Status, entry.Seed, entry.MadeCut,
                    entry.QualificationStatus, entry.QualificationReason
                }),
                segments = item.Segments.Select(segment => new
                {
                    segment.Id, segment.Name, segment.SegmentType,
                    segment.SortOrder, segment.Status
                }),
                officials = item.Officials.Select(official => new
                {
                    official.Id, official.OfficialId, official.Role,
                    official.DisplayName, official.IsConfirmed
                })
            }));
            AddJsonEntry(archive, "results.json", results);
            AddJsonEntry(archive, "registrations.json", registrations);
            AddJsonEntry(archive, "standings.json", standings);
            AddJsonEntry(archive, "live-scores.json", liveScores);
            AddJsonEntry(archive, "notifications.json", notifications);
            AddJsonEntry(archive, "officials.json", officials);
            AddJsonEntry(archive, "brackets.json", brackets.Select(item => new
            {
                item.Id, item.CompetitionId, item.Name, item.Type, item.Size,
                item.IsSeeded, item.DrawSeed, item.Status, item.ChampionId,
                item.CreatedAt, item.UpdatedAt,
                rounds = item.Rounds.Select(round => new
                {
                    round.Id, round.RoundNumber, round.Name, round.MatchCount,
                    round.ScheduledAt,
                    matches = round.Matches.Select(match => new
                    {
                        match.Id, match.MatchNumber, match.Participant1Id,
                        match.Participant2Id, match.WinnerId, match.ContestId,
                        match.SeriesId, match.BestOf, match.IsAggregateTie,
                        match.IsThirdPlace, match.NextMatchId, match.NextMatchSlot,
                        match.Status, match.Score1, match.Score2,
                        match.ScheduledAt, match.CompletedAt
                    })
                })
            }));
            AddJsonEntry(archive, "leaderboards.json", leaderboards.Select(item => new
            {
                item.Id, item.ContestId, item.Name, item.Description, item.Type,
                item.Scope, item.IsActive, item.CreatedAt, item.UpdatedAt,
                entries = item.Entries.Select(entry => new
                {
                    entry.Id, entry.ParticipantId, entry.Rank, entry.Score,
                    entry.ScoreDisplay, entry.PreviousRank, entry.UpdatedAt
                })
            }));
            AddJsonEntry(archive, "subscriptions.json", subscriptions);
            AddJsonEntry(archive, "report-archives.json", reportArchives);
            AddJsonEntry(archive, "sponsors.json", sponsors);
            AddJsonEntry(archive, "sponsorships.json", sponsorships.Select(item => new
            {
                item.Id, item.TenantId, item.SponsorId, item.ContestId, item.CompetitionId,
                item.Type, item.Name, item.Description, item.Value, item.Currency,
                item.Status, item.StartDate, item.EndDate, item.Benefits,
                item.Placement, item.DisplayOrder, item.CreatedAt, item.UpdatedAt
            }));
            AddJsonEntry(archive, "audit-entries.json", auditEntries);
        }
        var safeName = string.Concat(report.Name.Select(character =>
            Path.GetInvalidFileNameChars().Contains(character) ? '-' : character)).Trim();
        if (string.IsNullOrWhiteSpace(safeName)) safeName = "tenant-data-export";
        return new GeneratedReportFile(
            $"{safeName}.zip", "application/zip", output.ToArray());
    }

    private static readonly JsonSerializerOptions ExportJsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private static void AddJsonEntry(ZipArchive archive, string name, object value)
    {
        var entry = archive.CreateEntry(name, CompressionLevel.Optimal);
        using var stream = entry.Open();
        JsonSerializer.Serialize(stream, value, value.GetType(), ExportJsonOptions);
    }

    private async Task<ReportDocument> BuildRegistrationListAsync(
        Guid tenantId,
        ReportParameters parameters,
        CancellationToken cancellationToken)
    {
        var participants = await _database.Participants.AsNoTracking()
            .Where(item => item.TenantId == tenantId)
            .Select(item => new { item.Id, item.Name, item.Type })
            .ToArrayAsync(cancellationToken);
        var participantIds = participants.Select(item => item.Id).ToArray();
        var participantMap = participants.ToDictionary(item => item.Id);
        var competitionMap = await CompetitionMapAsync(tenantId, cancellationToken);
        var stageMap = await CompetitionStageMapAsync(tenantId, cancellationToken);
        var contests = await _database.Contests.AsNoTracking()
            .Where(item => item.TenantId == tenantId)
            .Select(item => new { item.Id, item.Name, item.StageId, item.SortOrder })
            .ToArrayAsync(cancellationToken);
        var contestMap = contests.ToDictionary(item => item.Id);
        var matchingContestIds = parameters.CompetitionId.HasValue
            ? contests.Where(item => item.StageId.HasValue
                    && stageMap.TryGetValue(item.StageId.Value, out var competition)
                    && competition.Id == parameters.CompetitionId.Value)
                .Select(item => item.Id).ToHashSet()
            : null;
        var registrations = await _database.Registrations.AsNoTracking()
            .Where(item => participantIds.Contains(item.ParticipantId)
                && (!parameters.CompetitionId.HasValue
                    || item.CompetitionId == parameters.CompetitionId
                    || (matchingContestIds != null
                        && matchingContestIds.Contains(item.ContestId ?? Guid.Empty)))
                && (!parameters.FromUtc.HasValue
                    || (item.RegisteredAt ?? item.CreatedAt) >= parameters.FromUtc)
                && (!parameters.ToUtc.HasValue
                    || (item.RegisteredAt ?? item.CreatedAt) <= parameters.ToUtc))
            .OrderBy(item => item.CompetitionId)
            .ThenBy(item => item.Status)
            .ThenBy(item => item.CreatedAt)
            .ToArrayAsync(cancellationToken);
        var rows = registrations.Select(registration =>
        {
            participantMap.TryGetValue(registration.ParticipantId, out var participant);
            contestMap.TryGetValue(registration.ContestId ?? Guid.Empty, out var contest);
            var competitionName = registration.CompetitionId.HasValue
                ? competitionMap.GetValueOrDefault(registration.CompetitionId.Value, "Unassigned")
                : contest?.StageId is Guid stageId && stageMap.TryGetValue(stageId, out var competition)
                    ? competition.Name
                    : "Unassigned";
            return new[]
            {
                competitionName,
                contest is null ? "" : contest.Name ?? $"Contest {contest.SortOrder}",
                participant?.Name ?? "Unknown",
                participant?.Type.ToString() ?? "Unknown",
                registration.Type.ToString(),
                registration.Status.ToString(),
                (registration.RegisteredAt ?? registration.CreatedAt)
                    .ToUniversalTime().ToString("u", CultureInfo.InvariantCulture),
                registration.EntryFee?.ToString("0.00", CultureInfo.InvariantCulture) ?? "",
                registration.IsEntryFeePaid ? "Yes" : "No",
                registration.ReviewedAt?.ToUniversalTime()
                    .ToString("u", CultureInfo.InvariantCulture) ?? "",
                registration.ReviewNotes ?? registration.Notes ?? ""
            };
        }).ToArray();
        return new ReportDocument(
            "Registration list",
            new[]
            {
                "Competition", "Contest", "Participant", "Participant type",
                "Registration type", "Status", "Registered (UTC)", "Entry fee",
                "Fee paid", "Reviewed (UTC)", "Notes"
            },
            rows);
    }

    private async Task<ReportDocument> BuildTeamRosterAsync(
        Guid tenantId,
        ReportParameters parameters,
        CancellationToken cancellationToken)
    {
        var rosters = await _database.Set<TeamRoster>().AsNoTracking()
            .Include(item => item.Entries)
            .Where(item => item.TenantId == tenantId
                && (!parameters.CompetitionId.HasValue
                    || item.CompetitionId == parameters.CompetitionId)
                && (!parameters.FromUtc.HasValue || item.CreatedAt >= parameters.FromUtc)
                && (!parameters.ToUtc.HasValue || item.CreatedAt <= parameters.ToUtc))
            .OrderBy(item => item.TeamId)
            .ThenBy(item => item.Name)
            .ToArrayAsync(cancellationToken);
        var teamIds = rosters.Select(item => item.TeamId).Distinct().ToArray();
        var athleteIds = rosters.SelectMany(item => item.Entries)
            .Select(item => item.AthleteId).Distinct().ToArray();
        var teams = await _database.Participants.OfType<Team>().AsNoTracking()
            .Where(item => item.TenantId == tenantId && teamIds.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, item => item.Name, cancellationToken);
        var athletes = await _database.Participants.OfType<Athlete>().AsNoTracking()
            .Where(item => item.TenantId == tenantId && athleteIds.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, item => item.Name, cancellationToken);
        var competitionMap = await CompetitionMapAsync(tenantId, cancellationToken);
        var rows = new List<IReadOnlyList<string>>();
        foreach (var roster in rosters)
        {
            var entries = roster.Entries.OrderBy(item => item.JerseyNumber)
                .ThenBy(item => athletes.GetValueOrDefault(item.AthleteId, "Unknown"))
                .ToArray();
            if (entries.Length == 0)
            {
                rows.Add(RosterRow(roster, null));
                continue;
            }
            rows.AddRange(entries.Select(entry => RosterRow(roster, entry)));
        }
        return new ReportDocument(
            "Team roster",
            new[]
            {
                "Competition", "Team", "Roster", "Athlete", "Jersey number",
                "Position", "Roster active", "Entry active", "Roster created (UTC)"
            },
            rows);

        IReadOnlyList<string> RosterRow(TeamRoster roster, RosterEntry? entry) => new[]
        {
            roster.CompetitionId.HasValue
                ? competitionMap.GetValueOrDefault(roster.CompetitionId.Value, "Unassigned")
                : "All competitions",
            teams.GetValueOrDefault(roster.TeamId, "Unknown"),
            roster.Name,
            entry is null ? "" : athletes.GetValueOrDefault(entry.AthleteId, "Unknown"),
            entry?.JerseyNumber ?? "",
            entry?.Position ?? "",
            roster.IsActive ? "Yes" : "No",
            entry is null ? "" : entry.IsActive ? "Yes" : "No",
            roster.CreatedAt.ToUniversalTime().ToString("u", CultureInfo.InvariantCulture)
        };
    }

    private async Task<ReportDocument> BuildFixtureScheduleAsync(
        Guid tenantId,
        ReportParameters parameters,
        CancellationToken cancellationToken)
    {
        var stageMap = await CompetitionStageMapAsync(tenantId, cancellationToken);
        var competitionStageIds = parameters.CompetitionId.HasValue
            ? stageMap.Where(item => item.Value.Id == parameters.CompetitionId.Value)
                .Select(item => item.Key).ToHashSet()
            : null;
        var contests = await _database.Contests.AsNoTracking()
            .Include(item => item.Participants)
            .Where(item => item.TenantId == tenantId
                && (!parameters.FromUtc.HasValue || item.StartTime >= parameters.FromUtc)
                && (!parameters.ToUtc.HasValue || item.StartTime <= parameters.ToUtc)
                && (competitionStageIds == null
                    || (item.StageId.HasValue && competitionStageIds.Contains(item.StageId.Value))))
            .OrderBy(item => item.StartTime)
            .ThenBy(item => item.SortOrder)
            .ToArrayAsync(cancellationToken);
        var participantIds = contests.SelectMany(item => item.Participants)
            .Select(item => item.ParticipantId).Distinct().ToArray();
        var participantNames = await _database.Participants.AsNoTracking()
            .Where(item => item.TenantId == tenantId && participantIds.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, item => item.Name, cancellationToken);
        var rows = contests.Select(contest => new[]
        {
            contest.StageId.HasValue && stageMap.TryGetValue(contest.StageId.Value, out var competition)
                ? competition.Name : "Unassigned",
            contest.Name ?? $"Contest {contest.SortOrder}",
            contest.StartTime?.ToUniversalTime().ToString("u", CultureInfo.InvariantCulture) ?? "Unscheduled",
            contest.Venue ?? "",
            contest.Status.ToString(),
            string.Join(" vs ", contest.Participants.OrderBy(item => item.SortOrder)
                .Select(item => participantNames.GetValueOrDefault(item.ParticipantId, "Unknown")))
        }).ToArray();
        return new ReportDocument(
            "Fixture schedule",
            new[] { "Competition", "Contest", "Start (UTC)", "Venue", "Status", "Participants" },
            rows);
    }

    private async Task<ReportDocument> BuildOfficialAssignmentsAsync(
        Guid tenantId,
        ReportParameters parameters,
        CancellationToken cancellationToken)
    {
        var stageMap = await CompetitionStageMapAsync(tenantId, cancellationToken);
        var competitionStageIds = parameters.CompetitionId.HasValue
            ? stageMap.Where(item => item.Value.Id == parameters.CompetitionId.Value)
                .Select(item => item.Key).ToHashSet()
            : null;
        var contests = await _database.Contests.AsNoTracking()
            .Include(item => item.Officials)
            .Where(item => item.TenantId == tenantId
                && (!parameters.FromUtc.HasValue || item.StartTime >= parameters.FromUtc)
                && (!parameters.ToUtc.HasValue || item.StartTime <= parameters.ToUtc)
                && (competitionStageIds == null
                    || (item.StageId.HasValue && competitionStageIds.Contains(item.StageId.Value))))
            .OrderBy(item => item.StartTime)
            .ThenBy(item => item.SortOrder)
            .ToArrayAsync(cancellationToken);
        var sportIds = contests.Select(item => item.SportId).Distinct().ToArray();
        var sports = await _database.Sports.AsNoTracking()
            .Where(item => sportIds.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, item => item.Name, cancellationToken);
        var officialIds = contests.SelectMany(item => item.Officials)
            .Select(item => item.OfficialId).Distinct().ToArray();
        var tenantUserIds = await _database.Users.AsNoTracking()
            .Where(item => item.TenantId == tenantId)
            .Select(item => item.Id)
            .ToArrayAsync(cancellationToken);
        var officials = await _database.Officials.AsNoTracking()
            .Where(item => officialIds.Contains(item.Id)
                && tenantUserIds.Contains(item.UserId))
            .Select(item => new
            {
                item.Id,
                item.FirstName,
                item.MiddleName,
                item.LastName,
                item.DisplayName
            })
            .ToArrayAsync(cancellationToken);
        var officialNames = officials.ToDictionary(
            item => item.Id,
            item => !string.IsNullOrWhiteSpace(item.DisplayName)
                ? item.DisplayName!
                : string.Join(' ', new[] { item.FirstName, item.MiddleName, item.LastName }
                    .Where(part => !string.IsNullOrWhiteSpace(part))));
        var rows = contests.SelectMany(contest => contest.Officials
            .OrderBy(item => item.Role)
            .ThenBy(item => item.DisplayName)
            .Select(official => new[]
            {
                contest.StageId.HasValue && stageMap.TryGetValue(contest.StageId.Value, out var competition)
                    ? competition.Name : "Unassigned",
                sports.GetValueOrDefault(contest.SportId, "Unknown"),
                contest.Name ?? $"Contest {contest.SortOrder}",
                contest.StartTime?.ToUniversalTime()
                    .ToString("u", CultureInfo.InvariantCulture) ?? "Unscheduled",
                contest.Venue ?? "",
                official.DisplayName
                    ?? officialNames.GetValueOrDefault(official.OfficialId, "Unknown"),
                official.Role,
                official.IsConfirmed ? "Confirmed" : "Pending",
                contest.Status.ToString()
            })).ToArray();
        return new ReportDocument(
            "Official assignments",
            new[]
            {
                "Competition", "Sport", "Contest", "Start (UTC)", "Venue",
                "Official", "Role", "Confirmation", "Contest status"
            },
            rows);
    }

    private async Task<ReportDocument> BuildMatchSheetsAsync(
        Guid tenantId,
        ReportParameters parameters,
        CancellationToken cancellationToken)
    {
        var stageMap = await CompetitionStageMapAsync(tenantId, cancellationToken);
        var competitionStageIds = parameters.CompetitionId.HasValue
            ? stageMap.Where(item => item.Value.Id == parameters.CompetitionId.Value)
                .Select(item => item.Key).ToHashSet()
            : null;
        var contests = await _database.Contests.AsNoTracking()
            .Include(item => item.Participants)
            .Include(item => item.Segments)
            .Include(item => item.Officials)
            .Where(item => item.TenantId == tenantId
                && (!parameters.FromUtc.HasValue || item.StartTime >= parameters.FromUtc)
                && (!parameters.ToUtc.HasValue || item.StartTime <= parameters.ToUtc)
                && (competitionStageIds == null
                    || (item.StageId.HasValue && competitionStageIds.Contains(item.StageId.Value))))
            .OrderBy(item => item.StartTime)
            .ThenBy(item => item.SortOrder)
            .ToArrayAsync(cancellationToken);
        var participantIds = contests.SelectMany(item => item.Participants)
            .Select(item => item.ParticipantId).Distinct().ToArray();
        var participantNames = await _database.Participants.AsNoTracking()
            .Where(item => item.TenantId == tenantId && participantIds.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, item => item.Name, cancellationToken);
        var sportIds = contests.Select(item => item.SportId).Distinct().ToArray();
        var sports = await _database.Sports.AsNoTracking()
            .Where(item => sportIds.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, item => item.Name, cancellationToken);
        var rows = new List<IReadOnlyList<string>>();
        foreach (var contest in contests)
        {
            var participants = contest.Participants.OrderBy(item => item.SortOrder).ToArray();
            if (participants.Length == 0)
            {
                rows.Add(MatchRow(contest, null));
                continue;
            }
            rows.AddRange(participants.Select(participant => MatchRow(contest, participant)));
        }
        return new ReportDocument(
            "Match sheets",
            new[]
            {
                "Competition", "Sport", "Contest", "Start (UTC)", "Venue",
                "Status", "Contest format", "Duration (minutes)", "Participant slot",
                "Participant", "Participant type", "Segments", "Officials"
            },
            rows);

        IReadOnlyList<string> MatchRow(
            SportsPlatform.Modules.Contests.Entities.Contest contest,
            SportsPlatform.Modules.Contests.Entities.ContestParticipant? participant) => new[]
        {
            contest.StageId.HasValue && stageMap.TryGetValue(contest.StageId.Value, out var competition)
                ? competition.Name : "Unassigned",
            sports.GetValueOrDefault(contest.SportId, "Unknown"),
            contest.Name ?? $"Contest {contest.SortOrder}",
            contest.StartTime?.ToUniversalTime()
                .ToString("u", CultureInfo.InvariantCulture) ?? "Unscheduled",
            contest.Venue ?? "",
            contest.Status.ToString(),
            contest.ContestShape,
            contest.ScheduledDurationMinutes.ToString(CultureInfo.InvariantCulture),
            participant?.SortOrder.ToString(CultureInfo.InvariantCulture) ?? "",
            participant is null
                ? ""
                : participantNames.GetValueOrDefault(participant.ParticipantId, "Unknown"),
            participant?.ParticipantType ?? "",
            string.Join(" | ", contest.Segments.OrderBy(item => item.SortOrder)
                .Select(item => item.Name)),
            string.Join(" | ", contest.Officials.OrderBy(item => item.Role)
                .Select(item => $"{item.Role}: {item.DisplayName ?? "Unknown"}"))
        };
    }

    private async Task<ReportDocument> BuildResultsAsync(
        Guid tenantId,
        ReportParameters parameters,
        CancellationToken cancellationToken)
    {
        var stageMap = await CompetitionStageMapAsync(tenantId, cancellationToken);
        var competitionStageIds = parameters.CompetitionId.HasValue
            ? stageMap.Where(item => item.Value.Id == parameters.CompetitionId.Value)
                .Select(item => item.Key).ToHashSet()
            : null;
        var contests = await _database.Contests.AsNoTracking()
            .Where(item => item.TenantId == tenantId
                && (!parameters.FromUtc.HasValue || item.StartTime >= parameters.FromUtc)
                && (!parameters.ToUtc.HasValue || item.StartTime <= parameters.ToUtc)
                && (competitionStageIds == null
                    || (item.StageId.HasValue && competitionStageIds.Contains(item.StageId.Value))))
            .Select(item => new { item.Id, item.Name, item.StageId, item.SortOrder })
            .ToArrayAsync(cancellationToken);
        var contestIds = contests.Select(item => item.Id).ToArray();
        var contestMap = contests.ToDictionary(item => item.Id);
        var results = await _database.ContestResults.AsNoTracking()
            .Where(item => contestIds.Contains(item.ContestId))
            .OrderBy(item => item.ContestId).ThenBy(item => item.Rank)
            .ToArrayAsync(cancellationToken);
        var participantIds = results.Select(item => item.ParticipantId).Distinct().ToArray();
        var participantNames = await _database.Participants.AsNoTracking()
            .Where(item => item.TenantId == tenantId && participantIds.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, item => item.Name, cancellationToken);
        var rows = results.Select(result =>
        {
            var contest = contestMap[result.ContestId];
            return new[]
            {
                contest.StageId.HasValue && stageMap.TryGetValue(contest.StageId.Value, out var competition)
                    ? competition.Name : "Unassigned",
                contest.Name ?? $"Contest {contest.SortOrder}",
                participantNames.GetValueOrDefault(result.ParticipantId, "Unknown"),
                result.Rank?.ToString(CultureInfo.InvariantCulture) ?? "",
                result.ScoreDisplay ?? result.Score?.ToString(CultureInfo.InvariantCulture) ?? result.Time ?? "",
                result.Status.ToString(),
                result.ApprovedAt?.ToUniversalTime().ToString("u", CultureInfo.InvariantCulture) ?? ""
            };
        }).ToArray();
        return new ReportDocument(
            "Results",
            new[] { "Competition", "Contest", "Participant", "Rank", "Result", "Status", "Approved (UTC)" },
            rows);
    }

    private async Task<ReportDocument> BuildStandingsAsync(
        Guid tenantId,
        ReportParameters parameters,
        CancellationToken cancellationToken)
    {
        var competitions = await _database.Competitions.AsNoTracking()
            .Include(item => item.Editions).ThenInclude(item => item.Stages)
                .ThenInclude(item => item.Groups)
            .Where(item => item.TenantId == tenantId
                && (!parameters.CompetitionId.HasValue
                    || item.Id == parameters.CompetitionId))
            .ToArrayAsync(cancellationToken);
        var competitionIds = competitions.Select(item => item.Id).ToArray();
        var competitionMap = competitions.ToDictionary(item => item.Id, item => item.Name);
        var groupMap = competitions.SelectMany(item => item.Editions)
            .SelectMany(item => item.Stages)
            .SelectMany(item => item.Groups)
            .ToDictionary(item => item.Id, item => item.Name);
        var standings = await _database.Standings.AsNoTracking()
            .Where(item => competitionIds.Contains(item.CompetitionId)
                && (!parameters.FromUtc.HasValue || item.UpdatedAt >= parameters.FromUtc)
                && (!parameters.ToUtc.HasValue || item.UpdatedAt <= parameters.ToUtc))
            .OrderBy(item => item.CompetitionId)
            .ThenBy(item => item.GroupId)
            .ThenBy(item => item.Position)
            .ToArrayAsync(cancellationToken);
        var participantIds = standings.Select(item => item.ParticipantId).Distinct().ToArray();
        var participantNames = await _database.Participants.AsNoTracking()
            .Where(item => item.TenantId == tenantId && participantIds.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, item => item.Name, cancellationToken);
        var rows = standings.Select(standing => new[]
        {
            competitionMap.GetValueOrDefault(standing.CompetitionId, "Unknown"),
            standing.GroupId == Guid.Empty
                ? "Overall"
                : groupMap.GetValueOrDefault(standing.GroupId, "Unknown"),
            standing.Position.ToString(CultureInfo.InvariantCulture),
            participantNames.GetValueOrDefault(standing.ParticipantId, "Unknown"),
            standing.Played.ToString(CultureInfo.InvariantCulture),
            standing.Won.ToString(CultureInfo.InvariantCulture),
            standing.Drawn.ToString(CultureInfo.InvariantCulture),
            standing.Lost.ToString(CultureInfo.InvariantCulture),
            standing.PointsFor.ToString(CultureInfo.InvariantCulture),
            standing.PointsAgainst.ToString(CultureInfo.InvariantCulture),
            standing.PointDifferential.ToString(CultureInfo.InvariantCulture),
            standing.Points.ToString("0.##", CultureInfo.InvariantCulture),
            standing.Metadata ?? "",
            standing.UpdatedAt.ToUniversalTime().ToString("u", CultureInfo.InvariantCulture)
        }).ToArray();
        return new ReportDocument(
            "Standings",
            new[]
            {
                "Competition", "Group", "Position", "Participant", "Played",
                "Won", "Drawn", "Lost", "Points for", "Points against",
                "Differential", "Points", "Additional metrics", "Updated (UTC)"
            },
            rows);
    }

    private async Task<ReportDocument> BuildCompetitionSummaryAsync(
        Guid tenantId,
        ReportParameters parameters,
        CancellationToken cancellationToken)
    {
        var competitions = await _database.Competitions.AsNoTracking()
            .Include(item => item.Editions).ThenInclude(item => item.Stages)
            .Where(item => item.TenantId == tenantId
                && (!parameters.CompetitionId.HasValue
                    || item.Id == parameters.CompetitionId)
                && (!parameters.FromUtc.HasValue || item.CreatedAt >= parameters.FromUtc)
                && (!parameters.ToUtc.HasValue || item.CreatedAt <= parameters.ToUtc))
            .OrderBy(item => item.Name)
            .ToArrayAsync(cancellationToken);
        var competitionIds = competitions.Select(item => item.Id).ToArray();
        var stageOwners = competitions.SelectMany(competition => competition.Editions
                .SelectMany(edition => edition.Stages)
                .Select(stage => new { StageId = stage.Id, CompetitionId = competition.Id }))
            .ToDictionary(item => item.StageId, item => item.CompetitionId);
        var stageIds = stageOwners.Keys.ToArray();
        var contests = await _database.Contests.AsNoTracking()
            .Include(item => item.Participants)
            .Where(item => item.TenantId == tenantId
                && item.StageId.HasValue
                && stageIds.Contains(item.StageId.Value))
            .ToArrayAsync(cancellationToken);
        var contestIds = contests.Select(item => item.Id).ToArray();
        var registrations = await _database.Registrations.AsNoTracking()
            .Where(item => item.CompetitionId.HasValue
                && competitionIds.Contains(item.CompetitionId.Value))
            .Select(item => new { item.CompetitionId, item.ParticipantId })
            .ToArrayAsync(cancellationToken);
        var resultCounts = await _database.ContestResults.AsNoTracking()
            .Where(item => contestIds.Contains(item.ContestId))
            .GroupBy(item => item.ContestId)
            .Select(group => new { ContestId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.ContestId, item => item.Count, cancellationToken);
        var standingCounts = await _database.Standings.AsNoTracking()
            .Where(item => competitionIds.Contains(item.CompetitionId))
            .GroupBy(item => item.CompetitionId)
            .Select(group => new { CompetitionId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.CompetitionId, item => item.Count, cancellationToken);
        var sportIds = competitions.Select(item => item.SportId).Distinct().ToArray();
        var sports = await _database.Sports.AsNoTracking()
            .Where(item => sportIds.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, item => item.Name, cancellationToken);
        var rows = competitions.Select(competition =>
        {
            var competitionContests = contests.Where(item => item.StageId.HasValue
                    && stageOwners.GetValueOrDefault(item.StageId.Value) == competition.Id)
                .ToArray();
            var registrationParticipants = registrations
                .Where(item => item.CompetitionId == competition.Id)
                .Select(item => item.ParticipantId);
            var contestParticipants = competitionContests.SelectMany(item => item.Participants)
                .Select(item => item.ParticipantId);
            var startDates = competition.Editions.Select(item => item.StartDate).ToArray();
            var endDates = competition.Editions.Where(item => item.EndDate.HasValue)
                .Select(item => item.EndDate!.Value).ToArray();
            return new[]
            {
                competition.Name,
                sports.GetValueOrDefault(competition.SportId, "Unknown"),
                competition.Status.ToString(),
                competition.CreatedAt.ToUniversalTime().ToString("u", CultureInfo.InvariantCulture),
                competition.PublishedAt?.ToUniversalTime()
                    .ToString("u", CultureInfo.InvariantCulture) ?? "",
                startDates.Length == 0 ? "" : startDates.Min().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                endDates.Length == 0 ? "" : endDates.Max().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                competition.Editions.Count.ToString(CultureInfo.InvariantCulture),
                competition.Editions.Sum(item => item.Stages.Count)
                    .ToString(CultureInfo.InvariantCulture),
                registrations.Count(item => item.CompetitionId == competition.Id)
                    .ToString(CultureInfo.InvariantCulture),
                registrationParticipants.Concat(contestParticipants).Distinct().Count()
                    .ToString(CultureInfo.InvariantCulture),
                competitionContests.Length.ToString(CultureInfo.InvariantCulture),
                competitionContests.Count(item => item.Status.ToString() is "Completed" or "Approved" or "Published")
                    .ToString(CultureInfo.InvariantCulture),
                competitionContests.Sum(item => resultCounts.GetValueOrDefault(item.Id))
                    .ToString(CultureInfo.InvariantCulture),
                standingCounts.GetValueOrDefault(competition.Id)
                    .ToString(CultureInfo.InvariantCulture)
            };
        }).ToArray();
        return new ReportDocument(
            "Competition summary",
            new[]
            {
                "Competition", "Sport", "Status", "Created (UTC)", "Published (UTC)",
                "Start date", "End date", "Editions", "Stages", "Registrations",
                "Distinct participants", "Contests", "Completed contests", "Result rows",
                "Standing rows"
            },
            rows);
    }

    private async Task<ReportDocument> BuildDisciplineAsync(
        Guid tenantId,
        ReportParameters parameters,
        CancellationToken cancellationToken)
    {
        var stageMap = await CompetitionStageMapAsync(tenantId, cancellationToken);
        var competitionStageIds = parameters.CompetitionId.HasValue
            ? stageMap.Where(item => item.Value.Id == parameters.CompetitionId.Value)
                .Select(item => item.Key).ToHashSet()
            : null;
        var contests = await _database.Contests.AsNoTracking()
            .Include(item => item.Segments)
            .Where(item => item.TenantId == tenantId
                && (competitionStageIds == null
                    || (item.StageId.HasValue && competitionStageIds.Contains(item.StageId.Value))))
            .Select(item => new
            {
                item.Id,
                item.Name,
                item.StageId,
                item.SportId,
                item.SortOrder,
                Segments = item.Segments.Select(segment => new { segment.Id, segment.Name }).ToArray()
            })
            .ToArrayAsync(cancellationToken);
        var contestIds = contests.Select(item => item.Id).ToArray();
        var contestMap = contests.ToDictionary(item => item.Id);
        var liveEvents = await _database.LiveScores.AsNoTracking()
            .Where(item => item.TenantId == tenantId
                && contestIds.Contains(item.ContestId)
                && (!parameters.FromUtc.HasValue || item.ServerTimestamp >= parameters.FromUtc)
                && (!parameters.ToUtc.HasValue || item.ServerTimestamp <= parameters.ToUtc))
            .ToArrayAsync(cancellationToken);
        var disciplinaryEvents = liveEvents.Where(item =>
            IsDisciplinary(item.EventType, item.MetricName)).ToArray();
        var disqualifications = await _database.ContestCheckInHistory.AsNoTracking()
            .Where(item => item.TenantId == tenantId
                && contestIds.Contains(item.ContestId)
                && item.Action == "ParticipantDisqualified"
                && (!parameters.FromUtc.HasValue || item.OccurredAt >= parameters.FromUtc)
                && (!parameters.ToUtc.HasValue || item.OccurredAt <= parameters.ToUtc))
            .ToArrayAsync(cancellationToken);
        var participantIds = disciplinaryEvents.Select(item => item.ParticipantId)
            .Concat(disqualifications.Where(item => item.SubjectId.HasValue)
                .Select(item => item.SubjectId!.Value))
            .Distinct().ToArray();
        var participantNames = await _database.Participants.AsNoTracking()
            .Where(item => item.TenantId == tenantId && participantIds.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, item => item.Name, cancellationToken);
        var sportIds = contests.Select(item => item.SportId).Distinct().ToArray();
        var sports = await _database.Sports.AsNoTracking()
            .Where(item => sportIds.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, item => item.Name, cancellationToken);
        var rows = new List<ChronologicalReportRow>();
        rows.AddRange(disciplinaryEvents.Select(item =>
        {
            var contest = contestMap[item.ContestId];
            var segment = item.SegmentId.HasValue
                ? contest.Segments.FirstOrDefault(value => value.Id == item.SegmentId.Value)?.Name
                : null;
            return new ChronologicalReportRow(item.ServerTimestamp, new[]
            {
                CompetitionName(contest.StageId),
                sports.GetValueOrDefault(contest.SportId, "Unknown"),
                contest.Name ?? $"Contest {contest.SortOrder}",
                participantNames.GetValueOrDefault(item.ParticipantId, "Unknown"),
                item.EventType,
                item.MetricName,
                item.DisplayValue ?? item.Value.ToString(CultureInfo.InvariantCulture),
                segment ?? "",
                item.Metadata ?? "",
                item.ServerTimestamp.ToUniversalTime().ToString("u", CultureInfo.InvariantCulture)
            });
        }));
        rows.AddRange(disqualifications.Select(item =>
        {
            var contest = contestMap[item.ContestId];
            return new ChronologicalReportRow(item.OccurredAt, new[]
            {
                CompetitionName(contest.StageId),
                sports.GetValueOrDefault(contest.SportId, "Unknown"),
                contest.Name ?? $"Contest {contest.SortOrder}",
                item.SubjectId.HasValue
                    ? participantNames.GetValueOrDefault(item.SubjectId.Value, "Unknown")
                    : "Unknown",
                "ParticipantDisqualified",
                "disqualifications",
                "1",
                "",
                item.Reason ?? "",
                item.OccurredAt.ToUniversalTime().ToString("u", CultureInfo.InvariantCulture)
            });
        }));
        return new ReportDocument(
            "Discipline",
            new[]
            {
                "Competition", "Sport", "Contest", "Participant", "Incident",
                "Metric", "Value", "Segment", "Details", "Occurred (UTC)"
            },
            rows.OrderBy(item => item.OccurredAt).Select(item => item.Values).ToArray());

        string CompetitionName(Guid? stageId) => stageId.HasValue
            && stageMap.TryGetValue(stageId.Value, out var competition)
                ? competition.Name
                : "Unassigned";
    }

    private async Task<ReportDocument> BuildAthletePerformanceAsync(
        Guid tenantId,
        ReportParameters parameters,
        CancellationToken cancellationToken)
    {
        var stageMap = await CompetitionStageMapAsync(tenantId, cancellationToken);
        var competitionStageIds = parameters.CompetitionId.HasValue
            ? stageMap.Where(item => item.Value.Id == parameters.CompetitionId.Value)
                .Select(item => item.Key).ToHashSet()
            : null;
        var contests = await _database.Contests.AsNoTracking()
            .Where(item => item.TenantId == tenantId
                && (competitionStageIds == null
                    || (item.StageId.HasValue && competitionStageIds.Contains(item.StageId.Value))))
            .Select(item => new
            {
                item.Id, item.Name, item.StageId, item.SportId, item.SortOrder
            })
            .ToArrayAsync(cancellationToken);
        var contestIds = contests.Select(item => item.Id).ToArray();
        var contestMap = contests.ToDictionary(item => item.Id);
        var athletes = await _database.Participants.OfType<Athlete>().AsNoTracking()
            .Where(item => item.TenantId == tenantId)
            .Select(item => new { item.Id, item.Name })
            .ToArrayAsync(cancellationToken);
        var athleteIds = athletes.Select(item => item.Id).ToArray();
        var athleteNames = athletes.ToDictionary(item => item.Id, item => item.Name);
        var results = await _database.ContestResults.AsNoTracking()
            .Where(item => contestIds.Contains(item.ContestId)
                && athleteIds.Contains(item.ParticipantId)
                && (!parameters.FromUtc.HasValue
                    || (item.UpdatedAt ?? item.CreatedAt) >= parameters.FromUtc)
                && (!parameters.ToUtc.HasValue
                    || (item.UpdatedAt ?? item.CreatedAt) <= parameters.ToUtc))
            .OrderBy(item => item.ContestId)
            .ThenBy(item => item.Rank)
            .ToArrayAsync(cancellationToken);
        var sportIds = contests.Select(item => item.SportId).Distinct().ToArray();
        var sports = await _database.Sports.AsNoTracking()
            .Where(item => sportIds.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, item => item.Name, cancellationToken);
        var rows = results.Select(result =>
        {
            var contest = contestMap[result.ContestId];
            return new[]
            {
                contest.StageId.HasValue && stageMap.TryGetValue(contest.StageId.Value, out var competition)
                    ? competition.Name : "Unassigned",
                sports.GetValueOrDefault(contest.SportId, "Unknown"),
                contest.Name ?? $"Contest {contest.SortOrder}",
                athleteNames.GetValueOrDefault(result.ParticipantId, "Unknown"),
                result.Rank?.ToString(CultureInfo.InvariantCulture) ?? "",
                result.Position ?? "",
                result.ScoreDisplay ?? result.Score?.ToString(CultureInfo.InvariantCulture)
                    ?? result.Time ?? "",
                result.Status.ToString(),
                result.IsPersonalBest ? "Yes" : "No",
                result.IsSeasonBest ? "Yes" : "No",
                result.IsCompetitionRecord ? "Yes" : "No",
                result.PerformanceData ?? "",
                result.Statistics ?? "",
                result.Remarks ?? "",
                (result.UpdatedAt ?? result.CreatedAt).ToUniversalTime()
                    .ToString("u", CultureInfo.InvariantCulture)
            };
        }).ToArray();
        return new ReportDocument(
            "Athlete performance",
            new[]
            {
                "Competition", "Sport", "Contest", "Athlete", "Rank", "Position",
                "Performance", "Status", "Personal best", "Season best",
                "Competition record", "Performance data", "Statistics", "Remarks",
                "Updated (UTC)"
            },
            rows);
    }

    private async Task<ReportDocument> BuildAuditHistoryAsync(
        Guid tenantId,
        ReportParameters parameters,
        CancellationToken cancellationToken)
    {
        HashSet<string>? competitionEntityIds = null;
        if (parameters.CompetitionId.HasValue)
        {
            var competitionId = parameters.CompetitionId.Value;
            var stageIds = await _database.Competitions.AsNoTracking()
                .Where(item => item.Id == competitionId && item.TenantId == tenantId)
                .SelectMany(item => item.Editions)
                .SelectMany(item => item.Stages)
                .Select(item => item.Id)
                .ToArrayAsync(cancellationToken);
            var contestIds = await _database.Contests.AsNoTracking()
                .Where(item => item.TenantId == tenantId
                    && item.StageId.HasValue && stageIds.Contains(item.StageId.Value))
                .Select(item => item.Id)
                .ToArrayAsync(cancellationToken);
            var resultIds = await _database.ContestResults.AsNoTracking()
                .Where(item => contestIds.Contains(item.ContestId))
                .Select(item => item.Id)
                .ToArrayAsync(cancellationToken);
            var registrationIds = await _database.Registrations.AsNoTracking()
                .Where(item => item.CompetitionId == competitionId)
                .Select(item => item.Id)
                .ToArrayAsync(cancellationToken);
            var standingIds = await _database.Standings.AsNoTracking()
                .Where(item => item.CompetitionId == competitionId)
                .Select(item => item.Id)
                .ToArrayAsync(cancellationToken);
            competitionEntityIds = new[] { competitionId }.Concat(stageIds)
                .Concat(contestIds).Concat(resultIds).Concat(registrationIds)
                .Concat(standingIds)
                .Select(item => item.ToString())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }
        var entries = await _database.AuditEntries.AsNoTracking()
            .Where(item => item.TenantId == tenantId
                && (!parameters.FromUtc.HasValue || item.Timestamp >= parameters.FromUtc)
                && (!parameters.ToUtc.HasValue || item.Timestamp <= parameters.ToUtc))
            .OrderBy(item => item.Timestamp)
            .ThenBy(item => item.Id)
            .ToArrayAsync(cancellationToken);
        if (competitionEntityIds is not null)
            entries = entries.Where(item => competitionEntityIds.Contains(item.EntityId)).ToArray();
        var actorIds = entries.Where(item => item.UserId.HasValue)
            .Select(item => item.UserId!.Value).Distinct().ToArray();
        var actors = await _database.Users.AsNoTracking()
            .Where(item => actorIds.Contains(item.Id)
                && (item.TenantId == tenantId || item.TenantId == null))
            .Select(item => new { item.Id, item.FirstName, item.LastName })
            .ToDictionaryAsync(
                item => item.Id,
                item => $"{item.FirstName} {item.LastName}".Trim(),
                cancellationToken);
        var rows = entries.Select(entry => new[]
        {
            entry.Timestamp.ToUniversalTime().ToString("u", CultureInfo.InvariantCulture),
            entry.UserId.HasValue
                ? actors.GetValueOrDefault(entry.UserId.Value, "Unknown")
                : "System",
            entry.Action,
            entry.EntityType,
            entry.EntityId,
            entry.Reason ?? "",
            entry.OriginalValue ?? "",
            entry.NewValue ?? "",
            entry.IpAddress ?? "",
            entry.UserAgent ?? ""
        }).ToArray();
        return new ReportDocument(
            "Audit history",
            new[]
            {
                "Timestamp (UTC)", "Actor", "Action", "Entity type", "Entity ID",
                "Reason", "Original value", "New value", "IP address", "User agent"
            },
            rows);
    }

    private static bool IsDisciplinary(string eventType, string metricName)
    {
        var eventKey = eventType.Replace("-", "_", StringComparison.Ordinal)
            .ToUpperInvariant();
        var metricKey = metricName.ToLowerInvariant();
        return eventKey is "YELLOW_CARD" or "SECOND_YELLOW" or "RED_CARD"
            or "TEAM_FOUL" or "PERSONAL_FOUL" or "FOUL" or "PENALTY"
            or "DISQUALIFICATION" or "DQ" or "FIELD_DISQUALIFICATION"
            || metricKey is "yellow_cards" or "red_cards" or "team_fouls"
                or "personal_fouls" or "fouls" or "penalties" or "disqualifications";
    }

    private async Task<Dictionary<Guid, CompetitionReference>> CompetitionStageMapAsync(
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var competitions = await _database.Competitions.AsNoTracking()
            .Include(item => item.Editions).ThenInclude(item => item.Stages)
            .Where(item => item.TenantId == tenantId)
            .ToArrayAsync(cancellationToken);
        return competitions.SelectMany(competition => competition.Editions
                .SelectMany(edition => edition.Stages)
                .Select(stage => new { stage.Id, Competition = new CompetitionReference(competition.Id, competition.Name) }))
            .ToDictionary(item => item.Id, item => item.Competition);
    }

    private async Task<Dictionary<Guid, string>> CompetitionMapAsync(
        Guid tenantId,
        CancellationToken cancellationToken) =>
        await _database.Competitions.AsNoTracking()
            .Where(item => item.TenantId == tenantId)
            .ToDictionaryAsync(item => item.Id, item => item.Name, cancellationToken);

    private static byte[] ToCsv(ReportDocument document)
    {
        var output = new StringBuilder();
        output.AppendLine(string.Join(',', document.Columns.Select(EscapeCsv)));
        foreach (var row in document.Rows)
            output.AppendLine(string.Join(',', row.Select(EscapeCsv)));
        return new UTF8Encoding(encoderShouldEmitUTF8Identifier: true).GetBytes(output.ToString());
    }

    private static string EscapeCsv(string value) =>
        value.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;

    private static byte[] ToSpreadsheet(ReportDocument document)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Report");
        for (var column = 0; column < document.Columns.Count; column++)
            sheet.Cell(1, column + 1).Value = document.Columns[column];
        for (var row = 0; row < document.Rows.Count; row++)
        for (var column = 0; column < document.Rows[row].Count; column++)
        {
            var value = document.Rows[row][column];
            sheet.Cell(row + 2, column + 1).Value = value.Length <= 32767
                ? value
                : string.Concat(value.AsSpan(0, 32764), "...");
        }
        var header = sheet.Range(1, 1, 1, document.Columns.Count);
        header.Style.Font.Bold = true;
        header.Style.Fill.BackgroundColor = XLColor.FromHtml("#E0F2FE");
        sheet.SheetView.FreezeRows(1);
        sheet.Columns().AdjustToContents(1, Math.Min(document.Rows.Count + 1, 250));
        sheet.RangeUsed()?.SetAutoFilter();
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static byte[] ToHtml(ReportDocument document)
    {
        var html = new StringBuilder("<!doctype html><html><head><meta charset=\"utf-8\"><title>")
            .Append(WebUtility.HtmlEncode(document.Title))
            .Append("</title><style>body{font-family:Arial,sans-serif;margin:32px}table{border-collapse:collapse;width:100%}th,td{border:1px solid #bbb;padding:8px;text-align:left}th{background:#e0f2fe}@media print{body{margin:0}}</style></head><body><h1>")
            .Append(WebUtility.HtmlEncode(document.Title))
            .Append("</h1><p>Generated ").Append(DateTime.UtcNow.ToString("u"))
            .Append("</p><table><thead><tr>");
        foreach (var column in document.Columns)
            html.Append("<th>").Append(WebUtility.HtmlEncode(column)).Append("</th>");
        html.Append("</tr></thead><tbody>");
        foreach (var row in document.Rows)
        {
            html.Append("<tr>");
            foreach (var value in row)
                html.Append("<td>").Append(WebUtility.HtmlEncode(value)).Append("</td>");
            html.Append("</tr>");
        }
        html.Append("</tbody></table></body></html>");
        return Encoding.UTF8.GetBytes(html.ToString());
    }

    private static byte[] ToPdf(ReportDocument report)
    {
        ReportFontResolver.EnsureConfigured();
        var document = new Document();
        document.Info.Title = report.Title;
        document.Info.Author = "Sports Platform";
        document.Info.Subject = "Tenant-scoped generated report";
        var normal = document.Styles[StyleNames.Normal]
            ?? throw new InvalidOperationException("MigraDoc did not create its normal style.");
        normal.Font.Name = ReportFontResolver.FamilyName;
        normal.Font.Size = 7;

        var section = document.AddSection();
        section.PageSetup.PageFormat = PageFormat.A4;
        section.PageSetup.Orientation = report.Columns.Count > 6
            ? Orientation.Landscape
            : Orientation.Portrait;
        section.PageSetup.TopMargin = Unit.FromCentimeter(1.2);
        section.PageSetup.BottomMargin = Unit.FromCentimeter(1.2);
        section.PageSetup.LeftMargin = Unit.FromCentimeter(0.8);
        section.PageSetup.RightMargin = Unit.FromCentimeter(0.8);

        var title = section.AddParagraph(report.Title);
        title.Format.Font.Name = ReportFontResolver.FamilyName;
        title.Format.Font.Size = 18;
        title.Format.Font.Bold = true;
        title.Format.Font.Color = Color.FromRgb(3, 105, 161);
        title.Format.SpaceAfter = Unit.FromCentimeter(0.15);
        var generated = section.AddParagraph(
            $"Generated {DateTime.UtcNow:u} · {report.Rows.Count} row(s)");
        generated.Format.Font.Size = 8;
        generated.Format.Font.Color = Colors.DimGray;
        generated.Format.SpaceAfter = Unit.FromCentimeter(0.35);

        var table = section.AddTable();
        table.Borders.Width = 0.25;
        table.Borders.Color = Colors.LightGray;
        table.Rows.LeftIndent = 0;
        var availableWidth = section.PageSetup.Orientation == Orientation.Landscape
            ? 28.1
            : 19.4;
        var columnWidth = Unit.FromCentimeter(
            availableWidth / Math.Max(1, report.Columns.Count));
        foreach (var _ in report.Columns)
            table.AddColumn(columnWidth);
        var header = table.AddRow();
        header.HeadingFormat = true;
        header.Format.Font.Bold = true;
        header.Format.Font.Color = Colors.White;
        header.Shading.Color = Color.FromRgb(3, 105, 161);
        header.VerticalAlignment = VerticalAlignment.Center;
        for (var index = 0; index < report.Columns.Count; index++)
        {
            header.Cells[index].Format.Font.Name = ReportFontResolver.FamilyName;
            header.Cells[index].AddParagraph(PdfCell(report.Columns[index]));
        }
        foreach (var values in report.Rows)
        {
            var row = table.AddRow();
            row.VerticalAlignment = VerticalAlignment.Top;
            for (var index = 0; index < report.Columns.Count; index++)
            {
                row.Cells[index].Format.Font.Name = ReportFontResolver.FamilyName;
                row.Cells[index].AddParagraph(PdfCell(
                    index < values.Count ? values[index] : string.Empty));
            }
        }
        if (report.Rows.Count == 0)
        {
            var empty = table.AddRow();
            empty.Cells[0].MergeRight = Math.Max(0, report.Columns.Count - 1);
            empty.Cells[0].AddParagraph("No records matched the selected filters.");
        }

        var footer = section.Footers.Primary.AddParagraph();
        footer.Format.Alignment = ParagraphAlignment.Center;
        footer.Format.Font.Size = 7;
        footer.AddText("Sports Platform · Page ");
        footer.AddPageField();
        footer.AddText(" of ");
        footer.AddNumPagesField();

        var renderer = new PdfDocumentRenderer { Document = document };
        renderer.RenderDocument();
        using var stream = new MemoryStream();
        renderer.PdfDocument.Save(stream, closeStream: false);
        return stream.ToArray();
    }

    private static byte[] ToContestSheetPdf(ContestSheetSnapshot sheet, Guid archiveId)
    {
        ReportFontResolver.EnsureConfigured();
        var document = new Document();
        document.Info.Title = $"{sheet.Template}: {sheet.Contest.Name ?? "Contest"}";
        document.Info.Author = sheet.TenantName;
        document.Info.Subject = $"Archived {sheet.Template}; sponsors: "
            + (sheet.Sponsors.Count == 0
                ? "none"
                : string.Join(", ", sheet.Sponsors.Select(item => item.Name)));
        var normal = document.Styles[StyleNames.Normal]
            ?? throw new InvalidOperationException("MigraDoc did not create its normal style.");
        normal.Font.Name = ReportFontResolver.FamilyName;
        normal.Font.Size = 9;

        var section = document.AddSection();
        section.PageSetup.PageFormat = PageFormat.A4;
        section.PageSetup.Orientation = Orientation.Portrait;
        section.PageSetup.TopMargin = Unit.FromCentimeter(1.2);
        section.PageSetup.BottomMargin = Unit.FromCentimeter(1.4);
        section.PageSetup.LeftMargin = Unit.FromCentimeter(1.2);
        section.PageSetup.RightMargin = Unit.FromCentimeter(1.2);
        var primary = BrandingColor(sheet.PrimaryColor, Color.FromRgb(3, 105, 161));
        var secondary = BrandingColor(sheet.SecondaryColor, Color.FromRgb(14, 116, 144));

        var brand = section.AddParagraph(sheet.ShortName ?? sheet.TenantName);
        brand.Format.Font.Size = 11;
        brand.Format.Font.Bold = true;
        brand.Format.Font.Color = primary;
        if (!string.IsNullOrWhiteSpace(sheet.LogoUrl))
        {
            brand.AddLineBreak();
            brand.AddFormattedText($"Brand asset: {sheet.LogoUrl}", TextFormat.NotBold)
                .Size = 7;
        }
        brand.Format.SpaceAfter = Unit.FromCentimeter(0.15);

        var title = section.AddParagraph(sheet.Contest.Name ?? $"Contest {sheet.Contest.SortOrder}");
        title.Format.Font.Size = 20;
        title.Format.Font.Bold = true;
        title.Format.Font.Color = primary;
        title.Format.SpaceAfter = Unit.FromCentimeter(0.05);
        var subtitle = section.AddParagraph(sheet.Template);
        subtitle.Format.Font.Size = 11;
        subtitle.Format.Font.Color = secondary;
        subtitle.Format.SpaceAfter = Unit.FromCentimeter(0.3);

        AddKeyValueTable(section, new[]
        {
            ("Competition", sheet.CompetitionName ?? "Unassigned"),
            ("Sport", sheet.SportName),
            ("Format", sheet.Contest.ContestShape),
            ("Status", sheet.Contest.Status.ToString()),
            ("Start", sheet.Contest.StartTime?.ToUniversalTime().ToString("u", CultureInfo.InvariantCulture) ?? "Unscheduled"),
            ("Venue", sheet.Contest.Venue ?? "Unassigned")
        }, primary);

        AddHeading(section, ParticipantHeading(sheet.SportCode), primary);
        var participantColumns = ParticipantColumns(sheet.SportCode);
        var participantRows = sheet.Participants.Select(item => new[]
        {
            item.Position.ToString(CultureInfo.InvariantCulture),
            item.Name,
            item.Status,
            ParticipantDetail(sheet.SportCode, item)
        }).ToArray();
        AddSheetTable(section, participantColumns, participantRows, primary);

        AddSportSpecificDetails(section, sheet, primary);

        AddHeading(section, "Officials", primary);
        AddSheetTable(section,
            new[] { "Role", "Name", "Confirmed" },
            sheet.Officials.Select(item => new[]
                { item.Role, item.Name, item.Confirmed ? "Yes" : "No" }).ToArray(),
            primary);

        AddHeading(section, "Active sponsors", primary);
        AddSheetTable(section,
            new[] { "Level", "Sponsor", "Brand asset" },
            sheet.Sponsors.Select(item => new[]
                { item.Level, item.Name, item.LogoUrl ?? "" }).ToArray(),
            primary,
            "No active contest or competition sponsors.");

        if (sheet.Results.Count > 0)
        {
            AddHeading(section, "Archived result snapshot", primary);
            AddSheetTable(section,
                new[] { "Rank", "Participant", "Result", "Status" },
                sheet.Results.Select(item => new[]
                {
                    item.Rank?.ToString(CultureInfo.InvariantCulture) ?? "",
                    item.Participant,
                    item.Value,
                    item.Status
                }).ToArray(), primary);
        }

        AddHeading(section, "Sign-off", primary);
        var signoff = section.AddParagraph(
            "Lead official: ____________________    Team/participant representative: ____________________\n\n"
            + "Recorded by: ____________________    Date/time: ____________________");
        signoff.Format.SpaceAfter = Unit.FromCentimeter(0.3);

        var archive = section.AddParagraph(
            $"Archived {DateTime.UtcNow:u} · Archive ID {archiveId}");
        archive.Format.Font.Size = 7;
        archive.Format.Font.Color = Colors.DimGray;
        var footer = section.Footers.Primary.AddParagraph();
        footer.Format.Alignment = ParagraphAlignment.Center;
        footer.Format.Font.Size = 7;
        footer.AddText($"{sheet.TenantName} · {sheet.Template} · Page ");
        footer.AddPageField();
        footer.AddText(" of ");
        footer.AddNumPagesField();

        var renderer = new PdfDocumentRenderer { Document = document };
        renderer.RenderDocument();
        using var stream = new MemoryStream();
        renderer.PdfDocument.Save(stream, closeStream: false);
        return stream.ToArray();
    }

    private static void AddSportSpecificDetails(
        Section section,
        ContestSheetSnapshot sheet,
        Color primary)
    {
        var code = sheet.SportCode.ToLowerInvariant();
        AddHeading(section, SportSectionHeading(code), primary);
        if (code is "golf")
        {
            AddKeyValueTable(section, new[]
            {
                ("Course", sheet.Contest.CourseName ?? "Unassigned"),
                ("Location", sheet.Contest.CourseLocation ?? "Unassigned"),
                ("Par", sheet.Contest.ParPerRound?.ToString(CultureInfo.InvariantCulture) ?? ""),
                ("Cut", sheet.Contest.CutToParticipants?.ToString(CultureInfo.InvariantCulture) ?? "")
            }, primary);
            return;
        }
        if (code is "mma" or "boxing" or "combat" or "combat-sports")
        {
            AddKeyValueTable(section, new[]
            {
                ("Weight class", sheet.Contest.WeightClass ?? "Unassigned"),
                ("Card section", sheet.Contest.CardSection ?? "Unassigned"),
                ("Scheduled rounds", sheet.Contest.ScheduledRounds?.ToString(CultureInfo.InvariantCulture) ?? ""),
                ("Allowed methods", string.Join(", ", sheet.Contest.GetAllowedResultMethods()))
            }, primary);
            return;
        }
        if (code is "athletics" or "track-field")
        {
            AddKeyValueTable(section, new[]
            {
                ("Event", sheet.Contest.AthleticsEventName ?? "Unassigned"),
                ("Distance", sheet.Contest.DistanceMeters.HasValue ? $"{sheet.Contest.DistanceMeters} m" : ""),
                ("Lanes", sheet.Contest.AthleticsLanesPerHeat?.ToString(CultureInfo.InvariantCulture) ?? ""),
                ("Qualification", $"{sheet.Contest.QualifyByPlace ?? 0} by place / {sheet.Contest.QualifyByTime ?? 0} by time")
            }, primary);
            return;
        }

        AddSheetTable(section,
            new[] { "Segment/period", "Type", "Status" },
            sheet.Segments.Select(item => new[] { item.Name, item.Type, item.Status }).ToArray(),
            primary,
            "No periods or segments configured.");
        if (sheet.Events.Count > 0)
        {
            AddHeading(section, "Recorded event timeline", primary);
            AddSheetTable(section,
                new[] { "#", "Participant", "Event", "Value" },
                sheet.Events.TakeLast(40).Select(item => new[]
                {
                    item.Sequence.ToString(CultureInfo.InvariantCulture),
                    item.Participant,
                    item.EventType,
                    item.Value
                }).ToArray(), primary);
        }
    }

    private static string SportSectionHeading(string code) => code switch
    {
        "golf" => "Course and scorecard configuration",
        "mma" or "boxing" or "combat" or "combat-sports" => "Bout and decision configuration",
        "athletics" or "track-field" => "Event, lanes, and qualification",
        "cricket" or "cricket-t20" or "t20-cricket" => "Innings, overs, and scoring",
        "rugby" or "rugby-union" or "rugby-league" => "Halves, scoring, and disciplinary record",
        "basketball" => "Periods, scoring, and fouls",
        _ => "Periods and scoring record"
    };

    private static string ParticipantHeading(string sportCode) =>
        sportCode.ToLowerInvariant() switch
        {
            "golf" => "Player field",
            "athletics" or "track-field" => "Athletes and lanes",
            "mma" or "boxing" or "combat" or "combat-sports" => "Corners and fighters",
            "cricket" or "cricket-t20" or "t20-cricket" => "Teams and innings order",
            _ => "Participants and lineups"
        };

    private static string[] ParticipantColumns(string sportCode) => new[]
    {
        sportCode.ToLowerInvariant() switch
        {
            "athletics" or "track-field" => "Lane",
            "mma" or "boxing" or "combat" or "combat-sports" => "Corner",
            _ => "Slot"
        },
        "Participant",
        "Status",
        sportCode.Equals("golf", StringComparison.OrdinalIgnoreCase) ? "Cut / seed" : "Seed / qualification"
    };

    private static string ParticipantDetail(string sportCode, ContestSheetParticipant item)
    {
        if (sportCode.Equals("golf", StringComparison.OrdinalIgnoreCase) && item.MadeCut.HasValue)
            return item.MadeCut.Value ? "Made cut" : "Missed cut";
        return string.Join(" · ", new[] { item.Seed, item.Qualification }
            .Where(value => !string.IsNullOrWhiteSpace(value)));
    }

    private static void AddHeading(Section section, string text, Color color)
    {
        var heading = section.AddParagraph(text);
        heading.Format.Font.Size = 12;
        heading.Format.Font.Bold = true;
        heading.Format.Font.Color = color;
        heading.Format.SpaceBefore = Unit.FromCentimeter(0.35);
        heading.Format.SpaceAfter = Unit.FromCentimeter(0.12);
        heading.Format.KeepWithNext = true;
    }

    private static void AddKeyValueTable(
        Section section,
        IReadOnlyList<(string Label, string Value)> values,
        Color color)
    {
        var rows = values.Chunk(2).Select(pair => pair.Length == 2
            ? new[] { pair[0].Label, pair[0].Value, pair[1].Label, pair[1].Value }
            : new[] { pair[0].Label, pair[0].Value, "", "" }).ToArray();
        AddSheetTable(section, new[] { "Field", "Value", "Field", "Value" }, rows, color);
    }

    private static void AddSheetTable(
        Section section,
        IReadOnlyList<string> columns,
        IReadOnlyList<string[]> rows,
        Color color,
        string emptyMessage = "No records available.")
    {
        var table = section.AddTable();
        table.Borders.Width = 0.25;
        table.Borders.Color = Colors.LightGray;
        var width = Unit.FromCentimeter(18.6 / columns.Count);
        foreach (var _ in columns) table.AddColumn(width);
        var header = table.AddRow();
        header.HeadingFormat = true;
        header.Format.Font.Bold = true;
        header.Format.Font.Color = Colors.White;
        header.Shading.Color = color;
        for (var index = 0; index < columns.Count; index++)
            header.Cells[index].AddParagraph(PdfCell(columns[index]));
        foreach (var values in rows)
        {
            var row = table.AddRow();
            for (var index = 0; index < columns.Count; index++)
                row.Cells[index].AddParagraph(PdfCell(
                    index < values.Length ? values[index] : string.Empty));
        }
        if (rows.Count == 0)
        {
            var empty = table.AddRow();
            empty.Cells[0].MergeRight = columns.Count - 1;
            empty.Cells[0].AddParagraph(emptyMessage);
        }
    }

    private static Color BrandingColor(string? value, Color fallback)
    {
        if (string.IsNullOrWhiteSpace(value)) return fallback;
        var hex = value.Trim().TrimStart('#');
        return hex.Length == 6
            && byte.TryParse(hex[..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var red)
            && byte.TryParse(hex.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var green)
            && byte.TryParse(hex.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var blue)
                ? Color.FromRgb(red, green, blue)
                : fallback;
    }

    private static string PdfCell(string value) => value.Length <= 4000
        ? value
        : string.Concat(value.AsSpan(0, 3997), "...");

    private sealed record ReportDocument(
        string Title,
        IReadOnlyList<string> Columns,
        IReadOnlyList<IReadOnlyList<string>> Rows);
    private sealed record CompetitionReference(Guid Id, string Name);
    private sealed record ChronologicalReportRow(
        DateTime OccurredAt,
        IReadOnlyList<string> Values);
    private sealed record ContestSheetSnapshot(
        string TenantName,
        string? ShortName,
        string? LogoUrl,
        string? PrimaryColor,
        string? SecondaryColor,
        string? CompetitionName,
        string SportCode,
        string SportName,
        string Template,
        SportsPlatform.Modules.Contests.Entities.Contest Contest,
        IReadOnlyList<ContestSheetParticipant> Participants,
        IReadOnlyList<ContestSheetSegment> Segments,
        IReadOnlyList<ContestSheetOfficial> Officials,
        IReadOnlyList<ContestSheetSponsor> Sponsors,
        IReadOnlyList<ContestSheetResult> Results,
        IReadOnlyList<ContestSheetEvent> Events);
    private sealed record ContestSheetParticipant(
        int Position, string Name, string Status, string? Seed,
        string? Qualification, bool? MadeCut);
    private sealed record ContestSheetSegment(string Name, string Type, string Status);
    private sealed record ContestSheetOfficial(string Role, string Name, bool Confirmed);
    private sealed record ContestSheetSponsor(string Name, string Level, string? LogoUrl);
    private sealed record ContestSheetResult(
        string Participant, int? Rank, string Value, string Status);
    private sealed record ContestSheetEvent(
        int Sequence, string Participant, string EventType, string Value, decimal? ClockSeconds);
}

internal sealed class ReportFontResolver : IFontResolver
{
    public const string FamilyName = "SportsPlatformSans";
    private const string RegularFace = "SportsPlatformSans-Regular";
    private const string BoldFace = "SportsPlatformSans-Bold";
    private static readonly object ConfigurationLock = new();

    public static void EnsureConfigured()
    {
        if (GlobalFontSettings.FontResolver is not null) return;
        lock (ConfigurationLock)
        {
            GlobalFontSettings.FontResolver ??= new ReportFontResolver();
        }
    }

    public FontResolverInfo? ResolveTypeface(
        string familyName,
        bool isBold,
        bool isItalic) => new(
            isBold ? BoldFace : RegularFace,
            mustSimulateBold: false,
            mustSimulateItalic: isItalic);

    public byte[]? GetFont(string faceName) => File.ReadAllBytes(faceName switch
    {
        BoldFace => ResolveFontPath(bold: true),
        _ => ResolveFontPath(bold: false)
    });

    private static string ResolveFontPath(bool bold)
    {
        var path = OperatingSystem.IsWindows()
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Fonts),
                bold ? "arialbd.ttf" : "arial.ttf")
            : bold
                ? "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf"
                : "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf";
        return File.Exists(path)
            ? path
            : throw new FileNotFoundException(
                "The configured PDF report font is unavailable.", path);
    }
}

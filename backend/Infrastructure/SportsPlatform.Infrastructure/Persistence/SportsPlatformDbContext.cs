using Microsoft.EntityFrameworkCore;
using SportsPlatform.BuildingBlocks;
using SportsPlatform.Modules.Audit.Entities;
using SportsPlatform.Modules.Branding.Entities;
using SportsPlatform.Modules.Brackets.Entities;
using SportsPlatform.Modules.Competitions.Entities;
using SportsPlatform.Modules.Contests.Entities;
using SportsPlatform.Modules.Identity.Entities;
using SportsPlatform.Modules.Leaderboards.Entities;
using SportsPlatform.Modules.LiveScoring.Entities;
using SportsPlatform.Modules.Notifications.Entities;
using SportsPlatform.Modules.Participants.Entities;
using SportsPlatform.Modules.Registrations.Entities;
using SportsPlatform.Modules.Reporting.Entities;
using SportsPlatform.Modules.Results.Entities;
using SportsPlatform.Modules.Scheduling.Entities;
using SportsPlatform.Modules.Sports.Entities;
using SportsPlatform.Modules.Sponsorship.Entities;
using SportsPlatform.Modules.Standings.Entities;
using SportsPlatform.Modules.Subscriptions.Entities;
using SportsPlatform.Modules.Tenancy.Entities;
using SportsPlatform.Modules.Officials.Entities;

namespace SportsPlatform.Infrastructure.Persistence;

public class SportsPlatformDbContext : DbContext
{
    public SportsPlatformDbContext(DbContextOptions<SportsPlatformDbContext> options)
        : base(options)
    {
    }

    // Tenancy
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<TenantContact> TenantContacts => Set<TenantContact>();

    // Sports
    public DbSet<Sport> Sports => Set<Sport>();
    public DbSet<TenantSport> TenantSports => Set<TenantSport>();

    // Competitions
    public DbSet<Competition> Competitions => Set<Competition>();

    // Contests
    public DbSet<Contest> Contests => Set<Contest>();
    public DbSet<ContestLifecyclePolicy> ContestLifecyclePolicies => Set<ContestLifecyclePolicy>();
    public DbSet<ContestStatusHistory> ContestStatusHistory => Set<ContestStatusHistory>();

    // Participants
    public DbSet<Participant> Participants => Set<Participant>();
    public DbSet<BulkImportJob> BulkImportJobs => Set<BulkImportJob>();

    // Results
    public DbSet<ContestResult> ContestResults => Set<ContestResult>();

    // Registrations
    public DbSet<Registration> Registrations => Set<Registration>();
    public DbSet<RegistrationForm> RegistrationForms => Set<RegistrationForm>();
    public DbSet<RegistrationFormSubmission> RegistrationFormSubmissions =>
        Set<RegistrationFormSubmission>();
    public DbSet<CompetitionEligibilityPolicy> CompetitionEligibilityPolicies =>
        Set<CompetitionEligibilityPolicy>();

    // Notifications
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<PushDeviceRegistration> PushDeviceRegistrations =>
        Set<PushDeviceRegistration>();
    public DbSet<NotificationCampaign> NotificationCampaigns => Set<NotificationCampaign>();

    // Leaderboards
    public DbSet<Leaderboard> Leaderboards => Set<Leaderboard>();
    public DbSet<LeaderboardEntry> LeaderboardEntries => Set<LeaderboardEntry>();

    // Brackets
    public DbSet<Bracket> Brackets => Set<Bracket>();
    public DbSet<BracketRound> BracketRounds => Set<BracketRound>();
    public DbSet<BracketMatch> BracketMatches => Set<BracketMatch>();
    public DbSet<BracketAdvancementOverride> BracketAdvancementOverrides => Set<BracketAdvancementOverride>();

    // Standings
    public DbSet<Standing> Standings => Set<Standing>();
    public DbSet<StandingEntry> StandingEntries => Set<StandingEntry>();

    // Officials
    public DbSet<Official> Officials => Set<Official>();
    public DbSet<OfficialAssignment> OfficialAssignments => Set<OfficialAssignment>();

    // Audit
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();

    // Sponsorship
    public DbSet<Sponsorship> Sponsorships => Set<Sponsorship>();
    public DbSet<Sponsor> Sponsors => Set<Sponsor>();

    // Branding
    public DbSet<TenantBranding> TenantBrandings => Set<TenantBranding>();
    public DbSet<Brand> Brands => Set<Brand>();
    public DbSet<BrandAsset> BrandAssets => Set<BrandAsset>();

    // Reporting
    public DbSet<Report> Reports => Set<Report>();
    public DbSet<ReportSchedule> ReportSchedules => Set<ReportSchedule>();

    // Scheduling
    public DbSet<Schedule> Schedules => Set<Schedule>();
    public DbSet<ScheduledEvent> ScheduledEvents => Set<ScheduledEvent>();
    public DbSet<CompetitionSchedulePolicy> CompetitionSchedulePolicies => Set<CompetitionSchedulePolicy>();
    public DbSet<ScheduleConflictOverride> ScheduleConflictOverrides => Set<ScheduleConflictOverride>();
    public DbSet<FixtureAdjustment> FixtureAdjustments => Set<FixtureAdjustment>();

    // Live Scoring
    public DbSet<LiveScore> LiveScores => Set<LiveScore>();
    public DbSet<LiveScoreUpdate> LiveScoreUpdates => Set<LiveScoreUpdate>();
    public DbSet<LiveScoreSession> LiveScoreSessions => Set<LiveScoreSession>();
    public DbSet<GolfScorecardReview> GolfScorecardReviews => Set<GolfScorecardReview>();
    public DbSet<ContestReadiness> ContestReadiness => Set<ContestReadiness>();
    public DbSet<ContestCheckInHistory> ContestCheckInHistory => Set<ContestCheckInHistory>();

    // Subscriptions
    public DbSet<Subscription> Subscriptions => Set<Subscription>();
    public DbSet<TenantUsageCounter> TenantUsageCounters => Set<TenantUsageCounter>();

    // Identity
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<DeviceSession> DeviceSessions => Set<DeviceSession>();
    public DbSet<IdentityActionToken> IdentityActionTokens => Set<IdentityActionToken>();
    public DbSet<ConsentRecord> ConsentRecords => Set<ConsentRecord>();
    public DbSet<PrivacyControlSettings> PrivacyControlSettings => Set<PrivacyControlSettings>();
    public DbSet<PrivacyActionRequest> PrivacyActionRequests => Set<PrivacyActionRequest>();
    public DbSet<TenantRetentionPolicy> TenantRetentionPolicies => Set<TenantRetentionPolicy>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SportsPlatformDbContext).Assembly);

        modelBuilder.Entity<Report>(entity =>
        {
            entity.Property(item => item.Name).HasMaxLength(200);
            entity.Property(item => item.GeneratedContentType).HasMaxLength(150);
            entity.Property(item => item.GeneratedFileName).HasMaxLength(260);
            entity.HasIndex(item => new { item.TenantId, item.CreatedAt });
        });

        modelBuilder.Entity<PushDeviceRegistration>(entity =>
        {
            entity.Property(item => item.Token).HasMaxLength(2048);
            entity.Property(item => item.Platform).HasMaxLength(30);
            entity.Property(item => item.DeviceId).HasMaxLength(200);
            entity.HasIndex(item => item.Token).IsUnique();
            entity.HasIndex(item => new { item.UserId, item.IsEnabled });
        });

        // Global query filter for soft-delete if needed
        // modelBuilder.Entity<Entity>().HasQueryFilter(e => !e.IsDeleted);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        RejectAuditMutations();
        await IncrementTimelineVersionsAsync(cancellationToken);
        // Set audit timestamps for all entities
        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.Entity == null) continue;

            var createdAtProp = entry.Metadata.FindProperty("CreatedAt");
            var updatedAtProp = entry.Metadata.FindProperty("UpdatedAt");

            switch (entry.State)
            {
                case EntityState.Added:
                    if (createdAtProp is not null)
                        entry.Property("CreatedAt").CurrentValue = DateTime.UtcNow;
                    break;
                case EntityState.Modified:
                    if (updatedAtProp is not null)
                        entry.Property("UpdatedAt").CurrentValue = DateTime.UtcNow;
                    break;
            }
        }

        var addedLiveScores = ChangeTracker.Entries<LiveScore>()
            .Where(entry => entry.State == EntityState.Added)
            .Select(entry => entry.Entity)
            .ToArray();
        foreach (var score in addedLiveScores)
        {
            var alreadyRecorded = ChangeTracker.Entries<AuditEntry>().Any(entry =>
                entry.State == EntityState.Added
                && entry.Entity.Action == "LiveScoreRecorded"
                && entry.Entity.EntityId == score.Id.ToString());
            if (alreadyRecorded) continue;
            AuditEntries.Add(new AuditEntry(
                Guid.NewGuid(),
                score.TenantId,
                Guid.TryParse(score.ScorerId, out var scorerId) ? scorerId : null,
                "LiveScoreRecorded",
                "LiveScore",
                score.Id.ToString(),
                null,
                System.Text.Json.JsonSerializer.Serialize(new
                {
                    score.ContestId,
                    score.EventId,
                    score.EventType,
                    score.ParticipantId,
                    score.MetricName,
                    score.Value,
                    score.Sequence
                }),
                $"Live scoring event {score.EventType} recorded"));
        }

        var transitionedContests = ChangeTracker.Entries<Contest>()
            .Select(entry => entry.Entity)
            .Where(contest => contest.PendingTransitions.Count > 0)
            .ToArray();
        foreach (var contest in transitionedContests)
        {
            var alreadyRecorded = ChangeTracker.Entries<AuditEntry>().Any(entry =>
                entry.State == EntityState.Added
                && entry.Entity.Action == "ContestStatusChanged"
                && entry.Entity.EntityType == "Contest"
                && entry.Entity.EntityId == contest.Id.ToString());
            foreach (var transition in contest.PendingTransitions)
            {
                ContestStatusHistory.Add(new ContestStatusHistory(
                    Guid.NewGuid(),
                    contest.TenantId,
                    contest.Id,
                    transition.CanonicalFrom,
                    transition.CanonicalTo,
                    transition.OccurredAt,
                    transition.ActorId,
                    transition.Reason));
                if (!alreadyRecorded) AuditEntries.Add(new AuditEntry(
                    Guid.NewGuid(),
                    contest.TenantId,
                    null,
                    "ContestStatusChanged",
                    "Contest",
                    contest.Id.ToString(),
                    System.Text.Json.JsonSerializer.Serialize(new
                    {
                        status = transition.From.ToString()
                    }),
                    System.Text.Json.JsonSerializer.Serialize(new
                    {
                        status = transition.To.ToString()
                    }),
                    transition.Reason));
            }
        }

        try
        {
            var saved = await base.SaveChangesAsync(cancellationToken);
            foreach (var contest in transitionedContests)
                contest.AcceptPendingTransitions();
            return saved;
        }
        catch (DbUpdateConcurrencyException exception)
        {
            var entities = string.Join(
                ", ",
                exception.Entries.Select(entry =>
                    $"{entry.Metadata.ClrType.Name} ({entry.State})"));
            throw new DbUpdateConcurrencyException(
                $"A concurrency conflict occurred while saving: {entities}.",
                exception);
        }
    }

    private async Task IncrementTimelineVersionsAsync(CancellationToken cancellationToken)
    {
        var changedEvents = ChangeTracker.Entries<LiveScore>()
            .Where(entry => entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .Select(entry => (entry.Entity.ContestId,
                EventId: entry.Entity.EventId == Guid.Empty ? entry.Entity.Id : entry.Entity.EventId))
            .Concat(ChangeTracker.Entries<ContestEvent>()
                .Where(entry => entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
                .Select(entry => (entry.Entity.ContestId, EventId: entry.Entity.Id)))
            .Where(item => item.ContestId != Guid.Empty)
            .ToList();
        var changedScoreIds = ChangeTracker.Entries<LiveScoreUpdate>()
            .Where(entry => entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .Select(entry => entry.Entity.LiveScoreId)
            .Distinct()
            .ToArray();
        if (changedScoreIds.Length > 0)
        {
            var updatedEvents = await LiveScores.AsNoTracking()
                .Where(score => changedScoreIds.Contains(score.Id))
                .Select(score => new
                {
                    score.ContestId,
                    EventId = score.EventId == Guid.Empty ? score.Id : score.EventId
                })
                .ToArrayAsync(cancellationToken);
            changedEvents.AddRange(updatedEvents.Select(item => (item.ContestId, item.EventId)));
        }
        var increments = changedEvents
            .Distinct()
            .GroupBy(item => item.ContestId)
            .ToDictionary(group => group.Key, group => (long)group.Count());
        if (increments.Count == 0) return;

        var tracked = ChangeTracker.Entries<Contest>()
            .Where(entry => entry.State != EntityState.Deleted && increments.ContainsKey(entry.Entity.Id))
            .Select(entry => entry.Entity)
            .ToDictionary(contest => contest.Id);
        var missingIds = increments.Keys.Where(id => !tracked.ContainsKey(id)).ToArray();
        var contests = missingIds.Length == 0
            ? []
            : await Contests
            .Where(contest => missingIds.Contains(contest.Id))
            .ToListAsync(cancellationToken);
        foreach (var contest in contests) tracked[contest.Id] = contest;
        foreach (var (contestId, increment) in increments)
            if (tracked.TryGetValue(contestId, out var contest))
            {
                var persistedContestEventMax = await Contests
                    .AsNoTracking()
                    .Where(item => item.Id == contestId)
                    .SelectMany(item => item.Events)
                    .Select(item => (long?)item.TimelineSequence)
                    .MaxAsync(cancellationToken) ?? 0;
                var persistedLiveScoreMax = await LiveScores
                    .AsNoTracking()
                    .Where(item => item.ContestId == contestId)
                    .Select(item => (long?)item.TimelineSequence)
                    .MaxAsync(cancellationToken) ?? 0;
                var persistedTimelineMax = Math.Max(
                    persistedContestEventMax,
                    persistedLiveScoreMax);
                var sequenceBase = Math.Max(
                    contest.TimelineVersion,
                    persistedTimelineMax);

                var sequences = changedEvents
                    .Where(item => item.ContestId == contestId)
                    .Select(item => item.EventId)
                    .Distinct()
                    .Select((eventId, index) => new
                    {
                        EventId = eventId,
                        Sequence = sequenceBase + index + 1
                    })
                    .ToDictionary(item => item.EventId, item => item.Sequence);
                foreach (var entry in ChangeTracker.Entries<LiveScore>().Where(entry =>
                    entry.State == EntityState.Added && entry.Entity.ContestId == contestId))
                {
                    var eventId = entry.Entity.EventId == Guid.Empty
                        ? entry.Entity.Id
                        : entry.Entity.EventId;
                    entry.Entity.AssignTimelineSequence(sequences[eventId]);
                }
                foreach (var entry in ChangeTracker.Entries<ContestEvent>().Where(entry =>
                    entry.State == EntityState.Added && entry.Entity.ContestId == contestId))
                    entry.Entity.AssignTimelineSequence(sequences[entry.Entity.Id]);
                contest.IncrementTimelineVersion(
                    (sequenceBase - contest.TimelineVersion) + increment);
            }
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        RejectAuditMutations();
        IncrementTimelineVersionsAsync(CancellationToken.None).GetAwaiter().GetResult();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    private void RejectAuditMutations()
    {
        var mutation = ChangeTracker.Entries<AuditEntry>().FirstOrDefault(entry =>
            entry.State is EntityState.Modified or EntityState.Deleted);
        if (mutation is not null)
            throw new InvalidOperationException(
                $"Audit entries are append-only and cannot be {mutation.State.ToString().ToLowerInvariant()}.");
        var statusHistoryMutation = ChangeTracker.Entries<ContestStatusHistory>()
            .FirstOrDefault(entry =>
                entry.State is EntityState.Modified or EntityState.Deleted);
        if (statusHistoryMutation is not null)
            throw new InvalidOperationException(
                $"Contest status history is append-only and cannot be {statusHistoryMutation.State.ToString().ToLowerInvariant()}.");
    }
}

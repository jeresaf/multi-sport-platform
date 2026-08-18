using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SportsPlatform.Modules.Contests.Entities;

namespace SportsPlatform.Infrastructure.Persistence.EntityConfigurations;

public class ContestConfiguration : IEntityTypeConfiguration<Contest>
{
    public void Configure(EntityTypeBuilder<Contest> builder)
    {
        builder.ToTable("Contests");

        builder.HasKey(c => c.Id);
        builder.Ignore(c => c.PendingTransitions);
        builder.Ignore(c => c.ShapeKind);

        builder.Property(c => c.ContestShape).IsRequired().HasMaxLength(100);
        builder.Property(c => c.AllowedParticipantTypes).IsRequired().HasMaxLength(300);
        builder.Property(c => c.Venue).HasMaxLength(200);
        builder.Property(c => c.Status).IsRequired().HasMaxLength(50).HasConversion<string>();
        builder.Property(c => c.CanonicalStatus).IsRequired().HasMaxLength(50)
            .HasConversion<string>();
        builder.Property(c => c.StatusReason).HasMaxLength(500);
        builder.Property(c => c.CanonicalStatusChangedAt).IsRequired();
        builder.Property(c => c.TimelineVersion).IsRequired().IsConcurrencyToken();
        builder.Property(c => c.TenantId).IsRequired();
        builder.Property(c => c.SportId).IsRequired();
        builder.Property(c => c.CreatedAt).IsRequired();
        builder.Property(c => c.UpdatedAt);
        builder.Property(c => c.StartTime);
        builder.Property(c => c.EndTime);
        builder.Property(c => c.ScheduledDurationMinutes).IsRequired().HasDefaultValue(0);
        builder.Property(c => c.SeriesId);
        builder.Property(c => c.SeriesGameNumber);
        builder.Property(c => c.SeriesBestOf);
        builder.Property(c => c.ParPerRound);
        builder.Property(c => c.CutAfterRound);
        builder.Property(c => c.CutToParticipants);
        builder.Property(c => c.CutAppliedAt);
        builder.Property(c => c.CourseName).HasMaxLength(200);
        builder.Property(c => c.CourseLocation).HasMaxLength(300);
        builder.Property(c => c.HoleParsJson).HasColumnType("jsonb");
        builder.Property(c => c.WeightClass).HasMaxLength(100);
        builder.Property(c => c.CardSection).HasMaxLength(50);
        builder.Property(c => c.ScheduledRounds);
        builder.Property(c => c.ResultMethod).HasMaxLength(100);
        builder.Property(c => c.ResultRound);
        builder.Property(c => c.ResultTimeSeconds);
        builder.Property(c => c.ResultNotes).HasMaxLength(500);
        builder.Property(c => c.AllowedResultMethods).HasMaxLength(1000);
        builder.Property(c => c.AthleticsEventName).HasMaxLength(150);
        builder.Property(c => c.DistanceMeters);
        builder.Property(c => c.QualifyByPlace);
        builder.Property(c => c.QualifyByTime);
        builder.Property(c => c.AdvanceQualificationTies);
        builder.Property(c => c.AdvancePlaceTies);
        builder.Property(c => c.AdvanceTimeTies);
        builder.Property(c => c.AthleticsLanesPerHeat);
        builder.Property(c => c.AthleticsLaneAssignment).HasMaxLength(20);
        builder.Property(c => c.NextRoundHeatCount);
        builder.Property(c => c.NextRoundQualifyByPlace);
        builder.Property(c => c.NextRoundQualifyByTime);
        builder.Property(c => c.QualificationAppliedAt);
        builder.Property(c => c.AthleticsMetricType).HasMaxLength(20);
        builder.Property(c => c.AthleticsMaxAttempts);
        builder.Property(c => c.AthleticsRecordThreshold).HasPrecision(12, 3);

        builder.HasIndex(c => new { c.TenantId, c.SportId });
        builder.HasIndex(c => c.Status);
        builder.HasIndex(c => c.CanonicalStatus);
        builder.HasIndex(c => new { c.SeriesId, c.SeriesGameNumber }).IsUnique();

        // ContestParticipants
        builder.OwnsMany(c => c.Participants, p =>
        {
            p.WithOwner().HasForeignKey("ContestId");
            p.ToTable("ContestParticipants");
            p.HasKey(p => p.Id);

            p.Property(p => p.ParticipantType).IsRequired().HasMaxLength(50);
            p.Property(p => p.SortOrder).IsRequired();
            p.Property(p => p.Status).IsRequired().HasMaxLength(50).HasConversion<string>();
            p.Property(p => p.Seed).HasMaxLength(50);
            p.Property(p => p.TenantId).IsRequired();
            p.Property(p => p.MadeCut);
            p.Property(p => p.QualificationStatus).HasMaxLength(50);
            p.Property(p => p.QualificationReason).HasMaxLength(250);
        });

        // ContestSegments
        builder.OwnsMany(c => c.Segments, s =>
        {
            s.WithOwner().HasForeignKey("ContestId");
            s.ToTable("ContestSegments");
            s.HasKey(s => s.Id);

            s.Property(s => s.Name).IsRequired().HasMaxLength(200);
            s.Property(s => s.SegmentType).IsRequired().HasMaxLength(100);
            s.Property(s => s.SortOrder).IsRequired();
            s.Property(s => s.Status).IsRequired().HasMaxLength(50).HasConversion<string>();
            s.Property(s => s.IsOptional).IsRequired();
            s.Property(s => s.ActivationCondition).HasMaxLength(200);
            s.Property(s => s.TenantId).IsRequired();
            s.Property(s => s.StartedAt);
            s.Property(s => s.EndedAt);

            // Nested segments (self-referencing)
            s.OwnsMany(n => n.NestedSegments, n =>
            {
                n.WithOwner().HasForeignKey("ContestSegmentId");
                n.ToTable("ContestNestedSegments");
                n.HasKey(n => n.Id);

                n.Property(n => n.Name).IsRequired().HasMaxLength(200);
                n.Property(n => n.SegmentType).IsRequired().HasMaxLength(100);
                n.Property(n => n.SortOrder).IsRequired();
                n.Property(n => n.Status).IsRequired().HasMaxLength(50).HasConversion<string>();
                n.Property(n => n.IsOptional).IsRequired();
                n.Property(n => n.ActivationCondition).HasMaxLength(200);
                n.Property(n => n.TenantId).IsRequired();
                n.Property(n => n.StartedAt);
                n.Property(n => n.EndedAt);
            });
        });

        // ContestEvents
        builder.OwnsMany(c => c.Events, e =>
        {
            e.WithOwner().HasForeignKey("ContestId");
            e.ToTable("ContestEvents");
            e.HasKey(e => e.Id);

            e.Property(e => e.EventType).IsRequired().HasMaxLength(100);
            e.Property(e => e.MetricValue).HasMaxLength(500);
            e.Property(e => e.DeviceId).IsRequired().HasMaxLength(200);
            e.Property(e => e.IdempotencyKey).IsRequired().HasMaxLength(200);
            e.Property(e => e.CorrectionReason).HasMaxLength(500);
            e.Property(e => e.TimelineSequence).IsRequired();
            e.Property(e => e.Status).IsRequired().HasMaxLength(50).HasConversion<string>();
            e.Property(e => e.TenantId).IsRequired();

            e.HasIndex(e => e.IdempotencyKey).IsUnique();
            e.HasIndex(e => new { e.ContestId, e.SequenceNumber });
            e.HasIndex(e => new { e.ContestId, e.TimelineSequence }).IsUnique();
        });

        // ContestOfficials
        builder.OwnsMany(c => c.Officials, o =>
        {
            o.WithOwner().HasForeignKey("ContestId");
            o.ToTable("ContestOfficials");
            o.HasKey(o => o.Id);

            o.Property(o => o.Role).IsRequired().HasMaxLength(100);
            o.Property(o => o.DisplayName).HasMaxLength(200);
            o.Property(o => o.IsConfirmed).IsRequired();
            o.Property(o => o.TenantId).IsRequired();
        });

        builder.OwnsMany(c => c.Metrics, metric =>
        {
            metric.WithOwner().HasForeignKey(item => item.ContestId);
            metric.ToTable("ContestMetrics");
            metric.HasKey(item => item.Id);
            metric.Property(item => item.TenantId).IsRequired();
            metric.Property(item => item.Name).IsRequired().HasMaxLength(100);
            metric.Property(item => item.NumericValue).HasPrecision(18, 6);
            metric.Property(item => item.TextValue).HasMaxLength(1000);
            metric.Property(item => item.Unit).HasMaxLength(50);
            metric.Property(item => item.DataType).IsRequired().HasMaxLength(32);
            metric.Property(item => item.Precision).IsRequired();
            metric.Property(item => item.RecordedAt).IsRequired();
            metric.HasIndex(item => new { item.ContestId, item.Name });
        });
    }
}

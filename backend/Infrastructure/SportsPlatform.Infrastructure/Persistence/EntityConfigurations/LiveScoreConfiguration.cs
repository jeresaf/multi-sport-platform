using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SportsPlatform.Modules.LiveScoring.Entities;

namespace SportsPlatform.Infrastructure.Persistence.EntityConfigurations;

public sealed class LiveScoreConfiguration : IEntityTypeConfiguration<LiveScore>
{
    public void Configure(EntityTypeBuilder<LiveScore> builder)
    {
        builder.ToTable("LiveScores");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.EventType).IsRequired().HasMaxLength(100);
        builder.Property(item => item.MetricName).IsRequired().HasMaxLength(100);
        builder.Property(item => item.DeviceId).IsRequired().HasMaxLength(200);
        builder.Property(item => item.IdempotencyKey).IsRequired().HasMaxLength(200);
        builder.Property(item => item.ScorerId).HasMaxLength(200);
        builder.Property(item => item.AssistId).HasMaxLength(200);
        builder.Property(item => item.DisplayValue).HasMaxLength(500);
        builder.Property(item => item.TimelineSequence).IsRequired();
        builder.HasIndex(item => new { item.ContestId, item.EventId }).IsUnique();
        builder.HasIndex(item => new { item.ContestId, item.IdempotencyKey }).IsUnique();
        builder.HasIndex(item => new { item.ContestId, item.DeviceId, item.Sequence }).IsUnique();
        builder.HasIndex(item => new { item.ContestId, item.TimelineSequence }).IsUnique();
    }
}

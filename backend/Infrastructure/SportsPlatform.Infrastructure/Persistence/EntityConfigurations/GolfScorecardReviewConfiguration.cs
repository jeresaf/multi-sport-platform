using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SportsPlatform.Modules.LiveScoring.Entities;

namespace SportsPlatform.Infrastructure.Persistence.EntityConfigurations;

public sealed class GolfScorecardReviewConfiguration
    : IEntityTypeConfiguration<GolfScorecardReview>
{
    public void Configure(EntityTypeBuilder<GolfScorecardReview> builder)
    {
        builder.ToTable("GolfScorecardReviews");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.TenantId).IsRequired();
        builder.Property(item => item.HolesJson).HasColumnType("jsonb").IsRequired();
        builder.Property(item => item.Status).HasConversion<string>().HasMaxLength(30);
        builder.Property(item => item.ReviewNotes).HasMaxLength(1000);
        builder.HasIndex(item => new
        {
            item.ContestId,
            item.ParticipantId,
            item.RoundNumber,
            item.Status
        });
    }
}

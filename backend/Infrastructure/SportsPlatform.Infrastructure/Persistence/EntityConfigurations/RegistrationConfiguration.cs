using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SportsPlatform.Modules.Registrations.Entities;

namespace SportsPlatform.Infrastructure.Persistence.EntityConfigurations;

public class RegistrationConfiguration : IEntityTypeConfiguration<Registration>
{
    public void Configure(EntityTypeBuilder<Registration> builder)
    {
        builder.ToTable("Registrations");
        builder.HasKey(registration => registration.Id);
        builder.Property(registration => registration.Status)
            .IsRequired()
            .HasConversion<int>();
        builder.Property(registration => registration.Type)
            .IsRequired()
            .HasConversion<int>();
        builder.Property(registration => registration.EligibilityIssuesJson)
            .IsRequired().HasColumnType("jsonb").HasDefaultValue("[]");
        builder.Property(registration => registration.EligibilityOverrideReason).HasMaxLength(1000);
        builder.HasIndex(registration => registration.ContestId);
        builder.HasIndex(registration => registration.CompetitionId);
        builder.HasIndex(registration => new
        {
            registration.CompetitionId,
            registration.ParticipantId
        }).IsUnique();
    }
}

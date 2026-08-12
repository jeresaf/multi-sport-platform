using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SportsPlatform.Modules.Participants.Entities;

namespace SportsPlatform.Infrastructure.Persistence.EntityConfigurations;

public class ParticipantConfiguration : IEntityTypeConfiguration<Participant>
{
    public void Configure(EntityTypeBuilder<Participant> builder)
    {
        builder.ToTable("Participants");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(p => p.ShortName)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(p => p.Logo)
            .HasMaxLength(500);

        builder.Property(p => p.Type)
            .IsRequired()
            .HasMaxLength(50)
            .HasConversion<string>();

        builder.Property(p => p.Status)
            .IsRequired()
            .HasMaxLength(50)
            .HasConversion<string>();

        builder.Property(p => p.TenantId).IsRequired();
        builder.Property(p => p.CreatedAt).IsRequired();
        builder.Property(p => p.UpdatedAt);

        builder.HasIndex(p => new { p.TenantId, p.Type });

        // TPH inheritance for Athlete and Team
        builder.HasDiscriminator<ParticipantType>("Type")
            .HasValue<Athlete>(ParticipantType.Athlete)
            .HasValue<Team>(ParticipantType.Team)
            .HasValue<Pair>(ParticipantType.Pair)
            .HasValue<RelayTeam>(ParticipantType.RelayTeam)
            .HasValue<Crew>(ParticipantType.Crew)
            .HasValue<Club>(ParticipantType.Club)
            .HasValue<School>(ParticipantType.School)
            .HasValue<Country>(ParticipantType.Country)
            .HasValue<CompositeTeam>(ParticipantType.CompositeTeam);

        builder.OwnsMany(p => p.SportProfiles, profile =>
        {
            profile.WithOwner().HasForeignKey(p => p.ParticipantId);
            profile.ToTable("ParticipantSportProfiles");
            profile.HasKey(p => p.Id);
            profile.Property(p => p.TenantId).IsRequired();
            profile.Property(p => p.SportId).IsRequired();
            profile.Property(p => p.AttributesJson).IsRequired().HasColumnType("jsonb");
            profile.HasIndex(p => new { p.ParticipantId, p.SportId }).IsUnique();
        });

        // Properties declared by Athlete and Team are discovered by convention.
        // Configuring them by name on the abstract base creates invalid shadow
        // properties because those members do not exist on Participant itself.
    }
}

public sealed class AthleteConfiguration : IEntityTypeConfiguration<Athlete>
{
    public void Configure(EntityTypeBuilder<Athlete> builder)
    {
        builder.Property(item => item.Gender).HasMaxLength(50);
        builder.Property(item => item.CompetitionCategory).HasMaxLength(100);
        builder.Property(item => item.Nationality).HasMaxLength(100);
        builder.Property(item => item.Photo).HasMaxLength(500);
        builder.Property(item => item.ContactEmail).HasMaxLength(254);
        builder.Property(item => item.ContactPhone).HasMaxLength(50);
        builder.Property(item => item.EmergencyContact).HasMaxLength(300);
        builder.Property(item => item.EmergencyContactName).HasMaxLength(200);
        builder.Property(item => item.EmergencyContactRelationship).HasMaxLength(100);
        builder.Property(item => item.EmergencyContactPhone).HasMaxLength(50);
        builder.Property(item => item.MedicalClearanceStatus)
            .IsRequired().HasMaxLength(30).HasDefaultValue("NotProvided");
        builder.Property(item => item.IdentificationDocumentsJson)
            .IsRequired().HasColumnType("jsonb").HasDefaultValue("[]");
    }
}

public sealed class OrganizationParticipantConfiguration
    : IEntityTypeConfiguration<OrganizationParticipant>
{
    public void Configure(EntityTypeBuilder<OrganizationParticipant> builder)
    {
        builder.Property(item => item.HomeVenue).HasMaxLength(200);
        builder.Property(item => item.Coach).HasMaxLength(200);
        builder.Property(item => item.Manager).HasMaxLength(200);
        builder.Property(item => item.ContactEmail).HasMaxLength(254);
        builder.Property(item => item.ContactPhone).HasMaxLength(50);
    }
}

public sealed class TeamRosterConfiguration : IEntityTypeConfiguration<TeamRoster>
{
    public void Configure(EntityTypeBuilder<TeamRoster> builder)
    {
        builder.Property(item => item.Name).IsRequired().HasMaxLength(200);
        builder.Property(item => item.Season).IsRequired().HasMaxLength(100);
        builder.HasIndex(item => new { item.TeamId, item.Season });
    }
}

public sealed class RosterEntryConfiguration : IEntityTypeConfiguration<RosterEntry>
{
    public void Configure(EntityTypeBuilder<RosterEntry> builder)
    {
        builder.Property(item => item.JerseyNumber).HasMaxLength(30);
        builder.Property(item => item.Position).HasMaxLength(100);
        builder.Property(item => item.MovementReason).HasMaxLength(500);
        builder.HasIndex(item => new { item.RosterId, item.AthleteId });
    }
}

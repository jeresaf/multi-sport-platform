using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SportsPlatform.Modules.Competitions.Entities;

namespace SportsPlatform.Infrastructure.Persistence.EntityConfigurations;

public class CompetitionConfiguration : IEntityTypeConfiguration<Competition>
{
    public void Configure(EntityTypeBuilder<Competition> builder)
    {
        builder.ToTable("Competitions");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(c => c.Description)
            .HasMaxLength(1000);

        builder.Property(c => c.SportId)
            .IsRequired();

        builder.Property(c => c.RulesetVersionId);

        builder.Property(c => c.PresentationConfiguration)
            .IsRequired()
            .HasColumnType("jsonb")
            .HasDefaultValue("{}");

        builder.Property(c => c.Status)
            .IsRequired()
            .HasMaxLength(50)
            .HasConversion<string>();

        builder.Property(c => c.TenantId).IsRequired();
        builder.Property(c => c.CreatedAt).IsRequired();
        builder.Property(c => c.UpdatedAt);
        builder.Property(c => c.PublishedAt);

        builder.HasIndex(c => new { c.TenantId, c.SportId });

        // Editions as owned entities
        builder.OwnsMany(c => c.Editions, e =>
        {
            e.WithOwner().HasForeignKey("CompetitionId");
            e.ToTable("Editions");
            e.HasKey(e => e.Id);

            e.Property(e => e.Name).IsRequired().HasMaxLength(200);
            e.Property(e => e.StartDate).IsRequired();
            e.Property(e => e.EndDate);
            e.Property(e => e.Status).IsRequired().HasMaxLength(50).HasConversion<string>();
            e.Property(e => e.TenantId).IsRequired();
            e.Property(e => e.CreatedAt).IsRequired();
            e.Property(e => e.UpdatedAt);

            // Stages as owned entities of Edition
            e.OwnsMany(s => s.Stages, s =>
            {
                s.WithOwner().HasForeignKey("EditionId");
                s.ToTable("Stages");
                s.HasKey(s => s.Id);

                s.Property(s => s.Name).IsRequired().HasMaxLength(200);
                s.Property(s => s.Type).IsRequired().HasMaxLength(50).HasConversion<string>();
                s.Property(s => s.SortOrder).IsRequired();
                s.Property(s => s.Status).IsRequired().HasMaxLength(50).HasConversion<string>();
                s.Property(s => s.TenantId).IsRequired();
                s.Property(s => s.CreatedAt).IsRequired();
                s.Property(s => s.UpdatedAt);
                s.Property(s => s.QualificationTieBreakers).HasMaxLength(300);
                s.Property(s => s.FixtureVenue).HasMaxLength(200);

                // Groups as owned entities of Stage
                s.OwnsMany(g => g.Groups, g =>
                {
                    g.WithOwner().HasForeignKey("StageId");
                    g.ToTable("Groups");
                    g.HasKey(g => g.Id);

                    g.Property(g => g.Name).IsRequired().HasMaxLength(200);
                    g.Property(g => g.Status).IsRequired().HasMaxLength(50).HasConversion<string>();
                    g.Property(g => g.TenantId).IsRequired();
                    g.Property(g => g.CreatedAt).IsRequired();
                    g.Property(g => g.UpdatedAt);
                });

                // Rounds as owned entities of Stage
                s.OwnsMany(r => r.Rounds, r =>
                {
                    r.WithOwner().HasForeignKey("StageId");
                    r.ToTable("Rounds");
                    r.HasKey(r => r.Id);

                    r.Property(r => r.Name).IsRequired().HasMaxLength(200);
                    r.Property(r => r.SortOrder).IsRequired();
                    r.Property(r => r.Status).IsRequired().HasMaxLength(50).HasConversion<string>();
                    r.Property(r => r.TenantId).IsRequired();
                    r.Property(r => r.CreatedAt).IsRequired();
                    r.Property(r => r.UpdatedAt);
                });
            });
        });
    }
}

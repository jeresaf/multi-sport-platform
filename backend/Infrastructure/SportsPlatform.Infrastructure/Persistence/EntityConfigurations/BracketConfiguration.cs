using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SportsPlatform.Modules.Brackets.Entities;

namespace SportsPlatform.Infrastructure.Persistence.EntityConfigurations;

public class BracketConfiguration : IEntityTypeConfiguration<Bracket>
{
    public void Configure(EntityTypeBuilder<Bracket> builder)
    {
        builder.ToTable("Brackets");
        builder.HasKey(bracket => bracket.Id);
        builder.Property(bracket => bracket.Name).IsRequired().HasMaxLength(200);
        builder.Property(bracket => bracket.Status).HasConversion<int>();
        builder.Property(bracket => bracket.Type).HasConversion<int>();
        builder.HasIndex(bracket => bracket.CompetitionId);
        builder.HasMany(bracket => bracket.Rounds)
            .WithOne()
            .HasForeignKey(round => round.BracketId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class BracketAdvancementOverrideConfiguration : IEntityTypeConfiguration<BracketAdvancementOverride>
{
    public void Configure(EntityTypeBuilder<BracketAdvancementOverride> builder)
    {
        builder.ToTable("BracketAdvancementOverrides");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Reason).IsRequired().HasMaxLength(500);
        builder.Property(item => item.ReviewNotes).HasMaxLength(500);
        builder.Property(item => item.Status).HasConversion<int>();
        builder.HasIndex(item => item.BracketMatchId);
        builder.HasIndex(item => new { item.BracketMatchId, item.Status });
    }
}

public class BracketRoundConfiguration : IEntityTypeConfiguration<BracketRound>
{
    public void Configure(EntityTypeBuilder<BracketRound> builder)
    {
        builder.ToTable("BracketRounds");
        builder.HasKey(round => round.Id);
        builder.Property(round => round.Name).IsRequired().HasMaxLength(100);
        builder.HasMany(round => round.Matches)
            .WithOne()
            .HasForeignKey(match => match.RoundId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class BracketMatchConfiguration : IEntityTypeConfiguration<BracketMatch>
{
    public void Configure(EntityTypeBuilder<BracketMatch> builder)
    {
        builder.ToTable("BracketMatches");
        builder.HasKey(match => match.Id);
        builder.Property(match => match.Status).HasConversion<int>();
        builder.HasIndex(match => match.ContestId).IsUnique();
        builder.HasIndex(match => match.SeriesId).IsUnique();
        builder.HasIndex(match => match.NextMatchId);
        builder.HasIndex(match => match.LoserNextMatchId);
    }
}

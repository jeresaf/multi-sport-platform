using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SportsPlatform.Modules.Identity.Entities;

namespace SportsPlatform.Infrastructure.Persistence.EntityConfigurations;

public sealed class IdentityActionTokenConfiguration : IEntityTypeConfiguration<IdentityActionToken>
{
    public void Configure(EntityTypeBuilder<IdentityActionToken> builder)
    {
        builder.Property(token => token.Purpose).HasConversion<string>().HasMaxLength(40);
        builder.Property(token => token.TokenHash).HasMaxLength(64).IsRequired();
        builder.HasIndex(token => token.TokenHash).IsUnique();
        builder.HasIndex(token => new { token.UserId, token.Purpose, token.ConsumedAt });
        builder.HasOne<User>().WithMany().HasForeignKey(token => token.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

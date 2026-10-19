using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SportsPlatform.Modules.Identity.Infrastructure.Users;

namespace SportsPlatform.Modules.Identity.Infrastructure.Configurations;

/// <summary>
/// <c>identity.users</c> and Identity's satellite tables, as Identity's own model maps them (store schema version 3)
/// without the roles tables. The normalised email is unique in the database, not only in Identity's validator.
/// </summary>
internal sealed class UserConfiguration :
    IEntityTypeConfiguration<User>,
    IEntityTypeConfiguration<IdentityUserClaim<Guid>>,
    IEntityTypeConfiguration<IdentityUserLogin<Guid>>,
    IEntityTypeConfiguration<IdentityUserToken<Guid>>
{
    public const string NormalizedEmailIndex = "ix_users_normalized_email";

    // ADR-0015: contacts are PRIVATE (docs/architecture/data-conventions.md).
    private const string _pii = "pii";
    private const string _private = "PRIVATE";

    private const int _maxLength = 256;
    private const int _keyMaxLength = 128;

    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");
        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id).ValueGeneratedNever();
        builder.Property(u => u.ConcurrencyStamp).IsConcurrencyToken();

        builder.Property(u => u.UserName).HasMaxLength(_maxLength).HasAnnotation(_pii, _private);
        builder.Property(u => u.NormalizedUserName).HasMaxLength(_maxLength).HasAnnotation(_pii, _private);
        builder.Property(u => u.Email).HasMaxLength(_maxLength).HasAnnotation(_pii, _private);
        builder.Property(u => u.NormalizedEmail).HasMaxLength(_maxLength).HasAnnotation(_pii, _private);
        builder.Property(u => u.PhoneNumber).HasMaxLength(_maxLength).HasAnnotation(_pii, _private);

        builder.HasIndex(u => u.NormalizedUserName).IsUnique();
        builder.HasIndex(u => u.NormalizedEmail).IsUnique().HasDatabaseName(NormalizedEmailIndex);

        builder.HasMany<IdentityUserClaim<Guid>>().WithOne().HasForeignKey(c => c.UserId).IsRequired();
        builder.HasMany<IdentityUserLogin<Guid>>().WithOne().HasForeignKey(l => l.UserId).IsRequired();
        builder.HasMany<IdentityUserToken<Guid>>().WithOne().HasForeignKey(t => t.UserId).IsRequired();
    }

    public void Configure(EntityTypeBuilder<IdentityUserClaim<Guid>> builder)
    {
        builder.ToTable("user_claims");
        builder.HasKey(c => c.Id);
    }

    public void Configure(EntityTypeBuilder<IdentityUserLogin<Guid>> builder)
    {
        builder.ToTable("user_logins");
        builder.HasKey(l => new { l.LoginProvider, l.ProviderKey });
        builder.Property(l => l.LoginProvider).HasMaxLength(_keyMaxLength);
        builder.Property(l => l.ProviderKey).HasMaxLength(_keyMaxLength);
    }

    public void Configure(EntityTypeBuilder<IdentityUserToken<Guid>> builder)
    {
        builder.ToTable("user_tokens");
        builder.HasKey(t => new { t.UserId, t.LoginProvider, t.Name });
        builder.Property(t => t.LoginProvider).HasMaxLength(_keyMaxLength);
        builder.Property(t => t.Name).HasMaxLength(_keyMaxLength);
    }
}

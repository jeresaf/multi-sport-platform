using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SportsPlatform.Modules.Identity.Domain.PlatformRoles;
using SportsPlatform.Modules.Identity.Infrastructure.Users;

namespace SportsPlatform.Modules.Identity.Infrastructure.Configurations;

/// <summary><c>identity.platform_role_assignments</c>: global; at most one unrevoked assignment per user and role.</summary>
internal sealed class PlatformRoleAssignmentConfiguration : IEntityTypeConfiguration<PlatformRoleAssignment>
{
    public const string ActiveRoleIndex = "ux_platform_role_assignments_user_id_role_code_active";

    public void Configure(EntityTypeBuilder<PlatformRoleAssignment> builder)
    {
        builder.ToTable("platform_role_assignments", table => table.HasCheckConstraint(
            "ck_platform_role_assignments_role_code",
            $"role_code IN ({string.Join(", ", PlatformRoles.All.Select(role => $"'{role}'"))})"));
        builder.Ignore(a => a.IsActive);

        builder.Property(a => a.RoleCode).HasMaxLength(PlatformRoleAssignment.RoleCodeMaxLength);
        builder.HasOne<User>().WithMany().HasForeignKey(a => a.UserId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(a => new { a.UserId, a.RoleCode })
            .IsUnique()
            .HasFilter("revoked_at IS NULL")
            .HasDatabaseName(ActiveRoleIndex);
    }
}

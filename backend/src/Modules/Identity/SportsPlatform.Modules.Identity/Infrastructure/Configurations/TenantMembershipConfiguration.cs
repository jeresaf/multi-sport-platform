using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SportsPlatform.BuildingBlocks.Actions;
using SportsPlatform.BuildingBlocks.Persistence;
using SportsPlatform.Modules.Identity.Domain.Memberships;
using SportsPlatform.Modules.Identity.Infrastructure.Users;

namespace SportsPlatform.Modules.Identity.Infrastructure.Configurations;

/// <summary>
/// <c>identity.tenant_memberships</c>: tenant-owned (RLS, S03 D8); at most one membership per tenant and user that is
/// not <c>REMOVED</c> (a removed one stays as history). The user is global, so its key is single-column.
/// </summary>
internal sealed class TenantMembershipConfiguration : IEntityTypeConfiguration<TenantMembership>
{
    public const string Table = "tenant_memberships";

    public const string CurrentMembershipIndex = "ux_tenant_memberships_tenant_id_user_id_current";

    public void Configure(EntityTypeBuilder<TenantMembership> builder)
    {
        builder.ToTable(Table, table => table.HasCheckConstraint(
            "ck_tenant_memberships_status", $"status IN ({EnumColumns.SqlList<MembershipStatus>()})"));

        builder.Property(m => m.Status).HasConversion(EnumColumns.UpperSnake<MembershipStatus>()).HasMaxLength(20);
        builder.Property(m => m.StatusReason).HasMaxLength(TenantMembership.ReasonMaxLength);
        builder.Ignore(m => m.ActiveRoles);

        builder.HasOne<User>().WithMany().HasForeignKey(m => m.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasAlternateKey(m => new { m.TenantId, m.Id });
        builder.HasMany(m => m.Roles).WithOne()
            .HasForeignKey(a => new { a.TenantId, a.MembershipId })
            .HasPrincipalKey(m => new { m.TenantId, m.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(m => m.Roles).HasField("_roles").UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(m => new { m.TenantId, m.UserId })
            .IsUnique()
            .HasFilter("status <> 'REMOVED'")
            .HasDatabaseName(CurrentMembershipIndex);

        // The identity directory's cross-tenant read of a user's memberships (GET /api/account/v1/me).
        builder.HasIndex(m => m.UserId);
    }
}

/// <summary>
/// <c>identity.role_assignments</c>: tenant-owned (RLS), each pointing at its tenant's membership through
/// <c>(tenant_id, membership_id)</c>. A tenant-wide assignment has no scope ID; one active row per membership, role and
/// scope. Role codes are not listed in the database: roles are defined in code and checked at startup (S03 D9).
/// </summary>
internal sealed class RoleAssignmentConfiguration : IEntityTypeConfiguration<RoleAssignment>
{
    public const string Table = "role_assignments";

    public const string ActiveRoleIndex = "ux_role_assignments_membership_role_scope_active";

    public void Configure(EntityTypeBuilder<RoleAssignment> builder)
    {
        builder.ToTable(Table, table =>
        {
            table.HasCheckConstraint("ck_role_assignments_scope_type", $"scope_type IN ({EnumColumns.SqlList<AssignmentScope>()})");
            table.HasCheckConstraint("ck_role_assignments_scope_id", "(scope_type = 'TENANT') = (scope_id IS NULL)");
            table.HasCheckConstraint("ck_role_assignments_revoked", "(revoked_at IS NULL) = (revoked_by IS NULL)");
        });

        builder.Property(a => a.RoleCode).HasMaxLength(RoleAssignment.RoleCodeMaxLength);
        builder.Property(a => a.ScopeType).HasConversion(EnumColumns.UpperSnake<AssignmentScope>()).HasMaxLength(20);
        builder.Ignore(a => a.IsActive);

        builder.HasIndex(a => new { a.TenantId, a.MembershipId, a.RoleCode, a.ScopeType, a.ScopeId })
            .IsUnique()
            .AreNullsDistinct(false)
            .HasFilter("revoked_at IS NULL")
            .HasDatabaseName(ActiveRoleIndex);
    }
}

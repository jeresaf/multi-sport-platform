using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SportsPlatform.BuildingBlocks.Persistence;
using SportsPlatform.Modules.Identity.Domain.Invitations;
using SportsPlatform.Modules.Identity.Domain.Memberships;

namespace SportsPlatform.Modules.Identity.Infrastructure.Configurations;

/// <summary>
/// <c>identity.invitations</c>: tenant-owned (RLS, S03 D8). At most one pending invitation per tenant and email. The
/// token hash is unique across tenants and doesn't start with <c>tenant_id</c>: the identity directory finds an
/// invitation by it before any tenant is set (S03-T4c2).
/// </summary>
internal sealed class InvitationConfiguration : IEntityTypeConfiguration<Invitation>
{
    public const string Table = "invitations";

    /// <summary>
    /// Includes <c>status</c> although the filter fixes it: EF then sees a superseded invitation's index value change
    /// and saves that update before inserting its replacement.
    /// </summary>
    public const string PendingEmailIndex = "ux_invitations_tenant_id_normalized_email_pending";

    public const string TokenHashIndex = "ux_invitations_token_hash";

    // ADR-0015: contacts are PRIVATE (docs/architecture/data-conventions.md).
    private const string _pii = "pii";
    private const string _private = "PRIVATE";

    public void Configure(EntityTypeBuilder<Invitation> builder)
    {
        builder.ToTable(Table, table =>
        {
            table.HasCheckConstraint("ck_invitations_status", $"status IN ({EnumColumns.SqlList<InvitationStatus>()})");
            table.HasCheckConstraint("ck_invitations_role_codes", "cardinality(role_codes) >= 1");
            table.HasCheckConstraint("ck_invitations_token", "(token_hash IS NULL) = (token_issued_at IS NULL)");
            table.HasCheckConstraint("ck_invitations_revoked", "(status = 'REVOKED') = (revoked_by IS NOT NULL AND revoked_at IS NOT NULL)");
            table.HasCheckConstraint(
                "ck_invitations_accepted",
                "(status = 'ACCEPTED') = (accepted_by IS NOT NULL AND accepted_at IS NOT NULL AND membership_id IS NOT NULL)");
        });

        builder.Property(i => i.Email).HasMaxLength(Invitation.EmailMaxLength).HasAnnotation(_pii, _private);
        builder.Property(i => i.NormalizedEmail).HasMaxLength(Invitation.EmailMaxLength).HasAnnotation(_pii, _private);
        builder.Property(i => i.Status).HasConversion(EnumColumns.UpperSnake<InvitationStatus>()).HasMaxLength(20);
        builder.Property(i => i.RoleCodes).HasField("_roleCodes").UsePropertyAccessMode(PropertyAccessMode.Field)
            .HasColumnName("role_codes");

        builder.HasIndex(i => new { i.TenantId, i.NormalizedEmail, i.Status })
            .IsUnique()
            .HasFilter("status = 'PENDING'")
            .HasDatabaseName(PendingEmailIndex);

        builder.HasIndex(i => i.TokenHash).IsUnique().HasDatabaseName(TokenHashIndex);

        // The membership the acceptance created, in the same tenant (a composite key, like role assignments').
        builder.HasOne<TenantMembership>().WithMany()
            .HasForeignKey(i => new { i.TenantId, i.MembershipId })
            .HasPrincipalKey(m => new { m.TenantId, m.Id })
            .OnDelete(DeleteBehavior.Restrict);
    }
}

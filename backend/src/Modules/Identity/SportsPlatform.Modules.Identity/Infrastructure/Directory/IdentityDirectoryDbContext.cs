using Microsoft.EntityFrameworkCore;
using SportsPlatform.BuildingBlocks.Actions;
using SportsPlatform.BuildingBlocks.Persistence;
using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.BuildingBlocks.Tenancy;
using SportsPlatform.Modules.Identity.Domain.Memberships;
using SportsPlatform.Modules.Identity.Infrastructure.Configurations;

namespace SportsPlatform.Modules.Identity.Infrastructure.Directory;

/// <summary>
/// The identity directory's platform service context (S03 D8): <c>sp_platform</c> (BYPASSRLS), read-only, for the
/// lookups a request needs before its tenant is set. It maps <c>identity.tenant_memberships</c> and
/// <c>identity.role_assignments</c> (and the key columns of <c>identity.invitations</c>) as plain rows, not the
/// tenant-owned entities, so no tenant filter applies: every query names its user or an invitation's token hash. Never migrated (<see cref="IdentityDbContext"/> owns the tables); used only by
/// <c>Features.Directory</c>.
/// </summary>
public sealed class IdentityDirectoryDbContext(DbContextOptions<IdentityDirectoryDbContext> options, ITenantContext tenantContext)
    : PlatformServiceDbContext(options, IdentityModule.SchemaName, tenantContext)
{
    internal IQueryable<MembershipRow> Memberships => Set<MembershipRow>().AsNoTracking();

    internal IQueryable<RoleAssignmentRow> RoleAssignments => Set<RoleAssignmentRow>().AsNoTracking();

    internal IQueryable<InvitationRow> Invitations => Set<InvitationRow>().AsNoTracking();

    protected override Error ConcurrencyConflictError => IdentityErrors.ConcurrencyConflict;

    public override int SaveChanges(bool acceptAllChangesOnSuccess) =>
        throw new InvalidOperationException("The identity directory is read-only.");

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("The identity directory is read-only.");

    protected override void ConfigureModel(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MembershipRow>(row =>
        {
            row.ToTable(TenantMembershipConfiguration.Table);
            row.Property(m => m.Status).HasConversion(EnumColumns.UpperSnake<MembershipStatus>());
        });

        modelBuilder.Entity<RoleAssignmentRow>(row =>
        {
            row.ToTable(RoleAssignmentConfiguration.Table);
            row.Property(a => a.ScopeType).HasConversion(EnumColumns.UpperSnake<AssignmentScope>());
        });

        modelBuilder.Entity<InvitationRow>(row => row.ToTable(InvitationConfiguration.Table));
    }
}

/// <summary>A row of <c>identity.tenant_memberships</c>, as the directory reads it.</summary>
internal sealed class MembershipRow
{
    public Guid Id { get; init; }

    public Guid TenantId { get; init; }

    public Guid UserId { get; init; }

    public MembershipStatus Status { get; init; }
}

/// <summary>A row of <c>identity.role_assignments</c>, as the directory reads it.</summary>
internal sealed class RoleAssignmentRow
{
    public Guid Id { get; init; }

    public Guid TenantId { get; init; }

    public Guid MembershipId { get; init; }

    public string RoleCode { get; init; } = string.Empty;

    public AssignmentScope ScopeType { get; init; }

    public Guid? ScopeId { get; init; }

    public DateTimeOffset? RevokedAt { get; init; }
}

/// <summary>The key columns of a row of <c>identity.invitations</c>, as the directory reads them (never the email).</summary>
internal sealed class InvitationRow
{
    public Guid Id { get; init; }

    public Guid TenantId { get; init; }

    public byte[]? TokenHash { get; init; }
}

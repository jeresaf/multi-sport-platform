using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Auditing;
using SportsPlatform.BuildingBlocks.Persistence;
using SportsPlatform.Modules.Platform.Domain.Audit;

namespace SportsPlatform.Modules.Platform.Infrastructure.Configurations;

/// <summary><c>audit.audit_entries</c>: append-only (migration <c>AppendOnly</c>), enums as UPPER_SNAKE text.</summary>
internal sealed class AuditEntryConfiguration : IEntityTypeConfiguration<AuditEntry>
{
    public const string Schema = "audit";
    public const string Table = "audit_entries";

    public void Configure(EntityTypeBuilder<AuditEntry> builder)
    {
        builder.ToTable(Table, Schema, table =>
        {
            table.HasCheckConstraint("ck_audit_entries_outcome", "outcome IN ('SUCCESS', 'FAILURE', 'DENIED')");
            table.HasCheckConstraint("ck_audit_entries_actor_kind", "actor_kind IN ('USER', 'SYSTEM')");
            table.HasCheckConstraint("ck_audit_entries_source", "source IN ('API', 'SYSTEM')");
        });
        builder.IsAppendOnly();

        builder.Property(e => e.ActionCode).HasMaxLength(100);
        builder.Property(e => e.Outcome).HasConversion(UpperCase<AuditOutcome>()).HasMaxLength(20);
        builder.Property(e => e.ErrorCode).HasMaxLength(100);
        builder.Property(e => e.ActorKind).HasConversion(UpperCase<ActorKind>()).HasMaxLength(20);
        builder.Property(e => e.EntityType).HasMaxLength(AuditRecord.EntityTypeMaxLength);
        builder.Property(e => e.Source).HasConversion(UpperCase<AuditSource>()).HasMaxLength(20);
        builder.Property(e => e.CorrelationId).HasMaxLength(64);
        builder.Property(e => e.Reason).HasMaxLength(AuditRecord.ReasonMaxLength);
        builder.Property(e => e.Metadata).HasColumnType("jsonb");

        // pack spec 40 §7: date, tenant and action indexes; the rest serve T6b's query filters.
        builder.HasIndex(e => e.OccurredAt);
        builder.HasIndex(e => new { e.TenantId, e.OccurredAt });
        builder.HasIndex(e => new { e.ActionCode, e.OccurredAt });
        builder.HasIndex(e => new { e.ActorId, e.OccurredAt });
        builder.HasIndex(e => new { e.EntityType, e.EntityId });
        builder.HasIndex(e => e.CorrelationId);
    }

    // Single-word members, so upper case is their UPPER_SNAKE name.
    private static ValueConverter<TEnum, string> UpperCase<TEnum>()
        where TEnum : struct, Enum =>
        new(value => value.ToString().ToUpperInvariant(), value => Enum.Parse<TEnum>(value, true));
}

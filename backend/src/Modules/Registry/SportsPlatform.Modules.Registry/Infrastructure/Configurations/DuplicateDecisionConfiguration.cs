using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Persistence;
using SportsPlatform.BuildingBlocks.Privacy;
using SportsPlatform.Modules.Registry.Domain.People;
using SportsPlatform.Modules.Registry.Domain.People.Duplicates;

namespace SportsPlatform.Modules.Registry.Infrastructure.Configurations;

/// <summary>
/// <c>registry.duplicate_decisions</c> (S06 D7): tenant-owned (RLS). One row per pair of people, stored low ID first;
/// both point at their tenant's persons.
/// </summary>
internal sealed class DuplicateDecisionConfiguration : IEntityTypeConfiguration<DuplicateDecision>
{
    public const string Table = "duplicate_decisions";
    public const string PairIndex = "ux_duplicate_decisions_tenant_id_person_low_id_person_high_id";

    public void Configure(EntityTypeBuilder<DuplicateDecision> builder)
    {
        builder.ToTable(Table, table =>
        {
            table.HasCheckConstraint(
                "ck_duplicate_decisions_decision", $"decision IN ({EnumColumns.SqlList<DuplicateDecisionKind>()})");
            table.HasCheckConstraint("ck_duplicate_decisions_pair", "person_low_id < person_high_id");
            table.HasCheckConstraint("ck_duplicate_decisions_reason", "btrim(reason) <> ''");
            table.HasCheckConstraint(
                "ck_duplicate_decisions_actor_kind", $"actor_kind IN ({EnumColumns.SqlList<ActorKind>()})");
        });

        builder.Property(d => d.Decision).HasConversion(EnumColumns.UpperSnake<DuplicateDecisionKind>()).HasMaxLength(20)
            .IsPersonalData(PersonalDataClass.Internal);
        builder.Property(d => d.Reason).HasMaxLength(DuplicateDecision.ReasonMaxLength).IsPersonalData(PersonalDataClass.Internal);
        builder.Property(d => d.ActorKind).HasConversion(EnumColumns.UpperSnake<ActorKind>()).HasMaxLength(20);

        builder.HasOne<Person>().WithMany()
            .HasForeignKey(d => new { d.TenantId, d.PersonLowId })
            .HasPrincipalKey(p => new { p.TenantId, p.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Person>().WithMany()
            .HasForeignKey(d => new { d.TenantId, d.PersonHighId })
            .HasPrincipalKey(p => new { p.TenantId, p.Id })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(d => new { d.TenantId, d.PersonLowId, d.PersonHighId }).IsUnique().HasDatabaseName(PairIndex);
        builder.HasIndex(d => new { d.TenantId, d.PersonHighId });
    }
}

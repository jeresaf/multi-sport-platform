using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SportsPlatform.BuildingBlocks.Persistence;
using SportsPlatform.BuildingBlocks.Privacy;
using SportsPlatform.Modules.Registry.Domain.People.Consents;

namespace SportsPlatform.Modules.Registry.Infrastructure.Configurations;

/// <summary>
/// <c>registry.guardian_consents</c> (S06 D14): tenant-owned (RLS), each pointing at its tenant's person, and at the
/// record it replaced. The guardian's name, relationship and contact and the evidence are <c>PRIVATE</c>; the scopes,
/// dates, status and withdrawal <c>INTERNAL</c> (S06 D3). The evidence names a Platform media asset without a foreign
/// key (another module's schema). The three <c>withdrawn</c> columns are set together, and only on a withdrawn record.
/// </summary>
internal sealed class GuardianConsentConfiguration : IEntityTypeConfiguration<GuardianConsent>
{
    public const string Table = "guardian_consents";

    public void Configure(EntityTypeBuilder<GuardianConsent> builder)
    {
        var scopes = EnumColumns.SqlList<ConsentScope>();
        builder.ToTable(Table, table =>
        {
            table.HasCheckConstraint("ck_guardian_consents_status", $"status IN ({EnumColumns.SqlList<ConsentStatus>()})");
            table.HasCheckConstraint(
                "ck_guardian_consents_relationship", $"relationship IN ({EnumColumns.SqlList<GuardianRelationship>()})");
            table.HasCheckConstraint(
                "ck_guardian_consents_scopes",
                $"cardinality(scopes) BETWEEN 1 AND {Enum.GetValues<ConsentScope>().Length} AND scopes <@ ARRAY[{scopes}]::character varying(20)[]");
            table.HasCheckConstraint(
                "ck_guardian_consents_withdrawn",
                "(status = 'ACTIVE' AND withdrawn_at IS NULL AND withdrawn_by IS NULL AND withdrawal_reason IS NULL) " +
                "OR (status = 'WITHDRAWN' AND withdrawn_at IS NOT NULL AND withdrawn_by IS NOT NULL AND withdrawal_reason IS NOT NULL)");
        });

        builder.Property(c => c.GuardianName).HasMaxLength(GuardianConsent.GuardianNameMaxLength)
            .IsPersonalData(PersonalDataClass.Private);
        builder.Property(c => c.Relationship).HasConversion(EnumColumns.UpperSnake<GuardianRelationship>()).HasMaxLength(20)
            .IsPersonalData(PersonalDataClass.Private);
        builder.Property(c => c.Contact).HasMaxLength(GuardianConsent.ContactMaxLength).IsPersonalData(PersonalDataClass.Private);
        builder.PrimitiveCollection(c => c.Scopes).HasField("_scopes")
            .UsePropertyAccessMode(PropertyAccessMode.Field)
            .HasAnnotation(Pii.Annotation, Pii.Name(PersonalDataClass.Internal))
            .ElementType(element => element.HasConversion(EnumColumns.UpperSnake<ConsentScope>()).HasMaxLength(20));
        builder.Property(c => c.GivenOn).IsPersonalData(PersonalDataClass.Internal);
        builder.Property(c => c.EvidenceAssetId).IsPersonalData(PersonalDataClass.Private);
        builder.Property(c => c.Status).HasConversion(EnumColumns.UpperSnake<ConsentStatus>()).HasMaxLength(20)
            .IsPersonalData(PersonalDataClass.Internal);
        builder.Property(c => c.RecordedAt).IsPersonalData(PersonalDataClass.Internal);
        builder.Property(c => c.WithdrawnAt).IsPersonalData(PersonalDataClass.Internal);
        builder.Property(c => c.WithdrawalReason).HasMaxLength(GuardianConsent.ReasonMaxLength).IsPersonalData(PersonalDataClass.Internal);
        builder.Ignore(c => c.IsActive);

        builder.HasAlternateKey(c => new { c.TenantId, c.Id }).HasName("ak_guardian_consents_tenant_id_id");
        builder.HasOne<GuardianConsent>().WithMany()
            .HasForeignKey(c => new { c.TenantId, c.ReplacesConsentId })
            .HasPrincipalKey(c => new { c.TenantId, c.Id })
            .HasConstraintName("fk_guardian_consents_replaces_consent_id")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(c => new { c.TenantId, c.PersonId });
    }
}

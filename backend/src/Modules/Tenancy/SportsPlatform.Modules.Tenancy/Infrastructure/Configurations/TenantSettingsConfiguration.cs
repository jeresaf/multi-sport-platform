using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SportsPlatform.Modules.Tenancy.Domain.Settings;
using SportsPlatform.Modules.Tenancy.Domain.Tenants;

namespace SportsPlatform.Modules.Tenancy.Infrastructure.Configurations;

/// <summary>
/// <c>tenancy.tenant_settings</c>: tenant-owned (RLS), one row per tenant (unique <c>tenant_id</c>). The database
/// checks the formats and ranges too.
/// </summary>
internal sealed class TenantSettingsConfiguration : IEntityTypeConfiguration<TenantSettings>
{
    public const string Table = "tenant_settings";

    public void Configure(EntityTypeBuilder<TenantSettings> builder)
    {
        builder.ToTable(Table, table =>
        {
            table.HasCheckConstraint("ck_tenant_settings_currency_code", "currency_code ~ '^[A-Z]{3}$'");
            table.HasCheckConstraint("ck_tenant_settings_default_locale", "default_locale ~ '^[a-z]{2}(-[A-Z]{2})?$'");
            table.HasCheckConstraint(
                "ck_tenant_settings_starting_soon_minutes",
                $"starting_soon_minutes BETWEEN {TenantSettingsValues.StartingSoonMinutesMin} AND {TenantSettingsValues.StartingSoonMinutesMax}");
            table.HasCheckConstraint(
                "ck_tenant_settings_minor_registration_consent",
                $"minor_registration_consent IN ({StatusValues.SqlList<MinorRegistrationConsent>()})");
        });

        builder.Property(s => s.TimeZone).HasMaxLength(64);
        builder.Property(s => s.DefaultLocale).HasMaxLength(5);
        builder.Property(s => s.CurrencyCode).HasMaxLength(3);
        builder.Property(s => s.MinorRegistrationConsent)
            .HasConversion(StatusValues.UpperSnake<MinorRegistrationConsent>())
            .HasMaxLength(20);

        builder.Ignore(s => s.Values);

        builder.HasIndex(s => s.TenantId).IsUnique();
        builder.HasOne<Tenant>().WithOne().HasForeignKey<TenantSettings>(s => s.TenantId).OnDelete(DeleteBehavior.Restrict);
    }
}

using SportsPlatform.BuildingBlocks.Domain;

namespace SportsPlatform.Modules.Tenancy.Domain.Settings;

/// <summary>
/// A tenant's settings (<c>tenancy.tenant_settings</c>): one row per tenant, created with the tenant. Tenant-owned
/// (RLS): read and written under the tenant it belongs to. Typed, allow-listed keys only (<see cref="TenantSettingsValues"/>).
/// </summary>
public sealed class TenantSettings : AggregateRoot, ITenantOwned
{
    private TenantSettings(Guid tenantId, TenantSettingsValues values)
        : base(NewId())
    {
        TenantId = tenantId;
        Apply(values);
    }

    private TenantSettings()
    {
    }

    public Guid TenantId { get; private set; }

    public string TimeZone { get; private set; } = string.Empty;

    public string DefaultLocale { get; private set; } = string.Empty;

    public string CurrencyCode { get; private set; } = string.Empty;

    public bool PublicEnabled { get; private set; }

    public int StartingSoonMinutes { get; private set; }

    public MinorRegistrationConsent MinorRegistrationConsent { get; private set; }

    public TenantSettingsValues Values =>
        new(TimeZone, DefaultLocale, CurrencyCode, PublicEnabled, StartingSoonMinutes, MinorRegistrationConsent);

    /// <summary>
    /// The settings of a new tenant. The tenant ID is given rather than stamped, because the row is added in the same
    /// save as the tenant, which runs under it (S02 D2).
    /// </summary>
    public static TenantSettings Create(Guid tenantId, TenantSettingsValues values)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("A tenant ID cannot be empty.", nameof(tenantId));
        }

        return new TenantSettings(tenantId, values);
    }

    /// <summary>Replaces the values; returns whether anything changed.</summary>
    public bool Update(TenantSettingsValues values)
    {
        if (values == Values)
        {
            return false;
        }

        Apply(values);
        return true;
    }

    private void Apply(TenantSettingsValues values)
    {
        ArgumentNullException.ThrowIfNull(values);
        ArgumentException.ThrowIfNullOrWhiteSpace(values.TimeZone);
        ArgumentException.ThrowIfNullOrWhiteSpace(values.DefaultLocale);
        ArgumentException.ThrowIfNullOrWhiteSpace(values.CurrencyCode);
        ArgumentOutOfRangeException.ThrowIfLessThan(values.StartingSoonMinutes, TenantSettingsValues.StartingSoonMinutesMin);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(values.StartingSoonMinutes, TenantSettingsValues.StartingSoonMinutesMax);
        if (!Enum.IsDefined(values.MinorRegistrationConsent))
        {
            throw new ArgumentOutOfRangeException(nameof(values), values.MinorRegistrationConsent, "Unknown consent policy.");
        }

        TimeZone = values.TimeZone;
        DefaultLocale = values.DefaultLocale;
        CurrencyCode = values.CurrencyCode;
        PublicEnabled = values.PublicEnabled;
        StartingSoonMinutes = values.StartingSoonMinutes;
        MinorRegistrationConsent = values.MinorRegistrationConsent;
    }
}

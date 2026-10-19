using SportsPlatform.Modules.Tenancy.Domain.Settings;

namespace SportsPlatform.UnitTests.Tenancy;

/// <summary>A tenant's settings: defaults, ranges and change detection (S02-T4).</summary>
public sealed class TenantSettingsTests
{
    private static readonly Guid _tenantId = Guid.CreateVersion7();
    private static readonly TenantSettingsValues _defaults = TenantSettingsValues.Defaults("Africa/Kampala", "en-UG", "UGX", publicEnabled: false);

    [Fact]
    public void New_settings_take_the_values_given_and_the_defaults_for_the_rest()
    {
        var settings = TenantSettings.Create(_tenantId, _defaults);

        settings.ShouldSatisfyAllConditions(
            s => s.TenantId.ShouldBe(_tenantId),
            s => s.TimeZone.ShouldBe("Africa/Kampala"),
            s => s.DefaultLocale.ShouldBe("en-UG"),
            s => s.CurrencyCode.ShouldBe("UGX"),
            s => s.PublicEnabled.ShouldBeFalse(),
            s => s.StartingSoonMinutes.ShouldBe(30),
            s => s.MinorRegistrationConsent.ShouldBe(MinorRegistrationConsent.Recommended));
    }

    [Fact]
    public void Settings_need_a_tenant()
    {
        Should.Throw<ArgumentException>(() => TenantSettings.Create(Guid.Empty, _defaults));
    }

    [Theory]
    [InlineData(5)]
    [InlineData(180)]
    public void Starting_soon_minutes_accept_the_range_ends(int minutes)
    {
        TenantSettings.Create(_tenantId, _defaults with { StartingSoonMinutes = minutes }).StartingSoonMinutes.ShouldBe(minutes);
    }

    [Theory]
    [InlineData(4)]
    [InlineData(181)]
    [InlineData(0)]
    public void Starting_soon_minutes_outside_the_range_are_refused(int minutes)
    {
        var settings = TenantSettings.Create(_tenantId, _defaults);

        Should.Throw<ArgumentOutOfRangeException>(() => settings.Update(settings.Values with { StartingSoonMinutes = minutes }));
        settings.StartingSoonMinutes.ShouldBe(30);
    }

    [Fact]
    public void An_unknown_consent_policy_is_refused()
    {
        Should.Throw<ArgumentOutOfRangeException>(() =>
            TenantSettings.Create(_tenantId, _defaults with { MinorRegistrationConsent = (MinorRegistrationConsent)42 }));
    }

    [Fact]
    public void Update_reports_whether_anything_changed()
    {
        var settings = TenantSettings.Create(_tenantId, _defaults);

        settings.Update(_defaults).ShouldBeFalse();
        settings.Update(_defaults with { PublicEnabled = true, MinorRegistrationConsent = MinorRegistrationConsent.Required }).ShouldBeTrue();

        settings.ShouldSatisfyAllConditions(
            s => s.PublicEnabled.ShouldBeTrue(),
            s => s.MinorRegistrationConsent.ShouldBe(MinorRegistrationConsent.Required),
            s => s.TimeZone.ShouldBe("Africa/Kampala"));
    }
}

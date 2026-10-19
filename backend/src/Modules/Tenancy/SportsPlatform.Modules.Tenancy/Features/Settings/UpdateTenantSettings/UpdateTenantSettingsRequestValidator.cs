using FluentValidation;
using SportsPlatform.Modules.Platform.Contracts.Reference;
using SportsPlatform.Modules.Tenancy.Domain.Settings;
using SportsPlatform.Modules.Tenancy.Features.Tenants;

namespace SportsPlatform.Modules.Tenancy.Features.Settings.UpdateTenantSettings;

/// <summary>Only the keys sent are checked; codes with the same rules as the tenant profile. Scoped (reference data).</summary>
public sealed class UpdateTenantSettingsRequestValidator : AbstractValidator<UpdateTenantSettingsRequest>
{
    public UpdateTenantSettingsRequestValidator(IReferenceData reference)
    {
        ArgumentNullException.ThrowIfNull(reference);

        RuleFor(request => request.TimeZone).TimeZone(reference).When(r => r.TimeZone is not null);
        RuleFor(request => request.DefaultLocale).Locale(reference).When(r => r.DefaultLocale is not null);
        RuleFor(request => request.CurrencyCode).Currency(reference).When(r => r.CurrencyCode is not null);
        RuleFor(request => request.StartingSoonMinutes)
            .InclusiveBetween(TenantSettingsValues.StartingSoonMinutesMin, TenantSettingsValues.StartingSoonMinutesMax)
            .WithMessage($"'startingSoonMinutes' must be {TenantSettingsValues.StartingSoonMinutesMin}–{TenantSettingsValues.StartingSoonMinutesMax}.")
            .When(r => r.StartingSoonMinutes is not null);
    }
}

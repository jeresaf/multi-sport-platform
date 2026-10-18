using FluentValidation;
using SportsPlatform.Modules.Platform.Contracts.Reference;
using SportsPlatform.Modules.Tenancy.Domain.Tenants;

namespace SportsPlatform.Modules.Tenancy.Features.Tenants.UpdateTenantProfile;

/// <summary>Only the fields sent are checked, with the same rules as on create. Scoped (reference data).</summary>
public sealed class UpdateTenantProfileRequestValidator : AbstractValidator<UpdateTenantProfileRequest>
{
    public UpdateTenantProfileRequestValidator(IReferenceData reference)
    {
        ArgumentNullException.ThrowIfNull(reference);

        RuleFor(request => request.LegalName).Name("legalName", TenantProfile.LegalNameMaxLength).When(r => r.LegalName is not null);
        RuleFor(request => request.DisplayName).Name("displayName", TenantProfile.DisplayNameMaxLength).When(r => r.DisplayName is not null);
        RuleFor(request => request.CountryCode).Country(reference).When(r => r.CountryCode is not null);
        RuleFor(request => request.TimeZone).TimeZone(reference).When(r => r.TimeZone is not null);
        RuleFor(request => request.DefaultLocale).Locale(reference).When(r => r.DefaultLocale is not null);
        RuleFor(request => request.CurrencyCode).Currency(reference).When(r => r.CurrencyCode is not null);
    }
}

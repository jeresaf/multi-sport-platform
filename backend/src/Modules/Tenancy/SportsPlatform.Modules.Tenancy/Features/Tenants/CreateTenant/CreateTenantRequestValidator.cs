using FluentValidation;
using SportsPlatform.Modules.Platform.Contracts.Reference;
using SportsPlatform.Modules.Tenancy.Domain.Tenants;

namespace SportsPlatform.Modules.Tenancy.Features.Tenants.CreateTenant;

/// <summary>Scoped: the reference checks read Platform's reference data.</summary>
public sealed class CreateTenantRequestValidator : AbstractValidator<CreateTenantRequest>
{
    public CreateTenantRequestValidator(IReferenceData reference)
    {
        ArgumentNullException.ThrowIfNull(reference);

        RuleFor(request => request.Slug)
            .Cascade(CascadeMode.Stop)
            .Must(TenantSlug.HasValidFormat)
            .WithMessage($"'slug' must be {TenantSlug.MinLength}–{TenantSlug.MaxLength} lower-case letters, digits and hyphens, " +
                "starting and ending with a letter or digit.")
            .Must(slug => !TenantSlug.IsReserved(slug!))
            .WithMessage($"'slug' must not be one of the reserved words: {string.Join(", ", TenantSlug.Reserved.Order(StringComparer.Ordinal))}.");
        RuleFor(request => request.LegalName).Name("legalName", TenantProfile.LegalNameMaxLength);
        RuleFor(request => request.DisplayName).Name("displayName", TenantProfile.DisplayNameMaxLength);
        RuleFor(request => request.CountryCode).Country(reference);
        RuleFor(request => request.TimeZone).TimeZone(reference);
        RuleFor(request => request.DefaultLocale).Locale(reference);
        RuleFor(request => request.CurrencyCode).Currency(reference);
    }
}

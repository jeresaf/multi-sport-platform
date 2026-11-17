using System.Text.Json.Serialization;
using FluentValidation;
using SportsPlatform.Modules.Registry.Domain.People.Profiles;

namespace SportsPlatform.Modules.Registry.Features.People.Profiles;

/// <summary>
/// One official capability as sent: a variant's code, an official role code of its definition, and an optional grade,
/// licence number and licence end date (each optional on its own).
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record OfficialCapabilityRequest(
    string? Variant,
    string? Role,
    string? Grade = null,
    string? LicenceNumber = null,
    DateOnly? LicenceValidUntil = null);

/// <summary>
/// An official profile's capabilities, for a new profile or replacing a profile's as a whole (S06 D12): 1–10, one per
/// variant and role. <c>version</c>, when sent, must be the person's current one.
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record OfficialProfileRequest(IReadOnlyList<OfficialCapabilityRequest>? Capabilities, uint? Version = null)
{
    /// <summary>The capabilities as the domain takes them; call it only after the validator accepted the request.</summary>
    public IReadOnlyList<OfficialCapabilityDetails> ToDetails() =>
    [
        .. Capabilities!.Select(c => new OfficialCapabilityDetails(
            c.Variant!, c.Role!, c.Grade, c.LicenceNumber, c.LicenceValidUntil)),
    ];
}

/// <summary>
/// The capabilities' shape: 1–10; each a variant and a role code of 1–50 characters (letters, digits and underscores, as
/// definitions write them), at most once each pair, and a grade and a licence number of at most 50 characters each
/// without control characters. Whether the codes are the definitions' is checked by <see cref="SportCodes"/>. A licence
/// end date in the past is accepted: the read shows it expired.
/// </summary>
public sealed class OfficialProfileRequestValidator : AbstractValidator<OfficialProfileRequest>
{
    public OfficialProfileRequestValidator()
    {
        RuleFor(request => request.Capabilities)
            .Cascade(CascadeMode.Stop)
            .Must(capabilities => capabilities is { Count: > 0 })
            .WithMessage("'capabilities' must hold at least one capability.")
            .Must(capabilities => capabilities!.Count <= OfficialProfile.MaxCapabilities)
            .WithMessage($"'capabilities' has at most {OfficialProfile.MaxCapabilities} capabilities.")
            .Must(capabilities => capabilities!.All(c => c is not null))
            .WithMessage("Each of 'capabilities' must be a capability.")
            .Must(capabilities => capabilities!.Select(c => (c.Variant, c.Role)).Distinct().Count() == capabilities!.Count)
            .WithMessage("'capabilities' can't hold a variant and role twice.");
        RuleForEach(request => request.Capabilities).Where(capability => capability is not null).ChildRules(capability =>
        {
            capability.RuleFor(c => c.Variant)
                .Must(variant => variant is not null && AthleteProfileDetailsRules.IsCode(variant, OfficialCapability.VariantMaxLength))
                .WithMessage($"'variant' must be a sport variant code of at most {OfficialCapability.VariantMaxLength} characters.");
            capability.RuleFor(c => c.Role)
                .Must(role => role is not null && AthleteProfileDetailsRules.IsCode(role, OfficialCapability.RoleMaxLength))
                .WithMessage($"'role' must be an official role code of at most {OfficialCapability.RoleMaxLength} characters.");
            capability.RuleFor(c => c.Grade)
                .Cascade(CascadeMode.Stop)
                .Must(text => text is null || text.Trim().Length <= OfficialCapability.GradeMaxLength)
                .WithMessage($"'grade' must be at most {OfficialCapability.GradeMaxLength} characters.")
                .Must(text => text is null || !text.Any(char.IsControl))
                .WithMessage("'grade' can't contain control characters.");
            capability.RuleFor(c => c.LicenceNumber)
                .Cascade(CascadeMode.Stop)
                .Must(text => text is null || text.Trim().Length <= OfficialCapability.LicenceNumberMaxLength)
                .WithMessage($"'licenceNumber' must be at most {OfficialCapability.LicenceNumberMaxLength} characters.")
                .Must(text => text is null || !text.Any(char.IsControl))
                .WithMessage("'licenceNumber' can't contain control characters.");
        }).When(request => request.Capabilities is not null);
    }
}

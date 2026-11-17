using System.Text.Json.Serialization;
using FluentValidation;
using SportsPlatform.Modules.Registry.Domain.People.Profiles;

namespace SportsPlatform.Modules.Registry.Features.People.Profiles;

/// <summary>One staff capability as sent: a variant's code, a staff role code of its definition, a qualification.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record StaffCapabilityRequest(string? Variant, string? Role, string? Qualification = null);

/// <summary>
/// A staff profile's capabilities, for a new profile or replacing a profile's as a whole (S06 D12): 1–10, one per
/// variant and role. <c>version</c>, when sent, must be the person's current one.
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record StaffProfileRequest(IReadOnlyList<StaffCapabilityRequest>? Capabilities, uint? Version = null)
{
    /// <summary>The capabilities as the domain takes them; call it only after the validator accepted the request.</summary>
    public IReadOnlyList<StaffCapabilityDetails> ToDetails() =>
        [.. Capabilities!.Select(c => new StaffCapabilityDetails(c.Variant!, c.Role!, c.Qualification))];
}

/// <summary>
/// The capabilities' shape: 1–10; each a variant and a role code of 1–50 characters (letters, digits and underscores, as
/// definitions write them), at most once each pair, and a qualification of at most 100 characters without control
/// characters. Whether the codes are the definitions' is checked by <see cref="SportCodes"/>.
/// </summary>
public sealed class StaffProfileRequestValidator : AbstractValidator<StaffProfileRequest>
{
    public StaffProfileRequestValidator()
    {
        RuleFor(request => request.Capabilities)
            .Cascade(CascadeMode.Stop)
            .Must(capabilities => capabilities is { Count: > 0 })
            .WithMessage("'capabilities' must hold at least one capability.")
            .Must(capabilities => capabilities!.Count <= StaffProfile.MaxCapabilities)
            .WithMessage($"'capabilities' has at most {StaffProfile.MaxCapabilities} capabilities.")
            .Must(capabilities => capabilities!.All(c => c is not null))
            .WithMessage("Each of 'capabilities' must be a capability.")
            .Must(capabilities => capabilities!.Select(c => (c.Variant, c.Role)).Distinct().Count() == capabilities!.Count)
            .WithMessage("'capabilities' can't hold a variant and role twice.");
        RuleForEach(request => request.Capabilities).Where(capability => capability is not null).ChildRules(capability =>
        {
            capability.RuleFor(c => c.Variant)
                .Must(variant => variant is not null && AthleteProfileDetailsRules.IsCode(variant, StaffCapability.VariantMaxLength))
                .WithMessage($"'variant' must be a sport variant code of at most {StaffCapability.VariantMaxLength} characters.");
            capability.RuleFor(c => c.Role)
                .Must(role => role is not null && AthleteProfileDetailsRules.IsCode(role, StaffCapability.RoleMaxLength))
                .WithMessage($"'role' must be a staff role code of at most {StaffCapability.RoleMaxLength} characters.");
            capability.RuleFor(c => c.Qualification)
                .Cascade(CascadeMode.Stop)
                .Must(text => text is null || text.Trim().Length <= StaffCapability.QualificationMaxLength)
                .WithMessage($"'qualification' must be at most {StaffCapability.QualificationMaxLength} characters.")
                .Must(text => text is null || !text.Any(char.IsControl))
                .WithMessage("'qualification' can't contain control characters.");
        }).When(request => request.Capabilities is not null);
    }
}

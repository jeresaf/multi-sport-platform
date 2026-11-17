using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using FluentValidation;
using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.Modules.Registry.Domain.People.Profiles;

namespace SportsPlatform.Modules.Registry.Features.People.Profiles;

/// <summary>
/// A new athlete profile (S06 D12): the variant's code, then the details as <see cref="UpdateAthleteProfileRequest"/>
/// takes them; <c>version</c>, when sent, must be the person's current one.
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreateAthleteProfileRequest(
    string? Variant,
    string? PrimaryPosition = null,
    IReadOnlyList<string>? OtherPositions = null,
    DominantSide? DominantSide = null,
    string? PublicName = null,
    uint? Version = null) : IAthleteProfileDetails;

/// <summary>
/// An athlete profile's details, replaced as a whole (a field left out is cleared); the variant never changes.
/// <c>version</c>, when sent, must be the person's current one.
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record UpdateAthleteProfileRequest(
    string? PrimaryPosition = null,
    IReadOnlyList<string>? OtherPositions = null,
    DominantSide? DominantSide = null,
    string? PublicName = null,
    uint? Version = null) : IAthleteProfileDetails;

/// <summary>
/// A profile's new status, with a reason (audit <c>REQUIRED_WITH_REASON</c>). The reason is kept permanently in the
/// profile's history and the audit trail and is not redacted: it must not contain personal data.
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ChangeProfileStatusRequest(ProfileStatus? Status, string? Reason, uint? Version = null);

/// <summary>What a create and an update of an athlete profile share.</summary>
public interface IAthleteProfileDetails
{
    string? PrimaryPosition { get; }

    IReadOnlyList<string>? OtherPositions { get; }

    DominantSide? DominantSide { get; }

    string? PublicName { get; }
}

internal static class AthleteProfileDetailsMapping
{
    /// <summary>The details as the domain takes them; call it only after the validator accepted the request.</summary>
    public static AthleteProfileDetails ToDetails(this IAthleteProfileDetails request) =>
        new(request.PrimaryPosition, request.OtherPositions ?? [], request.DominantSide, request.PublicName);
}

/// <summary>
/// The shape of an athlete profile's details: codes of 1–50 characters (letters, digits and underscores, as definitions
/// write them), up to five other positions, distinct and not the primary one, and a public name of at most 200
/// characters without control characters. Whether the codes are the definition's is checked by <see cref="SportCodes"/>.
/// </summary>
internal static partial class AthleteProfileDetailsRules
{
    public static void Apply<T>(AbstractValidator<T> validator)
        where T : IAthleteProfileDetails
    {
        validator.RuleFor(request => request.PrimaryPosition)
            .Must(code => code is null || IsCode(code, AthleteProfile.PositionMaxLength))
            .WithMessage($"'primaryPosition' must be a position code of at most {AthleteProfile.PositionMaxLength} characters.");
        validator.RuleFor(request => request.OtherPositions)
            .Cascade(CascadeMode.Stop)
            .Must(codes => codes is null || codes.Count <= AthleteProfile.MaxOtherPositions)
            .WithMessage($"'otherPositions' has at most {AthleteProfile.MaxOtherPositions} positions.")
            .Must(codes => codes is null || codes.Distinct(StringComparer.Ordinal).Count() == codes.Count)
            .WithMessage("'otherPositions' can't repeat a position.");
        validator.RuleForEach(request => request.OtherPositions)
            .Must(code => code is not null && IsCode(code, AthleteProfile.PositionMaxLength))
            .WithMessage($"Each of 'otherPositions' must be a position code of at most {AthleteProfile.PositionMaxLength} characters.");
        validator.RuleFor(request => request.OtherPositions)
            .Must((request, codes) => request.PrimaryPosition is null || codes is null || !codes.Contains(request.PrimaryPosition))
            .WithMessage("'otherPositions' can't hold the primary position.");
        validator.RuleFor(request => request.PublicName)
            .Cascade(CascadeMode.Stop)
            .Must(name => name is null || name.Trim().Length <= AthleteProfile.PublicNameMaxLength)
            .WithMessage($"'publicName' must be at most {AthleteProfile.PublicNameMaxLength} characters.")
            .Must(name => name is null || !name.Any(char.IsControl))
            .WithMessage("'publicName' can't contain control characters.");
    }

    public static bool IsCode(string code, int maxLength) => code.Length <= maxLength && Code().IsMatch(code);

    [GeneratedRegex(@"^[A-Z][A-Z0-9_]*\z", RegexOptions.CultureInvariant)]
    private static partial Regex Code();
}

/// <summary>A variant's code and the details' shape.</summary>
public sealed class CreateAthleteProfileRequestValidator : AbstractValidator<CreateAthleteProfileRequest>
{
    public CreateAthleteProfileRequestValidator()
    {
        RuleFor(request => request.Variant)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithMessage("'variant' must not be empty.")
            .Must(variant => AthleteProfileDetailsRules.IsCode(variant!, AthleteProfile.VariantMaxLength))
            .WithMessage($"'variant' must be a sport variant code of at most {AthleteProfile.VariantMaxLength} characters.");
        AthleteProfileDetailsRules.Apply(this);
    }
}

/// <summary>The details' shape.</summary>
public sealed class UpdateAthleteProfileRequestValidator : AbstractValidator<UpdateAthleteProfileRequest>
{
    public UpdateAthleteProfileRequestValidator() => AthleteProfileDetailsRules.Apply(this);
}

/// <summary>A status and a reason of 1–500 characters after trimming.</summary>
public sealed class ChangeProfileStatusRequestValidator : AbstractValidator<ChangeProfileStatusRequest>
{
    public ChangeProfileStatusRequestValidator()
    {
        RuleFor(request => request.Status).NotNull().WithMessage("'status' must not be empty.");
        RuleFor(request => request.Reason)
            .Cascade(CascadeMode.Stop)
            .Must(reason => !string.IsNullOrWhiteSpace(reason))
            .WithMessage("'reason' must not be empty.")
            .WithErrorCode(CommonErrors.ReasonRequired.Code)
            .Must(reason => reason!.Trim().Length <= ProfileStatusChange.ReasonMaxLength)
            .WithMessage($"'reason' must be at most {ProfileStatusChange.ReasonMaxLength} characters.")
            .WithErrorCode(CommonErrors.ReasonRequired.Code);
    }
}

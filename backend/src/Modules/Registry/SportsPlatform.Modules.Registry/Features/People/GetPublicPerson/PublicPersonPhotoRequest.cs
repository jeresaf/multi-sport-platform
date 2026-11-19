using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using SportsPlatform.BuildingBlocks.Persistence.Messaging;

namespace SportsPlatform.Modules.Registry.Features.People.GetPublicPerson;

/// <summary>The photo route's query as sent (text, so a missing or unknown variant is <c>VALIDATION_INVALID_QUERY</c>).</summary>
public sealed record PublicPersonPhotoRequest([FromQuery(Name = "variant")] string? Variant)
{
    private static readonly Dictionary<string, PublicPhotoVariant> _variants = Enum.GetValues<PublicPhotoVariant>()
        .ToDictionary(value => Name(value), StringComparer.Ordinal);

    public static IReadOnlyCollection<string> Variants => _variants.Keys;

    /// <summary>The variant as the API writes it (<c>THUMBNAIL</c>, <c>MEDIUM</c>, <c>LARGE</c>).</summary>
    public static string Name(PublicPhotoVariant variant) => MessageSerialization.EnumNamingPolicy.ConvertName(variant.ToString());

    public static bool TryParse(string? value, out PublicPhotoVariant variant)
    {
        variant = default;
        return value is not null && _variants.TryGetValue(value, out variant);
    }

    /// <summary>The variant; call it only after the validator accepted the request.</summary>
    public PublicPhotoVariant ToVariant() =>
        TryParse(Variant, out var variant) ? variant : throw new InvalidOperationException("Validate the request first.");
}

/// <summary>Messages name the parameter and the accepted values, never echo what was sent.</summary>
public sealed class PublicPersonPhotoRequestValidator : AbstractValidator<PublicPersonPhotoRequest>
{
    public PublicPersonPhotoRequestValidator() =>
        RuleFor(request => request.Variant)
            .Must(variant => PublicPersonPhotoRequest.TryParse(variant, out _))
            .WithMessage($"'variant' is required and must be one of: {string.Join(", ", PublicPersonPhotoRequest.Variants)}.");
}

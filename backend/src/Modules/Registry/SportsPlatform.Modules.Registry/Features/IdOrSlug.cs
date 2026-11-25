namespace SportsPlatform.Modules.Registry.Features;

/// <summary>
/// The <c>{idOrSlug}</c> of a public read (S07 D17): a UUID in its usual form (hyphens) is an ID, anything else a slug.
/// A slug never parses as a UUID (<see cref="Domain.Slugs"/>), so a value can't mean both. A value that is neither (a
/// slug that isn't well formed) finds nothing, without a query.
/// </summary>
public readonly record struct IdOrSlug(Guid? Id, string? Slug)
{
    public static IdOrSlug Parse(string value) =>
        Guid.TryParseExact(value, "D", out var id) ? new(id, null)
        : Domain.Slugs.IsValid(value) ? new(null, value)
        : default;

    /// <summary>Whether the value can name a record at all.</summary>
    public bool CanMatch => Id is not null || Slug is not null;
}

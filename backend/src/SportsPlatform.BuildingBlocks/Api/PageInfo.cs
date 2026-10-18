namespace SportsPlatform.BuildingBlocks.Api;

/// <summary>
/// The <c>page</c> member of a cursor-paginated list (docs/architecture/api-conventions.md): pass
/// <paramref name="NextCursor"/> as <c>cursor</c> to get the next page; it is null on the last page.
/// </summary>
public sealed record PageInfo(string? NextCursor, bool HasMore);

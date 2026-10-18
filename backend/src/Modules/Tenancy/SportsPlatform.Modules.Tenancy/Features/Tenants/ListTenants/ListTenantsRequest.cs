using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using SportsPlatform.BuildingBlocks.Persistence.Messaging;
using SportsPlatform.Modules.Tenancy.Contracts.Tenants;

namespace SportsPlatform.Modules.Tenancy.Features.Tenants.ListTenants;

/// <summary>The tenant list's query parameters as sent (text, so every bad value is <c>VALIDATION_INVALID_QUERY</c>).</summary>
public sealed record ListTenantsRequest(
    [FromQuery(Name = "status")] string? Status,
    [FromQuery(Name = "limit")] string? Limit,
    [FromQuery(Name = "cursor")] string? Cursor)
{
    public const int DefaultLimit = 25;
    public const int MaxLimit = 100;

    private static readonly Dictionary<string, TenantStatus> _statuses = Enum.GetValues<TenantStatus>()
        .ToDictionary(status => MessageSerialization.EnumNamingPolicy.ConvertName(status.ToString()), StringComparer.Ordinal);

    public static IReadOnlyCollection<string> Statuses => _statuses.Keys;

    public static bool TryParseStatus(string value, out TenantStatus status) => _statuses.TryGetValue(value, out status);

    public static bool TryParseLimit(string value, out int limit) =>
        int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out limit) && limit is >= 1 and <= MaxLimit;

    /// <summary>The query; call it only after the validator accepted the request.</summary>
    public TenantQuery ToQuery()
    {
        TenantStatus? status = null;
        if (Status is not null)
        {
            status = TryParseStatus(Status, out var parsed) ? parsed : throw NotValidated();
        }

        var limit = DefaultLimit;
        if (Limit is not null && !TryParseLimit(Limit, out limit))
        {
            throw NotValidated();
        }

        string? after = null;
        if (Cursor is not null)
        {
            after = TenantCursor.TryDecode(Cursor, out var slug) ? slug : throw NotValidated();
        }

        return new TenantQuery(status, limit, after);
    }

    private static InvalidOperationException NotValidated() => new("Validate the request first.");
}

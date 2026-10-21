using System.Text.Json;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using SportsPlatform.BuildingBlocks.Entitlements;

namespace SportsPlatform.Modules.Tenancy.Infrastructure.Configurations;

/// <summary>
/// An <see cref="EntitlementValue"/> column is <c>jsonb</c> in the value's one JSON shape
/// (<see cref="EntitlementValueJsonConverter"/>), for every Tenancy table that stores one (TenancyDbContext's conventions).
/// Records compare by value, so EF sees a changed value.
/// </summary>
internal sealed class EntitlementValueConverter() : ValueConverter<EntitlementValue, string>(
    value => JsonSerializer.Serialize(value, JsonSerializerOptions.Default),
    json => JsonSerializer.Deserialize<EntitlementValue>(json, JsonSerializerOptions.Default)!)
{
    public const string ColumnType = "jsonb";
}

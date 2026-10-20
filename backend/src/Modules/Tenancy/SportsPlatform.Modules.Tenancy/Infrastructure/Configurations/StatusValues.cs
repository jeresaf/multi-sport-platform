using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using SportsPlatform.BuildingBlocks.Persistence;
using SportsPlatform.Modules.Tenancy.Contracts.Tenants;

namespace SportsPlatform.Modules.Tenancy.Infrastructure.Configurations;

/// <summary>Enums stored as their UPPER_SNAKE names (<see cref="EnumColumns"/>).</summary>
internal static class StatusValues
{
    public static ValueConverter<TEnum, string> UpperSnake<TEnum>()
        where TEnum : struct, Enum => EnumColumns.UpperSnake<TEnum>();

    /// <summary>The SQL list of a check constraint, e.g. <c>'PROVISIONING', 'ACTIVE'</c>.</summary>
    public static string SqlList<TEnum>()
        where TEnum : struct, Enum => EnumColumns.SqlList<TEnum>();

    public static string TenantStatuses => SqlList<TenantStatus>();
}

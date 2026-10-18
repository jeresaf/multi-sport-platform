using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using SportsPlatform.BuildingBlocks.Persistence.Messaging;
using SportsPlatform.Modules.Tenancy.Contracts.Tenants;

namespace SportsPlatform.Modules.Tenancy.Infrastructure.Configurations;

/// <summary>Enums stored as their UPPER_SNAKE names, the same as in the HTTP and event JSON.</summary>
internal static class StatusValues
{
    public static ValueConverter<TEnum, string> UpperSnake<TEnum>()
        where TEnum : struct, Enum =>
        new(value => MessageSerialization.EnumNamingPolicy.ConvertName(value.ToString()), value => Parse<TEnum>(value));

    /// <summary>The SQL list of a check constraint, e.g. <c>'PROVISIONING', 'ACTIVE'</c>.</summary>
    public static string SqlList<TEnum>()
        where TEnum : struct, Enum =>
        string.Join(", ", Enum.GetNames<TEnum>().Select(name => $"'{MessageSerialization.EnumNamingPolicy.ConvertName(name)}'"));

    public static string TenantStatuses => SqlList<TenantStatus>();

    private static TEnum Parse<TEnum>(string value)
        where TEnum : struct, Enum =>
        Enum.Parse<TEnum>(value.Replace("_", string.Empty, StringComparison.Ordinal), ignoreCase: true);
}

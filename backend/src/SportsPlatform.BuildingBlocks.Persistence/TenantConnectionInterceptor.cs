using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SportsPlatform.BuildingBlocks.Tenancy;

namespace SportsPlatform.BuildingBlocks.Persistence;

/// <summary>
/// Writes the tenant for PostgreSQL's row-level security every time a connection opens:
/// <c>set_config('app.tenant_id', …, false)</c>, session-level so it also covers queries outside an explicit transaction.
/// Always a value: the tenant ID, or an empty string (no rows) without a tenant, so a pooled connection never keeps a
/// previous scope's tenant (docs/architecture/multi-tenancy.md).
/// </summary>
public sealed class TenantConnectionInterceptor(ITenantContext tenant) : DbConnectionInterceptor
{
    public const string Setting = "app.tenant_id";

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        ArgumentNullException.ThrowIfNull(connection);
        using var command = CreateCommand(connection);
        command.ExecuteNonQuery();
    }

    public override async Task ConnectionOpenedAsync(
        DbConnection connection, ConnectionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        await using var command = CreateCommand(connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private DbCommand CreateCommand(DbConnection connection)
    {
        var command = connection.CreateCommand();
        command.CommandText = $"SELECT set_config('{Setting}', @tenant_id, false)";
        var parameter = command.CreateParameter();
        parameter.ParameterName = "tenant_id";
        parameter.Value = tenant.TenantId?.ToString() ?? string.Empty;
        command.Parameters.Add(parameter);
        return command;
    }
}

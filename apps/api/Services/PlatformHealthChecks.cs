using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using SportsPlatform.Infrastructure.Persistence;
using StackExchange.Redis;
using System.Net;
using System.Net.Sockets;

namespace SportsPlatform.Api.Services;

public sealed class DatabaseHealthCheck : IHealthCheck
{
    private readonly SportsPlatformDbContext _database;
    public DatabaseHealthCheck(SportsPlatformDbContext database) => _database = database;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var connectivityTask = _database.Database.CanConnectAsync(cancellationToken);
            var completed = await Task.WhenAny(
                connectivityTask,
                Task.Delay(TimeSpan.FromSeconds(2), cancellationToken));
            if (completed != connectivityTask)
                return HealthCheckResult.Unhealthy("PostgreSQL connectivity check timed out.");
            return await connectivityTask
                ? HealthCheckResult.Healthy("PostgreSQL is reachable.")
                : HealthCheckResult.Unhealthy("PostgreSQL is unreachable.");
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy("PostgreSQL is unreachable.", exception);
        }
    }
}

public sealed class RedisHealthCheck : IHealthCheck
{
    private readonly string? _connectionString;
    public RedisHealthCheck(IConfiguration configuration) =>
        _connectionString = configuration["Database:RedisConnectionString"];

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_connectionString))
            return HealthCheckResult.Degraded("Redis is not configured; cache-dependent features are bypassed.");
        try
        {
            var options = ConfigurationOptions.Parse(_connectionString);
            if (options.EndPoints.Count == 0)
                return HealthCheckResult.Degraded("Redis has no configured endpoint.");
            var endpoint = options.EndPoints[0];
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(2));
            using var client = new TcpClient();
            switch (endpoint)
            {
                case DnsEndPoint dns:
                    await client.ConnectAsync(dns.Host, dns.Port, timeout.Token);
                    break;
                case IPEndPoint ip:
                    await client.ConnectAsync(ip.Address, ip.Port, timeout.Token);
                    break;
                default:
                    return HealthCheckResult.Degraded($"Unsupported Redis endpoint type: {endpoint.GetType().Name}.");
            }
            return HealthCheckResult.Healthy("Redis TCP endpoint is reachable.");
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Degraded(
                "Redis is unavailable; authoritative PostgreSQL-backed operations remain available.", exception);
        }
    }
}

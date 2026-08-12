using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using SportsPlatform.Infrastructure.Persistence;
using SportsPlatform.Modules.Notifications.Entities;
using SportsPlatform.Modules.Reporting.Entities;

namespace SportsPlatform.Api.Services;

public sealed class OperationalMetrics
{
    private readonly ConcurrentDictionary<RequestKey, RequestValue> _requests = new();
    private readonly ConcurrentDictionary<string, string> _connections = new();

    public void RecordRequest(string method, int statusCode, Guid? tenantId, double seconds)
    {
        var key = new RequestKey(
            method.ToUpperInvariant(),
            statusCode,
            tenantId?.ToString() ?? "none");
        var value = _requests.GetOrAdd(key, _ => new RequestValue());
        Interlocked.Increment(ref value.Count);
        lock (value.Sync) value.DurationSeconds += seconds;
    }

    public void ConnectionOpened(string connectionId, Guid? tenantId) =>
        _connections[connectionId] = tenantId?.ToString() ?? "public";

    public void ConnectionClosed(string connectionId) =>
        _connections.TryRemove(connectionId, out _);

    public IReadOnlyList<RequestMetric> Requests() =>
        _requests.Select(item => new RequestMetric(
                item.Key.Method,
                item.Key.StatusCode,
                item.Key.TenantId,
                Interlocked.Read(ref item.Value.Count),
                ReadDuration(item.Value)))
            .ToArray();

    public IReadOnlyDictionary<string, int> Connections() =>
        _connections.Values
            .GroupBy(tenantId => tenantId)
            .ToDictionary(group => group.Key, group => group.Count());

    private static double ReadDuration(RequestValue value)
    {
        lock (value.Sync) return value.DurationSeconds;
    }

    private sealed record RequestKey(string Method, int StatusCode, string TenantId);
    private sealed class RequestValue
    {
        public readonly object Sync = new();
        public long Count;
        public double DurationSeconds;
    }
}

public sealed record RequestMetric(
    string Method,
    int StatusCode,
    string TenantId,
    long Count,
    double DurationSeconds);

public static class OperationalMetricsEndpoint
{
    public static async Task<IResult> GetAsync(
        SportsPlatformDbContext database,
        OperationalMetrics runtime,
        HealthCheckService healthChecks,
        CancellationToken cancellationToken)
    {
        var output = new StringBuilder();
        Metric(output, "sportsplatform_api_up", "Whether this API instance can serve metrics.", 1);

        var health = await healthChecks.CheckHealthAsync(cancellationToken);
        Help(
            output,
            "sportsplatform_dependency_health",
            "Dependency health (1 healthy, 0 degraded or unhealthy).",
            "gauge");
        foreach (var entry in health.Entries.OrderBy(item => item.Key))
            Sample(
                output,
                "sportsplatform_dependency_health",
                entry.Value.Status == HealthStatus.Healthy ? 1 : 0,
                ("dependency", entry.Key));

        Help(output, "sportsplatform_http_requests_total", "HTTP requests completed by status and tenant.", "counter");
        Help(output, "sportsplatform_http_request_duration_seconds", "Accumulated HTTP request duration by status and tenant.", "summary");
        foreach (var request in runtime.Requests()
                     .OrderBy(item => item.TenantId)
                     .ThenBy(item => item.Method)
                     .ThenBy(item => item.StatusCode))
        {
            var labels = new[]
            {
                ("method", request.Method),
                ("status_code", request.StatusCode.ToString(CultureInfo.InvariantCulture)),
                ("tenant_id", request.TenantId)
            };
            Sample(output, "sportsplatform_http_requests_total", request.Count, labels);
            Sample(output, "sportsplatform_http_request_duration_seconds_sum", request.DurationSeconds, labels);
            Sample(output, "sportsplatform_http_request_duration_seconds_count", request.Count, labels);
        }

        Help(output, "sportsplatform_signalr_connections", "Current active SignalR connections by tenant context.", "gauge");
        var connections = runtime.Connections();
        Sample(output, "sportsplatform_signalr_connections", connections.Values.Sum(), ("tenant_id", "all"));
        foreach (var connection in connections.OrderBy(item => item.Key))
            Sample(output, "sportsplatform_signalr_connections", connection.Value, ("tenant_id", connection.Key));

        var databaseHealthy = health.Entries.TryGetValue("postgresql", out var databaseHealth)
            && databaseHealth.Status == HealthStatus.Healthy;
        Metric(
            output,
            "sportsplatform_operational_data_available",
            "Whether database-backed operational gauges are available.",
            databaseHealthy ? 1 : 0);
        if (!databaseHealthy)
        {
            Help(output, "sportsplatform_queue_backlog", "Durable work waiting or processing by queue and tenant.", "gauge");
            Help(output, "sportsplatform_failed_jobs", "Failed durable jobs by queue and tenant.", "gauge");
            Help(output, "sportsplatform_notification_backlog", "Pending durable notifications by tenant.", "gauge");
            Help(output, "sportsplatform_notification_failures", "Failed durable notifications by tenant.", "gauge");
            Help(output, "sportsplatform_tenant_usage", "Latest durable tenant usage counter value.", "gauge");
            Sample(output, "sportsplatform_queue_backlog", 0, ("queue", "reports"), ("tenant_id", "all"));
            Sample(output, "sportsplatform_failed_jobs", 0, ("queue", "reports"), ("tenant_id", "all"));
            Sample(output, "sportsplatform_notification_backlog", 0, ("tenant_id", "all"));
            Sample(output, "sportsplatform_notification_failures", 0, ("tenant_id", "all"));
            return Results.Text(output.ToString(), "text/plain; version=0.0.4; charset=utf-8");
        }

        var reportRows = await database.Reports.AsNoTracking()
            .Select(item => new { item.TenantId, item.Status })
            .ToArrayAsync(cancellationToken);
        var reportTenants = reportRows.Select(item => item.TenantId).Distinct().Order().ToArray();
        Help(output, "sportsplatform_queue_backlog", "Durable work waiting or processing by queue and tenant.", "gauge");
        Help(output, "sportsplatform_failed_jobs", "Failed durable jobs by queue and tenant.", "gauge");
        Sample(output, "sportsplatform_queue_backlog",
            reportRows.Count(item => item.Status is ReportStatus.Pending or ReportStatus.Processing),
            ("queue", "reports"), ("tenant_id", "all"));
        Sample(output, "sportsplatform_failed_jobs",
            reportRows.Count(item => item.Status == ReportStatus.Failed),
            ("queue", "reports"), ("tenant_id", "all"));
        foreach (var tenantId in reportTenants)
        {
            var tenantReports = reportRows.Where(item => item.TenantId == tenantId).ToArray();
            Sample(output, "sportsplatform_queue_backlog",
                tenantReports.Count(item => item.Status is ReportStatus.Pending or ReportStatus.Processing),
                ("queue", "reports"), ("tenant_id", tenantId.ToString()));
            Sample(output, "sportsplatform_failed_jobs",
                tenantReports.Count(item => item.Status == ReportStatus.Failed),
                ("queue", "reports"), ("tenant_id", tenantId.ToString()));
        }

        var notificationRows = await (
                from notification in database.Notifications.AsNoTracking()
                join user in database.Users.AsNoTracking() on notification.UserId equals user.Id
                select new NotificationMetricRow(user.TenantId, notification.Status))
            .ToArrayAsync(cancellationToken);
        Help(output, "sportsplatform_notification_backlog", "Pending durable notifications by tenant.", "gauge");
        Help(output, "sportsplatform_notification_failures", "Failed durable notifications by tenant.", "gauge");
        WriteNotificationMetrics(output, notificationRows, "all");
        foreach (var group in notificationRows
                     .Where(item => item.TenantId.HasValue)
                     .GroupBy(item => item.TenantId!.Value)
                     .OrderBy(item => item.Key))
            WriteNotificationMetrics(output, group, group.Key.ToString());

        var usageRows = await database.TenantUsageCounters.AsNoTracking().ToArrayAsync(cancellationToken);
        Help(output, "sportsplatform_tenant_usage", "Latest durable tenant usage counter value.", "gauge");
        foreach (var usage in usageRows
                     .GroupBy(item => new { item.TenantId, item.Metric })
                     .Select(group => group.OrderByDescending(item => item.PeriodStartUtc).First())
                     .OrderBy(item => item.TenantId)
                     .ThenBy(item => item.Metric))
            Sample(output, "sportsplatform_tenant_usage", usage.Value,
                ("tenant_id", usage.TenantId.ToString()),
                ("metric", usage.Metric));

        return Results.Text(output.ToString(), "text/plain; version=0.0.4; charset=utf-8");
    }

    private static void WriteNotificationMetrics(
        StringBuilder output,
        IEnumerable<NotificationMetricRow> rows,
        string tenantId)
    {
        var statuses = rows.Select(item => item.Status).ToArray();
        Sample(output, "sportsplatform_notification_backlog",
            statuses.Count(status => status == NotificationStatus.Pending),
            ("tenant_id", tenantId));
        Sample(output, "sportsplatform_notification_failures",
            statuses.Count(status => status == NotificationStatus.Failed),
            ("tenant_id", tenantId));
    }

    private static void Metric(
        StringBuilder output,
        string name,
        string help,
        double value,
        params (string Name, string Value)[] labels)
    {
        Help(output, name, help, "gauge");
        Sample(output, name, value, labels);
    }

    private static void Help(StringBuilder output, string name, string help, string type)
    {
        output.Append("# HELP ").Append(name).Append(' ').AppendLine(help);
        output.Append("# TYPE ").Append(name).Append(' ').AppendLine(type);
    }

    private static void Sample(
        StringBuilder output,
        string name,
        double value,
        params (string Name, string Value)[] labels)
    {
        output.Append(name);
        if (labels.Length > 0)
        {
            output.Append('{');
            for (var index = 0; index < labels.Length; index++)
            {
                if (index > 0) output.Append(',');
                output.Append(labels[index].Name).Append("=\"")
                    .Append(Escape(labels[index].Value)).Append('"');
            }
            output.Append('}');
        }
        output.Append(' ').AppendLine(value.ToString("0.################", CultureInfo.InvariantCulture));
    }

    private static string Escape(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
}

internal sealed record NotificationMetricRow(Guid? TenantId, NotificationStatus Status);

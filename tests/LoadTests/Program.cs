using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR.Client;
using Npgsql;

var mode = args.FirstOrDefault() ?? "run";
var outputPath = ValueAfter("--output") ?? Path.Combine("artifacts", "load-resilience-results.json");
var statePath = ValueAfter("--state") ?? Path.Combine("artifacts", "load-resilience-state.json");
var apiBase = Environment.GetEnvironmentVariable("LOAD_TEST_API_URL") ?? "http://localhost:8080";
var tenantSlug = Environment.GetEnvironmentVariable("LOAD_TEST_TENANT_SLUG") ?? "premier-league";
var email = Environment.GetEnvironmentVariable("LOAD_TEST_EMAIL") ?? "admin@premier.com";
var password = Environment.GetEnvironmentVariable("LOAD_TEST_PASSWORD") ?? "TenantAdmin123!";
var databasePassword = Environment.GetEnvironmentVariable("LOAD_TEST_DB_PASSWORD")
    ?? throw new InvalidOperationException("LOAD_TEST_DB_PASSWORD is required.");
var connectionString = new NpgsqlConnectionStringBuilder
{
    Host = Environment.GetEnvironmentVariable("LOAD_TEST_DB_HOST") ?? "localhost",
    Port = int.Parse(Environment.GetEnvironmentVariable("LOAD_TEST_DB_PORT") ?? "5434"),
    Database = Environment.GetEnvironmentVariable("LOAD_TEST_DB_NAME") ?? "sports_platform_staging",
    Username = Environment.GetEnvironmentVariable("LOAD_TEST_DB_USER") ?? "postgres",
    Password = databasePassword,
    Timeout = 5,
    CommandTimeout = 15
}.ConnectionString;

Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath))!);
using var client = new HttpClient { BaseAddress = new Uri(apiBase), Timeout = TimeSpan.FromSeconds(15) };

if (mode.Equals("verify", StringComparison.OrdinalIgnoreCase))
{
    var state = JsonSerializer.Deserialize<LoadState>(await File.ReadAllTextAsync(statePath), JsonOptions())
        ?? throw new InvalidOperationException("Load state could not be read.");
    var verification = await VerifyStateAsync(state);
    Console.WriteLine(JsonSerializer.Serialize(verification, JsonOptions()));
    return verification.Passed ? 0 : 1;
}

if (!mode.Equals("run", StringComparison.OrdinalIgnoreCase))
    throw new ArgumentException("Mode must be 'run' or 'verify'.");

var runId = $"load-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}";
var auth = await PostJsonAsync("/api/auth/login", new
{
    email,
    password,
    deviceId = runId,
    deviceName = "Docker load and resilience runner",
    deviceType = "load-test"
});
client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
    "Bearer", auth.GetProperty("accessToken").GetString());
var tenantId = auth.GetProperty("user").GetProperty("tenantId").GetGuid();
var userId = auth.GetProperty("user").GetProperty("id").GetGuid();

var home = await GetJsonAsync($"/api/public/tenants/{tenantSlug}/home", authenticated: false);
var publicContestId = FirstContestId(home, "liveContests")
    ?? FirstContestId(home, "upcomingContests")
    ?? FirstContestId(home, "recentResults", "contestId");
if (!publicContestId.HasValue)
    throw new InvalidOperationException("The selected tenant has no public contest for viewer/reconnect testing.");
var publicContestPath = $"/api/public/tenants/{tenantSlug}/contests/{publicContestId.Value}";

var viewer = await RunViewerLoadAsync(publicContestPath, requestCount: 200, concurrency: 20);
var signalR = await RunSignalRReconnectsAsync(publicContestId.Value, clients: 10, cycles: 2);

var participants = await GetJsonAsync($"/api/participants?tenantId={tenantId}");
var participantIds = participants.EnumerateArray().Select(item => item.GetProperty("id").GetGuid())
    .Distinct().Take(2).ToArray();
if (participantIds.Length < 2)
    throw new InvalidOperationException("The selected tenant needs at least two participants.");

var contest = await PostJsonAsync("/api/contests", new
{
    tenantId,
    sportId = Guid.Parse("00000000-0000-0000-0000-000000000100"),
    contestShape = "TeamVsTeam",
    name = $"Load resilience {runId}"
});
var contestId = contest.GetProperty("id").GetGuid();
foreach (var participantId in participantIds)
    await PostJsonAsync($"/api/contests/{contestId}/participants", new
    {
        participantId,
        participantType = "Team"
    });
var segment = await PostJsonAsync($"/api/contests/{contestId}/segments", new
{
    name = "Load period",
    segmentType = "Half",
    sortOrder = 1
});
var segmentId = segment.GetProperty("id").GetGuid();

var scoreBurst = await RunScoreBurstAsync(contestId, participantIds[0], segmentId, uniqueEvents: 40);
var duplicates = await RunDuplicateBurstAsync(contestId, participantIds[0], segmentId, submissions: 20);
var notificationQueue = await RunNotificationQueueAsync(userId, runId, count: 250);

var stateRecord = new LoadState(
    runId, tenantSlug, publicContestId.Value, contestId,
    ExpectedScoreRows: 41, NotificationTitlePrefix: runId, ExpectedNotifications: 250);
await File.WriteAllTextAsync(statePath, JsonSerializer.Serialize(stateRecord, JsonOptions()));
var stateVerification = await VerifyStateAsync(stateRecord);

var result = new LoadResult(
    DateTime.UtcNow,
    apiBase,
    viewer,
    signalR,
    scoreBurst,
    duplicates,
    notificationQueue,
    stateVerification,
    Passed: viewer.Passed && signalR.Passed && scoreBurst.Passed
        && duplicates.Passed && notificationQueue.Passed && stateVerification.Passed);
await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(result, JsonOptions()));
Console.WriteLine(JsonSerializer.Serialize(result, JsonOptions()));
return result.Passed ? 0 : 1;

string? ValueAfter(string key)
{
    var index = Array.IndexOf(args, key);
    return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
}

async Task<JsonElement> PostJsonAsync(string path, object body)
{
    using var response = await client.PostAsJsonAsync(path, body);
    var text = await response.Content.ReadAsStringAsync();
    if (!response.IsSuccessStatusCode)
        throw new InvalidOperationException($"POST {path} failed ({(int)response.StatusCode}): {text}");
    return JsonDocument.Parse(text).RootElement.Clone();
}

async Task<JsonElement> GetJsonAsync(string path, bool authenticated = true)
{
    using var request = new HttpRequestMessage(HttpMethod.Get, path);
    if (!authenticated) request.Headers.Authorization = null;
    using var response = await client.SendAsync(request);
    var text = await response.Content.ReadAsStringAsync();
    if (!response.IsSuccessStatusCode)
        throw new InvalidOperationException($"GET {path} failed ({(int)response.StatusCode}): {text}");
    return JsonDocument.Parse(text).RootElement.Clone();
}

async Task<PerformanceScenario> RunViewerLoadAsync(string path, int requestCount, int concurrency)
{
    using var anonymous = new HttpClient { BaseAddress = new Uri(apiBase), Timeout = TimeSpan.FromSeconds(10) };
    var timings = new long[requestCount];
    var statuses = new HttpStatusCode[requestCount];
    var total = Stopwatch.StartNew();
    await Parallel.ForEachAsync(Enumerable.Range(0, requestCount),
        new ParallelOptions { MaxDegreeOfParallelism = concurrency }, async (index, cancellationToken) =>
        {
            var timer = Stopwatch.StartNew();
            using var response = await anonymous.GetAsync(path, cancellationToken);
            timings[index] = timer.ElapsedMilliseconds;
            statuses[index] = response.StatusCode;
        });
    total.Stop();
    var success = statuses.Count(status => (int)status is >= 200 and < 300);
    var p95 = Percentile(timings, 0.95);
    var throughput = requestCount / total.Elapsed.TotalSeconds;
    return new PerformanceScenario("Concurrent contest viewers", requestCount, success, p95,
        throughput, success == requestCount && p95 <= 1500 && throughput >= 10,
        "Thresholds: 100% HTTP 2xx, p95 <= 1500 ms, >= 10 requests/second.");
}

async Task<PerformanceScenario> RunSignalRReconnectsAsync(Guid contestId, int clients, int cycles)
{
    var attempts = clients * cycles;
    var timings = new long[attempts];
    var success = 0;
    await Parallel.ForEachAsync(Enumerable.Range(0, attempts),
        new ParallelOptions { MaxDegreeOfParallelism = clients }, async (index, cancellationToken) =>
        {
            var snapshot = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            await using var connection = new HubConnectionBuilder()
                .WithUrl($"{apiBase}/hubs/contest")
                .WithAutomaticReconnect()
                .Build();
            connection.On<JsonElement>("ContestSnapshot", value =>
            {
                if (value.TryGetProperty("contestId", out var id) && id.GetGuid() == contestId)
                    snapshot.TrySetResult(true);
            });
            var timer = Stopwatch.StartNew();
            try
            {
                await connection.StartAsync(cancellationToken);
                await connection.InvokeAsync("JoinPublicContestGroup", tenantSlug, contestId, cancellationToken);
                await snapshot.Task.WaitAsync(TimeSpan.FromSeconds(8), cancellationToken);
                Interlocked.Increment(ref success);
            }
            finally
            {
                timings[index] = timer.ElapsedMilliseconds;
                await connection.StopAsync(CancellationToken.None);
            }
        });
    var p95 = Percentile(timings, 0.95);
    return new PerformanceScenario("SignalR reconnect and snapshot recovery", attempts, success, p95, 0,
        success == attempts && p95 <= 5000,
        "Thresholds: 100% reconnects recover a contest snapshot, p95 <= 5000 ms.");
}

async Task<PerformanceScenario> RunScoreBurstAsync(
    Guid contestId, Guid participantId, Guid segmentId, int uniqueEvents)
{
    var timings = new long[uniqueEvents];
    var success = 0;
    var total = Stopwatch.StartNew();
    await Parallel.ForEachAsync(Enumerable.Range(0, uniqueEvents),
        new ParallelOptions { MaxDegreeOfParallelism = 10 }, async (index, cancellationToken) =>
        {
            var eventId = Guid.NewGuid();
            var timer = Stopwatch.StartNew();
            using var response = await client.PostAsJsonAsync($"/api/livescoring/contests/{contestId}/events", new
            {
                eventId,
                participantId,
                metricName = "score",
                value = 1,
                segmentId,
                deviceTimestamp = DateTime.UtcNow,
                deviceId = $"{runId}-burst-{index}",
                sequenceNumber = 1,
                idempotencyKey = $"{runId}-burst-{index}"
            }, cancellationToken);
            timings[index] = timer.ElapsedMilliseconds;
            if (response.IsSuccessStatusCode) Interlocked.Increment(ref success);
        });
    total.Stop();
    var p95 = Percentile(timings, 0.95);
    var throughput = uniqueEvents / total.Elapsed.TotalSeconds;
    return new PerformanceScenario("Score update burst", uniqueEvents, success, p95, throughput,
        success == uniqueEvents && p95 <= 2000 && throughput >= 5,
        "Thresholds: 100% accepted, p95 <= 2000 ms, >= 5 updates/second.");
}

async Task<DuplicateScenario> RunDuplicateBurstAsync(
    Guid contestId, Guid participantId, Guid segmentId, int submissions)
{
    var eventId = Guid.NewGuid();
    var responses = new JsonElement?[submissions];
    var statuses = new HttpStatusCode[submissions];
    await Parallel.ForEachAsync(Enumerable.Range(0, submissions),
        new ParallelOptions { MaxDegreeOfParallelism = submissions }, async (index, cancellationToken) =>
        {
            using var response = await client.PostAsJsonAsync($"/api/livescoring/contests/{contestId}/events", new
            {
                eventId,
                participantId,
                metricName = "score",
                value = 1,
                segmentId,
                deviceTimestamp = DateTime.UtcNow,
                deviceId = $"{runId}-duplicate",
                sequenceNumber = 1,
                idempotencyKey = $"{runId}-duplicate"
            }, cancellationToken);
            statuses[index] = response.StatusCode;
            var text = await response.Content.ReadAsStringAsync(cancellationToken);
            if (response.IsSuccessStatusCode)
                responses[index] = JsonDocument.Parse(text).RootElement.Clone();
        });
    var accepted = statuses.Count(status => status == HttpStatusCode.OK);
    var replays = responses.Count(item => item.HasValue
        && item.Value.GetProperty("isReplay").GetBoolean());
    return new DuplicateScenario(submissions, accepted, replays,
        accepted == submissions && replays == submissions - 1,
        "Threshold: all responses are 200, exactly one insert and every remaining request is a replay.");
}

async Task<QueueScenario> RunNotificationQueueAsync(Guid targetUserId, string prefix, int count)
{
    await using var connection = new NpgsqlConnection(connectionString);
    await connection.OpenAsync();
    await using (var transaction = await connection.BeginTransactionAsync())
    {
        for (var index = 0; index < count; index++)
        {
            await using var command = new NpgsqlCommand(
                """
                INSERT INTO "Notifications"
                    ("Id", "UserId", "Type", "Channel", "Title", "Content", "Status", "RetryCount", "CreatedAt")
                VALUES (@id, @user, 0, 0, @title, 'Load queue verification', 0, 0, NOW())
                """, connection, transaction);
            command.Parameters.AddWithValue("id", Guid.NewGuid());
            command.Parameters.AddWithValue("user", targetUserId);
            command.Parameters.AddWithValue("title", $"{prefix}-{index:D4}");
            await command.ExecuteNonQueryAsync();
        }
        await transaction.CommitAsync();
    }
    var timer = Stopwatch.StartNew();
    var sent = 0;
    while (timer.Elapsed < TimeSpan.FromSeconds(15))
    {
        await using var command = new NpgsqlCommand(
            "SELECT COUNT(*) FROM \"Notifications\" WHERE \"Title\" LIKE @prefix AND \"Status\" = 1", connection);
        command.Parameters.AddWithValue("prefix", $"{prefix}%");
        sent = Convert.ToInt32(await command.ExecuteScalarAsync());
        if (sent == count) break;
        await Task.Delay(250);
    }
    return new QueueScenario(count, sent, timer.Elapsed.TotalSeconds,
        sent == count && timer.Elapsed <= TimeSpan.FromSeconds(15),
        "Threshold: 250 durable pending notifications transition to Sent within 15 seconds without loss.");
}

async Task<StateVerification> VerifyStateAsync(LoadState state)
{
    await using var connection = new NpgsqlConnection(connectionString);
    await connection.OpenAsync();
    await using var scoreCommand = new NpgsqlCommand(
        "SELECT COUNT(*) FROM \"LiveScores\" WHERE \"ContestId\" = @contest", connection);
    scoreCommand.Parameters.AddWithValue("contest", state.ContestId);
    var scores = Convert.ToInt32(await scoreCommand.ExecuteScalarAsync());
    await using var notificationCommand = new NpgsqlCommand(
        "SELECT COUNT(*) FROM \"Notifications\" WHERE \"Title\" LIKE @prefix", connection);
    notificationCommand.Parameters.AddWithValue("prefix", $"{state.NotificationTitlePrefix}%");
    var notifications = Convert.ToInt32(await notificationCommand.ExecuteScalarAsync());
    using var publicResponse = await client.GetAsync(
        $"/api/public/tenants/{state.TenantSlug}/contests/{state.PublicContestId}");
    var passed = scores == state.ExpectedScoreRows
        && notifications == state.ExpectedNotifications
        && publicResponse.IsSuccessStatusCode;
    return new StateVerification(scores, state.ExpectedScoreRows, notifications,
        state.ExpectedNotifications, (int)publicResponse.StatusCode, passed);
}

static long Percentile(long[] values, double percentile)
{
    var sorted = values.Order().ToArray();
    return sorted[Math.Clamp((int)Math.Ceiling(sorted.Length * percentile) - 1, 0, sorted.Length - 1)];
}

static Guid? FirstContestId(JsonElement root, string property, string idProperty = "id")
{
    if (!root.TryGetProperty(property, out var values) || values.ValueKind != JsonValueKind.Array)
        return null;
    var first = values.EnumerateArray().FirstOrDefault();
    return first.ValueKind == JsonValueKind.Undefined
        ? null
        : first.GetProperty(idProperty).GetGuid();
}

static JsonSerializerOptions JsonOptions() => new(JsonSerializerDefaults.Web) { WriteIndented = true };

public sealed record PerformanceScenario(
    string Name, int Attempts, int Successes, long P95Milliseconds,
    double ThroughputPerSecond, bool Passed, string Threshold);
public sealed record DuplicateScenario(
    int Attempts, int Accepted, int Replays, bool Passed, string Threshold);
public sealed record QueueScenario(
    int Enqueued, int Sent, double DrainSeconds, bool Passed, string Threshold);
public sealed record StateVerification(
    int ScoreRows, int ExpectedScoreRows, int Notifications,
    int ExpectedNotifications, int PublicContestStatus, bool Passed);
public sealed record LoadState(
    string RunId, string TenantSlug, Guid PublicContestId, Guid ContestId,
    int ExpectedScoreRows, string NotificationTitlePrefix, int ExpectedNotifications);
public sealed record LoadResult(
    DateTime CompletedAtUtc,
    string ApiBaseUrl,
    PerformanceScenario ConcurrentViewers,
    PerformanceScenario SignalRReconnects,
    PerformanceScenario ScoreBurst,
    DuplicateScenario DuplicateSubmissions,
    QueueScenario NotificationQueue,
    StateVerification StateVerification,
    bool Passed);

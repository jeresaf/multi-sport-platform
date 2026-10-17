using System.Collections;
using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Trace;
using Serilog.Core;
using Serilog.Events;

namespace SportsPlatform.IntegrationTests.Infrastructure;

/// <summary>
/// Collects the app's log events (a Serilog sink picked up from DI) and finished spans (in-memory exporter).
/// Request completion events and server spans are written as the request ends, possibly just after the client has
/// the response, so tests wait for the item they expect.
/// </summary>
public sealed class TelemetryCapture : ILogEventSink
{
    private static readonly TimeSpan _waitLimit = TimeSpan.FromSeconds(10);

    private readonly Lock _lock = new();
    private readonly List<LogEvent> _events = [];
    private readonly SpanCollection _spans;

    public TelemetryCapture() => _spans = new SpanCollection(_lock);

    public IReadOnlyList<LogEvent> Events
    {
        get
        {
            lock (_lock)
            {
                return [.. _events];
            }
        }
    }

    public IReadOnlyList<Activity> Spans
    {
        get
        {
            lock (_lock)
            {
                return [.. _spans];
            }
        }
    }

    public void Emit(LogEvent logEvent)
    {
        lock (_lock)
        {
            _events.Add(logEvent);
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _events.Clear();
            _spans.Clear();
        }
    }

    public void AddTo(IServiceCollection services)
    {
        services.AddSingleton<ILogEventSink>(this);
        services.ConfigureOpenTelemetryTracerProvider(tracing => tracing.AddInMemoryExporter(_spans));
    }

    public Task<LogEvent> WaitForEventAsync(Func<LogEvent, bool> predicate) => WaitForAsync(() => Events.FirstOrDefault(predicate), "log event");

    public Task<Activity> WaitForSpanAsync(Func<Activity, bool> predicate) => WaitForAsync(() => Spans.FirstOrDefault(predicate), "span");

    /// <summary>The completion event written by request logging for <paramref name="path"/>.</summary>
    public Task<LogEvent> WaitForRequestLogAsync(string path) => WaitForEventAsync(e => IsRequestCompletion(e, path));

    /// <summary>
    /// Request logging's one event per request. Every event logged during a request has <c>RequestPath</c>; only this
    /// one has <c>StatusCode</c>.
    /// </summary>
    public static bool IsRequestCompletion(LogEvent logEvent, string path) =>
        logEvent.Properties.ContainsKey("StatusCode")
        && logEvent.Properties.TryGetValue("RequestPath", out var value) && value is ScalarValue { Value: string p } && p == path;

    /// <summary>The ASP.NET Core server span for <paramref name="path"/>.</summary>
    public Task<Activity> WaitForServerSpanAsync(string path) =>
        WaitForSpanAsync(s => s.Kind == ActivityKind.Server && (string?)s.GetTagItem("url.path") == path);

    private static async Task<T> WaitForAsync<T>(Func<T?> find, string what)
        where T : class
    {
        var deadline = Stopwatch.StartNew();
        while (deadline.Elapsed < _waitLimit)
        {
            if (find() is { } found)
            {
                return found;
            }

            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        throw new TimeoutException($"No matching {what} within {_waitLimit.TotalSeconds} s.");
    }

    /// <summary>The exporter's target collection, guarded by the capture's lock.</summary>
    private sealed class SpanCollection(Lock gate) : ICollection<Activity>
    {
        private readonly List<Activity> _items = [];

        public int Count
        {
            get
            {
                lock (gate)
                {
                    return _items.Count;
                }
            }
        }

        public bool IsReadOnly => false;

        public void Add(Activity item)
        {
            lock (gate)
            {
                _items.Add(item);
            }
        }

        public void Clear()
        {
            lock (gate)
            {
                _items.Clear();
            }
        }

        public bool Contains(Activity item)
        {
            lock (gate)
            {
                return _items.Contains(item);
            }
        }

        public void CopyTo(Activity[] array, int arrayIndex)
        {
            lock (gate)
            {
                _items.CopyTo(array, arrayIndex);
            }
        }

        public bool Remove(Activity item)
        {
            lock (gate)
            {
                return _items.Remove(item);
            }
        }

        // Callers enumerate under the capture's lock (see Spans).
        public IEnumerator<Activity> GetEnumerator() => _items.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}

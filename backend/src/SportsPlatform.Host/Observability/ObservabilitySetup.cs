using Npgsql;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting;
using Serilog.Formatting.Compact;
using SportsPlatform.Host.Health;

namespace SportsPlatform.Host.Observability;

/// <summary>
/// Structured JSON logs (Serilog) and OpenTelemetry traces and metrics. Telemetry is exported over OTLP only when
/// <c>OTEL_EXPORTER_OTLP_ENDPOINT</c> is set (the Aspire dashboard locally; Azure Monitor gets its own adapter).
/// </summary>
public static class ObservabilitySetup
{
    public const string ServiceName = "sportsplatform-api";

    public const string OtlpEndpointSetting = "OTEL_EXPORTER_OTLP_ENDPOINT";

    /// <summary>One JSON object per line: <c>@t</c>, <c>@m</c>, <c>@l</c> (not Information), <c>@x</c> and properties.</summary>
    public static ITextFormatter ConsoleFormatter { get; } = new RenderedCompactJsonFormatter();

    public static WebApplicationBuilder AddObservability(this WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var configuration = builder.Configuration;
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddSingleton<ILogEventEnricher, RequestContextEnricher>();

        // Enrichers and sinks sit in a sub-logger, which gets each event only after Microsoft.Extensions.Logging has
        // added its scope properties, so RequestContextEnricher can drop the framework's RequestId. ReadFrom.Services
        // also picks up sinks registered in DI (tests capture events that way). The logger lives in DI only: the
        // static Log.Logger is left alone, so several hosts in one process (tests) don't share one.
        builder.Services.AddSerilog(
            (services, logger) => logger
                .ReadFrom.Configuration(configuration)
                .Enrich.FromLogContext()
                .WriteTo.Logger(output => output
                    .ReadFrom.Services(services)
                    .WriteTo.Console(ConsoleFormatter)),
            preserveStaticLogger: true);

        var exportOtlp = !string.IsNullOrWhiteSpace(configuration[OtlpEndpointSetting]);
        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(ServiceName))
            .WithTracing(tracing =>
            {
                // Probes would drown real traffic; a filtered request's database spans are dropped with it.
                tracing
                    .AddAspNetCoreInstrumentation(options => options.Filter = context => !HealthEndpoints.IsHealthRequest(context))
                    .AddHttpClientInstrumentation()
                    .AddNpgsql();
                if (exportOtlp)
                {
                    tracing.AddOtlpExporter();
                }
            })
            .WithMetrics(metrics =>
            {
                metrics
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddNpgsqlInstrumentation()
                    .AddMeter("System.Runtime");
                if (exportOtlp)
                {
                    metrics.AddOtlpExporter();
                }
            });

        return builder;
    }

    /// <summary>
    /// One completion event per request (method, path without the query string, status, elapsed). Put it before
    /// <c>UseApiConventions</c> so the event sees the final status, 500s from the exception handler included.
    /// </summary>
    public static IApplicationBuilder UseRequestLogging(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var logger = app.ApplicationServices.GetRequiredService<Serilog.ILogger>();
        return app.UseSerilogRequestLogging(options =>
        {
            options.Logger = logger; // the default is the static Log.Logger, which AddObservability leaves silent
            options.GetLevel = (context, _, exception) => RequestLogLevel(context, exception);
        });
    }

    /// <summary>Error for failures (5xx or an exception), Verbose for health probes, otherwise Information.</summary>
    public static LogEventLevel RequestLogLevel(HttpContext context, Exception? exception)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (exception is not null || context.Response.StatusCode >= StatusCodes.Status500InternalServerError)
        {
            return LogEventLevel.Error;
        }

        return HealthEndpoints.IsHealthRequest(context) ? LogEventLevel.Verbose : LogEventLevel.Information;
    }
}

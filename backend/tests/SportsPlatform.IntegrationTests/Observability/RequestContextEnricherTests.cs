using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using SportsPlatform.BuildingBlocks.Tenancy;
using SportsPlatform.Host.Observability;
using SportsPlatform.IntegrationTests.Infrastructure;

namespace SportsPlatform.IntegrationTests.Observability;

public sealed class RequestContextEnricherTests
{
    private static readonly Guid _tenantId = Guid.Parse("01a14a89-0000-7000-8000-00000000c0de");

    [Fact]
    public void A_tenant_context_that_logs_while_being_built_does_not_recurse()
    {
        var capture = new TelemetryCapture();
        var services = new ServiceCollection();
        services.AddScoped<ITenantContext, LoggingTenantContext>();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var accessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext { TraceIdentifier = "kmsa-req-0042", RequestServices = scope.ServiceProvider } };
        using var logger = CreateLogger(accessor, capture);
        LoggingTenantContext.Logger = logger;

        logger.Information("Fixture list opened");

        var outer = capture.Events.Single(e => e.MessageTemplate.Text == "Fixture list opened");
        outer.Properties[RequestContextEnricher.TenantId].ShouldBe(new ScalarValue(_tenantId));
        var inner = capture.Events.Single(e => e.MessageTemplate.Text == LoggingTenantContext.Message);
        inner.Properties[RequestContextEnricher.CorrelationId].ShouldBe(new ScalarValue("kmsa-req-0042"));
        inner.Properties.ShouldNotContainKey(RequestContextEnricher.TenantId);
    }

    [Fact]
    public void Events_after_the_request_scope_is_gone_keep_the_correlation_ID()
    {
        var capture = new TelemetryCapture();
        var context = new DefaultHttpContext { TraceIdentifier = "kmsa-req-0043" };
        context.RequestServices = null!; // what RequestServicesFeature leaves once the scope is disposed
        using var logger = CreateLogger(new HttpContextAccessor { HttpContext = context }, capture);

        logger.Information("Request finished");

        var logEvent = capture.Events.ShouldHaveSingleItem();
        logEvent.Properties[RequestContextEnricher.CorrelationId].ShouldBe(new ScalarValue("kmsa-req-0043"));
        logEvent.Properties.ShouldNotContainKey(RequestContextEnricher.ActorKind);
    }

    private static Logger CreateLogger(IHttpContextAccessor accessor, TelemetryCapture capture) =>
        new LoggerConfiguration().Enrich.With(new RequestContextEnricher(accessor)).WriteTo.Sink(capture).CreateLogger();

    private sealed class LoggingTenantContext : ITenantContext
    {
        public const string Message = "Resolving tenant";

        public LoggingTenantContext() => Logger?.Information(Message);

        public static Logger? Logger { get; set; }

        public Guid? TenantId => _tenantId;
    }
}

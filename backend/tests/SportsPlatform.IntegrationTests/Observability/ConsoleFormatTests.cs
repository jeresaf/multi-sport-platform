using System.Text.Json;
using Serilog.Events;
using Serilog.Parsing;
using SportsPlatform.Host.Observability;

namespace SportsPlatform.IntegrationTests.Observability;

public sealed class ConsoleFormatTests
{
    [Fact]
    public void Console_writes_one_JSON_object_per_event_with_its_properties()
    {
        var logEvent = new LogEvent(
            new DateTimeOffset(2026, 10, 17, 9, 0, 0, TimeSpan.Zero),
            LogEventLevel.Warning,
            exception: null,
            new MessageTemplateParser().Parse("Fixture {FixtureCode} moved"),
            [
                new LogEventProperty("FixtureCode", new ScalarValue("KMSA-PL-014")),
                new LogEventProperty(RequestContextEnricher.CorrelationId, new ScalarValue("kmsa-req-0001")),
            ]);
        using var output = new StringWriter();

        ObservabilitySetup.ConsoleFormatter.Format(logEvent, output);

        var line = output.ToString();
        line.TrimEnd('\n', '\r').ShouldNotContain("\n");
        using var json = JsonDocument.Parse(line);
        json.RootElement.GetProperty("@t").GetString().ShouldBe("2026-10-17T09:00:00.0000000Z");
        json.RootElement.GetProperty("@l").GetString().ShouldBe("Warning");
        json.RootElement.GetProperty("@m").GetString().ShouldBe("Fixture \"KMSA-PL-014\" moved");
        json.RootElement.GetProperty(RequestContextEnricher.CorrelationId).GetString().ShouldBe("kmsa-req-0001");
    }
}

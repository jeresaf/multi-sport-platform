using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SportsPlatform.BuildingBlocks.Persistence;
using SportsPlatform.BuildingBlocks.Persistence.Messaging;
using SportsPlatform.IntegrationTests.Infrastructure;

namespace SportsPlatform.IntegrationTests.Messaging;

/// <summary>A module whose outbox can't be processed backs off without holding up the others (#20 R1).</summary>
public sealed class OutboxModuleFailureTests : DatabaseTest
{
    public OutboxModuleFailureTests(PostgresFixture fixture)
        : base(fixture) => App.Consumers.Reset();

    [Fact]
    public async Task Failing_module_is_retried_with_backoff_while_the_others_keep_delivering()
    {
        var logger = new CountingLogger();
        var healthy = App.Services.GetServices<ModuleDatabase>().Single(d => d.Schema == MessagingTestDbContext.SchemaName);
        var broken = new ModuleDatabase("broken_test", typeof(UnregisteredContext), _ => throw new NotSupportedException());
        var processor = new OutboxProcessor(
            App.Services.GetRequiredService<IServiceScopeFactory>(),
            [broken, healthy],
            App.Services.GetServices<IntegrationEventSubscription>(),
            App.Clock,
            App.Services.GetRequiredService<IOptions<OutboxOptions>>(),
            logger);

        await CreateProbeAsync();
        (await processor.ProcessOnceAsync(Ct)).ShouldBe(1);
        logger.Errors.ShouldBe(1);

        // Within the first second the broken module is skipped; the healthy one still delivers.
        await CreateProbeAsync();
        (await processor.ProcessOnceAsync(Ct)).ShouldBe(1);
        logger.Errors.ShouldBe(1);

        App.Clock.Advance(TimeSpan.FromSeconds(1));
        await processor.ProcessOnceAsync(Ct);
        logger.Errors.ShouldBe(2); // now skipped for 2 s

        App.Clock.Advance(TimeSpan.FromSeconds(1));
        await processor.ProcessOnceAsync(Ct);
        logger.Errors.ShouldBe(2);

        App.Clock.Advance(TimeSpan.FromSeconds(1));
        await processor.ProcessOnceAsync(Ct);
        logger.Errors.ShouldBe(3);
        App.Consumers.For(RecordingConsumer.Name).Count.ShouldBe(2);
    }

    private async Task CreateProbeAsync()
    {
        await using var scope = App.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<MessagingTestDbContext>();
        context.Probes.Add(new MessagingProbe("Ggaba Lakeside Rugby Ground"));
        await context.SaveChangesAsync(Ct);
    }

    private sealed class UnregisteredContext;

    private sealed class CountingLogger : ILogger<OutboxProcessor>
    {
        private int _errors;

        public int Errors => Volatile.Read(ref _errors);

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Error)
            {
                Interlocked.Increment(ref _errors);
            }
        }
    }
}

using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using SportsPlatform.IntegrationTests.Infrastructure;

namespace SportsPlatform.IntegrationTests.Email;

/// <summary>A Mailpit container (docker compose's image tag) for the SMTP adapter's tests; its API reads what arrived.</summary>
public sealed class MailpitFixture : IAsyncLifetime
{
    private const ushort _smtpPort = 1025;
    private const ushort _apiPort = 8025;

    private IContainer? _container;

    public string Host => Container.Hostname;

    public int Smtp => Container.GetMappedPublicPort(_smtpPort);

    /// <summary>Mailpit's HTTP API (<c>/api/v1/…</c>).</summary>
    public HttpClient Api { get; private set; } = null!;

    private IContainer Container => _container ?? throw new InvalidOperationException("Mailpit is not started.");

    public async ValueTask InitializeAsync()
    {
        _container = new ContainerBuilder(ComposeFile.Image("axllent/mailpit"))
            .WithPortBinding(_smtpPort, assignRandomHostPort: true)
            .WithPortBinding(_apiPort, assignRandomHostPort: true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(request => request.ForPort(_apiPort).ForPath("/readyz")))
            .Build();
        await _container.StartAsync();
        Api = new HttpClient { BaseAddress = new Uri($"http://{Host}:{Container.GetMappedPublicPort(_apiPort)}/") };
    }

    /// <summary>Deletes every message, so each test starts with an empty mailbox.</summary>
    public async Task ClearAsync(CancellationToken cancellationToken)
    {
        using var response = await Api.DeleteAsync(new Uri("api/v1/messages", UriKind.Relative), cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async ValueTask DisposeAsync()
    {
        Api?.Dispose();
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }
}

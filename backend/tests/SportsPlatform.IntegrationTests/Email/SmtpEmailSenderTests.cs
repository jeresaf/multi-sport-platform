using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SportsPlatform.Modules.Platform.Contracts.Email;
using SportsPlatform.Modules.Platform.Infrastructure.Email;

namespace SportsPlatform.IntegrationTests.Email;

/// <summary>The SMTP adapter against a real Mailpit: what arrives is what was sent, and nothing personal is logged.</summary>
public sealed class SmtpEmailSenderTests(MailpitFixture mailpit) : IClassFixture<MailpitFixture>, IAsyncLifetime
{
    private static readonly EmailMessage _invitation = new(
        To: "grace.nakato@kmsa.example",
        ToName: "Grace Nakato",
        Subject: "You're invited to Kampala Metro Sports Association",
        TextBody: "Daniel Kato invited you to join Kampala Metro Sports Association as Tenant owner.",
        HtmlBody: "<p>Daniel Kato invited you to join <strong>Kampala Metro Sports Association</strong> as Tenant owner.</p>");

    private readonly ListLogger<SmtpEmailSender> _logger = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => await mailpit.ClearAsync(Ct);

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Sends_the_text_and_HTML_parts_from_the_configured_sender()
    {
        await Sender(SmtpSecurity.None).SendAsync(_invitation, Ct);

        var summary = (await MessagesAsync()).ShouldHaveSingleItem();
        var message = await MessageAsync(summary.GetProperty("ID").GetString()!);
        message.GetProperty("From").GetProperty("Address").GetString().ShouldBe("no-reply@sportsplatform.localhost");
        message.GetProperty("From").GetProperty("Name").GetString().ShouldBe("SportsPlatform");
        var to = message.GetProperty("To").EnumerateArray().ShouldHaveSingleItem();
        to.GetProperty("Address").GetString().ShouldBe(_invitation.To);
        to.GetProperty("Name").GetString().ShouldBe(_invitation.ToName);
        message.GetProperty("Subject").GetString().ShouldBe(_invitation.Subject);
        message.GetProperty("Text").GetString()!.Trim().ShouldBe(_invitation.TextBody);
        message.GetProperty("HTML").GetString()!.Trim().ShouldBe(_invitation.HtmlBody);
    }

    [Fact]
    public async Task Logs_the_message_ID_but_never_the_recipient()
    {
        await Sender(SmtpSecurity.None).SendAsync(_invitation, Ct);

        var summary = (await MessagesAsync()).ShouldHaveSingleItem();
        var entry = _logger.Entries.ShouldHaveSingleItem();
        entry.ShouldContain(summary.GetProperty("MessageID").GetString()!);
        entry.ShouldNotContain("grace.nakato", Case.Insensitive);
        entry.ShouldNotContain("Grace Nakato", Case.Insensitive);
    }

    [Fact]
    public async Task Refuses_a_server_without_STARTTLS_when_TLS_is_required()
    {
        // Mailpit's SMTP port offers no TLS here: the adapter must not fall back to plain text.
        await Should.ThrowAsync<NotSupportedException>(() => Sender(SmtpSecurity.StartTls).SendAsync(_invitation, Ct));

        (await MessagesAsync()).ShouldBeEmpty();
        _logger.Entries.ShouldBeEmpty();
    }

    private SmtpEmailSender Sender(SmtpSecurity security) => new(
        Options.Create(new EmailOptions
        {
            From = "no-reply@sportsplatform.localhost",
            Smtp = new SmtpOptions { Host = mailpit.Host, Port = mailpit.Smtp, Security = security, TimeoutSeconds = 10 },
        }),
        _logger);

    private async Task<List<JsonElement>> MessagesAsync()
    {
        var list = await mailpit.Api.GetFromJsonAsync<JsonElement>("api/v1/messages", Ct);
        return [.. list.GetProperty("messages").EnumerateArray()];
    }

    private async Task<JsonElement> MessageAsync(string id) =>
        await mailpit.Api.GetFromJsonAsync<JsonElement>($"api/v1/message/{id}", Ct);

    private sealed class ListLogger<T> : ILogger<T>
    {
        private readonly Lock _lock = new();
        private readonly List<string> _entries = [];

        public IReadOnlyList<string> Entries
        {
            get
            {
                lock (_lock)
                {
                    return [.. _entries];
                }
            }
        }

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        // The rendered message and every structured property, so a value can't hide in either.
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var properties = state is IEnumerable<KeyValuePair<string, object?>> pairs
                ? string.Join(' ', pairs.Select(pair => $"{pair.Key}={pair.Value}"))
                : "";
            lock (_lock)
            {
                _entries.Add($"{formatter(state, exception)} {properties}");
            }
        }
    }
}

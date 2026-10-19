using Microsoft.Extensions.DependencyInjection;
using SportsPlatform.IntegrationTests.Infrastructure;
using SportsPlatform.Modules.Platform.Contracts.Email;

namespace SportsPlatform.IntegrationTests.Email;

/// <summary>Consumers under test send through <see cref="TestApp.Emails"/>, which each test starts empty.</summary>
public sealed class RecordingEmailSenderTests(PostgresFixture fixture) : DatabaseTest(fixture)
{
    [Fact]
    public async Task TestApp_records_instead_of_sending()
    {
        var sender = App.Services.GetRequiredService<IEmailSender>();
        var message = new EmailMessage("esther.nabirye@kmsa.example", "Esther Nabirye", "Reset your password", "Use the link to choose a new password.");

        await sender.SendAsync(message, Ct);

        sender.ShouldBeSameAs(App.Emails);
        App.Emails.Sent.ShouldHaveSingleItem().ShouldBe(message);
    }
}

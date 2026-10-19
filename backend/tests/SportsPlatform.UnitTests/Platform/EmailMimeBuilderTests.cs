using MimeKit;
using SportsPlatform.Modules.Platform.Contracts.Email;
using SportsPlatform.Modules.Platform.Infrastructure.Email;

namespace SportsPlatform.UnitTests.Platform;

public sealed class EmailMimeBuilderTests
{
    private static readonly EmailOptions _options = new() { From = "no-reply@sportsplatform.localhost", FromName = "SportsPlatform" };

    [Fact]
    public void Addresses_the_recipient_from_the_configured_sender()
    {
        using var mime = EmailMimeBuilder.Build(
            new EmailMessage("brian.ssemakula@kmsa.example", "Brian Ssemakula", "Your KMSA invitation", "Welcome."), _options);

        var from = mime.From.Mailboxes.ShouldHaveSingleItem();
        from.Address.ShouldBe("no-reply@sportsplatform.localhost");
        from.Name.ShouldBe("SportsPlatform");
        var to = mime.To.Mailboxes.ShouldHaveSingleItem();
        to.Address.ShouldBe("brian.ssemakula@kmsa.example");
        to.Name.ShouldBe("Brian Ssemakula");
        mime.Subject.ShouldBe("Your KMSA invitation");
        mime.MessageId.ShouldEndWith("@sportsplatform.localhost");
    }

    [Fact]
    public void Sends_plain_text_alone_without_HTML()
    {
        using var mime = EmailMimeBuilder.Build(
            new EmailMessage("brian.ssemakula@kmsa.example", null, "Your KMSA invitation", "Welcome."), _options);

        var text = mime.Body.ShouldBeOfType<TextPart>();
        text.IsPlain.ShouldBeTrue();
        text.Text.ShouldBe("Welcome.");
        mime.To.Mailboxes.ShouldHaveSingleItem().Name.ShouldBeNullOrEmpty();
    }

    [Fact]
    public void Offers_text_and_HTML_as_alternatives()
    {
        using var mime = EmailMimeBuilder.Build(
            new EmailMessage("brian.ssemakula@kmsa.example", null, "Your KMSA invitation", "Welcome.", "<p>Welcome.</p>"), _options);

        var alternative = mime.Body.ShouldBeOfType<MultipartAlternative>();
        alternative.TextBody.ShouldBe("Welcome.");
        alternative.HtmlBody.ShouldBe("<p>Welcome.</p>");
    }
}

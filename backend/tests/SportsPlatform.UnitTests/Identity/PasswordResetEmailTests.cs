using SportsPlatform.Modules.Identity.Features.Account.ForgotPassword;
using SportsPlatform.Modules.Identity.Infrastructure.PasswordReset;

namespace SportsPlatform.UnitTests.Identity;

public sealed class PasswordResetEmailTests
{
    private const string _token = "0192a3b4c5d67e8f9a0b1c2d3e4f5a6b.b2xkLXRva2VuLWZvci1hLXRlc3Q";

    [Theory]
    [InlineData("https://admin.kmsa.example", "https://admin.kmsa.example/reset-password?token=")]
    [InlineData("https://sportsplatform.example/admin/", "https://sportsplatform.example/admin/reset-password?token=")]
    public void The_link_points_at_the_admin_web_reset_page(string adminAppUrl, string expectedStart) =>
        PasswordResetEmail.Link(adminAppUrl, _token).AbsoluteUri.ShouldBe(expectedStart + _token);

    [Fact]
    public void Text_and_html_carry_the_link_and_say_how_long_it_works()
    {
        var link = PasswordResetEmail.Link("https://admin.kmsa.example", _token);

        var message = PasswordResetEmail.Create("grace.nakato@kmsa.example", link);

        message.To.ShouldBe("grace.nakato@kmsa.example");
        message.Subject.ShouldBe("Reset your password");
        message.TextBody.ShouldContain(link.AbsoluteUri);
        message.TextBody.ShouldContain("within 1 hour");
        message.HtmlBody.ShouldNotBeNull().ShouldContain($"href=\"{link.AbsoluteUri}\"");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-token")]
    [InlineData("0192a3b4c5d67e8f9a0b1c2d3e4f5a6b")]
    [InlineData("0192a3b4c5d67e8f9a0b1c2d3e4f5a6b.")]
    [InlineData("0192a3b4-c5d6-7e8f-9a0b-1c2d3e4f5a6b.secret")]
    [InlineData("0192a3b4c5d67e8f9a0b1c2d3e4f5a6b.secret.extra")]
    public void A_malformed_token_names_no_user(string? token) =>
        PasswordResetTokenProvider.TryReadUserId(token).ShouldBeNull();

    [Fact]
    public void A_token_names_its_user() =>
        PasswordResetTokenProvider.TryReadUserId(_token).ShouldBe(Guid.Parse("0192a3b4-c5d6-7e8f-9a0b-1c2d3e4f5a6b"));
}

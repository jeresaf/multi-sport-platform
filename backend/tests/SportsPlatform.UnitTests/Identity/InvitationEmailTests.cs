using SportsPlatform.Modules.Identity.Features.Invitations.SendInvitationEmail;

namespace SportsPlatform.UnitTests.Identity;

public sealed class InvitationEmailTests
{
    private const string _token = "b2xkLXRva2VuLWZvci1hLXRlc3QtaW52aXRhdGlvbi0x";

    [Theory]
    [InlineData("https://admin.kmsa.example", "https://admin.kmsa.example/accept-invitation?token=")]
    [InlineData("https://sportsplatform.example/admin/", "https://sportsplatform.example/admin/accept-invitation?token=")]
    public void The_link_points_at_the_admin_web_acceptance_page(string adminAppUrl, string expectedStart) =>
        InvitationEmail.Link(adminAppUrl, _token).AbsoluteUri.ShouldBe(expectedStart + _token);

    [Fact]
    public void Text_and_html_name_the_tenant_carry_the_link_and_say_how_long_it_works()
    {
        var link = InvitationEmail.Link("https://admin.kmsa.example", _token);

        var message = InvitationEmail.Create("brian.ssemakula@kmsa.example", "Kampala Metro <Sports> Association", link);

        message.To.ShouldBe("brian.ssemakula@kmsa.example");
        message.Subject.ShouldBe("You're invited to join Kampala Metro <Sports> Association");
        message.TextBody.ShouldContain("join Kampala Metro <Sports> Association.");
        message.TextBody.ShouldContain(link.AbsoluteUri);
        message.TextBody.ShouldContain("within 7 days");
        message.HtmlBody.ShouldNotBeNull().ShouldSatisfyAllConditions(
            html => html.ShouldContain($"href=\"{link.AbsoluteUri}\""),
            html => html.ShouldContain("Kampala Metro &lt;Sports&gt; Association"),
            html => html.ShouldNotContain("<Sports>"));
    }
}

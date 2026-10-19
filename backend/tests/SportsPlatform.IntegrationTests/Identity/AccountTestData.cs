using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Persistence.Messaging;
using SportsPlatform.Modules.Identity.Domain.PlatformRoles;
using SportsPlatform.Modules.Identity.Infrastructure;
using SportsPlatform.Modules.Identity.Infrastructure.Users;
using SportsPlatform.Modules.Platform.Contracts.Email;

namespace SportsPlatform.IntegrationTests.Identity;

/// <summary>Users and sign-in for account tests. Passwords come from <c>TestPasswords</c>, never written down.</summary>
internal static partial class AccountTestData
{
    public const string AccountPath = "/api/account/v1";
    public static readonly Uri LoginPath = new($"{AccountPath}/login", UriKind.Relative);
    public static readonly Uri LogoutPath = new($"{AccountPath}/logout", UriKind.Relative);
    public static readonly Uri MePath = new($"{AccountPath}/me", UriKind.Relative);
    public static readonly Uri ForgotPasswordPath = new($"{AccountPath}/password/forgot", UriKind.Relative);
    public static readonly Uri ResetPasswordPath = new($"{AccountPath}/password/reset", UriKind.Relative);
    public static readonly Uri ChangePasswordPath = new($"{AccountPath}/password/change", UriKind.Relative);

    /// <summary>A user saved as the system actor, as the CLI or an invitation would create one.</summary>
    public static async Task<Guid> CreateUserAsync(
        IServiceProvider services, string email, string password, CancellationToken cancellationToken,
        bool emailConfirmed = true, string? platformRole = null)
    {
        await using var scope = services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var user = User.Create(email, emailConfirmed);
        (await users.CreateAsync(user, password)).Succeeded.ShouldBeTrue();
        var context = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        if (platformRole is not null)
        {
            context.Add(PlatformRoleAssignment.Grant(user.Id, platformRole, SystemActor.Instance, DateTimeOffset.UtcNow));
        }

        (await context.SaveChangesResultAsync(cancellationToken)).IsSuccess.ShouldBeTrue();
        return user.Id;
    }

    public static Task<HttpResponseMessage> LoginAsync(this HttpClient client, string email, string password, CancellationToken cancellationToken) =>
        client.PostAsJsonAsync(LoginPath, new { email, password }, cancellationToken);

    public static Task<HttpResponseMessage> ForgotPasswordAsync(this HttpClient client, string email, CancellationToken cancellationToken) =>
        client.PostAsJsonAsync(ForgotPasswordPath, new { email }, cancellationToken);

    public static Task<HttpResponseMessage> ResetPasswordAsync(this HttpClient client, string token, string newPassword, CancellationToken cancellationToken) =>
        client.PostAsJsonAsync(ResetPasswordPath, new { token, newPassword }, cancellationToken);

    public static Task<HttpResponseMessage> ChangePasswordAsync(
        this HttpClient client, string currentPassword, string newPassword, CancellationToken cancellationToken) =>
        client.PostAsJsonAsync(ChangePasswordPath, new { currentPassword, newPassword }, cancellationToken);

    /// <summary>The token in a reset email's link (the same in its text and HTML).</summary>
    public static string ResetToken(EmailMessage email)
    {
        ArgumentNullException.ThrowIfNull(email);
        var match = ResetLink().Match(email.TextBody);
        match.Success.ShouldBeTrue(email.TextBody);
        return Uri.UnescapeDataString(match.Groups["token"].Value);
    }

    /// <summary>Delivers the outbox (twice: audit entries are messages a consumer appends).</summary>
    public static async Task DeliverOutboxAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var processor = services.GetRequiredService<OutboxProcessor>();
        await processor.ProcessOnceAsync(cancellationToken);
        await processor.ProcessOnceAsync(cancellationToken);
    }

    [GeneratedRegex(@"/reset-password\?token=(?<token>[^\s&]+)")]
    private static partial Regex ResetLink();
}

using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Persistence.Messaging;
using SportsPlatform.Modules.Identity.Domain.PlatformRoles;
using SportsPlatform.Modules.Identity.Infrastructure;
using SportsPlatform.Modules.Identity.Infrastructure.Users;

namespace SportsPlatform.IntegrationTests.Identity;

/// <summary>Users and sign-in for account tests. Passwords come from <c>TestPasswords</c>, never written down.</summary>
internal static class AccountTestData
{
    public const string AccountPath = "/api/account/v1";
    public static readonly Uri LoginPath = new($"{AccountPath}/login", UriKind.Relative);
    public static readonly Uri LogoutPath = new($"{AccountPath}/logout", UriKind.Relative);
    public static readonly Uri MePath = new($"{AccountPath}/me", UriKind.Relative);

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

    /// <summary>Delivers the outbox (twice: audit entries are messages a consumer appends).</summary>
    public static async Task DeliverOutboxAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var processor = services.GetRequiredService<OutboxProcessor>();
        await processor.ProcessOnceAsync(cancellationToken);
        await processor.ProcessOnceAsync(cancellationToken);
    }
}

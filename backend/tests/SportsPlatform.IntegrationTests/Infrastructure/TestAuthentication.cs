using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SportsPlatform.BuildingBlocks.Tenancy;
using SportsPlatform.Host.Actors;

namespace SportsPlatform.IntegrationTests.Infrastructure;

/// <summary>
/// The test authentication scheme (S02 D5): a request with <c>X-Test-User</c> is that user, a member of the tenants in
/// <c>X-Test-Tenants</c> (comma-separated). Registered only by the test factories; the Host has no scheme until S03.
/// </summary>
public static class TestAuthentication
{
    public const string Scheme = "Test";
    public const string UserHeader = "X-Test-User";
    public const string TenantsHeader = "X-Test-Tenants";
    public const string TenantClaim = "test:tenant";

    public static IServiceCollection AddTestAuthentication(this IServiceCollection services)
    {
        services.AddAuthentication(Scheme).AddScheme<AuthenticationSchemeOptions, Handler>(Scheme, configureOptions: null);
        services.AddHttpContextAccessor();
        services.RemoveAll<ITenantMembershipChecker>();
        services.AddSingleton<ITenantMembershipChecker, ClaimsMembershipChecker>();
        return services;
    }

    /// <summary>Requests from this client are the user's, a member of <paramref name="memberOf"/>.</summary>
    public static HttpClient AsUser(this HttpClient client, Guid userId, params Guid[] memberOf)
    {
        ArgumentNullException.ThrowIfNull(client);
        client.DefaultRequestHeaders.Add(UserHeader, userId.ToString());
        if (memberOf.Length > 0)
        {
            client.DefaultRequestHeaders.Add(TenantsHeader, string.Join(',', memberOf));
        }

        return client;
    }

    private sealed class Handler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (Request.Headers[UserHeader] is not [{ } user])
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var claims = new List<Claim> { new(CurrentActor.UserIdClaim, user) };
            claims.AddRange(Request.Headers[TenantsHeader].ToString()
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(tenant => new Claim(TenantClaim, tenant)));
            var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, TestAuthentication.Scheme));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, TestAuthentication.Scheme)));
        }
    }

    private sealed class ClaimsMembershipChecker(IHttpContextAccessor accessor) : ITenantMembershipChecker
    {
        public ValueTask<bool> IsMemberAsync(Guid tenantId, CancellationToken cancellationToken) => ValueTask.FromResult(
            accessor.HttpContext?.User.FindAll(TenantClaim).Any(claim => claim.Value == tenantId.ToString()) == true);
    }
}

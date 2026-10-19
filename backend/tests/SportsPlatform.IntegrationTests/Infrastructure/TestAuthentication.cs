using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SportsPlatform.BuildingBlocks.Tenancy;
using SportsPlatform.Host.Actors;
using SportsPlatform.Host.Authorization;

namespace SportsPlatform.IntegrationTests.Infrastructure;

/// <summary>
/// The test authentication scheme (S02 D5): a request with <c>X-Test-User</c> is that user, a member of the tenants in
/// <c>X-Test-Tenants</c> (comma-separated). Registered only by the test factories, as the default scheme: a request
/// without the header is authenticated by the Host's session cookie (S03 D5), so account tests sign in for real.
/// </summary>
public static class TestAuthentication
{
    public const string Scheme = "TestOrSession";
    public const string HeaderScheme = "Test";
    public const string UserHeader = "X-Test-User";
    public const string TenantsHeader = "X-Test-Tenants";
    public const string TenantClaim = "test:tenant";

    public static IServiceCollection AddTestAuthentication(this IServiceCollection services)
    {
        services.AddAuthentication(Scheme)
            .AddScheme<AuthenticationSchemeOptions, Handler>(HeaderScheme, configureOptions: null)
            .AddPolicyScheme(Scheme, "X-Test-User, else the session cookie", options => options.ForwardDefaultSelector = context =>
                context.Request.Headers.ContainsKey(UserHeader) ? HeaderScheme : IdentityConstants.ApplicationScheme);
        services.AddHttpContextAccessor();
        services.RemoveAll<ITenantMembershipChecker>();
        services.AddSingleton<ITenantMembershipChecker, ClaimsMembershipChecker>();
        return services;
    }

    /// <summary>Requests from this client carry no <c>X-CSRF</c> header (S03 D7); test clients send it by default.</summary>
    public static HttpClient WithoutCsrfHeader(this HttpClient client)
    {
        ArgumentNullException.ThrowIfNull(client);
        client.DefaultRequestHeaders.Remove(CsrfProtection.HeaderName);
        return client;
    }

    /// <summary>Every test client sends <c>X-CSRF: 1</c>, as the web client does.</summary>
    public static void ConfigureTestClient(HttpClient client)
    {
        ArgumentNullException.ThrowIfNull(client);
        client.DefaultRequestHeaders.Add(CsrfProtection.HeaderName, CsrfProtection.HeaderValue);
    }

    /// <summary>
    /// The factory's clients use https, so the <c>Secure</c> session cookie is sent back (the factory sets the base
    /// address after <c>ConfigureClient</c>, so it is set here).
    /// </summary>
    public static void UseHttps(WebApplicationFactoryClientOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.BaseAddress = new Uri("https://localhost/");
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
            var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, HeaderScheme));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, HeaderScheme)));
        }
    }

    private sealed class ClaimsMembershipChecker(IHttpContextAccessor accessor) : ITenantMembershipChecker
    {
        public ValueTask<bool> IsMemberAsync(Guid tenantId, CancellationToken cancellationToken) => ValueTask.FromResult(
            accessor.HttpContext?.User.FindAll(TenantClaim).Any(claim => claim.Value == tenantId.ToString()) == true);
    }
}

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace SportsPlatform.Modules.Identity.Infrastructure.Sessions;

/// <summary>
/// The web session (S03 D5): ASP.NET Core Identity's application cookie, the API's only authentication scheme.
/// HttpOnly, <c>SameSite=Strict</c>, path <c>/</c>; <c>__Host-msp_session</c> and <c>Secure</c> outside Development,
/// where the site is http. 12 hours sliding, 7 days absolute from sign-in; the security stamp is checked every 5
/// minutes, so a password change or reset signs out the user's other sessions.
/// </summary>
public static class SessionCookie
{
    public static readonly string Scheme = IdentityConstants.ApplicationScheme;
    public const string Name = "__Host-msp_session";
    public const string DevelopmentName = "msp_session";

    /// <summary>When the session started, in the ticket's properties: sliding renewals and refreshes keep it.</summary>
    public const string IssuedAtItem = "issued_at";

    public static readonly TimeSpan IdleTimeout = TimeSpan.FromHours(12);
    public static readonly TimeSpan AbsoluteLifetime = TimeSpan.FromDays(7);
    public static readonly TimeSpan SecurityStampInterval = TimeSpan.FromMinutes(5);

    /// <summary>The properties of a new session started at <paramref name="now"/>.</summary>
    public static AuthenticationProperties NewSession(DateTimeOffset now) => new()
    {
        Items = { [IssuedAtItem] = now.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture) },
    };

    public static IServiceCollection AddSessionCookie(this IServiceCollection services)
    {
        services.AddAuthentication(Scheme).AddCookie(Scheme);
        services.AddSingleton<IConfigureOptions<CookieAuthenticationOptions>, ConfigureCookie>();
        services.Configure<SecurityStampValidatorOptions>(options => options.ValidationInterval = SecurityStampInterval);
        return services;
    }

    /// <summary>Past the absolute lifetime, or without a start: signed out, then the security stamp check.</summary>
    private static async Task ValidatePrincipalAsync(CookieValidatePrincipalContext context)
    {
        var now = context.HttpContext.RequestServices.GetRequiredService<TimeProvider>().GetUtcNow();
        if (!context.Properties.Items.TryGetValue(IssuedAtItem, out var value)
            || !long.TryParse(value, System.Globalization.CultureInfo.InvariantCulture, out var seconds)
            || now - DateTimeOffset.FromUnixTimeSeconds(seconds) >= AbsoluteLifetime)
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(Scheme);
            return;
        }

        await SecurityStampValidator.ValidatePrincipalAsync(context);
    }

    private sealed class ConfigureCookie(IHostEnvironment environment) : IConfigureNamedOptions<CookieAuthenticationOptions>
    {
        public void Configure(CookieAuthenticationOptions options) => Configure(Options.DefaultName, options);

        public void Configure(string? name, CookieAuthenticationOptions options)
        {
            if (name != Scheme)
            {
                return;
            }

            var development = environment.IsDevelopment();
            options.Cookie.Name = development ? DevelopmentName : Name;
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = development ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.Cookie.Path = "/";
            options.Cookie.IsEssential = true;
            options.ExpireTimeSpan = IdleTimeout;
            options.SlidingExpiration = true;

            // An API: the pipeline answers 401 and 403 with Problem Details; the cookie handler never redirects.
            options.Events.OnRedirectToLogin = static context => SetStatus(context.Response, StatusCodes.Status401Unauthorized);
            options.Events.OnRedirectToAccessDenied = static context => SetStatus(context.Response, StatusCodes.Status403Forbidden);
            options.Events.OnRedirectToLogout = static _ => Task.CompletedTask;
            options.Events.OnRedirectToReturnUrl = static _ => Task.CompletedTask;
            options.Events.OnValidatePrincipal = ValidatePrincipalAsync;
        }

        private static Task SetStatus(HttpResponse response, int status)
        {
            response.StatusCode = status;
            return Task.CompletedTask;
        }
    }
}

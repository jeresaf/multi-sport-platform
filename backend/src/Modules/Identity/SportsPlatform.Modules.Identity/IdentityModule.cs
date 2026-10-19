using FluentValidation;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SportsPlatform.BuildingBlocks.Actions;
using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Modules;
using SportsPlatform.BuildingBlocks.Persistence;
using SportsPlatform.Modules.Identity.Features.Account;
using SportsPlatform.Modules.Identity.Features.Account.GetMe;
using SportsPlatform.Modules.Identity.Features.Account.Login;
using SportsPlatform.Modules.Identity.Features.Account.Logout;
using SportsPlatform.Modules.Identity.Features.Authorization;
using SportsPlatform.Modules.Identity.Features.PlatformAdmins.CreatePlatformAdmin;
using SportsPlatform.Modules.Identity.Infrastructure;
using SportsPlatform.Modules.Identity.Infrastructure.Sessions;
using SportsPlatform.Modules.Identity.Infrastructure.Users;

namespace SportsPlatform.Modules.Identity;

/// <summary>
/// Identity &amp; Access: users, platform roles and the data protection key ring (S03-T1); the session cookie, the
/// account API and the authorization evaluator (T2); memberships and roles follow in S03 (docs/architecture/modules.md).
/// </summary>
public sealed class IdentityModule : IModule
{
    public const string SchemaName = "identity";

    /// <summary>Keys are isolated per application name: changing it invalidates every cookie and token.</summary>
    public const string DataProtectionApplicationName = "msp";

    public string Name => "Identity";

    public string Schema => SchemaName;

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleDbContext<IdentityDbContext>(configuration, Schema);

        // S03 D14: the key ring in identity.data_protection_keys, shared by every API instance.
        services.AddDataProtection()
            .SetApplicationName(DataProtectionApplicationName)
            .PersistKeysToDbContext<IdentityDbContext>();

        // Identity's core only: no UI, no MapIdentityApi (S03 D6), no roles tables (D9).
        services.AddIdentityCore<User>(ConfigureIdentity)
            .AddUserStore<UserStore>()
            .AddSignInManager()
            .AddClaimsPrincipalFactory<SessionClaimsFactory>()
            .AddDefaultTokenProviders();

        // S03 D5: the API's only authentication scheme. D10: the evaluator replaces the Host's deny-all default.
        services.AddSessionCookie();
        services.AddSingleton<IAuthorizationEvaluator, IdentityAuthorizationEvaluator>();

        services.AddScoped<CreatePlatformAdminHandler>();
        services.AddScoped<LoginHandler>();
        services.AddScoped<GetMeHandler>();
        services.AddSingleton<IValidator<LoginRequest>, LoginRequestValidator>();
    }

    public void MapEndpoints(ApiEndpointGroups groups)
    {
        ArgumentNullException.ThrowIfNull(groups);
        var account = AccountEndpoints.Group(groups.Account);
        LoginEndpoint.Map(account);
        LogoutEndpoint.Map(account);
        GetMeEndpoint.Map(account);
    }

    // S03 D15.
    private static void ConfigureIdentity(IdentityOptions options)
    {
        options.Password.RequiredLength = 12;
        options.Password.RequiredUniqueChars = 1;
        options.Password.RequireDigit = false;
        options.Password.RequireLowercase = false;
        options.Password.RequireUppercase = false;
        options.Password.RequireNonAlphanumeric = false;

        options.Lockout.AllowedForNewUsers = true;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);

        options.User.RequireUniqueEmail = true;

        // The session carries the user ID as the claim the Host reads the actor from (S03 D5).
        options.ClaimsIdentity.UserIdClaimType = ActorClaims.UserId;

        // The user name is the email, which Identity validates as an email; any character an address may hold is allowed.
        options.User.AllowedUserNameCharacters = string.Empty;
        options.SignIn.RequireConfirmedEmail = true;
    }
}

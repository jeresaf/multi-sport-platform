using FluentValidation;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SportsPlatform.BuildingBlocks.Actions;
using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Modules;
using SportsPlatform.BuildingBlocks.Persistence;
using SportsPlatform.BuildingBlocks.Persistence.Messaging;
using SportsPlatform.BuildingBlocks.Tenancy;
using SportsPlatform.Modules.Identity.Contracts.Account;
using SportsPlatform.Modules.Identity.Domain.Roles;
using SportsPlatform.Modules.Identity.Features.Account;
using SportsPlatform.Modules.Identity.Features.Account.ChangePassword;
using SportsPlatform.Modules.Identity.Features.Account.ForgotPassword;
using SportsPlatform.Modules.Identity.Features.Account.GetMe;
using SportsPlatform.Modules.Identity.Features.Account.Login;
using SportsPlatform.Modules.Identity.Features.Account.Logout;
using SportsPlatform.Modules.Identity.Features.Account.ResetPassword;
using SportsPlatform.Modules.Identity.Features.Authorization;
using SportsPlatform.Modules.Identity.Features.Directory;
using SportsPlatform.Modules.Identity.Features.PlatformAdmins.CreatePlatformAdmin;
using SportsPlatform.Modules.Identity.Infrastructure;
using SportsPlatform.Modules.Identity.Infrastructure.Directory;
using SportsPlatform.Modules.Identity.Infrastructure.PasswordReset;
using SportsPlatform.Modules.Identity.Infrastructure.RateLimiting;
using SportsPlatform.Modules.Identity.Infrastructure.Sessions;
using SportsPlatform.Modules.Identity.Infrastructure.Users;

namespace SportsPlatform.Modules.Identity;

/// <summary>
/// Identity &amp; Access: users, platform roles and the data protection key ring (S03-T1); the session cookie, the
/// account API, password reset and change, sign-in rate limits (T2); roles in code and the authorization evaluator (T3);
/// tenant memberships, their role assignments and the identity directory (T4) (docs/architecture/modules.md).
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

        // S03 D8: the cross-tenant lookups before a request's tenant is set; the real ITenantMembershipChecker.
        services.AddPlatformServiceDbContext<IdentityDirectoryDbContext>(configuration, Schema);
        services.AddScoped<IdentityDirectory>();
        services.AddScoped<ITenantMembershipChecker>(provider => provider.GetRequiredService<IdentityDirectory>());

        // S03 D14: the key ring in identity.data_protection_keys, shared by every API instance.
        services.AddDataProtection()
            .SetApplicationName(DataProtectionApplicationName)
            .PersistKeysToDbContext<IdentityDbContext>();

        // Identity's core only: no UI, no MapIdentityApi (S03 D6), no roles tables (D9).
        services.AddIdentityCore<User>(ConfigureIdentity)
            .AddUserStore<UserStore>()
            .AddSignInManager()
            .AddClaimsPrincipalFactory<SessionClaimsFactory>()
            .AddDefaultTokenProviders()
            .AddTokenProvider<PasswordResetTokenProvider>(PasswordResetTokenProvider.ProviderName);

        // Emailed links and the sign-in and password-reset rate limits (D15), checked on start.
        services.AddOptions<AccountOptions>().Bind(configuration.GetSection(AccountOptions.SectionName)).ValidateOnStart();
        services.AddSingleton<IValidateOptions<AccountOptions>, AccountOptionsValidator>();
        services.AddAccountRateLimits();

        // S03 D5: the API's only authentication scheme. D10: the evaluator replaces the Host's deny-all default; D9: it
        // grants from the roles defined in code, which the Host checks against the action catalogue at startup.
        services.AddSessionCookie();
        services.AddSingleton(new RoleCatalogue(IdentityRoles.All));
        services.AddScoped<IAuthorizationEvaluator, IdentityAuthorizationEvaluator>();

        services.AddScoped<CreatePlatformAdminHandler>();
        services.AddScoped<LoginHandler>();
        services.AddScoped<GetMeHandler>();
        services.AddScoped<ForgotPasswordHandler>();
        services.AddScoped<ResetPasswordHandler>();
        services.AddScoped<ChangePasswordHandler>();
        services.AddSingleton<IValidator<LoginRequest>, LoginRequestValidator>();
        services.AddSingleton<IValidator<ForgotPasswordRequest>, ForgotPasswordRequestValidator>();
        services.AddSingleton<IValidator<ResetPasswordRequest>, ResetPasswordRequestValidator>();
        services.AddSingleton<IValidator<ChangePasswordRequest>, ChangePasswordRequestValidator>();

        // D13: the reset email is sent by Identity's own consumer, never inside the request.
        services.AddIntegrationEventConsumer<PasswordResetRequested.V1, PasswordResetEmailConsumer, IdentityDbContext>(
            PasswordResetEmailConsumer.Name);
    }

    public void MapEndpoints(ApiEndpointGroups groups)
    {
        ArgumentNullException.ThrowIfNull(groups);
        var account = AccountEndpoints.Group(groups.Account);
        LoginEndpoint.Map(account);
        LogoutEndpoint.Map(account);
        GetMeEndpoint.Map(account);
        ForgotPasswordEndpoint.Map(account);
        ResetPasswordEndpoint.Map(account);
        ChangePasswordEndpoint.Map(account);
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

        // D15: a random token whose hash is stored; 1 hour, works once, the newest wins.
        options.Tokens.PasswordResetTokenProvider = PasswordResetTokenProvider.ProviderName;
    }
}

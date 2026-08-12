using System.Text;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using SportsPlatform.BuildingBlocks;
using SportsPlatform.Infrastructure.Persistence;
using SportsPlatform.Infrastructure.Persistence.Repositories;
using SportsPlatform.Infrastructure.BackgroundJobs;
using SportsPlatform.Infrastructure.Services;
using SportsPlatform.Modules.Audit;
using SportsPlatform.Modules.Identity;
using SportsPlatform.Modules.Identity.Services;
using SportsPlatform.Modules.LiveScoring;
using SportsPlatform.Modules.Sports;
using SportsPlatform.Modules.Tenancy;
using SportsPlatform.Modules.Tenancy.Services;
using SportsPlatform.Modules.Sports.Services;
using SportsPlatform.Modules.Registrations.Services;
using SportsPlatform.Modules.Participants.Services;
using SportsPlatform.Modules.Participants;
using SportsPlatform.Modules.Results;
using SportsPlatform.Modules.Results.Services;
using SportsPlatform.Modules.Scheduling.Services;
using SportsPlatform.Modules.FormatEngines;
using SportsPlatform.Modules.FormatEngines.Interfaces;
using SportsPlatform.Api.Authorization;
using SportsPlatform.Api.Middleware;
using SportsPlatform.Api.Services;
using SportsPlatform.Shared.Configuration;
using SportsPlatform.Shared.Extensions;
using SportsPlatform.Modules.Notifications.Services;

var builder = WebApplication.CreateBuilder(args);

ConfigurationStartupValidator.ValidateAndThrow(
    builder.Configuration,
    builder.Environment.EnvironmentName);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .CreateLogger();

builder.Host.UseSerilog();

// Add services
builder.Services.AddSharedConfiguration(builder.Configuration);

// Database
builder.Services.AddDbContext<SportsPlatformDbContext>((sp, options) =>
{
    var config = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<SportsPlatform.Shared.Configuration.DatabaseSettings>>();
    options.UseNpgsql(config.Value.ConnectionString);
});

// Repositories
builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<ITenantRepository, TenantRepository>();
builder.Services.AddScoped<IParticipantRepository, ParticipantRepository>();

// Services
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ITenancyService, TenancyService>();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<PrivacyRetentionService>();
if (!builder.Environment.IsEnvironment("Testing"))
    builder.Services.AddHostedService<PrivacyRetentionHostedService>();
builder.Services.AddScoped<ISportsService, SportsService>();
builder.Services.AddScoped<IFootballRulesetService, FootballRulesetService>();
builder.Services.AddScoped<IBasketballRulesetService, BasketballRulesetService>();
builder.Services.AddScoped<IRugbyRulesetService, RugbyRulesetService>();
builder.Services.AddScoped<IGolfRulesetService, GolfRulesetService>();
builder.Services.AddScoped<ICombatSportsRulesetService, CombatSportsRulesetService>();
builder.Services.AddScoped<IAthleticsRulesetService, AthleticsRulesetService>();
builder.Services.AddScoped<ICricketT20RulesetService, CricketT20RulesetService>();
builder.Services.AddScoped<ICompetitionsService, CompetitionsService>();
builder.Services.AddScoped<IContestsService, ContestsService>();
builder.Services.AddScoped<IParticipantsService, ParticipantsService>();
builder.Services.AddScoped<BulkImportService>();
builder.Services.AddScoped<CsvImportParser>();
builder.Services.AddSingleton<CsvUploadValidator>();
builder.Services.AddSingleton<IBulkImportQueue, BulkImportQueue>();
builder.Services.AddScoped<BulkImportJobProcessor>();
builder.Services.AddHostedService<BulkImportWorker>();
builder.Services.AddScoped<IBrandingService, BrandingService>();
if (builder.Environment.IsEnvironment("Testing"))
    builder.Services.AddSingleton<IBrandingAssetStorage, InMemoryBrandingAssetStorage>();
else
    builder.Services.AddSingleton<IBrandingAssetStorage, MinioBrandingAssetStorage>();
builder.Services.AddScoped<ISubscriptionsService, SubscriptionsService>();
builder.Services.AddScoped<IFeatureEntitlementService, FeatureEntitlementService>();
builder.Services.AddScoped<IUsageLimitsService, UsageLimitsService>();
builder.Services.AddScoped<ICompetitionSchedulingService, CompetitionSchedulingService>();
builder.Services.AddScoped<IContestLifecycleService, ContestLifecycleService>();
builder.Services.AddSingleton<CompetitionScheduleConflictDetector>();
builder.Services.AddScoped<SubscriptionLifecycleJob>();
if (!builder.Environment.IsEnvironment("Testing"))
    builder.Services.AddHostedService<SubscriptionLifecycleHostedService>();
builder.Services.AddScoped<IRegistrationService, RegistrationService>();
builder.Services.AddScoped<IRegistrationFormService, RegistrationFormService>();
builder.Services.AddScoped<ICompetitionEligibilityService, CompetitionEligibilityService>();
builder.Services.AddScoped<IRoundRobinEngine, RoundRobinEngine>();
builder.Services.AddScoped<IKnockoutEngine, KnockoutEngine>();
builder.Services.AddScoped<IGroupPlusKnockoutEngine, GroupPlusKnockoutEngine>();
builder.Services.AddScoped<IBestOfSeriesEngine, BestOfSeriesEngine>();
builder.Services.AddScoped<IHeatQualificationEngine, HeatQualificationEngine>();
builder.Services.AddScoped<ILiveScoringService, LiveScoringService>();
builder.Services.AddScoped<ContestCheckInService>();
builder.Services.AddScoped<IContestCheckInWorkflowService, ContestCheckInWorkflowService>();
builder.Services.AddScoped<IResultRepository, ResultRepository>();
builder.Services.AddScoped<IResultService, ResultService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<ITenantNotificationDeliveryPolicy, TenantNotificationDeliveryPolicy>();
builder.Services.AddScoped<ContestDerivedStateService>();
builder.Services.AddSingleton<IPushDeliveryClient, FirebasePushDeliveryClient>();
builder.Services.AddScoped<ReportDocumentBuilder>();
builder.Services.AddSingleton<IReportGenerationQueue, ReportGenerationQueue>();
builder.Services.AddHostedService<ReportGenerationWorker>();
builder.Services.AddSingleton<OperationalMetrics>();
builder.Services.AddSingleton<IErrorReporter, LoggingErrorReporter>();
if (!builder.Environment.IsEnvironment("Testing"))
    builder.Services.AddHostedService<NotificationDeliveryWorker>();

// Identity Services
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IRoleRepository, RoleRepository>();
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddSingleton<IMfaService, TotpMfaService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IIdentityMessageSender, SmtpIdentityMessageSender>();
builder.Services.AddScoped<IRoleService, RoleService>();

// JWT Authentication
var jwtSettings = builder.Configuration.GetSection("Jwt").Get<JwtSettings>();
if (jwtSettings != null)
{
    var signingSecrets = new[] { jwtSettings.Secret }
        .Concat(jwtSettings.PreviousSecrets ?? Array.Empty<string>())
        .Where(secret => !string.IsNullOrWhiteSpace(secret))
        .Distinct(StringComparer.Ordinal)
        .ToArray();
    if (signingSecrets.Length == 0 || signingSecrets.Any(secret => Encoding.UTF8.GetByteCount(secret) < 32))
        throw new InvalidOperationException("JWT signing secrets must each contain at least 32 UTF-8 bytes.");

    builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidAudience = jwtSettings.Audience,
            IssuerSigningKeys = signingSecrets.Select(secret =>
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret))),
            ClockSkew = TimeSpan.Zero
        };

        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var userIdValue = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
                if (!Guid.TryParse(userIdValue, out var userId))
                {
                    context.Fail("The access token does not identify a valid user.");
                    return;
                }

                var database = context.HttpContext.RequestServices
                    .GetRequiredService<SportsPlatformDbContext>();
                var canAccess = await database.Users
                    .AsNoTracking()
                    .AnyAsync(user => user.Id == userId && user.IsActive && !user.IsSuspended,
                        context.HttpContext.RequestAborted);
                if (!canAccess)
                    context.Fail("The user account is inactive or suspended.");
            },
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            }
        };
    });
}

// Authorization
builder.Services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();
builder.Services.AddSingleton<IAuthorizationPolicyProvider, DynamicPermissionPolicyProvider>();
builder.Services.AddAuthorization();

var dataProtection = builder.Services.AddDataProtection()
    .SetApplicationName("SportsPlatform");
var dataProtectionKeyPath = builder.Configuration["Security:DataProtectionKeyPath"];
if (!string.IsNullOrWhiteSpace(dataProtectionKeyPath))
    dataProtection.PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeyPath));

var globalRequestsPerMinute = builder.Configuration.GetValue("Security:RateLimits:GlobalPerMinute", 300);
var authenticationRequestsPerMinute = builder.Configuration.GetValue("Security:RateLimits:AuthenticationPerMinute", 30);
var passwordRecoveryRequestsPerWindow = builder.Configuration.GetValue("Security:RateLimits:PasswordRecoveryPer15Minutes", 10);
var uploadRequestsPerMinute = builder.Configuration.GetValue("Security:RateLimits:UploadsPerMinute", 10);
builder.Services.AddRateLimiter(options =>
{
    static string ClientKey(HttpContext context) =>
        context.User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? context.Connection.RemoteIpAddress?.ToString()
        ?? "unknown";

    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? context.Connection.RemoteIpAddress?.ToString()
                ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = globalRequestsPerMinute,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
    options.AddPolicy("authentication", context =>
        RateLimitPartition.GetFixedWindowLimiter($"auth:{ClientKey(context)}", _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = authenticationRequestsPerMinute,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            AutoReplenishment = true
        }));
    options.AddPolicy("password-recovery", context =>
        RateLimitPartition.GetFixedWindowLimiter($"recovery:{ClientKey(context)}", _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = passwordRecoveryRequestsPerWindow,
            Window = TimeSpan.FromMinutes(15),
            QueueLimit = 0,
            AutoReplenishment = true
        }));
    options.AddPolicy("uploads", context =>
        RateLimitPartition.GetFixedWindowLimiter($"upload:{ClientKey(context)}", _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = uploadRequestsPerMinute,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            AutoReplenishment = true
        }));
    options.OnRejected = async (context, cancellationToken) =>
    {
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
            context.HttpContext.Response.Headers.RetryAfter = Math.Max(1, (int)retryAfter.TotalSeconds).ToString();
        await context.HttpContext.Response.WriteAsJsonAsync(new
        {
            error = "RateLimitExceeded",
            message = "Too many requests. Retry after the indicated delay."
        }, cancellationToken);
    };
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Sports Platform API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new()
    {
        Description = "JWT Authorization header using the Bearer scheme",
        Name = "Authorization",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });
    c.AddSecurityRequirement(new()
    {
        {
            new()
            {
                Reference = new() { Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

var allowedOrigins = builder.Configuration.GetSection("Security:AllowedOrigins").Get<string[]>()
    ?? new[] { "http://localhost:3000", "http://localhost:3001" };
builder.Services.AddCors(options =>
{
    options.AddPolicy("WebClients", policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyMethod()
              .AllowAnyHeader()
              .WithExposedHeaders("Content-Disposition");
    });
});

// Add SignalR
builder.Services.AddSignalR();
builder.Services.AddContestHubServices();

// Add Health Checks
builder.Services.AddHealthChecks()
    .AddCheck("self", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy(), tags: new[] { "live" })
    .AddCheck<DatabaseHealthCheck>("postgresql", tags: new[] { "ready" })
    .AddCheck<RedisHealthCheck>("redis", tags: new[] { "ready" });

// Add OpenTelemetry
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService(
        builder.Configuration["Observability:ServiceName"] ?? "sports-platform-api"))
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation())
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation());

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseCors("WebClients");
app.UseCorrelationContext();
app.UseSerilogRequestLogging(options =>
{
    options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} completed {StatusCode} in {Elapsed:0.0000} ms";
    options.EnrichDiagnosticContext = (diagnosticContext, context) =>
    {
        diagnosticContext.Set("RequestId", context.TraceIdentifier);
        diagnosticContext.Set("CorrelationId", context.Items[CorrelationContextMiddleware.ItemName]?.ToString() ?? string.Empty);
        diagnosticContext.Set("TraceId", System.Diagnostics.Activity.Current?.TraceId.ToString() ?? string.Empty);
        diagnosticContext.Set("TenantId", context.Items["TenantId"]?.ToString() ?? "anonymous");
        diagnosticContext.Set("UserId", context.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? context.User.FindFirstValue("sub")
            ?? "anonymous");
    };
});
app.UseErrorHandling();
app.UseMiddleware<OperationalMetricsMiddleware>();
app.UseRouting();
app.UseAuthentication();
app.UseRateLimiter();
app.UseTenantResolution();
app.UseMiddleware<ObservabilityContextMiddleware>();
app.UseMiddleware<UsageMeteringMiddleware>();
app.UseTenantLifecycleMiddleware();
app.UseSubscriptionReadOnlyMiddleware();
app.UseAuthorization();

app.MapControllers();
app.MapGet("/metrics", OperationalMetricsEndpoint.GetAsync).AllowAnonymous();
var livenessOptions = new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("live"),
    ResponseWriter = HealthCheckResponseWriter.WriteAsync
};
var readinessOptions = new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("ready"),
    ResponseWriter = HealthCheckResponseWriter.WriteAsync
};
app.MapHealthChecks("/health", livenessOptions);
app.MapHealthChecks("/health/live", livenessOptions);
app.MapHealthChecks("/health/ready", readinessOptions);
app.MapHealthChecks("/health/dependencies", readinessOptions);
app.MapContestHub();

// Initialize database on startup
await app.InitializeDatabaseAsync();

app.Run();

// Exposed for WebApplicationFactory-based integration tests.
public partial class Program;

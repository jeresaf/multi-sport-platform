using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SportsPlatform.Shared.Configuration;

namespace SportsPlatform.Shared.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSharedConfiguration(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<DatabaseSettings>(options =>
            configuration.GetSection("Database").Bind(options));
        services.Configure<JwtSettings>(options =>
            configuration.GetSection("Jwt").Bind(options));
        services.Configure<TenantSettings>(options =>
            configuration.GetSection("Tenant").Bind(options));

        return services;
    }
}

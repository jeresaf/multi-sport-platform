using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace SportsPlatform.Infrastructure.Persistence.Migrations;

/// <summary>
/// Design-time DbContext factory for EF Core migrations.
/// Used when running `dotnet ef migrations add` commands.
/// Requires PostgreSQL so generated migrations always match the production schema.
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<SportsPlatformDbContext>
{
    public SportsPlatformDbContext CreateDbContext(string[] args)
    {
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";
        
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile($"appsettings.{environment}.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("DefaultConnection");
        
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Set ConnectionStrings__DefaultConnection to a PostgreSQL connection string before running EF Core migration commands.");
        }

        if (!connectionString.Contains("Host=", StringComparison.OrdinalIgnoreCase) &&
            !connectionString.Contains("Server=", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "EF Core migrations for SportsPlatform must be generated with the PostgreSQL provider.");
        }

        var optionsBuilder = new DbContextOptionsBuilder<SportsPlatformDbContext>();
        optionsBuilder.UseNpgsql(connectionString, npgsqlOptions =>
        {
            npgsqlOptions.MigrationsAssembly(typeof(SportsPlatformDbContext).Assembly.FullName);
            npgsqlOptions.EnableRetryOnFailure(
                maxRetryCount: 3,
                maxRetryDelay: TimeSpan.FromSeconds(10),
                errorCodesToAdd: null);
        });

        return new SportsPlatformDbContext(optionsBuilder.Options);
    }
}

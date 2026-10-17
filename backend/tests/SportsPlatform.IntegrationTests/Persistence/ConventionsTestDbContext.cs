using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using SportsPlatform.BuildingBlocks.Domain;
using SportsPlatform.BuildingBlocks.Persistence;
using SportsPlatform.BuildingBlocks.Results;

namespace SportsPlatform.IntegrationTests.Persistence;

/// <summary>Test-only module context (schema <c>conventions_test</c>) that proves the ModuleDbContext conventions.</summary>
public sealed class ConventionsTestDbContext(DbContextOptions<ConventionsTestDbContext> options)
    : ModuleDbContext(options, SchemaName)
{
    public const string SchemaName = "conventions_test";

    public static readonly Error ConcurrencyConflict =
        new("TEST_CONCURRENCY_CONFLICT", "The probe was changed by someone else.", ErrorKind.Conflict);

    public DbSet<ConventionProbe> ConventionProbes => Set<ConventionProbe>();

    protected override Error ConcurrencyConflictError => ConcurrencyConflict;

    protected override void ConfigureModel(ModelBuilder modelBuilder) =>
        modelBuilder.Entity<ConventionProbe>(probe => probe.Property(p => p.VenueName).HasMaxLength(200));
}

/// <summary>An aggregate with one business property; everything else comes from the conventions.</summary>
public sealed class ConventionProbe : AggregateRoot
{
    public ConventionProbe(string venueName)
        : base(NewId()) => VenueName = venueName;

    private ConventionProbe()
    {
    }

    public string VenueName { get; private set; } = string.Empty;

    public void Rename(string venueName) => VenueName = venueName;
}

/// <summary>Lets <c>dotnet ef migrations add</c> build the context without starting the Host.</summary>
public sealed class ConventionsTestDbContextFactory : IDesignTimeDbContextFactory<ConventionsTestDbContext>
{
    public ConventionsTestDbContext CreateDbContext(string[] args) => new(
        ModuleDbContextOptions.ForMigrations<ConventionsTestDbContext>("Host=localhost", ConventionsTestDbContext.SchemaName));
}

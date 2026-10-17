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

    public DbSet<ReferenceProbe> ReferenceProbes => Set<ReferenceProbe>();

    public DbSet<AppendOnlyProbe> AppendOnlyProbes => Set<AppendOnlyProbe>();

    protected override Error ConcurrencyConflictError => ConcurrencyConflict;

    protected override void ConfigureModel(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ConventionProbe>(probe => probe.Property(p => p.VenueName).HasMaxLength(200));
        modelBuilder.Entity<ReferenceProbe>(probe => probe.Property(p => p.Code).HasMaxLength(10));
        modelBuilder.Entity<AppendOnlyProbe>(probe =>
        {
            probe.IsAppendOnly();
            probe.Property(p => p.Note).HasMaxLength(200);
        });
    }
}

/// <summary>An aggregate with one business property; everything else comes from the conventions.</summary>
public sealed class ConventionProbe : AggregateRoot
{
    public ConventionProbe(string venueName)
        : base(NewId()) => VenueName = venueName;

    /// <summary>With a chosen ID, e.g. a UUIDv7 for a known instant.</summary>
    public ConventionProbe(Guid id, string venueName)
        : base(id) => VenueName = venueName;

    private ConventionProbe()
    {
    }

    public string VenueName { get; private set; } = string.Empty;

    public void Rename(string venueName) => VenueName = venueName;
}

/// <summary>A reference-style table: the runtime roles may only read it (<c>ReadOnlyForRuntimeRoles</c>).</summary>
public sealed class ReferenceProbe : Entity
{
    public ReferenceProbe(string code)
        : base(NewId()) => Code = code;

    private ReferenceProbe()
    {
    }

    public string Code { get; private set; } = string.Empty;
}

/// <summary>An audit-style table: insert and read only (<c>AppendOnly</c>).</summary>
public sealed class AppendOnlyProbe : Entity
{
    public AppendOnlyProbe(string note)
        : base(NewId()) => Note = note;

    private AppendOnlyProbe()
    {
    }

    public string Note { get; private set; } = string.Empty;
}

/// <summary>Lets <c>dotnet ef migrations add</c> build the context without starting the Host.</summary>
public sealed class ConventionsTestDbContextFactory : IDesignTimeDbContextFactory<ConventionsTestDbContext>
{
    public ConventionsTestDbContext CreateDbContext(string[] args) => new(
        ModuleDbContextOptions.ForMigrations<ConventionsTestDbContext>("Host=localhost", ConventionsTestDbContext.SchemaName));
}

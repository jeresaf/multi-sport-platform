using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using SportsPlatform.BuildingBlocks.Domain;
using SportsPlatform.BuildingBlocks.Messaging;
using SportsPlatform.BuildingBlocks.Persistence;
using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.BuildingBlocks.Tenancy;

namespace SportsPlatform.IntegrationTests.Messaging;

/// <summary>
/// Test-only module (schema <c>messaging_test</c>) that both publishes and consumes, to exercise domain event
/// dispatch, the outbox and the inbox.
/// </summary>
public sealed class MessagingTestDbContext(DbContextOptions<MessagingTestDbContext> options, ITenantContext tenantContext)
    : ModuleDbContext(options, SchemaName, tenantContext)
{
    public const string SchemaName = "messaging_test";

    public static readonly Error ConcurrencyConflict =
        new("TEST_CONCURRENCY_CONFLICT", "The probe was changed by someone else.", ErrorKind.Conflict);

    public DbSet<MessagingProbe> Probes => Set<MessagingProbe>();

    public DbSet<ReceivedProbe> ReceivedProbes => Set<ReceivedProbe>();

    public DbSet<ConsumedNote> ConsumedNotes => Set<ConsumedNote>();

    protected override Error ConcurrencyConflictError => ConcurrencyConflict;

    protected override void ConfigureModel(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MessagingProbe>(probe => probe.Property(p => p.Name).HasMaxLength(200));
        modelBuilder.Entity<ReceivedProbe>();
        modelBuilder.Entity<ConsumedNote>(note =>
        {
            note.ToTable(ConsumedNote.Table);
            note.IsAppendOnly();
        });
    }
}

/// <summary>An aggregate that raises <see cref="ProbeRegistered"/> when created and <see cref="ProbeEchoed"/> on request.</summary>
public sealed class MessagingProbe : AggregateRoot
{
    public MessagingProbe(string name)
        : base(NewId())
    {
        Name = name;
        Raise(new ProbeRegistered(Id, name));
    }

    private MessagingProbe()
    {
    }

    public string Name { get; private set; } = string.Empty;

    /// <summary>How many <see cref="ProbeEchoed"/> events were handled.</summary>
    public int Echoes { get; private set; }

    public void Echo(int remaining) => Raise(new ProbeEchoed(this, remaining));

    /// <summary>Asks <see cref="AddConsumedNote"/> for a tenant-owned row, added while the save runs.</summary>
    public void Note(Guid noteId) => Raise(new ProbeNoted(noteId));

    internal void CountEcho() => Echoes++;
}

/// <summary>Written by the recording consumer: the consumer's own change, committed with its inbox row.</summary>
public sealed class ReceivedProbe : Entity
{
    public ReceivedProbe(Guid probeId, Guid messageId)
        : base(NewId())
    {
        ProbeId = probeId;
        MessageId = messageId;
    }

    private ReceivedProbe()
    {
    }

    public Guid ProbeId { get; private set; }

    public Guid MessageId { get; private set; }
}

/// <summary>A tenant-owned row (RLS) written by <see cref="NoteWriter"/>, for consumers under RLS.</summary>
public sealed class ConsumedNote : Entity, ITenantOwned
{
    public const string Table = "consumed_notes";

    public ConsumedNote(Guid id)
        : base(id)
    {
    }

    private ConsumedNote()
    {
    }

    public Guid TenantId { get; private set; }
}

public sealed record ProbeRegistered(Guid ProbeId, string Name) : IDomainEvent;

public sealed record ProbeNoted(Guid NoteId) : IDomainEvent;

/// <summary>Each handled echo raises the next one until <see cref="Remaining"/> is 0.</summary>
public sealed record ProbeEchoed(MessagingProbe Probe, int Remaining) : IDomainEvent;

public static class ProbeCreated
{
    public sealed record V1(Guid ProbeId, string Name) : IIntegrationEvent;
}

/// <summary>
/// Asks <see cref="NoteWriter"/> for a <see cref="ConsumedNote"/>; with <c>WriteAsTenantId</c> it inserts
/// the row for that tenant with raw SQL, past the tenant interceptor, so only RLS can stop it.
/// </summary>
public static class NoteRequested
{
    public sealed record V1(Guid NoteId, Guid? WriteAsTenantId) : IIntegrationEvent;
}

/// <summary>An event nobody subscribes to.</summary>
public static class ProbeIgnored
{
    public sealed record V1(Guid ProbeId) : IIntegrationEvent;
}

/// <summary>Lets <c>dotnet ef migrations add</c> build the context without starting the Host.</summary>
public sealed class MessagingTestDbContextFactory : IDesignTimeDbContextFactory<MessagingTestDbContext>
{
    public MessagingTestDbContext CreateDbContext(string[] args) => new(
        ModuleDbContextOptions.ForMigrations<MessagingTestDbContext>("Host=localhost", MessagingTestDbContext.SchemaName), new TenantContext());
}

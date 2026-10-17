using Microsoft.EntityFrameworkCore;

namespace SportsPlatform.BuildingBlocks.Persistence.Messaging;

/// <summary>Maps <c>outbox_messages</c> and <c>inbox_messages</c> into every module schema.</summary>
internal static class MessagingModel
{
    public const string OutboxTable = "outbox_messages";
    public const string InboxTable = "inbox_messages";

    public static void Configure(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<OutboxMessage>(outbox =>
        {
            outbox.ToTable(OutboxTable, table => table.HasCheckConstraint(
                "ck_outbox_messages_status", "status IN ('PENDING', 'PROCESSED', 'FAILED')"));
            outbox.Property(m => m.EventName).HasMaxLength(200);
            outbox.Property(m => m.CorrelationId).HasMaxLength(64);
            outbox.Property(m => m.AggregateType).HasMaxLength(100);
            outbox.Property(m => m.Payload).HasColumnType("jsonb");
            outbox.Property(m => m.Status)
                .HasConversion(status => status.ToString().ToUpperInvariant(), value => Enum.Parse<OutboxMessageStatus>(value, true))
                .HasMaxLength(20);
            outbox.Property(m => m.LastError).HasMaxLength(OutboxMessage.LastErrorMaxLength);
            // The processor's claim query: due PENDING messages, oldest first.
            outbox.HasIndex(m => new { m.Status, m.NextAttemptAt, m.OccurredAt });
        });

        modelBuilder.Entity<InboxMessage>(inbox =>
        {
            inbox.ToTable(InboxTable);
            inbox.HasKey(m => new { m.MessageId, m.Consumer });
            inbox.Property(m => m.Consumer).HasMaxLength(200);
        });
    }
}

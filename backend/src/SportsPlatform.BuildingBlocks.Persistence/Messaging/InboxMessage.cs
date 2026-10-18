namespace SportsPlatform.BuildingBlocks.Persistence.Messaging;

/// <summary>An event a consumer has handled, in the consumer module's <c>inbox_messages</c> (key: message + consumer).</summary>
public sealed class InboxMessage
{
    internal InboxMessage(Guid messageId, string consumer)
    {
        MessageId = messageId;
        Consumer = consumer;
    }

    private InboxMessage()
    {
    }

    public Guid MessageId { get; private set; }

    public string Consumer { get; private set; } = string.Empty;
}

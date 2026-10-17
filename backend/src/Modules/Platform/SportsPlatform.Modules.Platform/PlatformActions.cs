using SportsPlatform.BuildingBlocks.Actions;

namespace SportsPlatform.Modules.Platform;

/// <summary>The Platform module's actions; exported to docs/generated/actions.md by the CLI.</summary>
public static class PlatformActions
{
    public static readonly ActionDefinition OutboxRead = new(
        "platform.outbox.read",
        "View outbox messages and their delivery status.",
        ActionScope.Platform,
        AuditRequirement.Required,
        since: "S01");

    public static readonly ActionDefinition OutboxRedrive = new(
        "platform.outbox.redrive",
        "Re-deliver failed outbox messages.",
        ActionScope.Platform,
        AuditRequirement.RequiredWithReason,
        since: "S01");
}

using SportsPlatform.BuildingBlocks.Messaging;

namespace SportsPlatform.Modules.Identity.Contracts.Invitations;

/// <summary>
/// Someone was invited to join the event's tenant (S03 D12, D13). The invitation ID only, never the email (ADR-0015):
/// Identity's own consumer loads the invitation, creates its token and sends the email.
/// </summary>
public static class InvitationIssued
{
    public sealed record V1(Guid InvitationId) : IIntegrationEvent;
}

using SportsPlatform.BuildingBlocks.Actions;
using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Auditing;
using SportsPlatform.BuildingBlocks.Messaging;
using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.BuildingBlocks.Tenancy;

namespace SportsPlatform.UnitTests.BuildingBlocks;

public sealed class AuditScopeTests
{
    private static readonly DateTimeOffset _now = new(2026, 10, 17, 9, 0, 0, TimeSpan.Zero);
    private static readonly Guid _kmsa = Guid.Parse("0192a1b2-0000-7000-8000-00000000c001");

    private static readonly ActionDefinition _redrive = new(
        "platform.outbox.redrive", "Re-deliver failed outbox messages.", ActionScope.Platform, AuditRequirement.RequiredWithReason, "S01");

    private static readonly ActionDefinition _read = new(
        "fixture.read", "View fixtures.", ActionScope.Tenant, AuditRequirement.None, "S10", [AssignmentScope.Tenant]);

    [Fact]
    public void A_save_records_success_unless_the_handler_records_a_refusal()
    {
        var scope = new AuditScope(AnonymousActor.Instance, new TenantContext(), new CorrelationContext(), new FixedClock(_now));
        scope.Begin(_redrive, AuditSource.Api);
        scope.SaveOutcome.ShouldBe(AuditOutcome.Success);

        scope.RecordOnSave(AuditOutcome.Denied, new Error("AUTH_INVALID_CREDENTIALS", "Refused.", ErrorKind.Unauthorized));

        scope.SaveOutcome.ShouldBe(AuditOutcome.Denied);
        scope.CreateRecord(scope.SaveOutcome).ShouldSatisfyAllConditions(
            r => r.Outcome.ShouldBe(AuditOutcome.Denied),
            r => r.ErrorCode.ShouldBe("AUTH_INVALID_CREDENTIALS"),
            r => r.ActorKind.ShouldBe(ActorKind.Anonymous),
            r => r.ActorId.ShouldBe(AnonymousActor.Id));
        Should.Throw<ArgumentException>(() => scope.RecordOnSave(AuditOutcome.Success, CommonErrors.UnexpectedError));
    }

    [Fact]
    public void Record_carries_the_action_actor_correlation_and_description()
    {
        var correlation = new CorrelationContext();
        correlation.Set("kmsa-req-0042", null);
        var scope = new AuditScope(SystemActor.Instance, new TenantContext(), correlation, new FixedClock(_now));
        var messageId = Guid.CreateVersion7();
        scope.Begin(_redrive, AuditSource.Api);

        var record = scope.Describe("OutboxMessage", messageId, "  Consumer fixed  ").CreateRecord(AuditOutcome.Success);

        record.ShouldSatisfyAllConditions(
            r => r.ActionCode.ShouldBe("platform.outbox.redrive"),
            r => r.Outcome.ShouldBe(AuditOutcome.Success),
            r => r.ActorKind.ShouldBe(ActorKind.System),
            r => r.ActorId.ShouldBe(SystemActor.Id),
            r => r.CorrelationId.ShouldBe("kmsa-req-0042"),
            r => r.Source.ShouldBe(AuditSource.Api),
            r => r.EntityType.ShouldBe("OutboxMessage"),
            r => r.EntityId.ShouldBe(messageId),
            r => r.Reason.ShouldBe("Consumer fixed"),
            r => r.OccurredAt.ShouldBe(_now),
            r => r.Id.Version.ShouldBe(7));
    }

    [Fact]
    public void Error_code_is_kept_for_failures_only()
    {
        var scope = NewScope();
        scope.Begin(_redrive, AuditSource.Api);
        scope.SetErrorCode("PLATFORM_OUTBOX_MESSAGE_NOT_FAILED");

        scope.CreateRecord(AuditOutcome.Failure).ErrorCode.ShouldBe("PLATFORM_OUTBOX_MESSAGE_NOT_FAILED");
        scope.CreateRecord(AuditOutcome.Success).ErrorCode.ShouldBeNull();
    }

    [Fact]
    public void Action_without_audit_is_not_pending()
    {
        var scope = NewScope();

        scope.Begin(_read, AuditSource.Api);

        scope.IsPending.ShouldBeFalse();
        scope.Action.ShouldBeNull();
    }

    [Fact]
    public void Recorded_entry_is_no_longer_pending()
    {
        var scope = NewScope();
        scope.Begin(_redrive, AuditSource.Api);
        scope.IsPending.ShouldBeTrue();

        scope.MarkRecorded();

        scope.IsPending.ShouldBeFalse();
    }

    [Fact]
    public void An_audit_begins_once()
    {
        var scope = NewScope();
        scope.Begin(_redrive, AuditSource.Api);

        Should.Throw<InvalidOperationException>(() => scope.Begin(_redrive, AuditSource.Api));
    }

    [Fact]
    public void Record_has_the_scope_tenant()
    {
        var scope = NewScope(_kmsa);
        scope.Begin(_redrive, AuditSource.Api);

        scope.CreateRecord(AuditOutcome.Success).TenantId.ShouldBe(_kmsa);
    }

    [Fact]
    public void Record_has_the_target_tenant_when_the_scope_has_none()
    {
        var scope = NewScope();
        scope.Begin(_redrive, AuditSource.Api);

        scope.Describe(targetTenantId: _kmsa).Describe(reason: "Consumer fixed");

        scope.CreateRecord(AuditOutcome.Success).TenantId.ShouldBe(_kmsa);
    }

    [Fact]
    public void A_target_tenant_may_repeat_the_scope_tenant()
    {
        var scope = NewScope(_kmsa);
        scope.Begin(_redrive, AuditSource.Api);

        scope.Describe(targetTenantId: _kmsa);

        scope.CreateRecord(AuditOutcome.Success).TenantId.ShouldBe(_kmsa);
    }

    [Fact]
    public void A_target_tenant_other_than_the_scope_tenant_is_a_bug()
    {
        var scope = NewScope(_kmsa);
        scope.Begin(_redrive, AuditSource.Api);

        Should.Throw<InvalidOperationException>(() => scope.Describe(targetTenantId: Guid.CreateVersion7()));
    }

    private static AuditScope NewScope(Guid? tenantId = null)
    {
        var tenant = new TenantContext();
        tenant.Set(tenantId);
        return new(SystemActor.Instance, tenant, new CorrelationContext(), new FixedClock(_now));
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}

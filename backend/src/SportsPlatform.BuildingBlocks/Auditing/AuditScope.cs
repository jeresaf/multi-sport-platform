using SportsPlatform.BuildingBlocks.Actions;
using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Messaging;
using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.BuildingBlocks.Tenancy;

namespace SportsPlatform.BuildingBlocks.Auditing;

/// <summary>
/// The audit of the current request (scoped). The Host begins it with the endpoint's action; handlers describe what
/// the action touched. The entry is then written exactly once: by the first module DbContext save (to that module's
/// outbox, in the same transaction as the change), by an explicit <see cref="CreateRecord"/> handed to code that saves
/// in its own scope, or by the Host after the response for denials, failures and actions that saved nothing.
/// </summary>
public sealed class AuditScope(ICurrentActor actor, ITenantContext tenant, CorrelationContext correlation, TimeProvider timeProvider)
{
    private string? _entityType;
    private Guid? _entityId;
    private string? _reason;
    private string? _metadata;
    private Guid? _targetTenantId;

    /// <summary>The action being performed, when it is audited (<c>REQUIRED</c> or <c>REQUIRED_WITH_REASON</c>).</summary>
    public ActionDefinition? Action { get; private set; }

    public AuditSource Source { get; private set; } = AuditSource.System;

    /// <summary>The error code of the response's Problem Details, if any.</summary>
    public string? ErrorCode { get; private set; }

    /// <summary>
    /// The outcome the next module save commits the entry with: <see cref="AuditOutcome.Success"/> unless the handler
    /// saves while refusing (a failed sign-in saves the failed-attempt count; <see cref="RecordOnSave"/>).
    /// </summary>
    public AuditOutcome SaveOutcome { get; private set; } = AuditOutcome.Success;

    /// <summary>Whether the entry has been written or committed to an outbox.</summary>
    public bool IsRecorded { get; private set; }

    /// <summary>Whether an audited action is under way and its entry is not written yet.</summary>
    public bool IsPending => Action is not null && !IsRecorded;

    /// <summary>Starts the audit of an action; actions with audit <c>NONE</c> are ignored.</summary>
    public void Begin(ActionDefinition action, AuditSource source)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (Action is not null)
        {
            throw new InvalidOperationException($"The audit of '{Action.Code}' has already begun.");
        }

        if (action.Audit != AuditRequirement.None)
        {
            Action = action;
            Source = source;
        }
    }

    /// <summary>
    /// What the action touched and why. <paramref name="metadata"/> is serialized now, with personal data masked
    /// (<see cref="AuditMetadata"/>). <paramref name="targetTenantId"/> is the tenant a platform action works on when
    /// the scope has none (a platform read filtered to one tenant); with a scope tenant it must be that tenant.
    /// </summary>
    public AuditScope Describe(
        string? entityType = null, Guid? entityId = null, string? reason = null, object? metadata = null, Guid? targetTenantId = null)
    {
        if (entityType is { Length: > AuditRecord.EntityTypeMaxLength })
        {
            throw new ArgumentException($"An audit entity type is at most {AuditRecord.EntityTypeMaxLength} characters.", nameof(entityType));
        }

        if (targetTenantId is { } target && tenant.TenantId is { } current && target != current)
        {
            throw new InvalidOperationException(
                $"The audit target tenant {target} differs from the scope's tenant {current}.");
        }

        _entityType = entityType ?? _entityType;
        _entityId = entityId ?? _entityId;
        _reason = reason is null ? _reason : Truncate(reason.Trim(), AuditRecord.ReasonMaxLength);
        _metadata = metadata is null ? _metadata : AuditMetadata.Serialize(metadata);
        _targetTenantId = targetTenantId ?? _targetTenantId;
        return this;
    }

    /// <summary>
    /// The handler saves although the action is refused or failed: the save commits the entry with
    /// <paramref name="outcome"/> and <paramref name="error"/>'s code, in the same transaction as what it saved.
    /// </summary>
    public void RecordOnSave(AuditOutcome outcome, Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        if (outcome == AuditOutcome.Success)
        {
            throw new ArgumentException("A successful action is recorded on save already.", nameof(outcome));
        }

        SaveOutcome = outcome;
        ErrorCode = error.Code;
    }

    /// <summary>Remembers the response's error code for a <see cref="AuditOutcome.Failure"/> or <see cref="AuditOutcome.Denied"/> entry.</summary>
    public void SetErrorCode(string? errorCode) => ErrorCode = errorCode;

    /// <summary>The entry for the current action as it stands. Call <see cref="MarkRecorded"/> once it is committed.</summary>
    public AuditRecord CreateRecord(AuditOutcome outcome)
    {
        var action = Action ?? throw new InvalidOperationException("No audited action is under way.");
        var now = timeProvider.GetUtcNow();
        return new AuditRecord(
            Guid.CreateVersion7(now),
            action.Code.Value,
            outcome,
            outcome == AuditOutcome.Success ? null : ErrorCode,
            actor.Kind,
            actor.ActorId,
            tenant.TenantId ?? _targetTenantId,
            _entityType,
            _entityId,
            now,
            Source,
            correlation.CorrelationId,
            _reason,
            _metadata);
    }

    public void MarkRecorded()
    {
        if (Action is null)
        {
            throw new InvalidOperationException("No audited action is under way.");
        }

        IsRecorded = true;
    }

    private static string Truncate(string value, int length) => value.Length <= length ? value : value[..length];
}

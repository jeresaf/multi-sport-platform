using SportsPlatform.BuildingBlocks.Actions;
using SportsPlatform.BuildingBlocks.Auditing;
using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.Modules.Platform.Contracts.Audit;

namespace SportsPlatform.Host.Auditing;

/// <summary>
/// The automatic audit of audited actions (AR-19, docs/architecture/events-and-jobs.md). Before authorization it begins
/// the request's <see cref="AuditScope"/> with the endpoint's action, so the first module DbContext save commits the
/// entry with the change. After the response it writes, directly, the entry no save recorded: denials (401/403),
/// failures and actions that saved nothing (reads).
/// </summary>
internal sealed partial class AuditMiddleware(RequestDelegate next, ILogger<AuditMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var required = context.GetEndpoint()?.Metadata.GetOrderedMetadata<RequiresActionMetadata>() ?? [];
        if (required.Count != 1 || required[0].Action.Audit == AuditRequirement.None)
        {
            await next(context);
            return;
        }

        var audit = context.RequestServices.GetRequiredService<AuditScope>();
        audit.Begin(required[0].Action, AuditSource.Api);
        try
        {
            await next(context);
        }
        catch (Exception) when (audit.IsPending)
        {
            audit.SetErrorCode(CommonErrors.UnexpectedError.Code);
            await AppendAsync(context, audit, AuditOutcome.Failure);
            throw;
        }

        if (audit.IsPending)
        {
            await AppendAsync(context, audit, Outcome(context.Response.StatusCode));
        }
    }

    private static AuditOutcome Outcome(int status) => status switch
    {
        < 400 => AuditOutcome.Success,
        StatusCodes.Status401Unauthorized or StatusCodes.Status403Forbidden => AuditOutcome.Denied,
        _ => AuditOutcome.Failure,
    };

    private async Task AppendAsync(HttpContext context, AuditScope audit, AuditOutcome outcome)
    {
        var record = audit.CreateRecord(outcome);
        try
        {
            // Not the request's token: a client that hangs up after the action must not erase its audit.
            await context.RequestServices.GetRequiredService<IAuditLog>().AppendAsync(record, CancellationToken.None);
            audit.MarkRecorded();
        }
#pragma warning disable CA1031 // The response is already written; a lost entry is logged as an error instead.
        catch (Exception exception)
#pragma warning restore CA1031
        {
            LogAppendFailed(exception, record.ActionCode, record.Id, outcome);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Audit entry {EntryId} for action {ActionCode} ({Outcome}) could not be written")]
    private partial void LogAppendFailed(Exception exception, string actionCode, Guid entryId, AuditOutcome outcome);
}

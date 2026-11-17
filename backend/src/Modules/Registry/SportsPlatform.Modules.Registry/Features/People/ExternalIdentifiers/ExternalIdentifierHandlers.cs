using Microsoft.EntityFrameworkCore;
using Npgsql;
using SportsPlatform.BuildingBlocks.Api;
using SportsPlatform.BuildingBlocks.Auditing;
using SportsPlatform.BuildingBlocks.Privacy;
using SportsPlatform.BuildingBlocks.Tenancy;
using SportsPlatform.Modules.Registry.Domain.People;
using SportsPlatform.Modules.Registry.Infrastructure;
using SportsPlatform.Modules.Registry.Infrastructure.Configurations;

namespace SportsPlatform.Modules.Registry.Features.People.ExternalIdentifiers;

/// <summary>
/// Adds an external identifier to a person (S06 D10). An authority naming a national identity document is
/// <c>PERSON_EXTERNAL_ID_AUTHORITY_RESERVED</c>; an archived or deceased person takes none (D9). Holding
/// <c>registry.people:&lt;tenantId&gt;</c> (as creates do), it runs the exact check only: a value with this authority
/// that any person has or had, this one included, is <c>PERSON_EXTERNAL_ID_EXISTS</c>. Names and dates aren't compared
/// (owner, S06-T3a plan). The identifier has no foreign key to the person, so the person is marked changed (D11).
/// </summary>
public sealed class AddExternalIdentifierHandler(RegistryDbContext context, ITenantContext tenantContext, AuditScope audit)
{
    public async Task<FieldRefusal?> HandleAsync(
        Guid personId, AddExternalIdentifierRequest request, IfMatch ifMatch, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var tenantId = tenantContext.TenantId ?? throw new InvalidOperationException("A person is changed under the request's tenant.");
        var authority = request.Authority!;
        var value = request.Value!.Trim();
        audit.Describe(PeopleEndpoints.EntityType, personId);
        if (ExternalIdentifier.ReservedAuthorities.Contains(authority))
        {
            return new(RegistryErrors.ExternalIdAuthorityReserved, "authority");
        }

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        var (person, refusal) = await PersonChanges.LoadAsync(context, personId, request.Version, ifMatch, cancellationToken);
        if (person is null)
        {
            return refusal;
        }

        var accepts = person.EnsureAcceptsNewChildren();
        if (accepts.IsFailure)
        {
            return new(accepts.Error);
        }

        await PersonChanges.LockPeopleAsync(context, tenantId, cancellationToken);

        // Read after the lock: a new statement, so in READ COMMITTED it sees every identifier committed before it.
        if (await context.Set<ExternalIdentifier>().AnyAsync(
                identifier => identifier.EntityType == ExternalIdentifierEntityType.Person
                    && identifier.Authority == authority && identifier.Value == value,
                cancellationToken))
        {
            return new(RegistryErrors.ExternalIdExists, "value");
        }

        var identifier = ExternalIdentifier.ForPerson(person.Id, authority, value, request.ValidFrom);
        context.Add(identifier);
        context.MarkChanged(person);
        audit.Describe(metadata: new ExternalIdentifierAuditMetadata(identifier.Id, authority));
        try
        {
            var saved = await context.SaveChangesResultAsync(cancellationToken);
            if (saved.IsFailure)
            {
                return new(saved.Error);
            }
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: ExternalIdentifierConfiguration.ValueIndex,
        })
        {
            // Not reachable while every write of an identifier holds the lock; kept so a missed lock can't answer 500.
            return new(RegistryErrors.ExternalIdExists, "value");
        }

        // The audit counts as recorded once saved: a client hanging up now must not roll the change back unaudited.
        await transaction.CommitAsync(CancellationToken.None);
        return null;
    }
}

/// <summary>
/// Ends a person's external identifier (S06 D10), whatever the person's status (owner, Q45): <c>valid_to</c> is set,
/// the row stays and the value is never reused. The date defaults to the tenant's today and can't be after it or before
/// the identifier's start (owner, S06-T3b plan).
/// </summary>
public sealed class EndExternalIdentifierHandler(RegistryDbContext context, TenantToday tenantToday, AuditScope audit)
{
    public async Task<FieldRefusal?> HandleAsync(
        Guid personId, Guid identifierId, EndExternalIdentifierRequest request, IfMatch ifMatch, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim();
        audit.Describe(PeopleEndpoints.EntityType, personId, reason);

        var (person, refusal) = await PersonChanges.LoadAsync(context, personId, request.Version, ifMatch, cancellationToken);
        if (person is null)
        {
            return refusal;
        }

        var identifier = await context.Set<ExternalIdentifier>().SingleOrDefaultAsync(
            i => i.Id == identifierId && i.EntityType == ExternalIdentifierEntityType.Person && i.EntityId == personId,
            cancellationToken);
        if (identifier is null)
        {
            return new(RegistryErrors.ExternalIdNotFound);
        }

        audit.Describe(metadata: new ExternalIdentifierAuditMetadata(identifier.Id, identifier.Authority));
        var today = await tenantToday.GetAsync(cancellationToken);
        var ended = identifier.End(request.ValidTo ?? today, today);
        if (ended.IsFailure)
        {
            return new(ended.Error, ended.Error.Code == RegistryErrors.ExternalIdValidityInvalid.Code ? "validTo" : null);
        }

        context.MarkChanged(person);
        var saved = await context.SaveChangesResultAsync(cancellationToken);
        return saved.IsFailure ? new(saved.Error) : null;
    }
}

/// <summary>The audit metadata of an identifier change: the identifier by ID and its authority, never its value.</summary>
public sealed record ExternalIdentifierAuditMetadata(
    [property: PersonalData(PersonalDataClass.Internal)] Guid IdentifierId,
    [property: PersonalData(PersonalDataClass.Internal)] string Authority);

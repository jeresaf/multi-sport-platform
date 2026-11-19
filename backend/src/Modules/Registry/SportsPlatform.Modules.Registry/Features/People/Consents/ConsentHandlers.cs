using Microsoft.Extensions.Logging;
using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Api;
using SportsPlatform.BuildingBlocks.Auditing;
using SportsPlatform.BuildingBlocks.Privacy;
using SportsPlatform.BuildingBlocks.Results;
using SportsPlatform.Modules.Platform.Contracts.Media;
using SportsPlatform.Modules.Registry.Domain.People;
using SportsPlatform.Modules.Registry.Domain.People.Consents;
using SportsPlatform.Modules.Registry.Infrastructure;
using SportsPlatform.Modules.Tenancy.Contracts.Entitlements;

namespace SportsPlatform.Modules.Registry.Features.People.Consents;

/// <summary>
/// Records a guardian's consent (S06 D14) for a person who isn't an adult on the tenant's date: refused for an adult
/// (<c>PERSON_CONSENT_NOT_NEEDED</c>) and on an archived or deceased person (D9). The date given can't be after the
/// tenant's date or before the date of birth. The audit entry names the consent and its scopes, never the guardian.
/// </summary>
public sealed class RecordConsentHandler(
    RegistryDbContext context, TenantToday tenantToday, ICurrentActor actor, AuditScope audit, TimeProvider timeProvider)
{
    public async Task<FieldRefusal?> HandleAsync(
        Guid personId, RecordConsentRequest request, IfMatch ifMatch, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        audit.Describe(PeopleEndpoints.EntityType, personId);

        var (person, refusal) = await PersonChanges.LoadAsync(context, personId, request.Version, ifMatch, cancellationToken);
        if (person is null)
        {
            return refusal;
        }

        var today = await tenantToday.GetAsync(cancellationToken);
        var givenOn = request.GivenOn!.Value;
        if (givenOn > today)
        {
            return new(CommonErrors.InvalidRequest, "givenOn", "'givenOn' can't be after today.");
        }

        if (person.DateOfBirth is { } born && givenOn < born)
        {
            return new(CommonErrors.InvalidRequest, "givenOn", "'givenOn' can't be before the person's date of birth.");
        }

        var details = new GuardianConsentDetails(
            request.GuardianName!, request.Relationship!.Value, request.Contact!, [.. request.Scopes!], givenOn);
        var recorded = person.RecordConsent(details, Age.StatusOn(person.DateOfBirth, today), actor, timeProvider.GetUtcNow());
        if (recorded.IsFailure)
        {
            return new(recorded.Error);
        }

        audit.Describe(metadata: new ConsentAuditMetadata(recorded.Value.Id, recorded.Value.Scopes, null));
        var saved = await context.SaveChangesResultAsync(cancellationToken);
        return saved.IsFailure ? new(saved.Error) : null;
    }
}

/// <summary>
/// Withdraws scopes of an active consent with a reason (S06 D14), in any status and at any age: the record is closed
/// and, when scopes remain, a successor with them is recorded. A scope the consent doesn't have is refused on
/// <c>scopes</c>.
/// </summary>
public sealed class WithdrawConsentHandler(RegistryDbContext context, ICurrentActor actor, AuditScope audit, TimeProvider timeProvider)
{
    public async Task<FieldRefusal?> HandleAsync(
        Guid personId, Guid consentId, WithdrawConsentRequest request, IfMatch ifMatch, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var reason = request.Reason!.Trim();
        var scopes = request.Scopes!.Order().ToList();
        audit.Describe(PeopleEndpoints.EntityType, personId, reason, new ConsentAuditMetadata(consentId, scopes, null));

        var (person, refusal) = await PersonChanges.LoadAsync(
            context, personId, request.Version, ifMatch, cancellationToken, withConsents: true);
        if (person is null)
        {
            return refusal;
        }

        var found = person.FindActiveConsent(consentId);
        if (found.IsFailure)
        {
            return new(found.Error);
        }

        if (scopes.Any(scope => !found.Value.Scopes.Contains(scope)))
        {
            return new(CommonErrors.InvalidRequest, "scopes", "'scopes' must be scopes the consent gives.");
        }

        var withdrawn = person.WithdrawConsent(consentId, scopes, reason, actor, timeProvider.GetUtcNow());
        if (withdrawn.IsFailure)
        {
            return new(withdrawn.Error);
        }

        audit.Describe(metadata: new ConsentAuditMetadata(consentId, scopes, withdrawn.Value?.Id));
        var saved = await context.SaveChangesResultAsync(cancellationToken);
        return saved.IsFailure ? new(saved.Error) : null;
    }
}

/// <summary>
/// Attaches the evidence of an active consent (S06 D19): a PDF, JPEG or PNG up to 10 MB, checked by Platform by its
/// bytes. Refused on a withdrawn consent, on an archived or deceased person (D9) and at the <c>STORAGE_GB</c> limit
/// (D20), all before storing. Evidence already attached is replaced (a wrong file, owner, S06-T6a plan); the old asset
/// is marked <c>REMOVED</c> after the save unless another record still names it (a partial withdrawal's successor keeps
/// the evidence).
/// </summary>
public sealed class UploadConsentEvidenceHandler(
    RegistryDbContext context,
    IMediaAssets media,
    IEntitlements entitlements,
    AuditScope audit,
    ILogger<UploadConsentEvidenceHandler> logger)
{
    public async Task<FieldRefusal?> HandleAsync(
        Guid personId, Guid consentId, Stream file, IfMatch ifMatch, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(file);
        audit.Describe(PeopleEndpoints.EntityType, personId);

        var (person, refusal) = await PersonChanges.LoadAsync(
            context, personId, version: null, ifMatch, cancellationToken, withConsents: true);
        if (person is null)
        {
            return refusal;
        }

        // Everything the attach would refuse, checked before a file is stored.
        var accepts = person.EnsureAcceptsNewChildren();
        if (accepts.IsFailure)
        {
            return new(accepts.Error);
        }

        var found = person.FindActiveConsent(consentId);
        if (found.IsFailure)
        {
            return new(found.Error);
        }

        if (await PersonMedia.CheckStorageAsync(media, entitlements, cancellationToken) is { } overQuota)
        {
            return overQuota;
        }

        var stored = await media.StoreAsync(file, MediaPurpose.ConsentEvidence, cancellationToken);
        if (stored.IsFailure)
        {
            return new(stored.Error, PersonMedia.FileField);
        }

        var attached = person.AttachConsentEvidence(consentId, stored.Value);
        if (attached.IsFailure)
        {
            return new(attached.Error);
        }

        audit.Describe(metadata: new ConsentEvidenceAuditMetadata(consentId, stored.Value, attached.Value));
        var saved = await context.SaveChangesResultAsync(cancellationToken);
        if (saved.IsFailure)
        {
            return new(saved.Error);
        }

        if (attached.Value is { } previous && !person.Consents.Any(c => c.EvidenceAssetId == previous))
        {
            await PersonMedia.RemoveUnreferencedAsync(media, logger, previous);
        }

        return null;
    }
}

/// <summary>
/// The audit metadata of a consent change: the consent by ID, its scopes (recorded or withdrawn) and, for a partial
/// withdrawal, the successor recorded with the remaining scopes. Never the guardian's name, relationship or contact
/// (<c>PRIVATE</c>, S06 D3).
/// </summary>
public sealed record ConsentAuditMetadata(
    [property: PersonalData(PersonalDataClass.Internal)] Guid ConsentId,
    [property: PersonalData(PersonalDataClass.Internal)] IReadOnlyList<ConsentScope> Scopes,
    [property: PersonalData(PersonalDataClass.Internal)] Guid? SuccessorConsentId);

/// <summary>
/// The audit metadata of an evidence upload: the consent, the new and the replaced asset by ID. An ID opens nothing: the
/// file is served only through the person's private read.
/// </summary>
public sealed record ConsentEvidenceAuditMetadata(
    [property: PersonalData(PersonalDataClass.Internal)] Guid ConsentId,
    [property: PersonalData(PersonalDataClass.Internal)] Guid AssetId,
    [property: PersonalData(PersonalDataClass.Internal)] Guid? ReplacedAssetId);

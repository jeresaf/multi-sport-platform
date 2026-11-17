using SportsPlatform.BuildingBlocks.Privacy;
using SportsPlatform.Modules.Registry.Domain.People.Duplicates;

namespace SportsPlatform.Modules.Registry.Features.People;

/// <summary>
/// The audit metadata of a create or an update that ran the duplicate check (S06 D6, D8): the people it found, by ID,
/// with their confidence. No names, no dates; the reason to proceed is the entry's reason.
/// </summary>
public sealed record DuplicateReviewAuditMetadata(
    [property: PersonalData(PersonalDataClass.Internal)] IReadOnlyList<DuplicateCandidateAudit> Candidates);

public sealed record DuplicateCandidateAudit(
    [property: PersonalData(PersonalDataClass.Internal)] Guid PersonId,
    [property: PersonalData(PersonalDataClass.Internal)] DuplicateConfidence Confidence);

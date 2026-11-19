namespace SportsPlatform.Modules.Registry.Domain.People.Consents;

/// <summary>What a guardian consents to (S06 D14, ADR-0015).</summary>
public enum ConsentScope
{
    /// <summary>Processing the person's data; its wording says where data is stored and names the sub-processors.</summary>
    DataProcessing,

    /// <summary>A public profile: the display name on public sites (S06 D15, D18).</summary>
    PublicProfile,

    /// <summary>The photo on the public profile (S06 D18).</summary>
    Photo,
}

/// <summary>Who the guardian is to the person (S06 D14).</summary>
public enum GuardianRelationship
{
    Mother,
    Father,
    Guardian,
    Other,
}

/// <summary>A consent record's states (S06 D14, Q39; docs/domain/state-machines.md, Registry).</summary>
public enum ConsentStatus
{
    Active,
    Withdrawn,
}

/// <summary>What a guardian gave, as the use case has validated it (S06 D14).</summary>
/// <param name="GuardianName">1–200 characters.</param>
/// <param name="Relationship">Who the guardian is to the person.</param>
/// <param name="Contact">A phone number or an email address, free text, 1–200 characters.</param>
/// <param name="Scopes">One to three distinct scopes.</param>
/// <param name="GivenOn">The date the guardian gave it.</param>
public sealed record GuardianConsentDetails(
    string GuardianName,
    GuardianRelationship Relationship,
    string Contact,
    IReadOnlyCollection<ConsentScope> Scopes,
    DateOnly GivenOn);

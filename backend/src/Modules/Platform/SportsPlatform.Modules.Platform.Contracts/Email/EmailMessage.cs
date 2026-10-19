using SportsPlatform.BuildingBlocks.Privacy;

namespace SportsPlatform.Modules.Platform.Contracts.Email;

/// <summary>
/// One email to one recipient, from the configured sender (<c>Email:From</c>). Plain text is required; the HTML
/// part is optional and should say the same thing (Q7: English only).
/// </summary>
/// <param name="To">The recipient's address. Never log it (ADR-0015).</param>
/// <param name="ToName">The recipient's display name, if known.</param>
/// <param name="Subject">One line, without personal data beyond what the recipient already knows.</param>
/// <param name="TextBody">The plain-text body, always sent.</param>
/// <param name="HtmlBody">An HTML alternative of the same text, if any.</param>
public sealed record EmailMessage(
    [property: PersonalData(PersonalDataClass.Private)] string To,
    [property: PersonalData(PersonalDataClass.Internal)] string? ToName,
    string Subject,
    string TextBody,
    string? HtmlBody = null);

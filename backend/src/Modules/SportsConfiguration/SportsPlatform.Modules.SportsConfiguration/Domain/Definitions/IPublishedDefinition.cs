namespace SportsPlatform.Modules.SportsConfiguration.Domain.Definitions;

/// <summary>
/// A stored configuration row (sport definition, format, ruleset template): its code and version, unique together, and
/// the hash of its canonical content. Rows are immutable: a change is a new version (S05 D4).
/// </summary>
public interface IPublishedDefinition
{
    /// <summary>The variant of a sport definition, the code of a format or template.</summary>
    string Code { get; }

    int Version { get; }

    /// <summary><c>sha256:</c> and the hex SHA-256 of the content in RFC 8785 canonical JSON.</summary>
    string ContentHash { get; }
}

/// <summary>Only published definitions exist until the in-app editors (ADR-0006).</summary>
public enum DefinitionStatus
{
    Published,
}

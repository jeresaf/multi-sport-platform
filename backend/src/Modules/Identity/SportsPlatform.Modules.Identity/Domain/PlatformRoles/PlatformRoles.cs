namespace SportsPlatform.Modules.Identity.Domain.PlatformRoles;

/// <summary>
/// The platform roles (docs/architecture/authorization.md). Their actions are defined with the other roles in S03-T3
/// (D9); T1 only grants <see cref="Admin"/> from the CLI.
/// </summary>
public static class PlatformRoles
{
    public const string Admin = "PLATFORM_ADMIN";

    public const string Support = "PLATFORM_SUPPORT";

    public static IReadOnlyList<string> All { get; } = [Admin, Support];
}

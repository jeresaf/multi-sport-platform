using SportsPlatform.BuildingBlocks.Results;

namespace SportsPlatform.BuildingBlocks.Actions;

/// <summary>Authorization failures shared by every module.</summary>
public static class AuthorizationErrors
{
    public static readonly Error Forbidden = new(
        "AUTH_FORBIDDEN", "You are not allowed to perform this action.", ErrorKind.Forbidden);
}

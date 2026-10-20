namespace SportsPlatform.BuildingBlocks.Results;

/// <summary>Category of an expected failure; the API layer maps each kind to an HTTP status.</summary>
public enum ErrorKind
{
    Validation,
    Unauthorized,
    Forbidden,
    NotFound,
    MethodNotAllowed,
    Conflict,

    /// <summary>The resource existed and is gone for good (410), such as an expired invitation.</summary>
    Gone,
    PayloadTooLarge,
    UnsupportedMediaType,
    Unprocessable,
    TooManyRequests,
    Unexpected,
    Unavailable,
}

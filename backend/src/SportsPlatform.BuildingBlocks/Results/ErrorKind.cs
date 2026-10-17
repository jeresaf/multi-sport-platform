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
    PayloadTooLarge,
    UnsupportedMediaType,
    Unprocessable,
    TooManyRequests,
    Unexpected,
    Unavailable,
}

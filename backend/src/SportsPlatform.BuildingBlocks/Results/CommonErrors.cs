namespace SportsPlatform.BuildingBlocks.Results;

/// <summary>Failures raised by the HTTP pipeline itself rather than by a module.</summary>
public static class CommonErrors
{
    public static readonly Error MalformedRequest = new(
        "VALIDATION_MALFORMED_REQUEST", "The request could not be read.", ErrorKind.Validation);

    public static readonly Error UnsupportedMediaType = new(
        "VALIDATION_UNSUPPORTED_MEDIA_TYPE", "The request body must be JSON.", ErrorKind.UnsupportedMediaType);

    public static readonly Error RouteNotFound = new(
        "PLATFORM_ROUTE_NOT_FOUND", "No endpoint matches this path.", ErrorKind.NotFound);

    public static readonly Error MethodNotAllowed = new(
        "PLATFORM_METHOD_NOT_ALLOWED", "This endpoint does not support the HTTP method.", ErrorKind.MethodNotAllowed);

    public static readonly Error UnexpectedError = new(
        "PLATFORM_UNEXPECTED_ERROR", "An unexpected error occurred. Quote the correlation ID when reporting it.", ErrorKind.Unexpected);
}

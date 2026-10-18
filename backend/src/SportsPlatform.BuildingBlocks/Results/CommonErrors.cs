namespace SportsPlatform.BuildingBlocks.Results;

/// <summary>
/// Failures raised by the HTTP pipeline itself rather than by a module, and request-shape failures every module's
/// endpoints share.
/// </summary>
public static class CommonErrors
{
    public static readonly Error MalformedRequest = new(
        "VALIDATION_MALFORMED_REQUEST", "The request could not be read.", ErrorKind.Validation);

    public static readonly Error RequestTooLarge = new(
        "VALIDATION_REQUEST_TOO_LARGE", "The request body is too large.", ErrorKind.PayloadTooLarge);

    public static readonly Error UnsupportedMediaType = new(
        "VALIDATION_UNSUPPORTED_MEDIA_TYPE", "The request body must be JSON.", ErrorKind.UnsupportedMediaType);

    /// <summary>A query parameter has a value the endpoint doesn't accept; the detail names the parameter.</summary>
    public static readonly Error InvalidQuery = new(
        "VALIDATION_INVALID_QUERY", "A query parameter is not valid.", ErrorKind.Validation);

    /// <summary>
    /// A request body was read but a field has a value the endpoint doesn't accept; <c>errors</c> names each field.
    /// </summary>
    public static readonly Error InvalidRequest = new(
        "VALIDATION_INVALID_REQUEST", "A field of the request is not valid.", ErrorKind.Validation);

    /// <summary>An action whose audit is <c>REQUIRED_WITH_REASON</c> was sent without a usable reason.</summary>
    public static readonly Error ReasonRequired = new(
        "VALIDATION_REASON_REQUIRED", "This action needs a reason.", ErrorKind.Validation);

    public static readonly Error RouteNotFound = new(
        "PLATFORM_ROUTE_NOT_FOUND", "No endpoint matches this path.", ErrorKind.NotFound);

    public static readonly Error MethodNotAllowed = new(
        "PLATFORM_METHOD_NOT_ALLOWED", "This endpoint does not support the HTTP method.", ErrorKind.MethodNotAllowed);

    public static readonly Error UnexpectedError = new(
        "PLATFORM_UNEXPECTED_ERROR", "An unexpected error occurred. Quote the correlation ID when reporting it.", ErrorKind.Unexpected);
}

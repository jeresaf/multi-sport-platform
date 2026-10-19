using FluentValidation;
using SportsPlatform.BuildingBlocks.Api;

namespace SportsPlatform.Host.Api;

/// <summary>
/// Fail closed (S01 #22 N1): every endpoint with <c>.ValidateRequest&lt;T&gt;()</c> has an <c>IValidator&lt;T&gt;</c>
/// registered, so a forgotten registration stops the Host when it is built instead of failing the first request.
/// </summary>
public static class RequestValidatorRules
{
    public static IReadOnlyList<string> Validate(IEnumerable<Endpoint> endpoints, IServiceProviderIsService services)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentNullException.ThrowIfNull(services);

        return endpoints
            .SelectMany(endpoint => endpoint.Metadata.GetOrderedMetadata<ValidatesRequestMetadata>()
                .Where(metadata => !services.IsService(typeof(IValidator<>).MakeGenericType(metadata.RequestType)))
                .Select(metadata => $"{Describe(endpoint)} validates {metadata.RequestType.Name}, but no " +
                    $"IValidator<{metadata.RequestType.Name}> is registered."))
            .Distinct()
            .ToList();
    }

    /// <summary>Throws when any endpoint validates a request type without a validator, listing every one.</summary>
    public static void EnsureRequestValidators(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var violations = Validate(
            ((IEndpointRouteBuilder)app).DataSources.SelectMany(source => source.Endpoints),
            app.Services.GetRequiredService<IServiceProviderIsService>());
        if (violations.Count > 0)
        {
            throw new InvalidOperationException("Every validated request needs a registered validator:\n" + string.Join("\n", violations));
        }
    }

    private static string Describe(Endpoint endpoint) =>
        endpoint is RouteEndpoint route ? route.RoutePattern.RawText ?? route.DisplayName ?? "An endpoint" : endpoint.DisplayName ?? "An endpoint";
}

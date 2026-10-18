using System.Globalization;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using Scalar.AspNetCore;
using SportsPlatform.BuildingBlocks.Api;

namespace SportsPlatform.Host.OpenApi;

/// <summary>
/// The OpenAPI document (docs/architecture/api-conventions.md, "OpenAPI and clients"). It is generated in every
/// environment so the CLI can export it (<c>contracts/openapi.json</c>, the source of the TypeScript client); the
/// document and Scalar are served over HTTP in Development only.
/// </summary>
public static class ApiDocumentation
{
    public const string DocumentName = "v1";

    public const string DocumentPath = "/openapi/{documentName}.json";

    public const string ScalarPath = "/scalar";

    public const string ProblemDetailsSchema = "ProblemDetails";

    public static IServiceCollection AddApiDocumentation(this IServiceCollection services)
    {
        services.AddOpenApi(DocumentName, options =>
        {
            options.OpenApiVersion = OpenApiSpecVersion.OpenApi3_1;
            options.AddDocumentTransformer(DescribeDocumentAsync);
            options.AddSchemaTransformer(DescribeProblemDetails);
            options.AddOperationTransformer(ListErrorCodes);
        });
        return services;
    }

    public static IEndpointRouteBuilder MapApiDocumentation(this IEndpointRouteBuilder endpoints, IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);
        if (environment.IsDevelopment())
        {
            endpoints.MapOpenApi(DocumentPath).AllowPublic();
            endpoints.MapScalarApiReference(ScalarPath).AllowPublic();
        }

        return endpoints;
    }

    /// <summary>The document as the CLI writes it: OpenAPI 3.1 JSON, LF line endings, trailing newline.</summary>
    public static async Task<string> ExportAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        var provider = services.GetRequiredKeyedService<IOpenApiDocumentProvider>(DocumentName);
        var document = await provider.GetOpenApiDocumentAsync(cancellationToken);
        var json = await document.SerializeAsJsonAsync(OpenApiSpecVersion.OpenApi3_1, cancellationToken);
        return json.ReplaceLineEndings("\n").TrimEnd('\n') + "\n";
    }

    private static async Task DescribeDocumentAsync(
        OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        document.Info = new OpenApiInfo
        {
            Title = "MSP API",
            Version = DocumentName,
            Description = "Multi-sport competition platform. Errors are Problem Details with a stable `code` " +
                "(contracts/errors.json) and the request's `correlationId`.",
        };

        // Servers come from the request that asked for the document; the committed file must not depend on it.
        document.Servers = null;

        // Clients need the error shape even before an endpoint declares an error response.
        if (document.Components?.Schemas?.ContainsKey(ProblemDetailsSchema) != true)
        {
            var schema = await context.GetOrCreateSchemaAsync(typeof(ProblemDetails), cancellationToken: cancellationToken);
            document.AddComponent(ProblemDetailsSchema, schema);
        }
    }

    // `code` and `correlationId` are on every error response (ApiConventions); the framework types (ProblemDetails and
    // its validation subtypes) don't know them.
    private static Task DescribeProblemDetails(
        OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
    {
        if (!typeof(ProblemDetails).IsAssignableFrom(context.JsonTypeInfo.Type))
        {
            return Task.CompletedTask;
        }

        schema.Properties ??= new Dictionary<string, IOpenApiSchema>();
        schema.Properties[ProblemFields.Code] = new OpenApiSchema
        {
            Type = JsonSchemaType.String,
            Description = "Stable error code, `<AREA>_<CONDITION>` (contracts/errors.json). Clients branch on it.",
        };
        schema.Properties[ProblemFields.CorrelationId] = new OpenApiSchema
        {
            Type = JsonSchemaType.String,
            Description = "The request's correlation ID (also the `X-Correlation-Id` response header).",
        };
        // HttpValidationProblemDetails has its own `errors` (a dictionary); ours is the field list of RequestValidationExtensions.
        if (!schema.Properties.ContainsKey(ProblemFields.Errors))
        {
            schema.Properties[ProblemFields.Errors] = new OpenApiSchema
            {
                Type = JsonSchemaType.Array,
                Description = "Validation failures, one per field: `field` (camelCase) and `code`.",
                Items = new OpenApiSchema
                {
                    Type = JsonSchemaType.Object,
                    Properties = new Dictionary<string, IOpenApiSchema>
                    {
                        ["field"] = new OpenApiSchema { Type = JsonSchemaType.String },
                        ["code"] = new OpenApiSchema { Type = JsonSchemaType.String },
                    },
                    Required = new HashSet<string> { "field", "code" },
                },
            };
        }

        schema.Required ??= new HashSet<string>();
        schema.Required.Add(ProblemFields.Code);
        schema.Required.Add(ProblemFields.CorrelationId);
        return Task.CompletedTask;
    }

    // ProducesErrors adds one Problem Details response per status; name the codes each status can carry.
    private static Task ListErrorCodes(
        OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        // Every declaration counts: ProducesErrors may be called on a route group and again on the endpoint.
        var declared = context.Description.ActionDescriptor.EndpointMetadata.OfType<DeclaredErrorsMetadata>()
            .SelectMany(metadata => metadata.Errors)
            .ToList();
        if (declared.Count == 0 || operation.Responses is null)
        {
            return Task.CompletedTask;
        }

        foreach (var group in declared.GroupBy(error => error.Kind.ToStatusCode()))
        {
            var status = group.Key.ToString(CultureInfo.InvariantCulture);
            if (operation.Responses.TryGetValue(status, out var response) && response is OpenApiResponse openApiResponse)
            {
                var codes = group.Select(error => error.Code).Distinct().Order(StringComparer.Ordinal).ToList();
                openApiResponse.Description = "Problem Details; `code`: " + string.Join(", ", codes.Select(code => $"`{code}`"));
                openApiResponse.Extensions ??= new Dictionary<string, IOpenApiExtension>();
                openApiResponse.Extensions["x-error-codes"] = new JsonNodeExtension(new JsonArray([.. codes.Select(code => JsonValue.Create(code))]));
            }
        }

        return Task.CompletedTask;
    }
}

namespace SportsPlatform.BuildingBlocks.Api;

/// <summary>Problem Details extension members every error response carries (docs/architecture/api-conventions.md).</summary>
public static class ProblemFields
{
    public const string Code = "code";

    public const string CorrelationId = "correlationId";
}

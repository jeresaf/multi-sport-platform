namespace SportsPlatform.BuildingBlocks.Actions;

/// <summary>Endpoint metadata: the endpoint requires this action (authorized by the evaluator).</summary>
public sealed record RequiresActionMetadata(ActionDefinition Action);

/// <summary>Endpoint metadata: the endpoint is deliberately public and declares no action.</summary>
public sealed record AllowPublicMetadata
{
    public static AllowPublicMetadata Instance { get; } = new();

    private AllowPublicMetadata()
    {
    }
}

/// <summary>
/// Endpoint metadata: a platform endpoint that works on one tenant named by a route value; the Host runs it under
/// that tenant (S02 D2).
/// </summary>
public sealed record RunsUnderRouteTenantMetadata(string RouteValueName);

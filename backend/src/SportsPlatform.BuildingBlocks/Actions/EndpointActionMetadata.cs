namespace SportsPlatform.BuildingBlocks.Actions;

/// <summary>Endpoint metadata: the endpoint requires this action (authorized by the evaluator).</summary>
public sealed record RequiresActionMetadata(ActionCode Action);

/// <summary>Endpoint metadata: the endpoint is deliberately public and declares no action.</summary>
public sealed record AllowPublicMetadata
{
    public static AllowPublicMetadata Instance { get; } = new();

    private AllowPublicMetadata()
    {
    }
}

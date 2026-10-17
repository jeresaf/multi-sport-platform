using System.Collections.Concurrent;
using SportsPlatform.BuildingBlocks.Actions;
using SportsPlatform.BuildingBlocks.Results;

namespace SportsPlatform.IntegrationTests.Infrastructure;

/// <summary>Denies like the Host's default until a test allows actions; records every request it is asked about.</summary>
public sealed class TestAuthorizationEvaluator : IAuthorizationEvaluator
{
    private readonly ConcurrentDictionary<ActionCode, bool> _allowed = new();
    private volatile bool _allowAll;

    public ConcurrentQueue<AuthorizationRequest> Requests { get; } = new();

    public void Allow(params ActionDefinition[] actions)
    {
        foreach (var action in actions)
        {
            _allowed[action.Code] = true;
        }
    }

    public void AllowAll() => _allowAll = true;

    public ValueTask<Result> AuthorizeAsync(AuthorizationRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        Requests.Enqueue(request);
        return ValueTask.FromResult(_allowAll || _allowed.ContainsKey(request.Action.Code)
            ? Result.Success()
            : Result.Failure(AuthorizationErrors.Forbidden));
    }
}

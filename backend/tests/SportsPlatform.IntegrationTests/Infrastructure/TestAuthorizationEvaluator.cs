using System.Collections.Concurrent;
using SportsPlatform.BuildingBlocks.Actions;
using SportsPlatform.BuildingBlocks.Results;

namespace SportsPlatform.IntegrationTests.Infrastructure;

/// <summary>
/// Allows account actions as Identity's evaluator does (the pipeline has already refused anonymous callers) and denies
/// everything else until a test allows it; records every request it is asked about.
/// </summary>
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

    /// <summary>Back to denying everything, with no recorded requests (each database test starts here).</summary>
    public void Reset()
    {
        _allowed.Clear();
        _allowAll = false;
        Requests.Clear();
    }

    public ValueTask<Result> AuthorizeAsync(AuthorizationRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        Requests.Enqueue(request);
        return ValueTask.FromResult(_allowAll || request.Action.Scope == ActionScope.Account || _allowed.ContainsKey(request.Action.Code)
            ? Result.Success()
            : Result.Failure(AuthorizationErrors.Forbidden));
    }
}

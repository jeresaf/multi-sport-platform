using System.Collections.Concurrent;
using SportsPlatform.BuildingBlocks.Actions;
using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Results;

namespace SportsPlatform.IntegrationTests.Infrastructure;

/// <summary>
/// Decides account actions as Identity's evaluator does (S03 D10) and denies everything else until a test allows it;
/// records every request it is asked about. <see cref="UseIdentityRoles"/> hands the app's requests to Identity's real
/// evaluator instead (roles from the database), until <see cref="Reset"/>.
/// </summary>
public sealed class TestAuthorizationEvaluator : IAuthorizationEvaluator
{
    private readonly ConcurrentDictionary<ActionCode, bool> _allowed = new();
    private volatile bool _allowAll;
    private volatile bool _usesIdentityRoles;

    public ConcurrentQueue<AuthorizationRequest> Requests { get; } = new();

    /// <summary>What Identity's evaluator decided while <see cref="UseIdentityRoles"/> is on (the authorization matrix reads it).</summary>
    public ConcurrentQueue<(AuthorizationRequest Request, bool Allowed)> Decisions { get; } = new();

    public void Allow(params ActionDefinition[] actions)
    {
        foreach (var action in actions)
        {
            _allowed[action.Code] = true;
        }
    }

    public void AllowAll() => _allowAll = true;

    /// <summary>Requests are decided by <c>IdentityAuthorizationEvaluator</c>, from the roles users hold.</summary>
    public void UseIdentityRoles() => _usesIdentityRoles = true;

    public bool UsesIdentityRoles => _usesIdentityRoles;

    /// <summary>Back to denying everything, with no recorded requests (each database test starts here).</summary>
    public void Reset()
    {
        _allowed.Clear();
        _allowAll = false;
        _usesIdentityRoles = false;
        Requests.Clear();
        Decisions.Clear();
    }

    /// <summary>Identity's evaluator, each decision recorded in <see cref="Decisions"/>.</summary>
    public IAuthorizationEvaluator Recording(IAuthorizationEvaluator identity) => new RecordingEvaluator(identity, Decisions);

    public ValueTask<Result> AuthorizeAsync(AuthorizationRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        Requests.Enqueue(request);
        var account = request.Action.Scope == ActionScope.Account
            && (request.Actor.Kind != ActorKind.Anonymous || request.Action.AllowsAnonymous);
        return ValueTask.FromResult(_allowAll || account || _allowed.ContainsKey(request.Action.Code)
            ? Result.Success()
            : Result.Failure(AuthorizationErrors.Forbidden));
    }

    private sealed class RecordingEvaluator(
        IAuthorizationEvaluator inner, ConcurrentQueue<(AuthorizationRequest Request, bool Allowed)> decisions) : IAuthorizationEvaluator
    {
        public async ValueTask<Result> AuthorizeAsync(AuthorizationRequest request, CancellationToken cancellationToken)
        {
            var result = await inner.AuthorizeAsync(request, cancellationToken);
            decisions.Enqueue((request, result.IsSuccess));
            return result;
        }
    }
}

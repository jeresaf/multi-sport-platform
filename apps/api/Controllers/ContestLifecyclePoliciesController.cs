using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SportsPlatform.Api.Authorization;
using SportsPlatform.Infrastructure.Services;
using SportsPlatform.Modules.Contests.Entities;

namespace SportsPlatform.Api.Controllers;

[ApiController]
[Route("api/contest-lifecycle/policies")]
[Authorize]
public sealed class ContestLifecyclePoliciesController : ControllerBase
{
    private readonly IContestLifecycleService _lifecycle;

    public ContestLifecyclePoliciesController(IContestLifecycleService lifecycle)
    {
        _lifecycle = lifecycle;
    }

    [HttpGet("{sportId:guid}")]
    [RequirePermission("contests.read")]
    public async Task<IActionResult> Get(
        Guid sportId,
        [FromQuery] Guid tenantId,
        CancellationToken cancellationToken)
    {
        if (!User.CanAccessTenant(tenantId)) return Forbid();
        var policy = await _lifecycle.GetPolicyAsync(tenantId, sportId, cancellationToken);
        return policy is null ? NotFound() : Ok(policy);
    }

    [HttpPut("{sportId:guid}")]
    [RequirePermission("contests.manage")]
    public async Task<IActionResult> Configure(
        Guid sportId,
        [FromBody] ConfigureContestLifecyclePolicyRequest request,
        CancellationToken cancellationToken)
    {
        if (!User.CanAccessTenant(request.TenantId)) return Forbid();
        var actorId = User.UserId();
        if (!actorId.HasValue) return Unauthorized();
        try
        {
            var transitions = request.Transitions.Select(item =>
            {
                if (!Enum.TryParse<ContestStatus>(item.From, true, out var from)
                    || !Enum.TryParse<ContestStatus>(item.To, true, out var to))
                    throw new ArgumentException($"Invalid transition {item.From} -> {item.To}.");
                return new ContestLifecycleTransition(from, to);
            });
            return Ok(await _lifecycle.ConfigurePolicyAsync(
                request.TenantId, sportId, transitions, actorId.Value, cancellationToken));
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
        catch (KeyNotFoundException exception)
        {
            return NotFound(new { error = exception.Message });
        }
    }
}

public sealed record ConfigureContestLifecyclePolicyRequest(
    Guid TenantId,
    IReadOnlyCollection<ContestLifecycleTransitionRequest> Transitions);
public sealed record ContestLifecycleTransitionRequest(string From, string To);

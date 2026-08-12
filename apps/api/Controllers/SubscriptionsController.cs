using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SportsPlatform.Infrastructure.Services;
using SportsPlatform.Api.Authorization;
using SportsPlatform.Modules.Audit;
using System.Text.Json;

namespace SportsPlatform.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SubscriptionsController : ControllerBase
{
    private readonly ISubscriptionsService _subscriptionsService;
    private readonly IFeatureEntitlementService _featureEntitlements;
    private readonly IUsageLimitsService _usageLimits;
    private readonly IAuditService _audit;

    public SubscriptionsController(
        ISubscriptionsService subscriptionsService,
        IFeatureEntitlementService featureEntitlements,
        IUsageLimitsService usageLimits,
        IAuditService audit)
    {
        _subscriptionsService = subscriptionsService;
        _featureEntitlements = featureEntitlements;
        _usageLimits = usageLimits;
        _audit = audit;
    }

    [HttpGet("active/{tenantId:guid}")]
    [RequirePermission("subscriptions.read")]
    public async Task<IActionResult> GetActive(Guid tenantId)
    {
        if (!User.CanAccessTenant(tenantId)) return Forbid();
        var subscription = await _subscriptionsService.GetActiveSubscriptionAsync(tenantId);
        if (subscription == null) return NotFound();
        return Ok(subscription);
    }

    [HttpGet("history/{tenantId:guid}")]
    [RequirePermission("subscriptions.read")]
    public async Task<IActionResult> GetHistory(Guid tenantId)
    {
        if (!User.CanAccessTenant(tenantId)) return Forbid();
        var subscriptions = await _subscriptionsService.GetSubscriptionHistoryAsync(tenantId);
        return Ok(subscriptions);
    }

    [HttpGet("entitlements/{tenantId:guid}")]
    [RequirePermission("subscriptions.read")]
    public async Task<IActionResult> GetEntitlements(Guid tenantId, CancellationToken cancellationToken)
    {
        if (!User.CanAccessTenant(tenantId)) return Forbid();
        return Ok(await _featureEntitlements.GetFeatureFlagsAsync(tenantId, cancellationToken));
    }

    [HttpGet("usage/{tenantId:guid}")]
    [RequirePermission("subscriptions.read")]
    public async Task<IActionResult> GetUsage(Guid tenantId, CancellationToken cancellationToken)
    {
        if (!User.CanAccessTenant(tenantId)) return Forbid();
        return Ok(await _usageLimits.GetUsageSummaryAsync(tenantId, cancellationToken));
    }

    [HttpPut("{id:guid}/entitlements")]
    [RequirePermission("subscriptions.manage")]
    public async Task<IActionResult> SetEntitlements(
        Guid id,
        [FromBody] Dictionary<string, bool> entitlements,
        CancellationToken cancellationToken)
    {
        var subscription = await _subscriptionsService.GetByIdAsync(id, cancellationToken);
        if (subscription is null) return NotFound();
        if (!User.CanAccessTenant(subscription.TenantId)) return Forbid();
        var originalEntitlements = subscription.FeatureEntitlements;
        await _featureEntitlements.SetFeatureEntitlementsAsync(id, entitlements, cancellationToken);
        await AuditAsync("LeaseEntitlementsUpdated", subscription.TenantId, id,
            entitlements, "Subscription entitlements updated", cancellationToken,
            originalEntitlements);
        return NoContent();
    }

    [HttpPut("{id:guid}/limits")]
    [RequirePermission("subscriptions.manage")]
    public async Task<IActionResult> SetLimits(
        Guid id,
        [FromBody] Dictionary<string, int> limits,
        CancellationToken cancellationToken)
    {
        var subscription = await _subscriptionsService.GetByIdAsync(id, cancellationToken);
        if (subscription is null) return NotFound();
        if (!User.CanAccessTenant(subscription.TenantId)) return Forbid();
        if (limits.Any(item => item.Value < -1))
            return BadRequest(new { error = "Usage limits must be -1 (unlimited), zero, or positive." });
        var originalLimits = subscription.UsageLimits;
        await _usageLimits.SetUsageLimitsAsync(id, limits, cancellationToken);
        await AuditAsync("LeaseUsageLimitsUpdated", subscription.TenantId, id,
            limits, "Subscription usage limits updated", cancellationToken,
            originalLimits);
        return NoContent();
    }

    [HttpPost]
    [RequirePermission("subscriptions.create")]
    public async Task<IActionResult> Create([FromBody] CreateSubscriptionRequest request)
    {
        if (!User.CanAccessTenant(request.TenantId)) return Forbid();
        var subscription = await _subscriptionsService.CreateSubscriptionAsync(
            request.TenantId, request.Plan, request.StartDate, request.EndDate);
        await AuditAsync("LeaseCreated", request.TenantId, subscription.Id,
            request, "Subscription lease created", HttpContext.RequestAborted);
        return CreatedAtAction(nameof(GetActive), new { tenantId = request.TenantId }, subscription);
    }

    [HttpPost("{id:guid}/activate")]
    [RequirePermission("subscriptions.manage")]
    public async Task<IActionResult> Activate(Guid id)
    {
        var subscription = await _subscriptionsService.GetByIdAsync(id);
        if (subscription == null) return NotFound();
        if (!User.CanAccessTenant(subscription.TenantId)) return Forbid();
        await _subscriptionsService.ActivateSubscriptionAsync(id);
        await AuditAsync("LeaseActivated", subscription.TenantId, id,
            new { status = "Active" }, "Subscription lease activated", HttpContext.RequestAborted);
        return NoContent();
    }

    [HttpPost("{id:guid}/suspend")]
    [RequirePermission("subscriptions.manage")]
    public async Task<IActionResult> Suspend(Guid id)
    {
        var subscription = await _subscriptionsService.GetByIdAsync(id);
        if (subscription == null) return NotFound();
        if (!User.CanAccessTenant(subscription.TenantId)) return Forbid();
        await _subscriptionsService.SuspendSubscriptionAsync(id);
        await AuditAsync("LeaseSuspended", subscription.TenantId, id,
            new { status = "Suspended" }, "Subscription lease suspended", HttpContext.RequestAborted);
        return NoContent();
    }

    [HttpPost("{id:guid}/renew")]
    [RequirePermission("subscriptions.manage")]
    public async Task<IActionResult> Renew(
        Guid id,
        [FromBody] RenewSubscriptionRequest request,
        CancellationToken cancellationToken)
    {
        var subscription = await _subscriptionsService.GetByIdAsync(
            id,
            cancellationToken);
        if (subscription is null) return NotFound();
        if (!User.CanAccessTenant(subscription.TenantId)) return Forbid();
        if (request.EndDateUtc <= DateTime.UtcNow)
            return BadRequest(new { error = "The renewal end date must be in the future." });
        var originalLease = new { subscription.EndDate, status = subscription.Status.ToString() };
        await _subscriptionsService.RenewAsync(
            id,
            request.EndDateUtc,
            cancellationToken);
        await AuditAsync("LeaseRenewed", subscription.TenantId, id,
            request, "Subscription lease renewed", cancellationToken,
            originalLease);
        return Ok(await _subscriptionsService.GetByIdAsync(id, cancellationToken));
    }

    private Task AuditAsync(
        string action,
        Guid tenantId,
        Guid subscriptionId,
        object value,
        string reason,
        CancellationToken cancellationToken,
        object? original = null) => _audit.RecordAsync(
            action,
            "Subscription",
            subscriptionId.ToString(),
            tenantId,
            User.UserId(),
            original is null ? null : JsonSerializer.Serialize(original),
            JsonSerializer.Serialize(value),
            reason,
            cancellationToken);
}

public record CreateSubscriptionRequest(Guid TenantId, string Plan, DateTime StartDate, DateTime EndDate);
public sealed record RenewSubscriptionRequest(DateTime EndDateUtc);

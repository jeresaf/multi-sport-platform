using SportsPlatform.BuildingBlocks.Actors;
using SportsPlatform.BuildingBlocks.Domain;

namespace SportsPlatform.Modules.Tenancy.Domain.Sports;

/// <summary>
/// One enablement of a sport variant by a tenant (S05 D12): from when it was enabled until it is disabled. Tenant-owned
/// (RLS). A row never changes except to be closed; enabling the variant again adds a new row, so every enablement stays in
/// the table (AR-18), as overrides do. The variant names Sports Configuration's definitions without a foreign key (another
/// module's table). The disable reason is free text kept permanently: never personal data.
/// </summary>
public sealed class TenantSport : Entity, ITenantOwned
{
    public const int VariantMaxLength = 50;
    public const int ReasonMaxLength = 500;

    private TenantSport(string variant, Guid enabledBy, DateTimeOffset enabledAt)
        : base(NewId())
    {
        Variant = variant;
        EnabledBy = enabledBy;
        EnabledAt = enabledAt;
    }

    private TenantSport()
    {
    }

    /// <summary>Stamped from the tenant context when saved.</summary>
    public Guid TenantId { get; private set; }

    /// <summary>A published sport variant's code, e.g. a definition's <c>variant</c>.</summary>
    public string Variant { get; private set; } = string.Empty;

    public Guid EnabledBy { get; private set; }

    public DateTimeOffset EnabledAt { get; private set; }

    public DateTimeOffset? DisabledAt { get; private set; }

    public Guid? DisabledBy { get; private set; }

    /// <summary>Free text kept permanently: never personal data.</summary>
    public string? DisableReason { get; private set; }

    public bool IsEnabled => DisabledAt is null;

    /// <summary>A new enablement of <paramref name="variant"/>, already checked against the published variants.</summary>
    public static TenantSport Enable(string variant, ICurrentActor actor, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(variant);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(variant.Length, VariantMaxLength, nameof(variant));
        ArgumentNullException.ThrowIfNull(actor);
        return new TenantSport(variant, actor.ActorId, now);
    }

    /// <summary>Closes the enablement now, with a reason; an enablement is closed once.</summary>
    public void Disable(string reason, ICurrentActor actor, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(reason.Trim().Length, ReasonMaxLength, nameof(reason));
        ArgumentNullException.ThrowIfNull(actor);
        if (!IsEnabled)
        {
            throw new InvalidOperationException($"Tenant sport {Id} is already disabled.");
        }

        DisabledAt = now;
        DisabledBy = actor.ActorId;
        DisableReason = reason.Trim();
    }
}

using SportsPlatform.BuildingBlocks.Domain;
using SportsPlatform.BuildingBlocks.Entitlements;

namespace SportsPlatform.Modules.Tenancy.Domain.Plans;

/// <summary>A plan's value of one entitlement (<c>tenancy.plan_entitlements</c>); the value is stored as <c>jsonb</c>.</summary>
public sealed class PlanEntitlement : Entity
{
    internal PlanEntitlement(Plan plan, string entitlementCode, EntitlementValue value)
        : base(IdFor(plan.Code, entitlementCode))
    {
        PlanId = plan.Id;
        EntitlementCode = entitlementCode;
        Value = value;
    }

    private PlanEntitlement()
    {
    }

    public Guid PlanId { get; private set; }

    /// <summary>An entitlement code from the catalogue, e.g. <c>MAX_STAFF_USERS</c>.</summary>
    public string EntitlementCode { get; private set; } = string.Empty;

    public EntitlementValue Value { get; private set; } = EntitlementValue.Flag(false);

    public static Guid IdFor(string planCode, string entitlementCode) =>
        DeterministicIds.Create($"tenancy.plan_entitlement:{planCode}:{entitlementCode}");

    /// <summary>Returns whether the value changed.</summary>
    internal bool Set(EntitlementValue value)
    {
        if (value == Value)
        {
            return false;
        }

        Value = value;
        return true;
    }
}

namespace SportsPlatform.BuildingBlocks.Tenancy;

/// <summary>
/// Whether the current actor may work in a tenant that is still being set up (<c>PROVISIONING</c>): step 1 of the
/// decision formula asks it for a tenant action (S03 D11). Identity implements it from the actor's roles.
/// </summary>
public interface ITenantSetupAccess
{
    /// <summary>Call after the scope's tenant is set to <paramref name="tenantId"/> (the roles are tenant-owned).</summary>
    ValueTask<bool> CanSetUpAsync(Guid tenantId, CancellationToken cancellationToken);
}

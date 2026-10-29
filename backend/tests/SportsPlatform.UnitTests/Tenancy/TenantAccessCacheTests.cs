using SportsPlatform.BuildingBlocks.Entitlements;
using SportsPlatform.Modules.Tenancy.Features.Access;

namespace SportsPlatform.UnitTests.Tenancy;

/// <summary>The tenant access cache (S04 D7): entries are reused, cleared per tenant, and a load a clear overtook is not kept.</summary>
public sealed class TenantAccessCacheTests : IDisposable
{
    private static readonly Guid _kmsa = Guid.CreateVersion7();
    private static readonly Guid _elssc = Guid.CreateVersion7();

    private readonly TenantAccessCache _cache = new();

    [Fact]
    public async Task A_cached_tenant_is_not_loaded_again()
    {
        var loads = 0;

        await _cache.GetOrLoadAsync(_kmsa, () => Load(ref loads));
        await _cache.GetOrLoadAsync(_kmsa, () => Load(ref loads));

        loads.ShouldBe(1);
    }

    [Fact]
    public async Task Clearing_a_tenant_reloads_it_and_keeps_the_others()
    {
        var loads = 0;
        await _cache.GetOrLoadAsync(_kmsa, () => Load(ref loads));
        await _cache.GetOrLoadAsync(_elssc, () => Load(ref loads));

        _cache.Clear(_kmsa);
        await _cache.GetOrLoadAsync(_kmsa, () => Load(ref loads));
        await _cache.GetOrLoadAsync(_elssc, () => Load(ref loads));

        loads.ShouldBe(3);
    }

    // A change commits and clears while a request is still reading the old rows: what it read must not be kept, or the
    // change would wait up to a minute (#68 R2).
    [Fact]
    public async Task A_load_that_a_clear_overtook_is_not_kept()
    {
        var loads = 0;

        await _cache.GetOrLoadAsync(_kmsa, () =>
        {
            _cache.Clear(_kmsa);
            return Load(ref loads);
        });
        await _cache.GetOrLoadAsync(_kmsa, () => Load(ref loads));

        loads.ShouldBe(2);
    }

    [Fact]
    public async Task Plan_values_stay_until_everything_is_cleared()
    {
        var loads = 0;
        Task<PlanValues> LoadPlans()
        {
            loads++;
            return Task.FromResult(new PlanValues(new Dictionary<string, IReadOnlyDictionary<string, EntitlementValue>>()));
        }

        await _cache.GetOrLoadPlanValuesAsync(LoadPlans);
        _cache.Clear(_kmsa);
        await _cache.GetOrLoadPlanValuesAsync(LoadPlans);
        loads.ShouldBe(1);

        _cache.Clear();
        await _cache.GetOrLoadPlanValuesAsync(LoadPlans);
        loads.ShouldBe(2);
    }

    public void Dispose() => _cache.Dispose();

    private static Task<TenantAccessInputs> Load(ref int loads)
    {
        loads++;
        return Task.FromResult(new TenantAccessInputs("Africa/Kampala", [], []));
    }
}

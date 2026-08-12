'use client';

import { useEffect, useState } from 'react';
import { adminApi, getStoredUser } from '@/lib/admin-api';

type Tenant = { id: string; name: string };
type Subscription = {
  id: string;
  plan: string;
  status: string;
  startDate: string;
  endDate: string;
  gracePeriodEnd?: string | null;
};
type UsageSummary = {
  current: Record<string, number>;
  limits: Record<string, number>;
  warnings: Array<{
    metric: string;
    current: number;
    limit: number;
    percentage: number;
    severity: 'warning' | 'exceeded';
    hardLimitEnforced: boolean;
  }>;
  hardLimitsEnabled: boolean;
};

const labels: Record<string, string> = {
  max_sports: 'Sports',
  max_competitions: 'Competitions',
  max_participants: 'Participants',
  max_admins: 'Administrators',
  max_operators: 'Scorers / operators',
  storage_mb: 'Storage (MB)',
  api_requests_per_hour: 'API requests / hour',
  notifications_per_month: 'Notifications / month',
};

export default function SubscriptionPage() {
  const [tenants, setTenants] = useState<Tenant[]>([]);
  const [tenantId, setTenantId] = useState('');
  const [subscription, setSubscription] = useState<Subscription | null>(null);
  const [usage, setUsage] = useState<UsageSummary | null>(null);
  const [features, setFeatures] = useState<Record<string, boolean>>({});
  const [savingPolicy, setSavingPolicy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    adminApi<Tenant[]>('/api/tenants').then(items => {
      setTenants(items);
      const ownTenant = getStoredUser()?.tenantId;
      setTenantId(items.find(item => item.id === ownTenant)?.id ?? items[0]?.id ?? '');
    }).catch((reason: Error) => setError(reason.message));
  }, []);

  useEffect(() => {
    if (!tenantId) return;
    setError(null);
    Promise.all([
      adminApi<Subscription>(`/api/subscriptions/active/${tenantId}`),
      adminApi<UsageSummary>(`/api/subscriptions/usage/${tenantId}`),
      adminApi<Record<string, boolean>>(`/api/subscriptions/entitlements/${tenantId}`),
    ]).then(([active, summary, entitlements]) => {
      setSubscription(active);
      setUsage(summary);
      setFeatures(entitlements);
    }).catch((reason: Error) => setError(reason.message));
  }, [tenantId]);

  async function saveHardLimitPolicy(enabled: boolean) {
    if (!subscription) return;
    setSavingPolicy(true);
    setError(null);
    const nextFeatures = { ...features, 'usage.hard_limits': enabled };
    try {
      await adminApi<void>(`/api/subscriptions/${subscription.id}/entitlements`, {
        method: 'PUT',
        body: JSON.stringify(nextFeatures),
      });
      setFeatures(nextFeatures);
      setUsage(current => current ? { ...current, hardLimitsEnabled: enabled } : current);
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'Unable to save the limit policy.');
    } finally {
      setSavingPolicy(false);
    }
  }

  return (
    <div className="space-y-6">
      <div>
        <p className="text-sm font-semibold uppercase tracking-wider text-primary">Plan controls</p>
        <h1 className="text-3xl font-bold text-gray-950">Subscription</h1>
        <p className="mt-1 text-gray-500">Live server-enforced features, resource use, and limits.</p>
      </div>
      <div className="max-w-xl">
        <label htmlFor="subscription-tenant" className="text-sm font-medium">Tenant</label>
        <select id="subscription-tenant" value={tenantId} onChange={event => setTenantId(event.target.value)} className="mt-1 h-10 w-full rounded-md border bg-white px-3">
          {tenants.map(tenant => <option key={tenant.id} value={tenant.id}>{tenant.name}</option>)}
        </select>
      </div>
      {error && <p className="rounded-lg bg-red-50 p-4 text-sm font-semibold text-red-700">{error}</p>}
      {subscription && (
        <section className="grid gap-4 sm:grid-cols-3">
          <Summary title="Plan" value={subscription.plan} />
          <Summary title="Status" value={subscription.status} />
          <Summary title="Renews / ends" value={new Date(subscription.endDate).toLocaleDateString()} />
        </section>
      )}
      <section className="grid gap-6 lg:grid-cols-2">
        <div className="rounded-xl border bg-white p-6">
          <h2 className="text-lg font-bold">Usage</h2>
          {usage && usage.warnings.length > 0 && (
            <div className="mt-4 space-y-2" role="status" aria-label="Usage limit warnings">
              {usage.warnings.map(warning => (
                <p key={warning.metric} className={`rounded-lg px-3 py-2 text-sm font-medium ${warning.severity === 'exceeded' ? 'bg-red-50 text-red-800' : 'bg-amber-50 text-amber-800'}`}>
                  {labels[warning.metric] ?? warning.metric} is at {warning.percentage}% ({warning.current} of {warning.limit}).
                  {warning.severity === 'exceeded' && !warning.hardLimitEnforced ? ' Soft limits allow work to continue.' : ''}
                </p>
              ))}
            </div>
          )}
          <div className="mt-4 space-y-4">
            {usage && Object.entries(usage.limits).map(([key, limit]) => {
              const current = usage.current[key] ?? 0;
              const percent = limit > 0 ? Math.min(100, current / limit * 100) : 0;
              return <div key={key}>
                <div className="flex justify-between text-sm"><span>{labels[key] ?? key}</span><span className="font-semibold">{current} / {limit === -1 ? 'Unlimited' : limit}</span></div>
                {limit > 0 && <div className="mt-1 h-2 overflow-hidden rounded bg-gray-100"><div className={`h-full ${current >= limit ? 'bg-red-500' : current / limit >= .8 ? 'bg-amber-500' : 'bg-primary'}`} style={{ width: `${percent}%` }} /></div>}
              </div>;
            })}
          </div>
        </div>
        <div className="rounded-xl border bg-white p-6">
          <h2 className="text-lg font-bold">Feature entitlements</h2>
          {usage && (
            <label className="mt-4 flex items-start justify-between gap-4 rounded-lg border bg-gray-50 px-4 py-3">
              <span>
                <span className="block text-sm font-semibold">Enforce hard usage limits</span>
                <span className="block text-xs text-gray-500">Turn off to warn at limits while allowing new resources.</span>
              </span>
              <input
                type="checkbox"
                className="mt-1 h-4 w-4"
                checked={usage.hardLimitsEnabled}
                disabled={savingPolicy}
                onChange={event => void saveHardLimitPolicy(event.target.checked)}
              />
            </label>
          )}
          <div className="mt-4 grid gap-2 sm:grid-cols-2">
            {Object.entries(features).sort(([a], [b]) => a.localeCompare(b)).map(([feature, enabled]) =>
              <div key={feature} className="flex items-center justify-between rounded-lg border px-3 py-2 text-sm">
                <span>{feature.replaceAll('.', ' · ').replaceAll('_', ' ')}</span>
                <span className={enabled ? 'font-semibold text-green-700' : 'font-semibold text-gray-400'}>{enabled ? 'Enabled' : 'Unavailable'}</span>
              </div>)}
          </div>
        </div>
      </section>
    </div>
  );
}

function Summary({ title, value }: { title: string; value: string }) {
  return <div className="rounded-xl border bg-white p-5"><p className="text-sm text-gray-500">{title}</p><p className="mt-1 text-xl font-bold">{value}</p></div>;
}

'use client';

import { useCallback, useEffect, useState } from 'react';
import {
  Activity,
  AlertTriangle,
  BellOff,
  Building2,
  CheckCircle2,
  Clock,
  RefreshCw,
  RotateCcw,
  Search,
  Trophy,
} from 'lucide-react';
import { Button } from '@/components/ui/button';
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from '@/components/ui/card';
import { AdminUser, adminApi, getStoredUser } from '@/lib/admin-api';
import { TenantDashboard } from '@/components/dashboard/tenant-dashboard';

type UsageWarning = {
  metric: string;
  current: number;
  limit: number;
  percentage: number;
  severity: string;
};
type Usage = {
  current: Record<string, number>;
  limits: Record<string, number>;
  warnings: UsageWarning[];
  hardLimitsEnabled: boolean;
};
type Lease = {
  id: string;
  plan: string;
  status: string;
  startDate: string;
  endDate: string;
  gracePeriodEnd: string | null;
  daysRemaining: number;
};
type TenantRow = {
  id: string;
  name: string;
  slug: string;
  status: string;
  lease: Lease | null;
  usage: Usage;
  entitlements: Record<string, boolean>;
  enabledEntitlementCount: number;
  notifications: {
    disabled: boolean;
    disabledUntilUtc: string | null;
    reason: string | null;
    changedAtUtc: string | null;
  };
};
type FailedJob = {
  id: string;
  tenantId: string;
  name: string;
  type: string;
  format: string;
  status: string;
  errorMessage: string | null;
  createdAt: string;
  updatedAt: string | null;
};
type SyncConflict = {
  id: string;
  tenantId: string | null;
  contestId: string;
  details: string | null;
  reason: string | null;
  timestamp: string;
};
type EventHistory = {
  id?: string;
  contestId?: string;
  tenantId?: string;
  name?: string;
  status?: string;
  notFound?: boolean;
  events: Array<{
    id: string;
    eventId: string;
    eventType: string;
    participantId: string;
    metricName: string;
    value: number;
    displayValue: string | null;
    sequence: number;
    deviceId: string;
    deviceTimestamp: string;
    serverTimestamp: string;
  }>;
  updates?: Array<{
    id: string;
    liveScoreId: string;
    updateType: string;
    oldValue: string | null;
    newValue: string | null;
    updatedAt: string;
  }>;
};
type AuditRow = {
  id: string;
  tenantId: string | null;
  userId: string | null;
  action: string;
  entityType: string;
  entityId: string;
  reason: string | null;
  timestamp: string;
};
type Dashboard = {
  generatedAtUtc: string;
  summary: {
    totalTenants: number;
    activeTenants: number;
    activeCompetitions: number;
    liveContests: number;
    scheduledNextSevenDays: number;
    usageWarnings: number;
  };
  health: {
    status: string;
    database: string;
    checkedAtUtc: string;
  };
  tenants: TenantRow[];
  failedJobs: FailedJob[];
  syncConflicts: SyncConflict[];
  eventHistory: EventHistory | null;
  recentAudit: AuditRow[];
};

const platformRoles = new Set(['Admin', 'Platform Owner', 'Platform Support']);

function readable(value: string) {
  return value.replaceAll('_', ' ').replace(/\b\w/g, (letter) => letter.toUpperCase());
}

function usageValue(tenant: TenantRow, metric: string) {
  const current = tenant.usage.current[metric] ?? 0;
  const limit = tenant.usage.limits[metric];
  return `${current} / ${limit === -1 ? '∞' : limit ?? '—'}`;
}

export default function DashboardPage() {
  const [dashboard, setDashboard] = useState<Dashboard | null>(null);
  const [user, setUser] = useState<AdminUser | null>(null);
  const [identityLoaded, setIdentityLoaded] = useState(false);
  const [reasons, setReasons] = useState<Record<string, string>>({});
  const [notificationReasons, setNotificationReasons] = useState<Record<string, string>>({});
  const [jobReasons, setJobReasons] = useState<Record<string, string>>({});
  const [searchDraft, setSearchDraft] = useState('');
  const [contestDraft, setContestDraft] = useState('');
  const [filters, setFilters] = useState({ search: '', contestId: '' });
  const [loading, setLoading] = useState(true);
  const [busyTenantId, setBusyTenantId] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const isPlatformOperator = user?.roles.some((role) => platformRoles.has(role)) ?? false;

  useEffect(() => {
    setUser(getStoredUser());
    setIdentityLoaded(true);
  }, []);

  const refresh = useCallback(async () => {
    if (!identityLoaded) return;
    if (!isPlatformOperator) {
      setLoading(false);
      return;
    }
    setError(null);
    try {
      const query = new URLSearchParams();
      if (filters.search) query.set('search', filters.search);
      if (filters.contestId) query.set('historyContestId', filters.contestId);
      const suffix = query.size ? `?${query.toString()}` : '';
      setDashboard(await adminApi<Dashboard>(`/api/platform-admin/dashboard${suffix}`));
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'Could not load platform administration.');
    } finally {
      setLoading(false);
    }
  }, [filters, identityLoaded, isPlatformOperator]);

  useEffect(() => {
    void refresh();
  }, [refresh]);

  async function supportAction(tenant: TenantRow, action: 'Activate' | 'Suspend') {
    const reason = reasons[tenant.id]?.trim();
    if (!reason) {
      setError('Enter a support reason before changing tenant access.');
      return;
    }
    if (!window.confirm(
      `${action} ${tenant.name}? This changes platform access and will be audited.`,
    )) return;

    setBusyTenantId(tenant.id);
    setError(null);
    setNotice(null);
    try {
      await adminApi(`/api/platform-admin/tenants/${tenant.id}/support-action`, {
        method: 'POST',
        body: JSON.stringify({ action, reason, confirmed: true }),
      });
      setReasons((current) => ({ ...current, [tenant.id]: '' }));
      setNotice(`${tenant.name} ${action.toLowerCase()} action completed and audited.`);
      await refresh();
    } catch (reasonValue) {
      setError(reasonValue instanceof Error ? reasonValue.message : 'Support action failed.');
    } finally {
      setBusyTenantId(null);
    }
  }

  async function setNotificationDelivery(tenant: TenantRow, disabled: boolean) {
    const reason = notificationReasons[tenant.id]?.trim();
    if (!reason) {
      setError('Enter a support reason before changing notification delivery.');
      return;
    }
    const action = disabled ? 'pause notifications for four hours' : 'resume notifications';
    if (!window.confirm(`${action} for ${tenant.name}? This action will be audited.`)) return;
    setBusyTenantId(tenant.id);
    setError(null);
    setNotice(null);
    try {
      await adminApi(`/api/platform-admin/tenants/${tenant.id}/notifications`, {
        method: 'POST',
        body: JSON.stringify({
          disabled,
          disabledUntilUtc: disabled
            ? new Date(Date.now() + 4 * 60 * 60 * 1000).toISOString()
            : null,
          reason,
          confirmed: true,
        }),
      });
      setNotificationReasons((current) => ({ ...current, [tenant.id]: '' }));
      setNotice(`${tenant.name} notification delivery ${disabled ? 'paused' : 'resumed'} and audited.`);
      await refresh();
    } catch (reasonValue) {
      setError(reasonValue instanceof Error ? reasonValue.message : 'Notification control failed.');
    } finally {
      setBusyTenantId(null);
    }
  }

  async function reprocessJob(job: FailedJob) {
    const reason = jobReasons[job.id]?.trim();
    if (!reason) {
      setError('Enter a reason before reprocessing a failed job.');
      return;
    }
    if (!window.confirm(`Reprocess failed job “${job.name}”? This action will be audited.`)) return;
    setBusyTenantId(job.id);
    setError(null);
    setNotice(null);
    try {
      await adminApi(`/api/platform-admin/jobs/${job.id}/reprocess`, {
        method: 'POST',
        body: JSON.stringify({ reason, confirmed: true }),
      });
      setJobReasons((current) => ({ ...current, [job.id]: '' }));
      setNotice(`${job.name} was safely requeued and audited.`);
      await refresh();
    } catch (reasonValue) {
      setError(reasonValue instanceof Error ? reasonValue.message : 'Job reprocessing failed.');
    } finally {
      setBusyTenantId(null);
    }
  }

  if (!identityLoaded) {
    return <p className="rounded-lg border bg-white p-4 text-gray-500">Loading platform access…</p>;
  }

  if (!isPlatformOperator) {
    return <TenantDashboard />;
  }

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <p className="text-sm font-semibold uppercase tracking-wider text-primary">Platform operations</p>
          <h1 className="text-3xl font-bold text-gray-950">Platform administration</h1>
          <p className="mt-1 text-gray-500">Tenants, leases, usage, entitlements, health, support, and audit review.</p>
        </div>
        <Button variant="outline" disabled={loading} onClick={() => void refresh()}>
          <RefreshCw className="mr-2 h-4 w-4" /> Refresh
        </Button>
      </div>

      {loading && <p className="rounded-lg border bg-white p-4 text-gray-500">Loading platform status…</p>}
      {error && <p role="alert" className="rounded-lg border border-red-200 bg-red-50 p-4 text-red-700">{error}</p>}
      {notice && <p role="status" className="rounded-lg border border-green-200 bg-green-50 p-4 text-green-700">{notice}</p>}

      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2"><Search className="h-5 w-5" /> Support investigation</CardTitle>
          <CardDescription>Search tenants and load the immutable event history for a contest.</CardDescription>
        </CardHeader>
        <CardContent className="grid gap-3 md:grid-cols-[1fr_1fr_auto]">
          <input className="rounded border px-3 py-2" aria-label="Search tenants"
            placeholder="Tenant name, slug, or ID" value={searchDraft}
            onChange={(event) => setSearchDraft(event.target.value)} />
          <input className="rounded border px-3 py-2" aria-label="Contest event history ID"
            placeholder="Contest ID for event history" value={contestDraft}
            onChange={(event) => setContestDraft(event.target.value)} />
          <Button onClick={() => setFilters({
            search: searchDraft.trim(),
            contestId: contestDraft.trim(),
          })}><Search className="mr-2 h-4 w-4" /> Apply</Button>
        </CardContent>
      </Card>

      {dashboard && (
        <>
          <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-5">
            <SummaryCard label="Active tenants" value={`${dashboard.summary.activeTenants}/${dashboard.summary.totalTenants}`} icon={Building2} />
            <SummaryCard label="Active competitions" value={dashboard.summary.activeCompetitions} icon={Trophy} />
            <SummaryCard label="Live contests" value={dashboard.summary.liveContests} icon={Activity} />
            <SummaryCard label="Next 7 days" value={dashboard.summary.scheduledNextSevenDays} icon={Clock} />
            <SummaryCard label="Usage warnings" value={dashboard.summary.usageWarnings} icon={AlertTriangle} warning={dashboard.summary.usageWarnings > 0} />
          </div>

          <Card>
            <CardHeader>
              <CardTitle className="flex items-center gap-2">
                <CheckCircle2 className={`h-5 w-5 ${dashboard.health.status === 'Healthy' ? 'text-green-600' : 'text-amber-600'}`} />
                System health
              </CardTitle>
              <CardDescription>Latest API dependency check.</CardDescription>
            </CardHeader>
            <CardContent className="grid gap-3 text-sm sm:grid-cols-3">
              <HealthValue label="Overall" value={dashboard.health.status} />
              <HealthValue label="Database" value={dashboard.health.database} />
              <HealthValue label="Checked" value={new Date(dashboard.health.checkedAtUtc).toLocaleString()} />
            </CardContent>
          </Card>

          <Card>
            <CardHeader>
              <CardTitle>Tenant operations</CardTitle>
              <CardDescription>Lease state, resource usage, enabled features, and confirmed support actions.</CardDescription>
            </CardHeader>
            <CardContent className="space-y-4">
              {dashboard.tenants.map((tenant) => (
                <section key={tenant.id} className="rounded-xl border p-4" data-testid={`platform-tenant-${tenant.slug}`}>
                  <div className="flex flex-wrap items-start justify-between gap-3">
                    <div>
                      <h2 className="text-lg font-semibold">{tenant.name}</h2>
                      <p className="text-sm text-gray-500">/{tenant.slug}</p>
                    </div>
                    <span className={`rounded-full px-3 py-1 text-xs font-semibold ${tenant.status === 'Active' ? 'bg-green-50 text-green-700' : 'bg-amber-50 text-amber-700'}`}>
                      {tenant.status}
                    </span>
                  </div>
                  <div className="mt-4 grid gap-4 lg:grid-cols-5">
                    <div className="rounded-lg bg-gray-50 p-3 text-sm">
                      <p className="text-xs font-semibold uppercase text-gray-400">Lease</p>
                      {tenant.lease ? <>
                        <p className="mt-1 font-semibold">{readable(tenant.lease.plan)} · {tenant.lease.status}</p>
                        <p className={tenant.lease.daysRemaining < 30 ? 'text-amber-700' : 'text-gray-600'}>
                          {tenant.lease.daysRemaining} days remaining
                        </p>
                      </> : <p className="mt-1 text-amber-700">No active lease</p>}
                    </div>
                    <div className="rounded-lg bg-gray-50 p-3 text-sm">
                      <p className="text-xs font-semibold uppercase text-gray-400">Usage</p>
                      <p>Competitions {usageValue(tenant, 'max_competitions')}</p>
                      <p>Participants {usageValue(tenant, 'max_participants')}</p>
                      {tenant.usage.warnings.map((warning) => (
                        <p key={warning.metric} className="font-medium text-amber-700">
                          {readable(warning.metric)} {warning.percentage}%
                        </p>
                      ))}
                    </div>
                    <div className="rounded-lg bg-gray-50 p-3 text-sm">
                      <p className="text-xs font-semibold uppercase text-gray-400">Entitlements</p>
                      <p className="font-semibold">{tenant.enabledEntitlementCount} enabled</p>
                      <p className="line-clamp-2 text-gray-600">
                        {Object.entries(tenant.entitlements).filter(([, enabled]) => enabled)
                          .slice(0, 4).map(([name]) => readable(name)).join(', ') || 'None'}
                      </p>
                    </div>
                    <div className="space-y-2 rounded-lg border border-dashed p-3 text-sm">
                      <p className="text-xs font-semibold uppercase text-gray-400">Support action</p>
                      <input className="w-full rounded border px-2 py-1.5" aria-label={`Support reason for ${tenant.name}`}
                        placeholder="Required reason" value={reasons[tenant.id] ?? ''}
                        onChange={(event) => setReasons((current) => ({
                          ...current, [tenant.id]: event.target.value,
                        }))} />
                      {tenant.status === 'Active' ? (
                        <Button size="sm" variant="outline" disabled={busyTenantId === tenant.id}
                          onClick={() => void supportAction(tenant, 'Suspend')}>Suspend tenant</Button>
                      ) : (
                        <Button size="sm" disabled={busyTenantId === tenant.id}
                          onClick={() => void supportAction(tenant, 'Activate')}>Activate tenant</Button>
                      )}
                    </div>
                    <div className="space-y-2 rounded-lg border border-dashed p-3 text-sm">
                      <p className="text-xs font-semibold uppercase text-gray-400">Notification delivery</p>
                      <p className={tenant.notifications.disabled ? 'font-semibold text-amber-700' : 'font-semibold text-green-700'}>
                        {tenant.notifications.disabled
                          ? `Paused until ${new Date(tenant.notifications.disabledUntilUtc!).toLocaleString()}`
                          : 'Enabled'}
                      </p>
                      <input className="w-full rounded border px-2 py-1.5"
                        aria-label={`Notification reason for ${tenant.name}`}
                        placeholder="Required reason" value={notificationReasons[tenant.id] ?? ''}
                        onChange={(event) => setNotificationReasons((current) => ({
                          ...current, [tenant.id]: event.target.value,
                        }))} />
                      {tenant.notifications.disabled ? (
                        <Button size="sm" disabled={busyTenantId === tenant.id}
                          onClick={() => void setNotificationDelivery(tenant, false)}>Resume delivery</Button>
                      ) : (
                        <Button size="sm" variant="outline" disabled={busyTenantId === tenant.id}
                          onClick={() => void setNotificationDelivery(tenant, true)}>
                          <BellOff className="mr-2 h-4 w-4" /> Pause 4 hours
                        </Button>
                      )}
                    </div>
                  </div>
                </section>
              ))}
              {dashboard.tenants.length === 0 && <p className="py-4 text-gray-500">No tenants match the support search.</p>}
            </CardContent>
          </Card>

          <Card>
            <CardHeader>
              <CardTitle>Failed jobs</CardTitle>
              <CardDescription>Only failed durable report jobs can be safely reprocessed from support tooling.</CardDescription>
            </CardHeader>
            <CardContent className="space-y-3">
              {dashboard.failedJobs.map((job) => (
                <section key={job.id} className="grid gap-3 rounded-lg border p-3 lg:grid-cols-[1fr_1fr_auto]">
                  <div>
                    <p className="font-semibold">{job.name}</p>
                    <p className="text-sm text-gray-500">{readable(job.type)} · {job.format} · tenant {job.tenantId}</p>
                    <p className="mt-1 text-sm text-red-700">{job.errorMessage ?? 'No failure detail recorded'}</p>
                  </div>
                  <input className="h-fit rounded border px-3 py-2" aria-label={`Reprocess reason for ${job.name}`}
                    placeholder="Required reprocessing reason" value={jobReasons[job.id] ?? ''}
                    onChange={(event) => setJobReasons((current) => ({
                      ...current, [job.id]: event.target.value,
                    }))} />
                  <Button variant="outline" disabled={busyTenantId === job.id}
                    onClick={() => void reprocessJob(job)}>
                    <RotateCcw className="mr-2 h-4 w-4" /> Reprocess
                  </Button>
                </section>
              ))}
              {dashboard.failedJobs.length === 0 && <p className="text-gray-500">No failed jobs require review.</p>}
            </CardContent>
          </Card>

          <div className="grid gap-4 xl:grid-cols-2">
            <Card>
              <CardHeader>
                <CardTitle>Mobile sync conflicts</CardTitle>
                <CardDescription>Durable sequence conflicts captured from scorer synchronization.</CardDescription>
              </CardHeader>
              <CardContent className="space-y-3">
                {dashboard.syncConflicts.map((conflict) => (
                  <div key={conflict.id} className="rounded-lg border p-3 text-sm">
                    <div className="flex flex-wrap justify-between gap-2">
                      <p className="font-semibold">Contest {conflict.contestId}</p>
                      <time className="text-gray-500">{new Date(conflict.timestamp).toLocaleString()}</time>
                    </div>
                    <p className="text-amber-800">{conflict.reason ?? 'Sequence conflict'}</p>
                    {conflict.details && <code className="mt-2 block overflow-x-auto rounded bg-gray-50 p-2 text-xs">{conflict.details}</code>}
                  </div>
                ))}
                {dashboard.syncConflicts.length === 0 && <p className="text-gray-500">No sync conflicts recorded.</p>}
              </CardContent>
            </Card>

            <Card>
              <CardHeader>
                <CardTitle>Contest event history</CardTitle>
                <CardDescription>Persisted score events and correction records for the selected contest.</CardDescription>
              </CardHeader>
              <CardContent className="space-y-3">
                {!dashboard.eventHistory && <p className="text-gray-500">Enter a contest ID above to load its history.</p>}
                {dashboard.eventHistory?.notFound && <p className="text-amber-700">The requested contest was not found.</p>}
                {dashboard.eventHistory && !dashboard.eventHistory.notFound && <>
                  <div><p className="font-semibold">{dashboard.eventHistory.name}</p>
                    <p className="text-sm text-gray-500">{dashboard.eventHistory.status} · {dashboard.eventHistory.events.length} events · {dashboard.eventHistory.updates?.length ?? 0} updates</p>
                  </div>
                  <div className="max-h-96 space-y-2 overflow-y-auto">
                    {dashboard.eventHistory.events.map((event) => (
                      <div key={event.id} className="rounded border p-2 text-sm">
                        <p className="font-medium">#{event.sequence} {readable(event.eventType)} · {event.displayValue ?? event.value}</p>
                        <p className="text-xs text-gray-500">{event.metricName} · device {event.deviceId} · {new Date(event.serverTimestamp).toLocaleString()}</p>
                      </div>
                    ))}
                    {dashboard.eventHistory.events.length === 0 && <p className="text-gray-500">No score events recorded.</p>}
                  </div>
                </>}
              </CardContent>
            </Card>
          </div>

          <Card>
            <CardHeader>
              <CardTitle>Recent audit review</CardTitle>
              <CardDescription>The thirty latest immutable platform and competition actions.</CardDescription>
            </CardHeader>
            <CardContent className="overflow-x-auto">
              <table className="w-full text-sm">
                <thead><tr className="border-b text-left">
                  <th className="p-2">Time</th><th className="p-2">Action</th>
                  <th className="p-2">Entity</th><th className="p-2">Reason</th>
                </tr></thead>
                <tbody>{dashboard.recentAudit.map((entry) => (
                  <tr key={entry.id} className="border-b">
                    <td className="whitespace-nowrap p-2">{new Date(entry.timestamp).toLocaleString()}</td>
                    <td className="p-2 font-medium">{readable(entry.action)}</td>
                    <td className="p-2">{entry.entityType} · {entry.entityId}</td>
                    <td className="p-2 text-gray-600">{entry.reason ?? '—'}</td>
                  </tr>
                ))}</tbody>
              </table>
              {dashboard.recentAudit.length === 0 && <p className="py-4 text-gray-500">No audit entries recorded.</p>}
            </CardContent>
          </Card>
        </>
      )}
    </div>
  );
}

function SummaryCard({
  label,
  value,
  icon: Icon,
  warning = false,
}: {
  label: string;
  value: string | number;
  icon: React.ComponentType<{ className?: string }>;
  warning?: boolean;
}) {
  return <Card><CardHeader className="flex flex-row items-center justify-between pb-2">
    <CardTitle className="text-sm font-medium text-gray-500">{label}</CardTitle>
    <Icon className={`h-4 w-4 ${warning ? 'text-amber-600' : 'text-primary'}`} />
  </CardHeader><CardContent><p className="text-2xl font-bold">{value}</p></CardContent></Card>;
}

function HealthValue({ label, value }: { label: string; value: string }) {
  return <div className="rounded-lg bg-gray-50 p-3"><p className="text-xs uppercase text-gray-400">{label}</p><p className="font-semibold">{value}</p></div>;
}

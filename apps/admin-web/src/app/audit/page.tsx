'use client';

import { useCallback, useEffect, useMemo, useState } from 'react';
import { RefreshCw, Search, ShieldCheck } from 'lucide-react';
import { adminApi, getEffectiveTenantId } from '@/lib/admin-api';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';

type Tenant = { id: string; name: string };
type AuditEntry = {
  id: string; tenantId?: string | null; userId?: string | null;
  user?: { email: string; firstName: string; lastName: string } | null;
  action: string; entityType: string; entityId: string;
  originalValue?: string | null; newValue?: string | null; reason: string;
  ipAddress?: string | null; userAgent?: string | null; timestamp: string;
};
type AuditPage = {
  page: number; pageSize: number; total: number; totalPages: number; items: AuditEntry[];
};
const toUtc = (value: string) => value ? new Date(value).toISOString() : '';

export default function AuditPageView() {
  const [tenants, setTenants] = useState<Tenant[]>([]);
  const [tenantId, setTenantId] = useState('');
  const [filters, setFilters] = useState({ search: '', action: '', entityType: '', fromUtc: '', toUtc: '' });
  const [result, setResult] = useState<AuditPage | null>(null);
  const [page, setPage] = useState(1);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const metrics = useMemo(() => {
    const items = result?.items ?? [];
    return {
      total: result?.total ?? 0,
      actors: new Set(items.map(item => item.userId || item.user?.email || 'system')).size,
      actions: new Set(items.map(item => item.action)).size,
      entities: new Set(items.map(item => item.entityType)).size,
    };
  }, [result]);

  useEffect(() => {
    adminApi<Tenant[]>('/api/tenants').then(items => {
      setTenants(items);
      const own = getEffectiveTenantId();
      setTenantId(items.find(item => item.id === own)?.id ?? items[0]?.id ?? '');
    }).catch((reason: Error) => setError(reason.message));
  }, []);

  const search = useCallback(async (selectedPage = 1) => {
    if (!tenantId) return;
    setLoading(true); setError(null);
    try {
      const query = new URLSearchParams({ tenantId, page: String(selectedPage), pageSize: '50' });
      if (filters.search.trim()) query.set('search', filters.search.trim());
      if (filters.action.trim()) query.set('action', filters.action.trim());
      if (filters.entityType.trim()) query.set('entityType', filters.entityType.trim());
      if (filters.fromUtc) query.set('fromUtc', toUtc(filters.fromUtc));
      if (filters.toUtc) query.set('toUtc', toUtc(filters.toUtc));
      setResult(await adminApi<AuditPage>(`/api/audit?${query}`));
      setPage(selectedPage);
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'Could not search audit history.');
    } finally { setLoading(false); }
  }, [tenantId, filters]);

  useEffect(() => { if (tenantId) void search(1); }, [tenantId]); // eslint-disable-line react-hooks/exhaustive-deps

  return <div className="space-y-6" data-testid="audit-history-workspace">
    <section className="overflow-hidden rounded-3xl bg-gradient-to-br from-slate-950 via-slate-900 to-slate-800 p-6 text-white shadow-sm">
      <div className="flex flex-col gap-5 xl:flex-row xl:items-end xl:justify-between">
        <div className="max-w-3xl">
          <p className="text-xs font-bold uppercase tracking-[0.2em] text-slate-400">Security & accountability</p>
          <h1 className="mt-2 flex items-center gap-3 text-3xl font-black tracking-tight"><ShieldCheck className="h-8 w-8" />Audit history</h1>
          <p className="mt-2 max-w-2xl text-sm leading-6 text-slate-300">
            Search the tenant&apos;s append-only operational history by actor, action, target type, and time window.
          </p>
        </div>
        <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
          <AuditMetric label="Matches" value={metrics.total} />
          <AuditMetric label="Actors" value={metrics.actors} />
          <AuditMetric label="Actions" value={metrics.actions} />
          <AuditMetric label="Entities" value={metrics.entities} />
        </div>
      </div>
      <div className="mt-5">
        <Button variant="outline" disabled={loading || !tenantId} onClick={() => void search(page)} className="border-white/15 bg-white/10 text-white hover:bg-white/15 hover:text-white"><RefreshCw className="mr-2 h-4 w-4" />Refresh</Button>
      </div>
    </section>

    <Card className="rounded-2xl border-slate-200 shadow-sm"><CardHeader><CardTitle>Search audit trail</CardTitle><CardDescription>Filter by actor activity, action, target type, or time window.</CardDescription></CardHeader><CardContent className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
      <div><Label htmlFor="audit-tenant">Tenant</Label><select id="audit-tenant" value={tenantId} onChange={event => setTenantId(event.target.value)} className="mt-1 h-10 w-full rounded-md border px-3">{tenants.map(item => <option key={item.id} value={item.id}>{item.name}</option>)}</select></div>
      <Field label="Search" value={filters.search} onChange={value => setFilters(current => ({ ...current, search: value }))} />
      <Field label="Action" value={filters.action} onChange={value => setFilters(current => ({ ...current, action: value }))} />
      <Field label="Entity type" value={filters.entityType} onChange={value => setFilters(current => ({ ...current, entityType: value }))} />
      <Field label="From" type="datetime-local" value={filters.fromUtc} onChange={value => setFilters(current => ({ ...current, fromUtc: value }))} />
      <Field label="To" type="datetime-local" value={filters.toUtc} onChange={value => setFilters(current => ({ ...current, toUtc: value }))} />
      <div className="flex items-end"><Button onClick={() => void search(1)} disabled={loading || !tenantId}><Search className="mr-2 h-4 w-4" />Search history</Button></div>
    </CardContent></Card>

    {error && <p role="alert" className="rounded-lg border border-red-200 bg-red-50 p-3 text-red-700">{error}</p>}
    <Card className="rounded-2xl border-slate-200 shadow-sm"><CardHeader><CardTitle className="flex items-center gap-2"><ShieldCheck className="h-5 w-5 text-primary" />Append-only entries</CardTitle><CardDescription>{result ? `${result.total.toLocaleString()} matching entries` : 'Loading history…'}</CardDescription></CardHeader><CardContent className="space-y-3">
      {result?.items.map(item => <details key={item.id} className="rounded-2xl border border-slate-200 bg-white p-4 transition hover:border-slate-300 hover:shadow-sm" data-testid="audit-entry">
        <summary className="cursor-pointer list-none"><div className="flex flex-wrap items-start justify-between gap-3"><div><p className="font-semibold text-gray-950">{item.action}</p><p className="text-sm text-gray-500">{item.entityType} · {item.entityId}</p></div><div className="text-right text-xs text-gray-500"><p>{new Date(item.timestamp).toLocaleString()}</p><p>{item.user ? `${item.user.firstName} ${item.user.lastName} (${item.user.email})` : item.userId || 'System/anonymous'}</p></div></div><p className="mt-2 text-sm text-gray-700">{item.reason}</p></summary>
        <div className="mt-3 grid gap-3 border-t pt-3 text-xs md:grid-cols-2"><AuditValue label="Original value" value={item.originalValue} /><AuditValue label="New value" value={item.newValue} /><p><span className="font-semibold">IP address:</span> {item.ipAddress || 'Not captured'}</p><p><span className="font-semibold">User agent:</span> {item.userAgent || 'Not captured'}</p></div>
      </details>)}
      {result && result.items.length === 0 && <p className="rounded-2xl border border-dashed border-slate-200 bg-slate-50 p-8 text-center text-sm text-slate-500">No audit entries match these filters.</p>}
      {result && result.totalPages > 1 && <div className="flex items-center justify-between pt-2"><Button variant="outline" disabled={loading || page <= 1} onClick={() => void search(page - 1)}>Previous</Button><span className="text-sm text-gray-500">Page {page} of {result.totalPages}</span><Button variant="outline" disabled={loading || page >= result.totalPages} onClick={() => void search(page + 1)}>Next</Button></div>}
    </CardContent></Card>
  </div>;
}

function Field({ label, value, onChange, type = 'text' }: { label: string; value: string; onChange: (value: string) => void; type?: string }) {
  const id = `audit-${label.toLowerCase().replaceAll(' ', '-')}`;
  return <div><Label htmlFor={id}>{label}</Label><Input id={id} type={type} value={value} onChange={event => onChange(event.target.value)} className="mt-1" /></div>;
}
function AuditValue({ label, value }: { label: string; value?: string | null }) {
  return <div><p className="font-semibold">{label}</p><pre className="mt-1 max-h-48 overflow-auto whitespace-pre-wrap rounded-lg border border-slate-200 bg-slate-50 p-3">{formatJson(value)}</pre></div>;
}
function formatJson(value?: string | null) {
  if (!value) return 'Not recorded';
  try { return JSON.stringify(JSON.parse(value), null, 2); } catch { return value; }
}

function AuditMetric({ label, value }: { label: string; value: number }) {
  return <div className="min-w-24 rounded-2xl border border-white/10 bg-white/5 px-4 py-3 backdrop-blur">
    <div className="text-[10px] font-bold uppercase tracking-[0.16em] text-slate-400">{label}</div>
    <div className="mt-1 text-2xl font-black text-white">{value.toLocaleString()}</div>
  </div>;
}


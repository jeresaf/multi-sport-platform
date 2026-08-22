'use client';

import { useCallback, useEffect, useMemo, useState } from 'react';
import { adminApi, adminDownload, getEffectiveTenantId, getStoredUser, isPlatformOperator } from '@/lib/admin-api';

type Tenant = { id: string; name: string };
type PreviewRow = { row: number; name: string; status: string; message?: string | null };
type Preview = { total: number; valid: number; duplicates: number; invalid: number; rows: PreviewRow[] };
type Job = { id: string; kind: string; fileName: string; status: string; duplicateHandling: string; totalRows: number; importedRows: number; duplicateRows: number; failedRows: number; createdAt: string; completedAt?: string | null };
type JobDetails = { summary: Job; errors: { row: number; message: string }[] };

export default function ImportsPage() {
  const user = getStoredUser();
  const platform = isPlatformOperator(user);
  const [tenants, setTenants] = useState<Tenant[]>([]);
  const [tenantId, setTenantId] = useState(getEffectiveTenantId() ?? '');
  const [kind, setKind] = useState('athletes');
  const [rosterId, setRosterId] = useState('');
  const [duplicateHandling, setDuplicateHandling] = useState('Skip');
  const [file, setFile] = useState<File | null>(null);
  const [preview, setPreview] = useState<Preview | null>(null);
  const [jobs, setJobs] = useState<Job[]>([]);
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  const metrics = useMemo(() => {
    const running = jobs.filter(job => job.status === 'Queued' || job.status === 'Running').length;
    const imported = jobs.reduce((sum, job) => sum + job.importedRows, 0);
    const failed = jobs.reduce((sum, job) => sum + job.failedRows, 0);
    return {
      jobs: jobs.length,
      running,
      imported,
      failed,
    };
  }, [jobs]);

  const loadHistory = useCallback(async () => {
    if (!tenantId) { setJobs([]); return; }
    setJobs(await adminApi<Job[]>(`/api/bulkimport/history?tenantId=${tenantId}`));
  }, [tenantId]);

  useEffect(() => {
    if (!platform) return;
    adminApi<Tenant[]>('/api/tenants').then(items => {
      setTenants(items); setTenantId(current => current || items[0]?.id || '');
    }).catch(reason => setError(reason instanceof Error ? reason.message : 'Unable to load tenants.'));
  }, [platform]);
  useEffect(() => { void loadHistory().catch(() => undefined); }, [loadHistory]);
  useEffect(() => {
    if (!jobs.some(job => job.status === 'Queued' || job.status === 'Running')) return;
    const timer = window.setInterval(() => void loadHistory(), 2000);
    return () => window.clearInterval(timer);
  }, [jobs, loadHistory]);

  function endpoint(previewMode: boolean) {
    const prefix = previewMode ? '/api/bulkimport/preview' : '/api/bulkimport';
    const resource = kind === 'roster-entries' ? `rosters/${rosterId}/entries` : kind;
    const query = new URLSearchParams({ tenantId, ...(!previewMode ? { duplicateHandling } : {}) });
    return `${prefix}/${resource}?${query}`;
  }

  async function upload(previewMode: boolean) {
    if (!file || !tenantId || (kind === 'roster-entries' && !rosterId.trim())) {
      setError('Choose a tenant and CSV file; roster imports also require a roster ID.'); return;
    }
    setBusy(true); setError(null); setMessage(null);
    const body = new FormData(); body.append('file', file);
    try {
      if (previewMode) {
        setPreview(await adminApi<Preview>(endpoint(true), { method: 'POST', body }));
        setMessage('Preview completed without changing participant data.');
      } else {
        const response = await adminApi<Job | JobDetails>(endpoint(false), { method: 'POST', body });
        const job = 'summary' in response ? response.summary : response;
        setMessage(job.status === 'Queued' ? 'Large import queued for background processing.' : 'Import completed. Download its report below.');
        setPreview(null); await loadHistory();
      }
    } catch (reason) { setError(reason instanceof Error ? reason.message : 'Import request failed.'); }
    finally { setBusy(false); }
  }

  return <div className="space-y-6">
    <section className="overflow-hidden rounded-3xl bg-gradient-to-br from-slate-950 via-slate-900 to-slate-800 p-6 text-white shadow-sm">
      <div className="flex flex-col gap-5 xl:flex-row xl:items-end xl:justify-between">
        <div className="max-w-3xl">
          <p className="text-xs font-bold uppercase tracking-[0.2em] text-slate-400">Data operations</p>
          <h1 className="mt-2 text-3xl font-black tracking-tight">Bulk participant imports</h1>
          <p className="mt-2 max-w-2xl text-sm leading-6 text-slate-300">
            Validate CSV data before it touches production, control duplicate handling, queue large files, and retain an auditable report for every import.
          </p>
        </div>
        <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
          <ImportMetric label="Jobs" value={metrics.jobs} />
          <ImportMetric label="Active" value={metrics.running} />
          <ImportMetric label="Imported" value={metrics.imported} />
          <ImportMetric label="Failed" value={metrics.failed} tone={metrics.failed > 0 ? 'danger' : 'default'} />
        </div>
      </div>
    </section>
    {error && <p role="alert" className="rounded-md bg-red-50 p-3 text-sm text-red-700">{error}</p>}
    {message && <p role="status" className="rounded-md bg-green-50 p-3 text-sm text-green-800">{message}</p>}
    <section className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm"><div className="mb-5"><h2 className="text-lg font-extrabold text-slate-950">Import setup</h2><p className="mt-1 text-sm text-slate-500">Choose the target, validation rules and source file before previewing.</p></div><div className="grid gap-4 md:grid-cols-2 lg:grid-cols-4">
      {platform && <label className="text-sm font-semibold">Tenant<select value={tenantId} onChange={event => { setTenantId(event.target.value); setPreview(null); }} className="mt-1 h-10 w-full rounded-md border bg-white px-3 font-normal"><option value="">Select tenant</option>{tenants.map(item => <option key={item.id} value={item.id}>{item.name}</option>)}</select></label>}
      <label className="text-sm font-semibold">Import type<select value={kind} onChange={event => { setKind(event.target.value); setPreview(null); }} className="mt-1 h-10 w-full rounded-md border bg-white px-3 font-normal"><option value="athletes">Athletes</option><option value="teams">Teams</option><option value="roster-entries">Roster entries</option></select></label>
      {kind === 'roster-entries' && <label className="text-sm font-semibold">Roster ID<input value={rosterId} onChange={event => setRosterId(event.target.value)} placeholder="Roster UUID" className="mt-1 h-10 w-full rounded-md border px-3 font-normal" /></label>}
      <label className="text-sm font-semibold">Duplicates<select value={duplicateHandling} onChange={event => setDuplicateHandling(event.target.value)} className="mt-1 h-10 w-full rounded-md border bg-white px-3 font-normal"><option value="Skip">Skip and count</option><option value="Reject">Report as errors</option></select></label>
      <label className="text-sm font-semibold">CSV file<input type="file" accept=".csv,text/csv" onChange={event => { setFile(event.target.files?.[0] ?? null); setPreview(null); }} className="mt-1 block w-full text-sm font-normal" /></label>
    </div><div className="mt-4 flex flex-wrap gap-2"><button type="button" disabled={busy} onClick={() => void upload(true)} className="rounded-lg border border-slate-300 bg-white px-4 py-2 text-sm font-bold text-slate-800 shadow-sm transition hover:bg-slate-50 disabled:opacity-50">Validate preview</button><button type="button" disabled={busy || !preview} onClick={() => void upload(false)} className="rounded-lg bg-slate-950 px-4 py-2 text-sm font-bold text-white shadow-sm transition hover:bg-slate-800 disabled:opacity-50">Start import</button><button type="button" onClick={() => void adminDownload(`/api/bulkimport/templates/${kind}`)} className="rounded-lg border border-slate-300 bg-white px-4 py-2 text-sm font-bold text-slate-800 shadow-sm transition hover:bg-slate-50">Download template</button></div></section>
    {preview && <section className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm"><div><h2 className="text-lg font-extrabold text-slate-950">Validation preview</h2><p className="mt-1 text-sm text-slate-500">Review row quality before starting the import.</p></div><div className="mt-3 grid grid-cols-4 gap-3 text-center text-sm"><Metric label="Total" value={preview.total} /><Metric label="Valid" value={preview.valid} /><Metric label="Duplicates" value={preview.duplicates} /><Metric label="Invalid" value={preview.invalid} /></div><div className="mt-4 max-h-80 overflow-auto"><table className="w-full text-left text-sm"><thead className="bg-slate-50"><tr className="border-b border-slate-200"><th className="p-2">Row</th><th>Name</th><th>Status</th><th>Message</th></tr></thead><tbody>{preview.rows.map(row => <tr key={`${row.row}-${row.name}`} className="border-b"><td className="p-2">{row.row}</td><td>{row.name || '—'}</td><td><span className={row.status === 'Valid' ? 'text-green-700' : row.status === 'Duplicate' ? 'text-amber-700' : 'text-red-700'}>{row.status}</span></td><td>{row.message ?? '—'}</td></tr>)}</tbody></table></div></section>}
    <section className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm"><div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between"><div><h2 className="text-lg font-extrabold text-slate-950">Import history</h2><p className="mt-1 text-sm text-slate-500">The latest 100 imports for this tenant, including queued and completed jobs.</p></div><button type="button" onClick={() => void loadHistory()} className="rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm font-bold text-slate-800 shadow-sm transition hover:bg-slate-50">Refresh</button></div><div className="mt-4 overflow-x-auto"><table className="w-full text-left text-sm"><thead className="bg-slate-50"><tr className="border-b border-slate-200"><th className="p-2">Created</th><th>File</th><th>Status</th><th>Imported</th><th>Duplicates</th><th>Failed</th><th>Report</th></tr></thead><tbody>{jobs.map(job => <tr key={job.id} className="border-b"><td className="p-2">{new Date(job.createdAt).toLocaleString()}</td><td>{job.fileName}<span className="block text-xs text-gray-500">{job.kind}</span></td><td>{job.status}</td><td>{job.importedRows}/{job.totalRows}</td><td>{job.duplicateRows}</td><td>{job.failedRows}</td><td><button type="button" onClick={() => void adminDownload(`/api/bulkimport/${job.id}/report`)} className="font-semibold text-blue-700">Download CSV</button></td></tr>)}{jobs.length === 0 && <tr><td colSpan={7} className="p-8 text-center text-slate-500">No imports recorded for this tenant yet.</td></tr>}</tbody></table></div></section>
  </div>;
}

function Metric({ label, value }: { label: string; value: number }) {
  return <div className="rounded-xl border border-slate-200 bg-slate-50 p-3">
    <div className="text-xs font-bold uppercase tracking-wide text-slate-500">{label}</div>
    <div className="mt-1 text-2xl font-black text-slate-950">{value}</div>
  </div>;
}

function ImportMetric({
  label,
  value,
  tone = 'default',
}: {
  label: string;
  value: number;
  tone?: 'default' | 'danger';
}) {
  return <div className={`min-w-24 rounded-2xl border px-4 py-3 backdrop-blur ${
    tone === 'danger'
      ? 'border-red-400/30 bg-red-400/10'
      : 'border-white/10 bg-white/5'
  }`}>
    <div className="text-[10px] font-bold uppercase tracking-[0.16em] text-slate-400">{label}</div>
    <div className={`mt-1 text-2xl font-black ${tone === 'danger' ? 'text-red-200' : 'text-white'}`}>{value}</div>
  </div>;
}

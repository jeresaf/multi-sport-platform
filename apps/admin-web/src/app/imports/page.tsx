'use client';

import { useCallback, useEffect, useState } from 'react';
import { adminApi, adminDownload, getStoredUser, isPlatformOperator } from '@/lib/admin-api';

type Tenant = { id: string; name: string };
type PreviewRow = { row: number; name: string; status: string; message?: string | null };
type Preview = { total: number; valid: number; duplicates: number; invalid: number; rows: PreviewRow[] };
type Job = { id: string; kind: string; fileName: string; status: string; duplicateHandling: string; totalRows: number; importedRows: number; duplicateRows: number; failedRows: number; createdAt: string; completedAt?: string | null };
type JobDetails = { summary: Job; errors: { row: number; message: string }[] };

export default function ImportsPage() {
  const user = getStoredUser();
  const platform = isPlatformOperator(user);
  const [tenants, setTenants] = useState<Tenant[]>([]);
  const [tenantId, setTenantId] = useState(user?.tenantId ?? '');
  const [kind, setKind] = useState('athletes');
  const [rosterId, setRosterId] = useState('');
  const [duplicateHandling, setDuplicateHandling] = useState('Skip');
  const [file, setFile] = useState<File | null>(null);
  const [preview, setPreview] = useState<Preview | null>(null);
  const [jobs, setJobs] = useState<Job[]>([]);
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

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
    <div><h1 className="text-3xl font-bold text-gray-950">Bulk participant imports</h1><p className="mt-1 text-sm text-gray-600">Preview CSV rows, handle duplicates explicitly, queue large files, and retain downloadable import reports.</p></div>
    {error && <p role="alert" className="rounded-md bg-red-50 p-3 text-sm text-red-700">{error}</p>}
    {message && <p role="status" className="rounded-md bg-green-50 p-3 text-sm text-green-800">{message}</p>}
    <section className="rounded-xl border bg-white p-5"><div className="grid gap-4 md:grid-cols-2 lg:grid-cols-4">
      {platform && <label className="text-sm font-semibold">Tenant<select value={tenantId} onChange={event => { setTenantId(event.target.value); setPreview(null); }} className="mt-1 h-10 w-full rounded-md border bg-white px-3 font-normal"><option value="">Select tenant</option>{tenants.map(item => <option key={item.id} value={item.id}>{item.name}</option>)}</select></label>}
      <label className="text-sm font-semibold">Import type<select value={kind} onChange={event => { setKind(event.target.value); setPreview(null); }} className="mt-1 h-10 w-full rounded-md border bg-white px-3 font-normal"><option value="athletes">Athletes</option><option value="teams">Teams</option><option value="roster-entries">Roster entries</option></select></label>
      {kind === 'roster-entries' && <label className="text-sm font-semibold">Roster ID<input value={rosterId} onChange={event => setRosterId(event.target.value)} placeholder="Roster UUID" className="mt-1 h-10 w-full rounded-md border px-3 font-normal" /></label>}
      <label className="text-sm font-semibold">Duplicates<select value={duplicateHandling} onChange={event => setDuplicateHandling(event.target.value)} className="mt-1 h-10 w-full rounded-md border bg-white px-3 font-normal"><option value="Skip">Skip and count</option><option value="Reject">Report as errors</option></select></label>
      <label className="text-sm font-semibold">CSV file<input type="file" accept=".csv,text/csv" onChange={event => { setFile(event.target.files?.[0] ?? null); setPreview(null); }} className="mt-1 block w-full text-sm font-normal" /></label>
    </div><div className="mt-4 flex flex-wrap gap-2"><button type="button" disabled={busy} onClick={() => void upload(true)} className="rounded-md border bg-white px-4 py-2 text-sm font-semibold disabled:opacity-50">Validate preview</button><button type="button" disabled={busy || !preview} onClick={() => void upload(false)} className="rounded-md bg-gray-950 px-4 py-2 text-sm font-semibold text-white disabled:opacity-50">Start import</button><button type="button" onClick={() => void adminDownload(`/api/bulkimport/templates/${kind}`)} className="rounded-md border bg-white px-4 py-2 text-sm font-semibold">Download template</button></div></section>
    {preview && <section className="rounded-xl border bg-white p-5"><h2 className="font-bold">Validation preview</h2><div className="mt-3 grid grid-cols-4 gap-3 text-center text-sm"><Metric label="Total" value={preview.total} /><Metric label="Valid" value={preview.valid} /><Metric label="Duplicates" value={preview.duplicates} /><Metric label="Invalid" value={preview.invalid} /></div><div className="mt-4 max-h-80 overflow-auto"><table className="w-full text-left text-sm"><thead><tr className="border-b"><th className="p-2">Row</th><th>Name</th><th>Status</th><th>Message</th></tr></thead><tbody>{preview.rows.map(row => <tr key={`${row.row}-${row.name}`} className="border-b"><td className="p-2">{row.row}</td><td>{row.name || '—'}</td><td><span className={row.status === 'Valid' ? 'text-green-700' : row.status === 'Duplicate' ? 'text-amber-700' : 'text-red-700'}>{row.status}</span></td><td>{row.message ?? '—'}</td></tr>)}</tbody></table></div></section>}
    <section className="rounded-xl border bg-white p-5"><div className="flex items-center justify-between"><div><h2 className="font-bold">Import history</h2><p className="text-sm text-gray-500">The latest 100 imports for this tenant.</p></div><button type="button" onClick={() => void loadHistory()} className="rounded-md border px-3 py-2 text-sm font-semibold">Refresh</button></div><div className="mt-4 overflow-x-auto"><table className="w-full text-left text-sm"><thead><tr className="border-b"><th className="p-2">Created</th><th>File</th><th>Status</th><th>Imported</th><th>Duplicates</th><th>Failed</th><th>Report</th></tr></thead><tbody>{jobs.map(job => <tr key={job.id} className="border-b"><td className="p-2">{new Date(job.createdAt).toLocaleString()}</td><td>{job.fileName}<span className="block text-xs text-gray-500">{job.kind}</span></td><td>{job.status}</td><td>{job.importedRows}/{job.totalRows}</td><td>{job.duplicateRows}</td><td>{job.failedRows}</td><td><button type="button" onClick={() => void adminDownload(`/api/bulkimport/${job.id}/report`)} className="font-semibold text-blue-700">Download CSV</button></td></tr>)}{jobs.length === 0 && <tr><td colSpan={7} className="p-4 text-center text-gray-500">No imports recorded.</td></tr>}</tbody></table></div></section>
  </div>;
}

function Metric({ label, value }: { label: string; value: number }) { return <div className="rounded bg-gray-50 p-3"><div className="text-xs font-semibold uppercase text-gray-500">{label}</div><div className="text-2xl font-bold">{value}</div></div>; }

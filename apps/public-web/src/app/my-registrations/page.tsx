'use client';

import Link from 'next/link';
import { useEffect, useState } from 'react';
import { ClipboardList, LogOut, Pencil, RotateCw } from 'lucide-react';
import { applicantApi, clearApplicantSession, getApplicantSession } from '@/lib/applicant-api';
import { resolveTenantSlug, tenantPath } from '@/lib/public-api';

type Registration = { id: string; competitionId: string; competitionName?: string | null; participantName?: string | null; type: string; status: string; formStatus?: string | null; formRevision?: number | null; createdAt: string; updatedAt?: string | null; reviewNotes?: string | null; cancellationReason?: string | null };

export default function MyRegistrationsPage() {
  const [tenantSlug, setTenantSlug] = useState('');
  const [items, setItems] = useState<Registration[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);

  const load = async () => {
    setLoading(true);
    try { setItems(await applicantApi<Registration[]>('/api/applicant/registrations')); }
    catch (reason) { setError(reason instanceof Error ? reason.message : 'Unable to load registrations.'); }
    finally { setLoading(false); }
  };

  useEffect(() => {
    const slug = resolveTenantSlug();
    if (!getApplicantSession()) { window.location.replace(tenantPath('/login?returnTo=%2Fmy-registrations', slug)); return; }
    applicantApi<Registration[]>('/api/applicant/registrations').then(registrations => {
      setItems(registrations); setLoading(false);
      setTenantSlug(slug);
      if (new URLSearchParams(window.location.search).has('submitted')) setMessage('Registration submitted for review.');
    }).catch(reason => { setError(reason instanceof Error ? reason.message : 'Unable to load registrations.'); setLoading(false); });
  }, []);

  async function withdraw(item: Registration) {
    const reason = window.prompt('Reason for withdrawing this registration:');
    if (!reason?.trim()) return;
    try { await applicantApi(`/api/applicant/registrations/${item.id}/withdraw`, { method: 'POST', body: JSON.stringify({ reason }) }); setMessage('Registration withdrawn.'); await load(); }
    catch (reasonValue) { setError(reasonValue instanceof Error ? reasonValue.message : 'Unable to withdraw.'); }
  }

  function logout() { clearApplicantSession(); window.location.assign(tenantPath('/', tenantSlug)); }

  return <main className="app-container py-8 sm:py-12">
    <div className="flex flex-col justify-between gap-4 sm:flex-row sm:items-end"><div><p className="text-xs font-bold uppercase tracking-[0.18em] text-[var(--brand-primary)]">Applicant account</p><h1 className="mt-2 text-3xl font-black">My Registrations</h1><p className="mt-2 muted">Resume drafts and track official review decisions for every athlete or team you manage.</p></div><button onClick={logout} className="inline-flex items-center justify-center gap-2 rounded-xl border border-[var(--color-border)] px-4 py-2 text-sm font-bold"><LogOut className="h-4 w-4" />Sign out</button></div>
    {error && <p role="alert" className="mt-5 rounded-xl bg-red-50 p-3 text-sm text-red-700">{error}</p>}{message && <p role="status" className="mt-5 rounded-xl bg-green-50 p-3 text-sm text-green-800">{message}</p>}
    {loading ? <p className="mt-8 muted">Loading registrations…</p> : items.length === 0 ? <section className="mt-8 rounded-2xl border border-dashed border-[var(--color-border)] p-10 text-center"><ClipboardList className="mx-auto h-10 w-10 muted" /><h2 className="mt-4 text-xl font-black">No registrations yet</h2><p className="mt-2 muted">Open a competition and choose Register to create your first entry.</p><Link href={tenantPath('/competitions', tenantSlug)} className="mt-5 inline-block rounded-xl bg-[var(--brand-primary)] px-5 py-3 font-bold text-white">Browse competitions</Link></section> : <div className="mt-8 grid gap-4 lg:grid-cols-2">{items.map(item => {
      const editable = item.status === 'Draft' || item.formStatus === 'ChangesRequested';
      const withdrawable = !['Withdrawn', 'Confirmed', 'Cancelled', 'NoShow'].includes(item.status);
      return <article key={item.id} className="surface rounded-2xl border border-[var(--color-border)] p-5 shadow-sm"><div className="flex items-start justify-between gap-3"><div><p className="text-xs font-bold uppercase muted">{item.participantName} · {item.type}</p><h2 className="mt-1 text-lg font-black">{item.competitionName ?? 'Competition registration'}</h2></div><Status value={item.status} /></div><div className="mt-4 grid grid-cols-2 gap-3 rounded-xl bg-[var(--color-surface-muted)] p-3 text-sm"><div><span className="block text-xs muted">Application</span><strong>{item.formStatus ?? 'No form'}</strong></div><div><span className="block text-xs muted">Revision</span><strong>{item.formRevision ?? '—'}</strong></div></div>{(item.reviewNotes || item.cancellationReason) && <p className="mt-3 rounded-xl border border-[var(--color-border)] p-3 text-sm"><strong>Official note:</strong> {item.reviewNotes || item.cancellationReason}</p>}<div className="mt-4 flex flex-wrap gap-2">{editable && <Link href={tenantPath(`/apply/${item.competitionId}?registration=${item.id}`, tenantSlug)} className="inline-flex items-center gap-2 rounded-xl bg-[var(--brand-primary)] px-4 py-2 text-sm font-bold text-white"><Pencil className="h-4 w-4" />{item.formStatus === 'ChangesRequested' ? 'Make changes' : 'Resume draft'}</Link>}<button onClick={() => void load()} className="inline-flex items-center gap-2 rounded-xl border border-[var(--color-border)] px-4 py-2 text-sm font-bold"><RotateCw className="h-4 w-4" />Refresh</button>{withdrawable && <button onClick={() => void withdraw(item)} className="rounded-xl border border-red-200 px-4 py-2 text-sm font-bold text-red-700">Withdraw</button>}</div></article>;
    })}</div>}
  </main>;
}

function Status({ value }: { value: string }) { const tone = value === 'Approved' || value === 'Confirmed' ? 'bg-green-50 text-green-800' : value === 'Rejected' || value === 'Cancelled' || value === 'Withdrawn' ? 'bg-red-50 text-red-700' : value === 'Draft' ? 'bg-gray-100 text-gray-700' : 'bg-amber-50 text-amber-800'; return <span className={`rounded-full px-3 py-1 text-xs font-bold ${tone}`}>{value.replace(/([a-z])([A-Z])/g, '$1 $2')}</span>; }

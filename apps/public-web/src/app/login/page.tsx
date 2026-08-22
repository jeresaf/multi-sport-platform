'use client';

import { ArrowLeft, LockKeyhole, ShieldCheck, UserPlus } from 'lucide-react';
import Link from 'next/link';
import { FormEvent, useEffect, useState } from 'react';
import { authenticateApplicant, clearApplicantSession, getApplicantSession } from '@/lib/applicant-api';
import { publicApi, resolveTenantSlug, tenantPath } from '@/lib/public-api';

type TenantIdentity = { id: string; name: string };

export default function PublicLoginPage() {
  const [mode, setMode] = useState<'login' | 'register'>('login');
  const [tenant, setTenant] = useState<TenantIdentity | null>(null);
  const [tenantSlug, setTenantSlug] = useState('');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [firstName, setFirstName] = useState('');
  const [lastName, setLastName] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [tenantMismatch, setTenantMismatch] = useState(false);

  useEffect(() => {
    const slug = resolveTenantSlug();
    publicApi<TenantIdentity>('', undefined, slug).then(identity => {
      setTenant(identity); setTenantSlug(slug);
      const session = getApplicantSession();
      if (session?.user.tenantId === identity.id) {
        const destination = new URLSearchParams(window.location.search).get('returnTo') || '/my-registrations';
        window.location.replace(tenantPath(destination, slug));
      } else if (session) setTenantMismatch(true);
    }).catch(reason =>
      setError(reason instanceof Error ? reason.message : 'Unable to load this organisation.'));
  }, []);

  async function submit(event: FormEvent) {
    event.preventDefault();
    if (!tenant) return;
    setBusy(true); setError(null);
    try {
      await authenticateApplicant(mode, mode === 'login'
        ? { email, password, deviceId: 'public-web', deviceName: 'Public web', deviceType: 'Web' }
        : { email, password, firstName, lastName, tenantId: tenant.id, deviceId: 'public-web', deviceName: 'Public web', deviceType: 'Web' });
      const destination = new URLSearchParams(window.location.search).get('returnTo') || '/my-registrations';
      window.location.assign(tenantPath(destination, tenantSlug));
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'Unable to sign in.');
    } finally { setBusy(false); }
  }

  const configured = process.env.NEXT_PUBLIC_ADMIN_URL?.replace(/\/$/, '');
  const inferredAdmin = typeof window !== 'undefined' ? `${window.location.protocol}//${window.location.hostname}:3000` : '';

  return <main className="app-container grid min-h-[75vh] items-center gap-8 py-10 lg:grid-cols-[0.8fr_1.2fr]">
    <section className="space-y-5">
      <Link href={tenantPath('/', tenantSlug || resolveTenantSlug())} className="inline-flex items-center gap-2 text-sm font-semibold muted"><ArrowLeft className="h-4 w-4" />Back to public scores</Link>
      <span className="flex h-14 w-14 items-center justify-center rounded-2xl bg-[var(--brand-primary)] text-white"><LockKeyhole className="h-7 w-7" /></span>
      <div><p className="text-xs font-bold uppercase tracking-[0.18em] text-[var(--brand-primary)]">Competition entry</p><h1 className="mt-2 text-4xl font-black tracking-tight">Register for {tenant?.name ?? 'your competition'}</h1><p className="mt-3 max-w-xl leading-7 muted">Create participant profiles, save application drafts, upload required documents, submit entries, and track every review decision from one account.</p></div>
      <div className="rounded-2xl bg-[var(--color-surface-muted)] p-4 text-sm"><p className="flex items-start gap-2"><ShieldCheck className="mt-0.5 h-4 w-4 shrink-0 text-[var(--status-finished)]" />Your account only sees participant profiles and registrations that you create.</p></div>
      <p className="text-sm muted">Administrator or scorer? <a href={`${configured || inferredAdmin}/login`} className="font-bold text-[var(--brand-primary)]">Open administration sign in</a>.</p>
    </section>
    <section className="surface rounded-[28px] p-6 shadow-[var(--shadow-card)] sm:p-8">
      <div className="grid grid-cols-2 rounded-xl bg-[var(--color-surface-muted)] p-1">
        <button onClick={() => setMode('login')} className={`rounded-lg px-4 py-3 text-sm font-bold ${mode === 'login' ? 'surface shadow-sm' : ''}`}>Sign in</button>
        <button onClick={() => setMode('register')} className={`rounded-lg px-4 py-3 text-sm font-bold ${mode === 'register' ? 'surface shadow-sm' : ''}`}><UserPlus className="mr-1 inline h-4 w-4" />Create account</button>
      </div>
      {error && <p role="alert" className="mt-5 rounded-xl bg-red-50 p-3 text-sm text-red-700">{error}</p>}
      {tenantMismatch && <div role="alert" className="mt-5 rounded-xl bg-amber-50 p-3 text-sm text-amber-900"><p>You are signed in to a different organisation.</p><button type="button" onClick={() => { clearApplicantSession(); setTenantMismatch(false); }} className="mt-2 font-bold underline">Sign out and continue here</button></div>}
      <form onSubmit={submit} className="mt-6 space-y-4">
        {mode === 'register' && <div className="grid gap-4 sm:grid-cols-2"><Field label="First name" value={firstName} onChange={setFirstName} /><Field label="Last name" value={lastName} onChange={setLastName} /></div>}
        <Field label="Email address" type="email" value={email} onChange={setEmail} />
        <Field label="Password" type="password" value={password} onChange={setPassword} minimum={8} />
        <button disabled={busy || !tenant} className="w-full rounded-xl bg-[var(--brand-primary)] px-4 py-3 font-bold text-white disabled:opacity-50">{busy ? 'Please wait…' : mode === 'login' ? 'Sign in to registrations' : 'Create registration account'}</button>
      </form>
    </section>
  </main>;
}

function Field({ label, value, onChange, type = 'text', minimum }: { label: string; value: string; onChange: (value: string) => void; type?: string; minimum?: number }) {
  return <label className="block text-sm font-bold">{label}<input required type={type} minLength={minimum} value={value} onChange={event => onChange(event.target.value)} className="mt-1 h-11 w-full rounded-xl border border-[var(--color-border)] bg-[var(--color-surface)] px-3 font-normal outline-none focus:border-[var(--brand-primary)]" /></label>;
}

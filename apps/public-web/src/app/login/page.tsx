'use client';

import { ArrowLeft, LockKeyhole, ShieldCheck } from 'lucide-react';
import Link from 'next/link';
import { useEffect, useState } from 'react';
import { resolveTenantSlug, tenantPath } from '@/lib/public-api';

export default function PublicLoginPage() {
  const [adminLoginUrl, setAdminLoginUrl] = useState<string | null>(null);
  const [homeUrl, setHomeUrl] = useState('/');

  useEffect(() => {
    const frame = window.requestAnimationFrame(() => {
      const configured = process.env.NEXT_PUBLIC_ADMIN_URL?.replace(/\/$/, '');
      const inferred = `${window.location.protocol}//${window.location.hostname}:3000`;
      setAdminLoginUrl(`${configured || inferred}/login`);
      setHomeUrl(tenantPath('/', resolveTenantSlug()));
    });
    return () => window.cancelAnimationFrame(frame);
  }, []);

  return (
    <main className="app-container flex min-h-[70vh] items-center justify-center py-10">
      <section className="surface w-full max-w-lg rounded-[28px] p-6 shadow-[var(--shadow-card)] sm:p-8">
        <span className="flex h-12 w-12 items-center justify-center rounded-2xl bg-[var(--brand-primary)] text-white"><LockKeyhole className="h-6 w-6" /></span>
        <p className="mt-6 text-xs font-bold uppercase tracking-[0.18em] text-[var(--brand-primary)]">Secure access</p>
        <h1 className="mt-2 text-3xl font-black tracking-tight">Sign in to manage or score</h1>
        <p className="mt-3 leading-6 muted">Public scores remain available without an account. Administrators, officials and scorers sign in through the secure administration workspace.</p>
        <div className="mt-6 rounded-2xl bg-[var(--color-surface-muted)] p-4 text-sm">
          <p className="flex items-start gap-2"><ShieldCheck className="mt-0.5 h-4 w-4 shrink-0 text-[var(--status-finished)]" />Your tenant permissions determine which administration and scoring tools are available.</p>
        </div>
        <div className="mt-7 flex flex-col gap-3 sm:flex-row">
          {adminLoginUrl ? <a href={adminLoginUrl} className="inline-flex flex-1 items-center justify-center rounded-xl bg-[var(--brand-primary)] px-4 py-3 font-bold text-white">Continue to sign in</a> : <span className="inline-flex flex-1 items-center justify-center rounded-xl bg-[var(--color-surface-muted)] px-4 py-3 font-semibold muted">Preparing secure sign in…</span>}
          <Link href={homeUrl} className="inline-flex flex-1 items-center justify-center gap-2 rounded-xl border border-[var(--color-border)] px-4 py-3 font-bold"><ArrowLeft className="h-4 w-4" />Back to scores</Link>
        </div>
      </section>
    </main>
  );
}

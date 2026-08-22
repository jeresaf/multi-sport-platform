'use client';

import Image from 'next/image';
import Link from 'next/link';
import { usePathname } from 'next/navigation';
import { Heart, Home, Menu, Moon, Radio, Search, Sun, Trophy, UserRound } from 'lucide-react';
import { useEffect, useMemo, useState } from 'react';
import { formatSport, publicApi, type PublicCompetition, resolveTenantSlug, sportSlug, TENANT_SLUG, tenantPath } from '@/lib/public-api';
import { getApplicantSession } from '@/lib/applicant-api';

type TenantIdentity = {
  id: string;
  name: string;
  shortName: string;
  branding?: { logoUrl?: string | null; primaryColor?: string | null; secondaryColor?: string | null; accentColor?: string | null; contactEmail?: string | null; contactPhone?: string | null; website?: string | null } | null;
};

export function TenantBrandShell({ children }: { children: React.ReactNode }) {
  const [tenant, setTenant] = useState<TenantIdentity | null>(null);
  const [competitions, setCompetitions] = useState<PublicCompetition[]>([]);
  const [tenantSlug, setTenantSlug] = useState(TENANT_SLUG);
  const [dark, setDark] = useState(false);
  const [applicantSignedIn, setApplicantSignedIn] = useState(false);

  useEffect(() => {
    const resolvedTenant = resolveTenantSlug();
    Promise.all([
      publicApi<TenantIdentity>('', undefined, resolvedTenant),
      publicApi<PublicCompetition[]>('/competitions', undefined, resolvedTenant),
    ]).then(([identity, publishedCompetitions]) => {
      setTenantSlug(resolvedTenant);
      setTenant(identity);
      setApplicantSignedIn(getApplicantSession()?.user.tenantId === identity.id);
      setCompetitions(publishedCompetitions);
      document.documentElement.style.setProperty('--brand-primary', validHex(identity.branding?.primaryColor) ?? '#2563EB');
      document.documentElement.style.setProperty('--brand-secondary', validHex(identity.branding?.secondaryColor) ?? '#7C3AED');
      document.documentElement.style.setProperty('--brand-accent', validHex(identity.branding?.accentColor) ?? '#F59E0B');
      document.title = identity.name;
    }).catch(() => undefined);
    const savedTheme = localStorage.getItem('sports-platform:theme');
    const prefersDark = window.matchMedia('(prefers-color-scheme: dark)').matches;
    const shouldUseDark = savedTheme ? savedTheme === 'dark' : prefersDark;
    const frame = window.requestAnimationFrame(() => setDark(shouldUseDark));
    document.documentElement.dataset.theme = shouldUseDark ? 'dark' : 'light';
    return () => window.cancelAnimationFrame(frame);
  }, []);

  useEffect(() => {
    const syncApplicant = () => setApplicantSignedIn(
      Boolean(tenant?.id && getApplicantSession()?.user.tenantId === tenant.id),
    );
    syncApplicant();
    window.addEventListener('sports-platform:applicant-session', syncApplicant);
    return () => window.removeEventListener('sports-platform:applicant-session', syncApplicant);
  }, [tenant?.id]);

  const href = (path: string) => tenantPath(path, tenantSlug);
  const sports = useMemo(() => Array.from(new Map(competitions.map(item => [
    item.sportId,
    {
      id: item.sportId,
      code: item.sportCode?.toLowerCase() ?? sportSlug(item.sportId, item.sportName ?? undefined),
      label: item.sportName || formatSport(item.sportId),
    },
  ])).values()), [competitions]);
  const flagshipCodes = (process.env.NEXT_PUBLIC_FLAGSHIP_SPORTS ?? 'football,basketball,cricket,rugby')
    .split(',').map(value => value.trim().toLowerCase()).filter(Boolean);
  const flagships = flagshipCodes
    .map(code => sports.find(item => item.code === code || item.code.startsWith(`${code}_`) || item.label.toLowerCase().startsWith(code)))
    .filter((item, index, values) => item && values.findIndex(other => other?.id === item.id) === index)
    .filter(Boolean) as typeof sports;
  const flagshipIds = new Set(flagships.map(item => item.id));
  const moreSports = sports.filter(item => !flagshipIds.has(item.id));

  const toggleTheme = () => {
    const next = !dark;
    setDark(next);
    document.documentElement.dataset.theme = next ? 'dark' : 'light';
    localStorage.setItem('sports-platform:theme', next ? 'dark' : 'light');
  };

  return (
    <div className="min-h-screen bg-[var(--color-background)] pb-16 md:pb-0">
      <a className="skip-link" href="#main-content">Skip to content</a>
      <header className="sticky top-0 z-40 border-b border-[var(--color-border)] bg-[var(--color-surface)]/95 backdrop-blur">
        <div className="app-container flex h-16 items-center gap-4">
          <Link href={href('/')} className="flex min-w-0 items-center gap-2.5" aria-label={`${tenant?.name ?? 'Sports Platform'} home`}>
            {tenant?.branding?.logoUrl ? <Image unoptimized width={36} height={36} src={tenant.branding.logoUrl} alt="" className="h-9 w-9 rounded-lg bg-white object-contain p-1" /> : <span className="flex h-9 w-9 items-center justify-center rounded-xl bg-[var(--brand-primary)] text-white"><Trophy className="h-5 w-5" /></span>}
            <span className="max-w-44 truncate font-extrabold sm:max-w-64">{tenant?.shortName ?? 'Sports Platform'}</span>
          </Link>

          <nav className="ml-auto hidden items-center gap-1 lg:flex" aria-label="Primary navigation">
            <NavLink href={href('/')}>Home</NavLink>
            <NavLink href={href('/live')}><Radio className="h-4 w-4 text-[var(--status-live)]" />Live</NavLink>
            {flagships.map(item => <NavLink key={item.id} href={href(`/sport/${sportSlug(item.id, item.label)}`)}>{item.label.startsWith('Rugby') ? 'Rugby' : item.label}</NavLink>)}
            {moreSports.length > 0 && <details className="relative"><summary className="flex cursor-pointer list-none items-center gap-1 rounded-lg px-3 py-2 text-sm font-semibold hover:bg-[var(--color-surface-muted)]">More <Menu className="h-4 w-4" /></summary><div className="surface absolute right-0 top-11 z-50 grid min-w-48 gap-1 rounded-xl p-2 shadow-xl">{moreSports.map(item => <NavLink key={item.id} href={href(`/sport/${sportSlug(item.id, item.label)}`)}>{item.label}</NavLink>)}</div></details>}
          </nav>

          <div className="ml-auto flex items-center gap-1 lg:ml-2">
            <Link href={href('/search')} className="rounded-full p-2 hover:bg-[var(--color-surface-muted)]" aria-label="Search"><Search className="h-5 w-5" /></Link>
            <Link href={href('/favourites')} className="hidden rounded-full p-2 hover:bg-[var(--color-surface-muted)] sm:block" aria-label="Favourites"><Heart className="h-5 w-5" /></Link>
            {applicantSignedIn && <Link href={href('/my-registrations')} className="hidden rounded-lg px-3 py-2 text-sm font-bold text-[var(--brand-primary)] sm:block">My registrations</Link>}
            <button onClick={toggleTheme} className="rounded-full p-2 hover:bg-[var(--color-surface-muted)]" aria-label={dark ? 'Use light theme' : 'Use dark theme'}>{dark ? <Sun className="h-5 w-5" /> : <Moon className="h-5 w-5" />}</button>
            <Link href={href(applicantSignedIn ? '/my-registrations' : '/login')} className="rounded-full p-2 hover:bg-[var(--color-surface-muted)]" aria-label={applicantSignedIn ? 'My registrations' : 'Log in'}><UserRound className="h-5 w-5" /></Link>
          </div>
        </div>
      </header>

      <div id="main-content">{children}</div>

      <nav className="fixed inset-x-0 bottom-0 z-40 grid h-16 grid-cols-5 border-t border-[var(--color-border)] bg-[var(--color-surface)] md:hidden" aria-label="Mobile navigation">
        <MobileLink href={href('/')} icon={<Home />}>Home</MobileLink>
        <MobileLink href={href('/live')} icon={<Radio />}>Live</MobileLink>
        <MobileLink href={href('/search')} icon={<Search />}>Search</MobileLink>
        <MobileLink href={href('/competitions')} icon={<Trophy />}>Competitions</MobileLink>
        <MobileLink href={href('/favourites')} icon={<Heart />}>Favourites</MobileLink>
      </nav>

      <footer className="mt-12 border-t border-[var(--color-border)] bg-[var(--color-surface)]"><div className="app-container space-y-2 py-8 text-center text-sm muted"><p>© 2026 {tenant?.name ?? 'Sports Platform'}. All rights reserved.</p>{(tenant?.branding?.contactEmail || tenant?.branding?.contactPhone || tenant?.branding?.website) && <p className="flex flex-wrap justify-center gap-3">{tenant.branding.contactEmail && <a href={`mailto:${tenant.branding.contactEmail}`}>{tenant.branding.contactEmail}</a>}{tenant.branding.contactPhone && <a href={`tel:${tenant.branding.contactPhone}`}>{tenant.branding.contactPhone}</a>}{tenant.branding.website && <a href={tenant.branding.website} target="_blank" rel="noreferrer">Official website</a>}</p>}</div></footer>
    </div>
  );
}

function NavLink({ href, children }: { href: string; children: React.ReactNode }) {
  const pathname = usePathname();
  const target = href.split('?')[0];
  const active = target === '/' ? pathname === '/' : pathname === target || pathname.startsWith(`${target}/`);
  return <Link href={href} aria-current={active ? 'page' : undefined} className={`flex items-center gap-1.5 rounded-lg px-3 py-2 text-sm font-semibold hover:bg-[var(--color-surface-muted)] ${active ? 'bg-[var(--color-surface-muted)] text-[var(--brand-primary)]' : ''}`}>{children}</Link>;
}
function MobileLink({ href, icon, children }: { href: string; icon: React.ReactNode; children: React.ReactNode }) {
  const pathname = usePathname();
  const target = href.split('?')[0];
  const active = target === '/' ? pathname === '/' : pathname === target || pathname.startsWith(`${target}/`);
  return <Link href={href} aria-current={active ? 'page' : undefined} className={`flex flex-col items-center justify-center gap-0.5 text-[10px] font-semibold [&_svg]:h-5 [&_svg]:w-5 ${active ? 'text-[var(--brand-primary)]' : 'muted'}`}>{icon}{children}</Link>;
}
function validHex(value?: string | null) { return value && /^#[0-9a-fA-F]{6}$/.test(value) ? value : null; }

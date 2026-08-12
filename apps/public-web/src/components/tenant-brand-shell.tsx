'use client';

import Link from 'next/link';
import Image from 'next/image';
import { useEffect, useState } from 'react';
import { publicApi, resolveTenantSlug, TENANT_SLUG, tenantPath } from '@/lib/public-api';

type TenantIdentity = {
  name: string;
  shortName: string;
  branding?: {
    logoUrl?: string | null;
    primaryColor?: string | null;
    secondaryColor?: string | null;
    accentColor?: string | null;
    contactEmail?: string | null;
    contactPhone?: string | null;
    website?: string | null;
  } | null;
};

export function TenantBrandShell({ children }: { children: React.ReactNode }) {
  const [tenant, setTenant] = useState<TenantIdentity | null>(null);
  const [tenantSlug, setTenantSlug] = useState('');

  useEffect(() => {
    const resolvedTenant = resolveTenantSlug();
    publicApi<TenantIdentity>('', undefined, resolvedTenant).then(identity => {
      setTenantSlug(resolvedTenant);
      setTenant(identity);
      const primary = validHex(identity.branding?.primaryColor) ?? '#0EA5E9';
      const secondary = validHex(identity.branding?.secondaryColor) ?? '#7C3AED';
      const accent = validHex(identity.branding?.accentColor) ?? '#F59E0B';
      document.documentElement.style.setProperty('--brand-primary', primary);
      document.documentElement.style.setProperty('--brand-secondary', secondary);
      document.documentElement.style.setProperty('--brand-accent', accent);
      document.title = identity.name;
    }).catch(() => undefined);
  }, []);

  const href = (path: string) => tenantPath(path, tenantSlug || TENANT_SLUG);
  const navigation = [
    ['Home', '/'],
    ['Search', '/search'],
    ['Competitions', '/competitions'],
    ['Results', '/results'],
    ['Standings', '/standings'],
    ['Playoffs', '/playoffs'],
    ['Golf', '/leaderboard'],
    ['Fight card', '/fight-card'],
    ['Athletics', '/athletics'],
    ['Cricket', '/cricket'],
    ['Rugby', '/rugby'],
  ];

  return (
    <div className="min-h-screen bg-white">
      <header className="text-white" style={{ backgroundColor: 'var(--brand-primary)' }}>
        <div className="container mx-auto px-4 py-4">
          <div className="flex items-center justify-between gap-4">
            <div className="flex items-center gap-2">
              {tenant?.branding?.logoUrl
                ? <Image unoptimized width={36} height={36} src={tenant.branding.logoUrl} alt={`${tenant.name} logo`} className="h-9 w-9 rounded-lg bg-white object-contain p-1" />
                : <div className="flex h-9 w-9 items-center justify-center rounded-lg" style={{ backgroundColor: 'var(--brand-secondary)' }}>🏆</div>}
              <span className="font-bold text-xl">{tenant?.name ?? 'Sports Platform'}</span>
            </div>
            <nav className="hidden items-center gap-5 text-sm lg:flex">
              {navigation.map(([label, path]) => <Link key={path} href={href(path)}>{label}</Link>)}
            </nav>
          </div>
          <nav className="mt-4 flex gap-5 overflow-x-auto pb-1 text-sm lg:hidden" aria-label="Public navigation">
            {navigation.map(([label, path]) => (
              <Link className="shrink-0" key={path} href={href(path)}>{label}</Link>
            ))}
          </nav>
        </div>
      </header>
      <main>{children}</main>
      <footer className="mt-12 border-t border-gray-200 bg-gray-100"><div className="container mx-auto space-y-2 px-4 py-8 text-center text-sm text-gray-500"><p>© 2026 {tenant?.name ?? 'Sports Platform'}. All rights reserved.</p>{(tenant?.branding?.contactEmail || tenant?.branding?.contactPhone || tenant?.branding?.website) && <p className="flex flex-wrap justify-center gap-3">{tenant.branding.contactEmail && <a href={`mailto:${tenant.branding.contactEmail}`}>{tenant.branding.contactEmail}</a>}{tenant.branding.contactPhone && <a href={`tel:${tenant.branding.contactPhone}`}>{tenant.branding.contactPhone}</a>}{tenant.branding.website && <a href={tenant.branding.website} target="_blank" rel="noreferrer">Official website</a>}</p>}</div></footer>
    </div>
  );
}

function validHex(value?: string | null) {
  return value && /^#[0-9a-fA-F]{6}$/.test(value) ? value : null;
}

'use client';

import Link from 'next/link';
import { useCallback, useEffect, useState } from 'react';
import {
  Activity, ArrowRight, CalendarDays, ClipboardCheck, FileBarChart, Flag, Palette,
  RefreshCw, ShieldCheck, Trophy, UserCheck, Users, Handshake,
} from 'lucide-react';
import { adminApi } from '@/lib/admin-api';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';

type Module = {
  name: string;
  key: string;
  count: number | null;
  accessible: boolean;
  permissions: string[];
};
type UpcomingContest = {
  id: string;
  name: string;
  startTime: string;
  venue?: string | null;
  status: string;
};
type TenantDashboardData = {
  generatedAtUtc: string;
  tenant: { id: string; name: string; shortName: string; slug: string; status: string; timezone?: string | null };
  branding: { tenantName: string; shortName?: string | null; logoUrl?: string | null; primaryColor?: string | null; secondaryColor?: string | null; accentColor?: string | null };
  summary: { liveContests: number; pendingResults: number };
  modules: Module[];
  upcoming: UpcomingContest[];
};

const modulePresentation: Record<string, { href?: string; icon: React.ComponentType<{ className?: string }> }> = {
  branding: { href: '/branding', icon: Palette },
  sports: { href: '/competitions', icon: Trophy },
  participants: { href: '/participants', icon: Users },
  registrations: { href: '/registrations', icon: ClipboardCheck },
  competitions: { href: '/competitions', icon: Trophy },
  fixtures: { href: '/schedule', icon: CalendarDays },
  officials: { href: '/schedule', icon: UserCheck },
  'live-contests': { href: '/scoring', icon: Activity },
  results: { href: '/results', icon: Flag },
  reports: { href: '/reports', icon: FileBarChart },
  sponsors: { href: '/sponsors', icon: Handshake },
};

export function TenantDashboard() {
  const [dashboard, setDashboard] = useState<TenantDashboardData | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const refresh = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      setDashboard(await adminApi<TenantDashboardData>('/api/tenant-admin/dashboard'));
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'Could not load tenant administration.');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => { void refresh(); }, [refresh]);

  if (loading && !dashboard) {
    return <p className="rounded-lg border bg-white p-4 text-gray-500">Loading tenant administration…</p>;
  }

  return (
    <div className="space-y-6" data-testid="tenant-administration-dashboard">
      <section className="overflow-hidden rounded-2xl border border-gray-200 bg-white shadow-sm">
        <div
          className="px-6 py-7 text-white lg:px-8"
          style={{ background: `linear-gradient(135deg, ${dashboard?.branding.primaryColor || '#0EA5E9'}, ${dashboard?.branding.secondaryColor || '#7C3AED'})` }}
        >
          <div className="flex flex-wrap items-end justify-between gap-5">
            <div className="max-w-2xl">
              <p className="text-xs font-bold uppercase tracking-[0.22em] text-white/70">Operations overview</p>
              <h1 className="mt-2 text-3xl font-bold tracking-tight sm:text-4xl">
                {dashboard?.branding.tenantName || dashboard?.tenant.name || 'Tenant administration'}
              </h1>
              <p className="mt-2 max-w-xl text-sm leading-6 text-white/80">
                Monitor live activity, fixtures, competitions, participants, registrations, results, and reporting from one command centre.
              </p>
            </div>
            <Button
              variant="outline"
              className="border-white/40 bg-white/10 text-white hover:bg-white/20"
              disabled={loading}
              onClick={() => void refresh()}
            >
              <RefreshCw className={`mr-2 h-4 w-4 ${loading ? 'animate-spin' : ''}`} />
              Refresh
            </Button>
          </div>
        </div>

        {dashboard && (
          <div className="grid gap-px bg-gray-200 sm:grid-cols-2 xl:grid-cols-4">
            <DashboardMetric label="Live contests" value={dashboard.summary.liveContests} hint="Active now" />
            <DashboardMetric label="Pending results" value={dashboard.summary.pendingResults} hint="Need review" />
            <DashboardMetric label="Upcoming" value={dashboard.upcoming.length} hint="Scheduled fixtures" />
            <DashboardMetric label="Tenant" value={dashboard.tenant.status} hint={dashboard.tenant.shortName} />
          </div>
        )}
      </section>

      {error && <p role="alert" className="rounded-lg border border-red-200 bg-red-50 p-4 text-red-700">{error}</p>}

      {dashboard && <>
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4">
          {dashboard.modules.map((module) => {
            const presentation = modulePresentation[module.key] ?? { icon: ShieldCheck };
            const Icon = presentation.icon;
            const content = <Card className={`h-full transition-shadow ${module.accessible && presentation.href ? 'hover:shadow-md' : 'bg-gray-50'}`}>
              <CardHeader className="flex flex-row items-center justify-between pb-2">
                <CardTitle className="text-base">{module.name}</CardTitle>
                <Icon className={`h-5 w-5 ${module.accessible ? 'text-primary' : 'text-gray-400'}`} />
              </CardHeader>
              <CardContent>
                <p className="text-2xl font-bold">{module.count ?? '—'}</p>
                <div className="mt-1 flex items-center justify-between gap-2 text-xs text-gray-500">
                  <span>
                    {module.accessible ? (presentation.href ? 'Open workspace' : 'Connected operational overview') : 'Not available for your role'}
                  </span>
                  {module.accessible && presentation.href && <ArrowRight className="h-4 w-4 text-gray-300" />}
                </div>
              </CardContent>
            </Card>;
            return module.accessible && presentation.href
              ? <Link key={module.key} href={presentation.href} aria-label={`Open ${module.name}`}>{content}</Link>
              : <div key={module.key}>{content}</div>;
          })}
        </div>

        <div className="grid gap-4 lg:grid-cols-[2fr_1fr]">
          <Card>
            <CardHeader>
              <div className="flex flex-wrap items-center justify-between gap-3">
                <div>
                  <CardTitle>Upcoming fixtures</CardTitle>
                  <CardDescription>Next contests scheduled for this tenant.</CardDescription>
                </div>
                <Link href="/schedule" className="inline-flex items-center gap-1.5 text-sm font-semibold text-primary">
                  Open schedule <ArrowRight className="h-4 w-4" />
                </Link>
              </div>
            </CardHeader>
            <CardContent className="space-y-3">
              {dashboard.upcoming.map((contest) => <div key={contest.id} className="flex flex-wrap items-center justify-between gap-2 rounded-lg border p-3 text-sm">
                <div><p className="font-semibold">{contest.name}</p><p className="text-gray-500">{contest.venue || 'Venue not set'}</p></div>
                <div className="text-right"><p>{new Date(contest.startTime).toLocaleString()}</p><p className="text-xs text-gray-500">{contest.status}</p></div>
              </div>)}
              {dashboard.upcoming.length === 0 && <p className="text-sm text-gray-500">No upcoming fixtures are scheduled.</p>}
            </CardContent>
          </Card>
          <Card>
            <CardHeader><CardTitle>Live workflow</CardTitle><CardDescription>Items needing operational attention.</CardDescription></CardHeader>
            <CardContent className="space-y-3">
              <Metric label="Live contests" value={dashboard.summary.liveContests} />
              <Metric label="Pending results" value={dashboard.summary.pendingResults} />
              <Metric label="Tenant status" value={dashboard.tenant.status} />
            </CardContent>
          </Card>
        </div>
      </>}
    </div>
  );
}

function Metric({ label, value }: { label: string; value: string | number }) {
  return <div className="flex items-center justify-between rounded-lg bg-gray-50 p-3"><span className="text-sm text-gray-500">{label}</span><span className="font-semibold">{value}</span></div>;
}

function DashboardMetric({
  label,
  value,
  hint,
}: {
  label: string;
  value: number | string;
  hint: string;
}) {
  return (
    <div className="bg-white px-5 py-4">
      <p className="text-xs font-semibold uppercase tracking-wider text-gray-400">{label}</p>
      <div className="mt-1 flex items-end justify-between gap-3">
        <strong className="truncate text-2xl font-bold tracking-tight text-gray-950">{value}</strong>
        <span className="shrink-0 text-xs text-gray-500">{hint}</span>
      </div>
    </div>
  );
}


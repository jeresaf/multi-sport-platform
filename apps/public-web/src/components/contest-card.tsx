'use client';

import Link from 'next/link';
import { Bell, Clock3, MapPin, Star } from 'lucide-react';
import { memo, useState } from 'react';
import { AlertPreferencesDialog } from './alert-preferences-dialog';
import { loadAlerts } from '@/lib/alert-preferences';
import type { PublicContest } from '@/lib/public-api';
import { formatSport, tenantPath } from '@/lib/public-api';
import { inferredRenderer } from '@/lib/presentation';
import { ContestRenderer } from './contest-renderers';
import { ContestStatus } from './contest-status';

export const ContestCard = memo(function ContestCard({ contest, tenantSlug, favourite, onToggleFavourite }: {
  contest: PublicContest;
  tenantSlug: string;
  favourite: boolean;
  onToggleFavourite: (id: string) => void;
}) {
  const [alertsOpen, setAlertsOpen] = useState(false);
  const [alertsEnabled, setAlertsEnabled] = useState(() => loadAlerts('contest', contest.id).size > 0);
  const participants = contest.participants ?? [];
  const start = contest.startTime ? new Date(contest.startTime) : null;
  return (
    <article className="surface group relative overflow-hidden rounded-2xl border border-[var(--color-border)] shadow-[var(--shadow-card)] transition duration-200 hover:-translate-y-0.5 hover:shadow-lg">
      <Link href={tenantPath(`/contest/${contest.id}`, tenantSlug)} className="block p-4 sm:p-5">
        <div className="mb-4 flex items-start justify-between gap-3">
          <div className="min-w-0">
            <p className="truncate text-xs font-bold uppercase tracking-wider text-[var(--brand-primary)]">{contest.sportName || formatSport(contest.sportId)}</p>
            {contest.name && <h3 className="mt-1 truncate text-sm font-semibold">{contest.name}</h3>}
          </div>
          <div className="flex shrink-0 flex-col items-end gap-1">
            <ContestStatus status={contest.contestStatus ?? contest.status} />
            {contest.presentation?.periodLabel && (
              <span className="text-xs font-bold tabular-nums text-[var(--color-text-muted)]">
                {[contest.presentation.periodLabel, contest.presentation.clock].filter(Boolean).join(' · ')}
              </span>
            )}
          </div>
        </div>
        <div className="space-y-2.5">
          <ContestRenderer renderer={inferredRenderer(contest)} contest={contest} participants={participants} />
        </div>
        <div className="mt-4 flex flex-wrap items-center gap-x-4 gap-y-2 border-t border-[var(--color-border)] pt-3 pr-16 text-xs muted">
          <span className="flex items-center gap-1.5"><Clock3 className="h-3.5 w-3.5" />{start ? start.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }) : 'Time TBC'}</span>
          {contest.venue && <span className="flex min-w-0 items-center gap-1.5"><MapPin className="h-3.5 w-3.5 shrink-0" /><span className="truncate">{contest.venue}</span></span>}
        </div>
      </Link>
      <button
        onClick={() => onToggleFavourite(contest.id)}
        className="absolute bottom-3 right-3 rounded-full p-2 hover:bg-[var(--color-surface-muted)]"
        aria-label={favourite ? 'Remove from favourites' : 'Add to favourites'}
        aria-pressed={favourite}
      >
        <Star className="h-4 w-4" fill={favourite ? 'var(--brand-accent)' : 'none'} color={favourite ? 'var(--brand-accent)' : 'currentColor'} />
      </button>
      <button onClick={() => favourite && setAlertsOpen(true)} disabled={!favourite} title={favourite ? 'Choose alerts' : 'Favourite this contest to enable alerts'} aria-label="Contest alert preferences" aria-pressed={alertsEnabled} className="absolute bottom-3 right-12 rounded-full p-2 hover:bg-[var(--color-surface-muted)] disabled:cursor-not-allowed disabled:opacity-40"><Bell className="h-4 w-4" fill={alertsEnabled ? 'currentColor' : 'none'} /></button>
      <AlertPreferencesDialog open={alertsOpen} type="contest" id={contest.id} title={contest.name ?? contest.competitionName ?? 'Contest'} sportCode={contest.sportCode} onClose={() => setAlertsOpen(false)} onChange={setAlertsEnabled} />
    </article>
  );
});

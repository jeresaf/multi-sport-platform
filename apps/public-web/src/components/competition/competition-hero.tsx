import { Bell, BellRing, Star } from 'lucide-react';
import type { CompetitionDetail } from './competition-types';

export function CompetitionHero({ competition, followed, alerts, onFollow, onAlerts }: {
  competition: CompetitionDetail;
  followed: boolean;
  alerts: boolean;
  onFollow: () => void;
  onAlerts: () => void;
}) {
  const edition = competition.editions[0];
  const sponsor = competition.sponsors[0];
  return (
    <header className="mt-5 overflow-hidden rounded-3xl bg-gray-950 p-5 text-white sm:p-8">
      <div className="flex flex-col gap-5 sm:flex-row sm:items-start">
        <div className="flex h-16 w-16 shrink-0 items-center justify-center rounded-2xl bg-white/10 text-xl font-black ring-1 ring-white/20" aria-label="Competition logo placeholder">
          {initials(competition.name)}
        </div>
        <div className="min-w-0 flex-1">
          <p className="text-sm font-semibold uppercase tracking-wider text-cyan-300">
            {[competition.sport?.name, competition.region].filter(Boolean).join(' · ')}
          </p>
          <h1 className="mt-1 text-3xl font-bold sm:text-4xl">{competition.name}</h1>
          <p className="mt-2 text-sm text-gray-300">
            {[edition?.name, dateRange(edition?.startDate, edition?.endDate)].filter(Boolean).join(' · ')}
          </p>
          {competition.description && <p className="mt-3 max-w-3xl text-gray-300">{competition.description}</p>}
          <div className="mt-5 flex flex-wrap gap-2">
            <Action active={followed} onClick={onFollow} icon={Star}>{followed ? 'Following' : 'Follow'}</Action>
            <Action active={alerts} onClick={onAlerts} icon={alerts ? BellRing : Bell}>{alerts ? 'Alerts on' : followed ? 'Alerts' : 'Follow for alerts'}</Action>
          </div>
        </div>
        <div className="flex shrink-0 flex-col items-start gap-2 sm:items-end">
          <span className="rounded-full bg-white/10 px-3 py-1 text-sm font-semibold">{competition.status}</span>
          {sponsor && <p className="text-xs text-gray-300">Presented by <strong className="text-white">{sponsor.name}</strong></p>}
        </div>
      </div>
    </header>
  );
}

function Action({ active, onClick, icon: Icon, children }: { active: boolean; onClick: () => void; icon: typeof Star; children: React.ReactNode }) {
  return <button type="button" aria-pressed={active} onClick={onClick} className={`inline-flex items-center gap-2 rounded-lg px-3 py-2 text-sm font-semibold ${active ? 'bg-white text-gray-950' : 'bg-white/10 hover:bg-white/20'}`}><Icon className="h-4 w-4" fill={active ? 'currentColor' : 'none'} />{children}</button>;
}

function initials(value: string) { return value.split(' ').filter(Boolean).slice(0, 3).map(word => word[0]).join('').toUpperCase(); }
function dateRange(start?: string | null, end?: string | null) {
  if (!start && !end) return null;
  return `${start ? new Date(start).toLocaleDateString() : 'TBC'} – ${end ? new Date(end).toLocaleDateString() : 'TBC'}`;
}

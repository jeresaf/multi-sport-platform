'use client';

import Link from 'next/link';
import { ArrowLeft, Calendar, MapPin, Shield, Star, Trophy, UserRound, Users } from 'lucide-react';
import { use, useEffect, useState } from 'react';
import { formatSport, publicApi, resolveTenantSlug, tenantPath } from '@/lib/public-api';

type Participant = {
  id: string;
  name: string;
  shortName: string;
  logo?: string | null;
  type: string;
  status: string;
  profile: {
    kind: string;
    nationality?: string | null;
    photoUrl?: string | null;
    sports?: Array<{ id: string; name: string }>;
    homeVenue?: string | null;
    coach?: string | null;
    manager?: string | null;
    rosters?: Array<{
      id: string;
      name: string;
      season?: string | null;
      members: Array<{
        athleteId: string;
        name: string;
        jerseyNumber?: string | null;
        position?: string | null;
      }>;
    }>;
  };
  upcomingContests: Array<{
    id: string;
    name?: string | null;
    sportId: string;
    contestShape: string;
    status: string;
    venue?: string | null;
    startTime?: string | null;
    opponents: Array<{ id: string; name: string }>;
  }>;
  results: Array<{
    id: string;
    contestId: string;
    contestName: string;
    contestShape?: string | null;
    startTime?: string | null;
    rank?: number | null;
    position?: string | null;
    score?: number | null;
    scoreDisplay?: string | null;
    time?: string | null;
    status: string;
  }>;
  statistics: {
    resultCount: number;
    wins: number;
    podiums: number;
    upcomingContestCount: number;
  };
};

export default function ParticipantPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = use(params);
  const [participant, setParticipant] = useState<Participant | null>(null);
  const [tenantSlug] = useState(resolveTenantSlug);
  const [error, setError] = useState<string | null>(null);
  const [favourite, setFavourite] = useState(false);

  useEffect(() => {
    let disposed = false;
    publicApi<Participant>(`/participants/${id}`, undefined, tenantSlug)
      .then(value => { if (!disposed) { setParticipant(value); setFavourite(readParticipantFavourite(value.id)); } })
      .catch((reason: Error) => !disposed && setError(reason.message));
    return () => { disposed = true; };
  }, [id, tenantSlug]);

  if (error) return <State message={`Participant unavailable: ${error}`} />;
  if (!participant) return <State message="Loading participant…" />;
  const image = participant.profile.photoUrl ?? participant.logo;

  return (
    <div className="app-container py-5 sm:py-7">
      <Link
        href={tenantPath('/', tenantSlug)}
        className="mb-5 inline-flex items-center gap-2 rounded-xl border border-[var(--color-border)] bg-[var(--color-surface)] px-3 py-2 text-sm font-semibold shadow-sm hover:bg-[var(--color-surface-muted)]"
      >
        <ArrowLeft className="h-4 w-4" /> Back to match centre
      </Link>
      <section className="overflow-hidden rounded-[28px] border border-[var(--color-border)] bg-[var(--color-surface)] shadow-sm">
        <div className="bg-gradient-to-br from-slate-950 via-slate-900 to-slate-800 p-6 text-white sm:p-8">
          <div className="flex flex-col items-start gap-6 sm:flex-row sm:items-center">
          <div className="flex h-24 w-24 shrink-0 items-center justify-center overflow-hidden rounded-2xl border border-white/10 bg-white/10">
            {image ? (
              // Public branding can use tenant-managed or third-party asset hosts.
              // eslint-disable-next-line @next/next/no-img-element
              <img src={image} alt="" className="h-full w-full object-cover" />
            ) : participant.profile.kind === 'Athlete' ? (
              <UserRound className="h-12 w-12 text-white/80" />
            ) : (
              <Shield className="h-12 w-12 text-white/80" />
            )}
          </div>
          <div className="min-w-0 flex-1">
            <p className="text-sm font-semibold uppercase tracking-wider text-white/60">{participant.profile.kind}</p>
            <h1 className="mt-1 text-4xl font-black tracking-tight text-white sm:text-5xl">{participant.name}</h1>
            <p className="mt-2 text-white/60">{participant.shortName} · {participant.status}</p>
          </div>
          <button onClick={() => {
            const values = favouriteKeys(); const key = `participant:${participant.id}`;
            if (values.has(key)) values.delete(key); else values.add(key);
            try { localStorage.setItem('sports-platform:favourite-contests', JSON.stringify([...values])); setFavourite(values.has(key)); } catch { /* retain prior state */ }
          }} aria-pressed={favourite} className="inline-flex items-center gap-2 rounded-xl border border-white/15 bg-white/10 px-4 py-2.5 font-semibold text-white hover:bg-white/15"><Star className="h-4 w-4" fill={favourite ? 'currentColor' : 'none'} />{favourite ? 'Following' : 'Follow'}</button>
          </div>
        </div>
        <div className="p-6 sm:p-8">
          <ProfileDetails participant={participant} tenantSlug={tenantSlug} />
        </div>
      </section>

      <section
        aria-label="Participant statistics"
        className="mt-8 overflow-hidden rounded-2xl border border-[var(--color-border)] bg-[var(--color-surface)] shadow-sm"
      >
        <div className="grid grid-cols-2 gap-px bg-[var(--color-border)] md:grid-cols-4">
          <Stat label="Results" value={participant.statistics.resultCount} />
          <Stat label="Wins" value={participant.statistics.wins} />
          <Stat label="Podiums" value={participant.statistics.podiums} />
          <Stat label="Upcoming" value={participant.statistics.upcomingContestCount} />
        </div>
      </section>

      <section className="mt-10">
        <div className="mb-4 flex items-center gap-2"><Calendar className="h-5 w-5 text-[var(--brand-primary)]" /><h2 className="text-2xl font-bold">Upcoming contests</h2></div>
        {participant.upcomingContests.length === 0 ? <Empty message="No upcoming public contests are scheduled." /> : <div className="grid gap-4 md:grid-cols-2">{participant.upcomingContests.map(contest => <Link key={contest.id} href={tenantPath(`/contest/${contest.id}`, tenantSlug)} className="rounded-2xl border border-[var(--color-border)] bg-[var(--color-surface)] p-5 shadow-sm transition hover:-translate-y-0.5 hover:shadow-md"><div className="flex items-start justify-between gap-3"><div><p className="text-xs font-semibold uppercase tracking-wide text-[var(--brand-primary)]">{formatSport(contest.sportId)} · {contest.contestShape}</p><h3 className="mt-1 text-lg font-bold">{contest.name ?? 'Published contest'}</h3></div><span className="rounded-full bg-[var(--color-surface-muted)] px-2 py-1 text-xs">{contest.status}</span></div><p className="mt-3 text-sm muted">{contest.opponents.length ? `vs ${contest.opponents.map(item => item.name).join(', ')}` : 'Field to be confirmed'}</p><div className="mt-3 flex flex-wrap gap-4 text-xs muted">{contest.startTime && <span>{new Date(contest.startTime).toLocaleString()}</span>}{contest.venue && <span className="inline-flex items-center gap-1"><MapPin className="h-3 w-3" />{contest.venue}</span>}</div></Link>)}</div>}
      </section>

      <section className="mt-10">
        <div className="mb-4 flex items-center gap-2"><Trophy className="h-5 w-5 text-[var(--brand-primary)]" /><h2 className="text-2xl font-bold">Results</h2></div>
        {participant.results.length === 0 ? <Empty message="No published results are available." /> : <div className="overflow-x-auto rounded-2xl border border-[var(--color-border)] bg-[var(--color-surface)] shadow-sm"><table className="w-full text-left"><thead><tr className="border-b bg-[var(--color-surface-muted)] text-sm muted"><th className="p-4">Contest</th><th className="p-4">Date</th><th className="p-4">Place</th><th className="p-4 text-right">Result</th></tr></thead><tbody>{participant.results.map(result => <tr key={result.id} className="border-b last:border-0"><td className="p-4"><Link className="font-semibold text-[var(--brand-primary)] hover:underline" href={tenantPath(`/contest/${result.contestId}`, tenantSlug)}>{result.contestName}</Link><p className="text-xs muted">{result.contestShape}</p></td><td className="p-4 text-sm muted">{result.startTime ? new Date(result.startTime).toLocaleDateString() : '—'}</td><td className="p-4 font-semibold">{result.rank ? `#${result.rank}` : result.position ?? '—'}</td><td className="p-4 text-right font-bold">{result.scoreDisplay ?? result.time ?? result.score ?? '—'}</td></tr>)}</tbody></table></div>}
      </section>
    </div>
  );
}

function ProfileDetails({ participant, tenantSlug }: { participant: Participant; tenantSlug: string }) {
  const profile = participant.profile;
  if (profile.kind === 'Athlete') return <div data-testid="participant-profile-athlete" className="mt-7 grid gap-4 border-t border-[var(--color-border)] pt-6 sm:grid-cols-2"><Detail label="Nationality" value={profile.nationality} /><Detail label="Sports" value={profile.sports?.map(item => item.name).join(', ')} /></div>;
  if (profile.kind === 'Team') return <div data-testid="participant-profile-team" className="mt-7 border-t border-[var(--color-border)] pt-6"><div className="grid gap-4 sm:grid-cols-3"><Detail label="Home venue" value={profile.homeVenue} /><Detail label="Coach" value={profile.coach} /><Detail label="Manager" value={profile.manager} /></div>{profile.rosters?.map(roster => <div key={roster.id} className="mt-6"><div className="mb-3 flex items-center gap-2"><Users className="h-4 w-4 text-[var(--brand-primary)]" /><h2 className="font-bold">{roster.name}{roster.season ? ` · ${roster.season}` : ''}</h2></div>{roster.members.length === 0 ? <p className="text-sm muted">No public roster members.</p> : <div className="grid gap-2 sm:grid-cols-2">{roster.members.map(member => <Link key={member.athleteId} href={tenantPath(`/participants/${member.athleteId}`, tenantSlug)} className="rounded-xl border border-[var(--color-border)] bg-[var(--color-surface-muted)] p-3 text-sm transition hover:border-[var(--color-border)] hover:bg-[var(--color-surface-muted)]"><strong>{member.name}</strong><span className="ml-2 muted">{[member.jerseyNumber && `#${member.jerseyNumber}`, member.position].filter(Boolean).join(' · ')}</span></Link>)}</div>}</div>)}</div>;
  if (profile.kind === 'Club') return <div data-testid="participant-profile-club" className="mt-7 grid gap-4 border-t border-[var(--color-border)] pt-6 sm:grid-cols-3"><Detail label="Home venue" value={profile.homeVenue} /><Detail label="Coach" value={profile.coach} /><Detail label="Manager" value={profile.manager} /></div>;
  return null;
}

function Detail({ label, value }: { label: string; value?: string | null }) { return <div><p className="text-xs font-semibold uppercase tracking-wide muted">{label}</p><p className="mt-1 font-medium">{value || 'Not published'}</p></div>; }
function Stat({ label, value }: { label: string; value: number }) {
  return (
    <div className="bg-[var(--color-surface)] p-5 text-center">
      <p className="text-3xl font-black text-[var(--brand-primary)]">{value}</p>
      <p className="mt-1 text-xs font-bold uppercase tracking-wider muted">{label}</p>
    </div>
  );
}
function Empty({ message }: { message: string }) { return <div className="surface rounded-2xl border border-dashed border-[var(--color-border)] px-4 py-12 text-center muted">{message}</div>; }
function State({ message }: { message: string }) { return <div className="app-container py-24 text-center muted">{message}</div>; }
function favouriteKeys() { try { const values = JSON.parse(localStorage.getItem('sports-platform:favourite-contests') ?? '[]') as string[]; return new Set(values.map(value => value.includes(':') ? value : `contest:${value}`)); } catch { return new Set<string>(); } }
function readParticipantFavourite(id: string) { return typeof window !== 'undefined' && favouriteKeys().has(`participant:${id}`); }

import type { PublicContest } from './public-api';

export type ContestPhase = 'live' | 'upcoming' | 'finished' | 'postponed' | 'cancelled';
export type ContestFilter = 'all' | ContestPhase | 'favourites';

export function contestPhase(status: string): ContestPhase {
  switch (normalizeStatus(status)) {
    case 'active':
    case 'inprogress':
    case 'live':
    case 'paused':
      return 'live';
    case 'completed':
    case 'ended':
    case 'closed':
    case 'final':
    case 'finished':
      return 'finished';
    case 'postponed':
    case 'suspended':
    case 'delayed':
    case 'interrupted':
      return 'postponed';
    case 'cancelled':
    case 'canceled':
    case 'abandoned':
      return 'cancelled';
    default:
      return 'upcoming';
  }
}

export function statusLabel(status: string) {
  const normalized = normalizeStatus(status);
  const labels: Record<string, string> = {
    notstarted: 'Not started', scheduled: 'Not started', draft: 'Not started',
    startingsoon: 'Starting soon', ready: 'Starting soon', checkinopen: 'Check-in open',
    active: 'Live', live: 'Live', inprogress: 'Live', delayed: 'Delayed', interrupted: 'Interrupted',
    paused: 'Interrupted', suspended: 'Suspended', postponed: 'Postponed',
    ended: 'Ended', completed: 'Ended', closed: 'Final', approved: 'Final',
    published: 'Final', corrected: 'Corrected', cancelled: 'Cancelled',
    canceled: 'Cancelled', abandoned: 'Abandoned',
  };
  return labels[normalized] ?? 'Status unavailable';
}

export function normalizeStatus(status: string) {
  return status.trim().replaceAll('_', '').replaceAll('-', '').replaceAll(' ', '').toLowerCase();
}

export function localDateKey(value: Date | string) {
  const date = typeof value === 'string' ? new Date(value) : value;
  const year = date.getFullYear();
  const month = `${date.getMonth() + 1}`.padStart(2, '0');
  const day = `${date.getDate()}`.padStart(2, '0');
  return `${year}-${month}-${day}`;
}

export function contestDateKey(contest: PublicContest) {
  return contest.startTime ? localDateKey(contest.startTime) : null;
}

export function groupByCompetition(contests: PublicContest[]) {
  return Array.from(
    contests.reduce((groups, contest) => {
      const key = contest.competitionId ?? contest.sportId;
      const current = groups.get(key) ?? {
        id: key,
        name: contest.competitionName ?? 'Other contests',
        sportId: contest.sportId,
        contests: [] as PublicContest[],
      };
      current.contests.push(contest);
      groups.set(key, current);
      return groups;
    }, new Map<string, { id: string; name: string; sportId: string; contests: PublicContest[] }>()).values(),
  );
}

export function inferredRenderer(contest: PublicContest) {
  if (contest.renderer) return contest.renderer;
  const shape = contest.contestShape.toLowerCase();
  if (shape.includes('bout')) return 'fight-card-bout';
  if (shape.includes('cricket')) return 'cricket-scorecard';
  if (shape.includes('heat') || shape.includes('race')) return 'race-heat';
  return (contest.participants?.length ?? 0) <= 2
    ? 'head-to-head'
    : 'multi-participant-leaderboard';
}

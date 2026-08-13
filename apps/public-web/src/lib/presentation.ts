import type { PublicContest } from './public-api';

export type ContestPhase = 'live' | 'upcoming' | 'finished' | 'postponed' | 'cancelled';
export type ContestFilter = 'all' | ContestPhase | 'favourites';

export function contestPhase(status: string): ContestPhase {
  switch (status.trim().toLowerCase()) {
    case 'inprogress':
    case 'live':
      return 'live';
    case 'completed':
    case 'final':
    case 'finished':
      return 'finished';
    case 'postponed':
    case 'suspended':
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
  const phase = contestPhase(status);
  return ({ live: 'Live', upcoming: 'Upcoming', finished: 'Finished', postponed: 'Postponed', cancelled: 'Cancelled' })[phase];
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

import type { PublicContest } from './public-api';

export type ContestLiveUpdate = {
  contestId: string;
  version: number;
  status?: string | null;
  contestStatus?: string | null;
  participants?: Array<{ participantId: string; score?: string | null; scoreDisplay?: string | null }>;
  summary?: string | null;
};

export function applyContestLiveUpdate(contest: PublicContest, update: ContestLiveUpdate): PublicContest {
  if (contest.id !== update.contestId) return contest;
  const scores = new Map((update.participants ?? []).map(item => [item.participantId, item]));
  return {
    ...contest,
    status: update.contestStatus || update.status || contest.status,
    participants: contest.participants?.map(participant => {
      const score = scores.get(participant.participantId);
      if (!score) return participant;
      const numeric = score.score == null ? null : Number(score.score);
      return { ...participant, score: Number.isFinite(numeric) ? numeric : participant.score, scoreDisplay: score.scoreDisplay ?? score.score ?? participant.scoreDisplay };
    }),
    presentation: update.summary ? { ...contest.presentation, summary: update.summary } : contest.presentation,
  };
}

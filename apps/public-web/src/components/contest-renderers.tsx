import type { ContestRenderer, PublicContest, PublicContestParticipant } from '@/lib/public-api';

type RendererProps = { contest: PublicContest; participants: PublicContestParticipant[] };

const rendererRegistry: Record<ContestRenderer, (props: RendererProps) => React.ReactNode> = {
  'head-to-head': props => <HeadToHeadRenderer {...props} />,
  'multi-participant-leaderboard': ({ participants }) => <ParticipantRows participants={[...participants].sort(byRank).slice(0, 6)} showRank />,
  'race-heat': ({ participants }) => <ParticipantRows participants={[...participants].sort(byRank).slice(0, 6)} showRank />,
  'fight-card-bout': ({ participants }) => <ParticipantRows participants={participants.slice(0, 2)} />,
  'cricket-scorecard': ({ participants }) => <ParticipantRows participants={participants.slice(0, 2)} />,
};

export function ContestRenderer({ renderer, contest, participants }: RendererProps & { renderer: ContestRenderer }) {
  return rendererRegistry[renderer]({ contest, participants });
}

function HeadToHeadRenderer({ contest, participants }: RendererProps) {
  const variant = (contest.presentation?.scoreVariant || contest.sportCode || '').toUpperCase();
  const adapter = variant.startsWith('RUGBY_')
    ? 'rugby'
    : variant === 'FOOTBALL' || variant === 'SOCCER'
      ? 'football'
      : variant === 'BASKETBALL'
        ? 'basketball'
        : variant === 'NETBALL' || variant === 'HOCKEY'
          ? 'period'
          : variant === 'VOLLEYBALL' || variant === 'TENNIS'
            ? 'set-based'
            : 'head-to-head';
  const context = secondaryContext(contest, variant, participants);
  return (
    <div data-contest-adapter={adapter}>
      {variant.startsWith('RUGBY_') && (
        <div className="mb-2 flex flex-wrap gap-1.5">
          <AdapterBadge>{variant.includes('15') ? 'Rugby 15s' : 'Rugby 7s'}</AdapterBadge>
          {contest.presentation?.stageLabel && <AdapterBadge>{contest.presentation.stageLabel}</AdapterBadge>}
        </div>
      )}
      <ParticipantRows participants={participants.slice(0, 2)} />
      {context && <p className="mt-2 hidden text-xs font-medium text-[var(--color-text-muted)] sm:block">{context}</p>}
    </div>
  );
}

function AdapterBadge({ children }: { children: React.ReactNode }) {
  return <span className="rounded-full bg-[var(--color-surface-muted)] px-2 py-0.5 text-[11px] font-bold uppercase tracking-wide">{children}</span>;
}

function secondaryContext(contest: PublicContest, variant: string, participants: PublicContestParticipant[]) {
  const configured = contest.presentation?.summary?.trim();
  if (configured) return configured;
  if (variant === 'VOLLEYBALL' || variant === 'TENNIS') {
    const setScores = participants.map(item => item.scoreDisplay).filter(Boolean);
    return setScores.length > 0 ? setScores.join(' · ') : undefined;
  }
  if (variant.startsWith('RUGBY_')) {
    const events = contest.presentation?.eventSummary ?? [];
    const counts = (participantId: string, types: string[]) => events
      .filter(item => item.participantId === participantId && types.includes(item.eventType.toUpperCase()))
      .reduce((total, item) => total + item.count, 0);
    if (participants.length >= 2 && events.length > 0) {
      const tries = participants.map(item => counts(item.participantId, ['TRY']));
      const penalties = participants.map(item => counts(item.participantId, ['PENALTY_GOAL']));
      return `Tries ${tries[0]}–${tries[1]} · Pens ${penalties[0]}–${penalties[1]}`;
    }
  }
  if (contest.presentation?.seriesGameNumber && contest.presentation.seriesBestOf) {
    return `Game ${contest.presentation.seriesGameNumber} · Best of ${contest.presentation.seriesBestOf}`;
  }
  return undefined;
}

function ParticipantRows({ participants, showRank = false }: { participants: PublicContestParticipant[]; showRank?: boolean }) {
  if (participants.length === 0) return <p className="py-3 text-sm muted">Participants to be confirmed</p>;
  return participants.map(participant => {
    const result = participant.scoreDisplay ?? participant.score ?? participant.position ?? null;
    return (
      <div className="flex min-h-8 items-center justify-between gap-3" key={participant.participantId}>
        <div className="flex min-w-0 items-center gap-2.5">
          <span className="flex h-7 w-7 shrink-0 items-center justify-center rounded-full bg-[var(--color-surface-muted)] text-xs font-bold" aria-hidden="true">
            {showRank && participant.rank ? participant.rank : participant.name.slice(0, 2).toUpperCase()}
          </span>
          <span className="truncate text-sm font-semibold">{participant.name}</span>
        </div>
        <span className="shrink-0 tabular-nums font-bold">{result ?? '—'}</span>
      </div>
    );
  });
}

function byRank(left: PublicContestParticipant, right: PublicContestParticipant) {
  return (left.rank ?? Number.MAX_SAFE_INTEGER) - (right.rank ?? Number.MAX_SAFE_INTEGER);
}

'use client';

import { useEffect, useState } from 'react';
import { PublicCompetition, publicApi } from '@/lib/public-api';

type Fighter = {
  participantId: string;
  name: string;
  corner: string;
};

type Official = {
  officialId: string;
  displayName: string;
  role: string;
};

type Bout = {
  id: string;
  name: string;
  boutNumber: number;
  cardSection: string;
  weightClass: string;
  scheduledRounds: number;
  status: string;
  fighters: Fighter[];
  officials: Official[];
  winnerId?: string | null;
  winnerName?: string | null;
  resultMethod?: string | null;
  resultRound?: number | null;
  resultTimeSeconds?: number | null;
};

type FightCard = {
  id: string;
  name: string;
  status: string;
  boutCount: number;
  completedBoutCount: number;
  allowedResultMethods: string[];
  bouts: Bout[];
};

export default function FightCardPage() {
  const [card, setCard] = useState<FightCard | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    async function load() {
      const query = new URLSearchParams(window.location.search);
      const tenantSlug = query.get('tenant') ?? undefined;
      const requestedId = query.get('competitionId');
      const competitionId = requestedId ?? (await publicApi<PublicCompetition[]>(
        '/competitions',
        undefined,
        tenantSlug,
      )).find((competition) =>
        competition.sportId === '00000000-0000-0000-0000-000000000105'
      )?.id;
      if (!competitionId) {
        setError('No published combat event is available.');
        return;
      }
      setCard(await publicApi<FightCard>(
        `/combat/card?competitionId=${encodeURIComponent(competitionId)}`,
        undefined,
        tenantSlug,
      ));
    }

    load().catch((reason: Error) => setError(reason.message));
  }, []);

  return (
    <div className="container mx-auto max-w-5xl px-4 py-10">
      <h1 className="text-3xl font-bold text-gray-900">Fight card</h1>
      <p className="mt-2 text-gray-600">
        Bout order, assigned officials, and official decision or stoppage results.
      </p>
      {error && <Message>Could not load fight card: {error}</Message>}
      {!error && !card && <Message>Loading fight card…</Message>}
      {card && (
        <>
          <div className="mt-8 rounded-xl bg-gray-950 p-6 text-white">
            <p className="text-sm uppercase tracking-wide text-red-300">{card.status}</p>
            <div className="mt-1 flex flex-wrap items-end justify-between gap-3">
              <h2 className="text-2xl font-bold">{card.name}</h2>
              <p className="text-sm text-gray-300">
                {card.completedBoutCount} of {card.boutCount} bouts completed
              </p>
            </div>
            <p className="mt-3 text-sm text-gray-300">
              Result methods: {card.allowedResultMethods.join(', ')}
            </p>
          </div>

          <div className="mt-8 space-y-5" data-testid="fight-card">
            {[...card.bouts].reverse().map((bout) => (
              <article
                key={bout.id}
                className={`overflow-hidden rounded-xl border bg-white shadow-sm ${
                  bout.cardSection === 'MainEvent' ? 'border-red-500' : 'border-gray-200'
                }`}
              >
                <div className="flex flex-wrap items-center justify-between gap-2 bg-gray-100 px-5 py-3 text-sm">
                  <span className="font-bold uppercase tracking-wide text-gray-700">
                    {formatSection(bout.cardSection)} · Bout {bout.boutNumber}
                  </span>
                  <span className="text-gray-600">
                    {bout.weightClass} · {bout.scheduledRounds} rounds
                  </span>
                </div>
                <div className="grid gap-4 px-5 py-5 md:grid-cols-[1fr_auto_1fr] md:items-center">
                  <FighterName fighter={bout.fighters[0]} winnerId={bout.winnerId} />
                  <span className="text-center text-xs font-bold uppercase text-gray-400">vs</span>
                  <FighterName fighter={bout.fighters[1]} winnerId={bout.winnerId} alignRight />
                </div>
                <div className="border-t border-gray-100 px-5 py-4">
                  {bout.resultMethod ? (
                    <p
                      className="font-semibold text-gray-900"
                      data-testid={`bout-result-${bout.boutNumber}`}
                    >
                      {bout.winnerName ? `${bout.winnerName} — ` : ''}
                      {bout.resultMethod}
                      {formatStoppage(bout)}
                    </p>
                  ) : (
                    <p className="text-gray-500">Result pending</p>
                  )}
                  <p className="mt-2 text-sm text-gray-500">
                    Officials: {bout.officials.map((official) =>
                      `${official.displayName} (${official.role})`
                    ).join(', ')}
                  </p>
                </div>
              </article>
            ))}
          </div>
        </>
      )}
    </div>
  );
}

function FighterName({
  fighter,
  winnerId,
  alignRight = false,
}: {
  fighter?: Fighter;
  winnerId?: string | null;
  alignRight?: boolean;
}) {
  return (
    <div className={alignRight ? 'md:text-right' : ''}>
      <p className="text-xs font-bold uppercase tracking-wide text-gray-400">
        {fighter?.corner ?? 'Corner'}
      </p>
      <p className={`mt-1 text-xl font-bold ${
        fighter?.participantId === winnerId ? 'text-red-700' : 'text-gray-900'
      }`}>
        {fighter?.name ?? 'TBA'}
      </p>
    </div>
  );
}

function formatSection(value: string) {
  return value.replace(/([A-Z])/g, ' $1').trim();
}

function formatStoppage(bout: Bout) {
  if (!bout.resultRound) return '';
  if (bout.resultTimeSeconds === null || bout.resultTimeSeconds === undefined) {
    return ` after ${bout.resultRound} rounds`;
  }
  const minutes = Math.floor(bout.resultTimeSeconds / 60);
  const seconds = String(bout.resultTimeSeconds % 60).padStart(2, '0');
  return ` · R${bout.resultRound} ${minutes}:${seconds}`;
}

function Message({ children }: { children: React.ReactNode }) {
  return (
    <div className="mt-8 rounded-xl border border-dashed border-gray-300 bg-gray-50 p-8 text-center text-gray-500">
      {children}
    </div>
  );
}

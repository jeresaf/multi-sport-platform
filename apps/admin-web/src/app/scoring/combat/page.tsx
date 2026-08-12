'use client';

import { useState } from 'react';
import { ArrowDown, ArrowUp, Play, RefreshCw, Save, Square } from 'lucide-react';
import { ConnectedSportScoring } from '@/components/scoring/connected-sport-scoring';
import { adminApi } from '@/lib/admin-api';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';

type Fighter = { id: string; name: string };
type BoutOfficial = { id: string; role: string; displayName: string };
type BoutRound = { number: number; status: 'Pending' | 'InProgress' | 'Completed' };
type CardBout = {
  id: string;
  name: string;
  sortOrder: number;
  cardSection: string;
  weightClass: string;
  scheduledRounds: number;
  status: string;
  winnerId: string | null;
  resultMethod: string | null;
  resultRound: number | null;
  resultTimeSeconds: number | null;
  fighters: Fighter[];
  officials: BoutOfficial[];
  rounds: BoutRound[];
  pendingCorrections: { id: string; requestedAt: string; payload: string }[];
};

type AdminFightCard = {
  id: string;
  name: string;
  status: string;
  canReorder: boolean;
  allowedResultMethods: string[];
  bouts: CardBout[];
};

type RoundScore = { fighter1Score: number; fighter2Score: number };
type ResultDraft = {
  method: string;
  winnerId: string;
  round: number;
  seconds: number;
  scores: Record<string, Record<number, RoundScore>>;
};

function newDraft(bout: CardBout, methods: string[]): ResultDraft {
  const scores: ResultDraft['scores'] = {};
  for (const judge of bout.officials.filter((official) => official.role === 'Judge')) {
    scores[judge.id] = {};
    for (const round of bout.rounds) {
      scores[judge.id][round.number] = { fighter1Score: 10, fighter2Score: 9 };
    }
  }
  return {
    method: methods[0] ?? '',
    winnerId: bout.fighters[0]?.id ?? '',
    round: 1,
    seconds: 0,
    scores,
  };
}

function needsScorecards(method: string) {
  return method.endsWith('Decision') || method === 'Draw';
}

export default function CombatScoringPage() {
  const [competitionId, setCompetitionId] = useState('');
  const [card, setCard] = useState<AdminFightCard | null>(null);
  const [drafts, setDrafts] = useState<Record<string, ResultDraft>>({});
  const [editingCorrection, setEditingCorrection] = useState<Record<string, boolean>>({});
  const [correctionReasons, setCorrectionReasons] = useState<Record<string, string>>({});
  const [message, setMessage] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  async function loadCard() {
    if (!competitionId.trim()) return;
    setBusy(true);
    setMessage(null);
    try {
      const loaded = await adminApi<AdminFightCard>(
        `/api/competitions/${competitionId.trim()}/combat/card`,
      );
      setCard(loaded);
      setDrafts(Object.fromEntries(loaded.bouts.map((bout) => [
        bout.id,
        newDraft(bout, loaded.allowedResultMethods),
      ])));
    } catch (reason) {
      setMessage(reason instanceof Error ? reason.message : 'Could not load fight card.');
    } finally {
      setBusy(false);
    }
  }

  function move(index: number, offset: -1 | 1) {
    if (!card) return;
    const target = index + offset;
    if (target < 0 || target >= card.bouts.length) return;
    if (card.bouts[index].cardSection !== card.bouts[target].cardSection) return;
    const bouts = [...card.bouts];
    [bouts[index], bouts[target]] = [bouts[target], bouts[index]];
    setCard({ ...card, bouts });
  }

  async function saveOrder() {
    if (!card) return;
    setBusy(true);
    setMessage(null);
    try {
      await adminApi(`/api/competitions/${card.id}/combat/card/order`, {
        method: 'PUT',
        body: JSON.stringify({ boutIds: card.bouts.map((bout) => bout.id) }),
      });
      setMessage('Fight-card order saved.');
      await loadCard();
    } catch (reason) {
      setMessage(reason instanceof Error ? reason.message : 'Could not save fight-card order.');
      setBusy(false);
    }
  }

  async function updateRound(bout: CardBout, round: BoutRound, action: 'start' | 'end') {
    if (!card) return;
    setBusy(true);
    setMessage(null);
    try {
      await adminApi(
        `/api/competitions/${card.id}/combat/bouts/${bout.id}/rounds/${round.number}/${action}`,
        { method: 'POST' },
      );
      setMessage(`Round ${round.number} ${action === 'start' ? 'started' : 'ended'}.`);
      await loadCard();
    } catch (reason) {
      setMessage(reason instanceof Error ? reason.message : 'Could not update the round.');
      setBusy(false);
    }
  }

  function updateDraft(bout: CardBout, values: Partial<ResultDraft>) {
    if (!card) return;
    setDrafts((current) => ({
      ...current,
      [bout.id]: { ...(current[bout.id] ?? newDraft(bout, card.allowedResultMethods)), ...values },
    }));
  }

  function updateScore(
    bout: CardBout,
    judgeId: string,
    roundNumber: number,
    corner: keyof RoundScore,
    value: number,
  ) {
    if (!card) return;
    setDrafts((current) => {
      const draft = current[bout.id] ?? newDraft(bout, card.allowedResultMethods);
      return {
        ...current,
        [bout.id]: {
          ...draft,
          scores: {
            ...draft.scores,
            [judgeId]: {
              ...draft.scores[judgeId],
              [roundNumber]: {
                ...draft.scores[judgeId][roundNumber],
                [corner]: value,
              },
            },
          },
        },
      };
    });
  }

  async function submitResult(bout: CardBout, correction = false) {
    if (!card) return;
    const draft = drafts[bout.id] ?? newDraft(bout, card.allowedResultMethods);
    const noWinner = draft.method === 'Draw' || draft.method === 'No Contest';
    const decision = needsScorecards(draft.method);
    const scorecards = decision
      ? bout.officials.filter((official) => official.role === 'Judge').slice(0, 3)
        .map((judge) => ({
          officialId: judge.id,
          rounds: bout.rounds.map((round) => ({
            roundNumber: round.number,
            ...draft.scores[judge.id][round.number],
          })),
        }))
      : [];

    setBusy(true);
    setMessage(null);
    try {
      const result = {
        winnerId: noWinner ? null : draft.winnerId,
        resultMethod: draft.method,
        round: decision ? null : draft.round,
        timeInRoundSeconds: decision ? null : draft.seconds,
        scorecards,
        notes: correction
          ? 'Corrected from the combat officials console'
          : 'Submitted from the combat officials console',
      };
      await adminApi(`/api/competitions/${card.id}/combat/bouts/${bout.id}/result${
        correction ? '/corrections' : ''}`, {
        method: 'POST',
        body: JSON.stringify(correction
          ? { result, reason: correctionReasons[bout.id] ?? '' }
          : result),
      });
      setMessage(correction
        ? `${bout.name} correction submitted for approval.`
        : `${bout.name} result recorded.`);
      setEditingCorrection((current) => ({ ...current, [bout.id]: false }));
      await loadCard();
    } catch (reason) {
      setMessage(reason instanceof Error ? reason.message : 'Could not record the result.');
      setBusy(false);
    }
  }

  async function reviewCorrection(
    bout: CardBout,
    correctionId: string,
    action: 'approve' | 'reject',
  ) {
    if (!card) return;
    setBusy(true);
    setMessage(null);
    try {
      await adminApi(
        `/api/competitions/${card.id}/combat/bouts/${bout.id}/result/corrections/${correctionId}/${action}`,
        {
          method: 'POST',
          body: JSON.stringify({ reason: correctionReasons[bout.id] ?? null }),
        },
      );
      setMessage(`${bout.name} correction ${action === 'approve' ? 'approved' : 'rejected'}.`);
      await loadCard();
    } catch (reason) {
      setMessage(reason instanceof Error ? reason.message : 'Could not review the correction.');
      setBusy(false);
    }
  }

  return (
    <div className="space-y-6">
      <ConnectedSportScoring config={{
        name: 'Combat', icon: '🥊',
        sportId: '00000000-0000-0000-0000-000000000105',
      }} />
      <Card>
        <CardHeader>
          <CardTitle>Fight-card officials console</CardTitle>
          <CardDescription>
            Control rounds, enter assigned judges&apos; scorecards, and record the official result.
          </CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="flex flex-wrap gap-2">
            <input
              value={competitionId}
              onChange={(event) => setCompetitionId(event.target.value)}
              placeholder="Competition ID"
              className="min-w-72 flex-1 rounded-md border border-gray-300 px-3 py-2"
            />
            <Button onClick={() => void loadCard()} disabled={busy || !competitionId.trim()}>
              <RefreshCw className="mr-2 h-4 w-4" /> Load card
            </Button>
          </div>
          {message && <p role="status" className="text-sm text-gray-600">{message}</p>}
          {card && (
            <>
              <div className="rounded-md bg-gray-50 p-3 text-sm text-gray-700">
                <strong>{card.name}</strong> · {card.status}<br />
                Allowed results: {card.allowedResultMethods.join(', ')}
              </div>
              <div className="space-y-4">
                {card.bouts.map((bout, index) => {
                  const draft = drafts[bout.id] ?? newDraft(bout, card.allowedResultMethods);
                  const decision = needsScorecards(draft.method);
                  const resultLocked = Boolean(bout.resultMethod);
                  return (
                    <section key={bout.id} className="space-y-4 rounded-md border p-4">
                      <div className="flex items-center gap-3">
                        <span className="w-8 font-bold">{index + 1}</span>
                        <div className="min-w-0 flex-1">
                          <p className="font-semibold">
                            {bout.fighters.map((fighter) => fighter.name).join(' vs ')}
                          </p>
                          <p className="text-xs text-gray-500">
                            {bout.cardSection} · {bout.weightClass} · {bout.scheduledRounds} rounds · {bout.status}
                          </p>
                        </div>
                        <Button variant="outline" size="icon" aria-label={`Move ${bout.name} up`}
                          disabled={!card.canReorder || index === 0
                            || card.bouts[index - 1].cardSection !== bout.cardSection}
                          onClick={() => move(index, -1)}>
                          <ArrowUp className="h-4 w-4" />
                        </Button>
                        <Button variant="outline" size="icon" aria-label={`Move ${bout.name} down`}
                          disabled={!card.canReorder || index === card.bouts.length - 1
                            || card.bouts[index + 1].cardSection !== bout.cardSection}
                          onClick={() => move(index, 1)}>
                          <ArrowDown className="h-4 w-4" />
                        </Button>
                      </div>

                      <div className="flex flex-wrap gap-2">
                        {bout.rounds.map((round) => (
                          <div key={round.number} className="flex items-center gap-2 rounded border px-3 py-2 text-sm">
                            <span>Round {round.number}: {round.status}</span>
                            {round.status === 'Pending' && !resultLocked && (
                              <Button size="sm" variant="outline" disabled={busy}
                                onClick={() => void updateRound(bout, round, 'start')}>
                                <Play className="mr-1 h-3 w-3" /> Start
                              </Button>
                            )}
                            {round.status === 'InProgress' && !resultLocked && (
                              <Button size="sm" variant="outline" disabled={busy}
                                onClick={() => void updateRound(bout, round, 'end')}>
                                <Square className="mr-1 h-3 w-3" /> End
                              </Button>
                            )}
                          </div>
                        ))}
                      </div>

                      {resultLocked && !editingCorrection[bout.id] ? (
                        <div className="rounded bg-green-50 p-3 text-sm text-green-800">
                          Official result: {bout.resultMethod}
                          {bout.winnerId && ` · ${bout.fighters.find((fighter) => fighter.id === bout.winnerId)?.name}`}
                          {bout.resultRound && ` · Round ${bout.resultRound}`}
                          {bout.resultTimeSeconds !== null && ` at ${bout.resultTimeSeconds}s`}
                          <p className="mt-1 text-xs">
                            Recorded results cannot be overwritten here; corrections require approval.
                          </p>
                          {bout.pendingCorrections.length === 0 && (
                            <Button className="mt-2" size="sm" variant="outline"
                              onClick={() => setEditingCorrection((current) => ({
                                ...current, [bout.id]: true,
                              }))}>
                              Request correction
                            </Button>
                          )}
                        </div>
                      ) : (
                        <div className="space-y-3 rounded bg-gray-50 p-3">
                          {resultLocked && (
                            <label className="block text-sm">Correction reason
                              <input className="mt-1 w-full rounded border px-2 py-2"
                                value={correctionReasons[bout.id] ?? ''}
                                onChange={(event) => setCorrectionReasons((current) => ({
                                  ...current, [bout.id]: event.target.value,
                                }))} />
                            </label>
                          )}
                          <div className="grid gap-3 md:grid-cols-4">
                            <label className="text-sm">Result method
                              <select className="mt-1 w-full rounded border bg-white px-2 py-2"
                                value={draft.method}
                                onChange={(event) => updateDraft(bout, { method: event.target.value })}>
                                {card.allowedResultMethods.map((method) => (
                                  <option key={method}>{method}</option>
                                ))}
                              </select>
                            </label>
                            <label className="text-sm">Winner
                              <select className="mt-1 w-full rounded border bg-white px-2 py-2"
                                value={draft.winnerId}
                                disabled={draft.method === 'Draw' || draft.method === 'No Contest'}
                                onChange={(event) => updateDraft(bout, { winnerId: event.target.value })}>
                                {bout.fighters.map((fighter) => (
                                  <option key={fighter.id} value={fighter.id}>{fighter.name}</option>
                                ))}
                              </select>
                            </label>
                            {!decision && (
                              <>
                                <label className="text-sm">Stoppage round
                                  <input type="number" min={1} max={bout.scheduledRounds}
                                    className="mt-1 w-full rounded border px-2 py-2" value={draft.round}
                                    onChange={(event) => updateDraft(bout, { round: Number(event.target.value) })} />
                                </label>
                                <label className="text-sm">Time in round (seconds)
                                  <input type="number" min={0} max={300}
                                    className="mt-1 w-full rounded border px-2 py-2" value={draft.seconds}
                                    onChange={(event) => updateDraft(bout, { seconds: Number(event.target.value) })} />
                                </label>
                              </>
                            )}
                          </div>

                          {decision && (
                            <div className="overflow-x-auto">
                              <table className="w-full text-sm">
                                <thead><tr><th className="p-2 text-left">Judge</th>
                                  {bout.rounds.map((round) => <th key={round.number} className="p-2">R{round.number}</th>)}
                                </tr></thead>
                                <tbody>
                                  {bout.officials.filter((official) => official.role === 'Judge').slice(0, 3)
                                    .map((judge) => (
                                      <tr key={judge.id} className="border-t">
                                        <td className="p-2">{judge.displayName}</td>
                                        {bout.rounds.map((round) => {
                                          const score = draft.scores[judge.id][round.number];
                                          return <td key={round.number} className="p-2 text-center">
                                            <input aria-label={`${judge.displayName} round ${round.number} fighter one`}
                                              type="number" min={7} max={10} value={score.fighter1Score}
                                              className="w-14 rounded border px-1 py-1"
                                              onChange={(event) => updateScore(bout, judge.id, round.number,
                                                'fighter1Score', Number(event.target.value))} />
                                            <span className="px-1">–</span>
                                            <input aria-label={`${judge.displayName} round ${round.number} fighter two`}
                                              type="number" min={7} max={10} value={score.fighter2Score}
                                              className="w-14 rounded border px-1 py-1"
                                              onChange={(event) => updateScore(bout, judge.id, round.number,
                                                'fighter2Score', Number(event.target.value))} />
                                          </td>;
                                        })}
                                      </tr>
                                    ))}
                                </tbody>
                              </table>
                              <p className="mt-2 text-xs text-gray-500">
                                Scores are shown as {bout.fighters[0]?.name} – {bout.fighters[1]?.name}.
                              </p>
                            </div>
                          )}

                          <Button disabled={busy || !draft.method || (resultLocked
                            && !(correctionReasons[bout.id] ?? '').trim()) || (!draft.winnerId
                            && draft.method !== 'Draw' && draft.method !== 'No Contest')}
                            onClick={() => void submitResult(bout, resultLocked)}>
                            <Save className="mr-2 h-4 w-4" />
                            {resultLocked ? 'Submit correction for approval' : 'Record official result'}
                          </Button>
                        </div>
                      )}
                      {bout.pendingCorrections.map((correction) => (
                        <div key={correction.id} className="space-y-2 rounded bg-amber-50 p-3 text-sm text-amber-900">
                          <strong>Result correction awaiting approval</strong>
                          <p>Requested {new Date(correction.requestedAt).toLocaleString()}.</p>
                          <input className="w-full rounded border px-2 py-2"
                            placeholder="Reason required when rejecting"
                            value={correctionReasons[bout.id] ?? ''}
                            onChange={(event) => setCorrectionReasons((current) => ({
                              ...current, [bout.id]: event.target.value,
                            }))} />
                          <div className="flex gap-2">
                            <Button size="sm" disabled={busy}
                              onClick={() => void reviewCorrection(bout, correction.id, 'approve')}>
                              Approve correction
                            </Button>
                            <Button size="sm" variant="outline"
                              disabled={busy || !(correctionReasons[bout.id] ?? '').trim()}
                              onClick={() => void reviewCorrection(bout, correction.id, 'reject')}>
                              Reject correction
                            </Button>
                          </div>
                        </div>
                      ))}
                    </section>
                  );
                })}
              </div>
              <Button onClick={() => void saveOrder()} disabled={busy || !card.canReorder}>
                <Save className="mr-2 h-4 w-4" /> Save card order
              </Button>
              {!card.canReorder && (
                <p className="text-sm text-amber-700">
                  Card ordering is locked because it is published or a bout has started.
                </p>
              )}
            </>
          )}
        </CardContent>
      </Card>
    </div>
  );
}

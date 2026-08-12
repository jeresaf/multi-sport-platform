'use client';

import { useMemo, useState } from 'react';
import { Check, RefreshCw, RotateCcw } from 'lucide-react';
import { Button } from '@/components/ui/button';
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from '@/components/ui/card';
import { ConnectedSportScoring } from '@/components/scoring/connected-sport-scoring';
import { adminApi } from '@/lib/admin-api';

type Team = { id: string; name: string | null };
type TournamentMatch = {
  id: string;
  name: string | null;
  status: string;
  teams: Array<{ participantId: string; name: string | null }>;
};
type Tournament = {
  groupMatches: TournamentMatch[];
  semifinals: TournamentMatch[];
  final: TournamentMatch | null;
};
type Delivery = {
  eventId: string;
  overNumber: number;
  ballNumber: number;
  runs: number;
  extraRuns: number;
  extraType: string | null;
  isWicket: boolean;
};
type Innings = {
  inningsNumber: number;
  battingTeamId: string | null;
  battingTeamName: string | null;
  isComplete: boolean;
  runs: number;
  wickets: number;
  balls: number;
  overs: string;
  extras: number;
  deliveries: Delivery[];
  overSummaries: Array<{
    overNumber: number;
    runs: number;
    wickets: number;
    balls: number;
  }>;
};
type Scorecard = {
  id: string;
  name: string | null;
  status: string;
  teams: Team[];
  innings: Innings[];
  target: number | null;
  runsRequired: number | null;
  winnerId: string | null;
  result: string | null;
};
type DeliveryDraft = {
  runs: number;
  extraRuns: number;
  extraType: string;
  isWicket: boolean;
};

const emptyDelivery: DeliveryDraft = {
  runs: 0,
  extraRuns: 0,
  extraType: '',
  isWicket: false,
};

function deliveryLabel(delivery: Delivery) {
  const pieces = [`${delivery.overNumber}.${delivery.ballNumber}`];
  if (delivery.isWicket) pieces.push('W');
  pieces.push(`${delivery.runs + delivery.extraRuns}`);
  if (delivery.extraRuns) pieces.push(`${delivery.extraRuns} ${delivery.extraType}`);
  return pieces.join(' · ');
}

export default function CricketScoringPage() {
  const [competitionId, setCompetitionId] = useState('');
  const [matches, setMatches] = useState<TournamentMatch[]>([]);
  const [matchId, setMatchId] = useState('');
  const [scorecard, setScorecard] = useState<Scorecard | null>(null);
  const [battingTeamId, setBattingTeamId] = useState('');
  const [delivery, setDelivery] = useState<DeliveryDraft>(emptyDelivery);
  const [correcting, setCorrecting] = useState<Delivery | null>(null);
  const [correction, setCorrection] = useState<DeliveryDraft>(emptyDelivery);
  const [correctionReason, setCorrectionReason] = useState('');
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState<string | null>(null);

  const currentInningsNumber = scorecard?.innings[0].isComplete ? 2 : 1;
  const currentInnings = scorecard?.innings[currentInningsNumber - 1];
  const nextBall = currentInnings
    ? `${Math.floor(currentInnings.balls / 6) + 1}.${(currentInnings.balls % 6) + 1}`
    : '1.1';
  const selectedMatch = matches.find((match) => match.id === matchId);
  const canRecord = Boolean(scorecard && !currentInnings?.isComplete
    && scorecard.status !== 'Completed');
  const allDeliveries = useMemo(
    () => scorecard?.innings.flatMap((innings) => innings.deliveries) ?? [],
    [scorecard],
  );

  function chooseDefaultBattingTeam(card: Scorecard) {
    const inningsNumber = card.innings[0].isComplete ? 2 : 1;
    const innings = card.innings[inningsNumber - 1];
    if (innings.battingTeamId) return innings.battingTeamId;
    if (inningsNumber === 2) {
      return card.teams.find((team) => team.id !== card.innings[0].battingTeamId)?.id
        ?? card.teams[0]?.id ?? '';
    }
    return card.teams[0]?.id ?? '';
  }

  async function loadScorecard(nextMatchId = matchId) {
    if (!competitionId.trim() || !nextMatchId) return;
    const loaded = await adminApi<Scorecard>(
      `/api/competitions/${competitionId.trim()}/cricket/matches/${nextMatchId}/scorecard`,
    );
    setScorecard(loaded);
    setBattingTeamId(chooseDefaultBattingTeam(loaded));
  }

  async function loadTournament() {
    if (!competitionId.trim()) return;
    setBusy(true);
    setMessage(null);
    try {
      const tournament = await adminApi<Tournament>(
        `/api/competitions/${competitionId.trim()}/cricket/tournament`,
      );
      const loadedMatches = [
        ...tournament.groupMatches,
        ...tournament.semifinals,
        ...(tournament.final ? [tournament.final] : []),
      ];
      setMatches(loadedMatches);
      const nextMatchId = loadedMatches.some((match) => match.id === matchId)
        ? matchId
        : loadedMatches[0]?.id ?? '';
      setMatchId(nextMatchId);
      if (nextMatchId) await loadScorecard(nextMatchId);
      else setScorecard(null);
    } catch (reason) {
      setMessage(reason instanceof Error ? reason.message : 'Could not load the tournament.');
    } finally {
      setBusy(false);
    }
  }

  async function recordBall(runs = delivery.runs) {
    if (!scorecard || !battingTeamId) return;
    setBusy(true);
    setMessage(null);
    try {
      const updated = await adminApi<Scorecard>(
        `/api/competitions/${competitionId.trim()}/cricket/matches/${scorecard.id}/deliveries`,
        {
          method: 'POST',
          body: JSON.stringify({
            inningsNumber: currentInningsNumber,
            battingTeamId,
            runs,
            extraRuns: delivery.extraRuns,
            extraType: delivery.extraRuns ? delivery.extraType : null,
            isWicket: delivery.isWicket,
          }),
        },
      );
      setScorecard(updated);
      setBattingTeamId(chooseDefaultBattingTeam(updated));
      setDelivery(emptyDelivery);
      setMessage(`Ball ${nextBall} recorded.`);
    } catch (reason) {
      setMessage(reason instanceof Error ? reason.message : 'Could not record the delivery.');
    } finally {
      setBusy(false);
    }
  }

  async function completeInnings() {
    if (!scorecard || !battingTeamId) return;
    setBusy(true);
    setMessage(null);
    try {
      const updated = await adminApi<Scorecard>(
        `/api/competitions/${competitionId.trim()}/cricket/matches/${scorecard.id}`
          + `/innings/${currentInningsNumber}/complete`,
        { method: 'POST', body: JSON.stringify({ battingTeamId }) },
      );
      setScorecard(updated);
      setBattingTeamId(chooseDefaultBattingTeam(updated));
      setMessage(`Innings ${currentInningsNumber} completed.`);
    } catch (reason) {
      setMessage(reason instanceof Error ? reason.message : 'Could not complete the innings.');
    } finally {
      setBusy(false);
    }
  }

  function beginCorrection(item: Delivery) {
    setCorrecting(item);
    setCorrection({
      runs: item.runs,
      extraRuns: item.extraRuns,
      extraType: item.extraType ?? '',
      isWicket: item.isWicket,
    });
    setCorrectionReason('');
  }

  async function saveCorrection() {
    if (!scorecard || !correcting) return;
    setBusy(true);
    setMessage(null);
    try {
      const updated = await adminApi<Scorecard>(
        `/api/competitions/${competitionId.trim()}/cricket/matches/${scorecard.id}`
          + `/deliveries/${correcting.eventId}`,
        {
          method: 'PUT',
          body: JSON.stringify({
            ...correction,
            extraType: correction.extraRuns ? correction.extraType : null,
            reason: correctionReason,
          }),
        },
      );
      setScorecard(updated);
      setCorrecting(null);
      setCorrectionReason('');
      setMessage(`Ball ${correcting.overNumber}.${correcting.ballNumber} corrected and innings rebuilt.`);
    } catch (reason) {
      setMessage(reason instanceof Error ? reason.message : 'Could not correct the delivery.');
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="space-y-6">
      <ConnectedSportScoring config={{
        name: 'Cricket',
        icon: '🏏',
        sportId: '00000000-0000-0000-0000-000000000104',
      }} />

      <Card>
        <CardHeader>
          <CardTitle>Cricket officials scorecard</CardTitle>
          <CardDescription>
            Record every legal delivery, follow over and innings totals, and rebuild results after corrections.
          </CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="flex flex-wrap gap-2">
            <input
              className="min-w-72 flex-1 rounded-md border px-3 py-2"
              aria-label="Competition ID"
              placeholder="Competition ID"
              value={competitionId}
              onChange={(event) => setCompetitionId(event.target.value)}
            />
            <Button disabled={busy || !competitionId.trim()} onClick={() => void loadTournament()}>
              <RefreshCw className="mr-2 h-4 w-4" /> Load tournament
            </Button>
          </div>
          {matches.length > 0 && (
            <select
              aria-label="Cricket match"
              className="w-full rounded-md border px-3 py-2"
              value={matchId}
              onChange={(event) => {
                setMatchId(event.target.value);
                void loadScorecard(event.target.value);
              }}
            >
              {matches.map((match) => (
                <option key={match.id} value={match.id}>
                  {match.name ?? match.id} · {match.status}
                </option>
              ))}
            </select>
          )}
          {message && <p role="status" className="text-sm text-gray-600">{message}</p>}
        </CardContent>
      </Card>

      {scorecard && (
        <>
          <Card>
            <CardHeader>
              <CardTitle>{scorecard.name ?? selectedMatch?.name ?? 'T20 match'}</CardTitle>
              <CardDescription>{scorecard.status}{scorecard.result && ` · ${scorecard.result}`}</CardDescription>
            </CardHeader>
            <CardContent className="space-y-4">
              <div className="grid gap-3 md:grid-cols-2">
                {scorecard.innings.map((innings) => (
                  <div key={innings.inningsNumber} className="rounded-lg border bg-gray-50 p-4">
                    <p className="text-sm text-gray-500">Innings {innings.inningsNumber}</p>
                    <p className="font-medium">{innings.battingTeamName ?? 'Not started'}</p>
                    <p className="text-3xl font-bold" data-testid={`cricket-innings-${innings.inningsNumber}-score`}>
                      {innings.runs}/{innings.wickets}
                    </p>
                    <p className="text-sm">{innings.overs} overs · {innings.extras} extras</p>
                    <p className="text-xs text-gray-500">{innings.isComplete ? 'Complete' : 'In progress'}</p>
                  </div>
                ))}
              </div>
              {scorecard.target !== null && (
                <div className="rounded-lg bg-blue-50 p-4 text-blue-900">
                  <strong>Target {scorecard.target}</strong>
                  <span className="ml-3" data-testid="cricket-runs-required">
                    {scorecard.runsRequired} runs required
                  </span>
                </div>
              )}
            </CardContent>
          </Card>

          {canRecord && currentInnings && (
            <Card>
              <CardHeader>
                <CardTitle>Record ball {nextBall}</CardTitle>
                <CardDescription>Innings {currentInningsNumber} · legal delivery</CardDescription>
              </CardHeader>
              <CardContent className="space-y-4">
                <label className="block text-sm font-medium">
                  Batting team
                  <select
                    aria-label="Batting team"
                    className="mt-1 w-full rounded-md border px-3 py-2"
                    value={battingTeamId}
                    disabled={currentInnings.balls > 0}
                    onChange={(event) => setBattingTeamId(event.target.value)}
                  >
                    {scorecard.teams.map((team) => (
                      <option key={team.id} value={team.id}>{team.name ?? team.id}</option>
                    ))}
                  </select>
                </label>
                <div className="flex flex-wrap gap-2" aria-label="Run outcomes">
                  {[0, 1, 2, 3, 4, 5, 6].map((runs) => (
                    <Button key={runs} variant="outline" disabled={busy}
                      aria-label={`Record ${runs} runs`} onClick={() => void recordBall(runs)}>
                      {runs}
                    </Button>
                  ))}
                </div>
                <div className="grid gap-3 sm:grid-cols-3">
                  <label className="text-sm">Extra runs
                    <input type="number" min="0" max="7" className="mt-1 w-full rounded border px-3 py-2"
                      value={delivery.extraRuns} onChange={(event) => setDelivery((current) => ({
                        ...current, extraRuns: Number(event.target.value),
                      }))} />
                  </label>
                  <label className="text-sm">Extra type
                    <select className="mt-1 w-full rounded border px-3 py-2" value={delivery.extraType}
                      onChange={(event) => setDelivery((current) => ({
                        ...current, extraType: event.target.value,
                      }))}>
                      <option value="">None</option><option value="Wide">Wide</option>
                      <option value="NoBall">No ball</option><option value="Bye">Bye</option>
                      <option value="LegBye">Leg bye</option><option value="Penalty">Penalty</option>
                    </select>
                  </label>
                  <label className="flex items-end gap-2 pb-2 text-sm">
                    <input type="checkbox" checked={delivery.isWicket}
                      onChange={(event) => setDelivery((current) => ({
                        ...current, isWicket: event.target.checked,
                      }))} /> Wicket
                  </label>
                </div>
                <Button disabled={busy || currentInnings.balls === 0} onClick={() => void completeInnings()}>
                  <Check className="mr-2 h-4 w-4" /> Complete innings
                </Button>
              </CardContent>
            </Card>
          )}

          <Card>
            <CardHeader>
              <CardTitle>Ball-by-ball record</CardTitle>
              <CardDescription>Approved deliveries and calculated over summaries.</CardDescription>
            </CardHeader>
            <CardContent className="space-y-4">
              {scorecard.innings.map((innings) => (
                <section key={innings.inningsNumber} className="space-y-2">
                  <h2 className="font-semibold">Innings {innings.inningsNumber}</h2>
                  <div className="flex flex-wrap gap-2">
                    {innings.overSummaries.map((over) => (
                      <span key={over.overNumber} className="rounded bg-gray-100 px-2 py-1 text-xs">
                        Over {over.overNumber}: {over.runs} runs, {over.wickets} wickets
                      </span>
                    ))}
                  </div>
                  <div className="grid gap-2 sm:grid-cols-2 lg:grid-cols-4">
                    {innings.deliveries.map((item) => (
                      <button key={item.eventId} type="button" onClick={() => beginCorrection(item)}
                        className="rounded border p-2 text-left text-sm hover:bg-gray-50"
                        aria-label={`Correct ball ${item.overNumber}.${item.ballNumber}`}>
                        {deliveryLabel(item)}
                      </button>
                    ))}
                  </div>
                </section>
              ))}
              {allDeliveries.length === 0 && <p className="text-sm text-gray-500">No balls recorded yet.</p>}
            </CardContent>
          </Card>

          {correcting && (
            <Card>
              <CardHeader>
                <CardTitle>Correct ball {correcting.overNumber}.{correcting.ballNumber}</CardTitle>
                <CardDescription>The original event remains in the audit history and the innings is rebuilt.</CardDescription>
              </CardHeader>
              <CardContent className="space-y-3">
                <div className="grid gap-3 sm:grid-cols-4">
                  <label className="text-sm">Runs<input type="number" min="0" max="6"
                    className="mt-1 w-full rounded border px-3 py-2" value={correction.runs}
                    onChange={(event) => setCorrection((current) => ({
                      ...current, runs: Number(event.target.value),
                    }))} /></label>
                  <label className="text-sm">Extras<input type="number" min="0" max="7"
                    className="mt-1 w-full rounded border px-3 py-2" value={correction.extraRuns}
                    onChange={(event) => setCorrection((current) => ({
                      ...current, extraRuns: Number(event.target.value),
                    }))} /></label>
                  <label className="text-sm">Extra type<select
                    className="mt-1 w-full rounded border px-3 py-2" value={correction.extraType}
                    onChange={(event) => setCorrection((current) => ({
                      ...current, extraType: event.target.value,
                    }))}>
                    <option value="">None</option><option value="Wide">Wide</option>
                    <option value="NoBall">No ball</option><option value="Bye">Bye</option>
                    <option value="LegBye">Leg bye</option><option value="Penalty">Penalty</option>
                  </select></label>
                  <label className="flex items-end gap-2 pb-2 text-sm"><input type="checkbox"
                    checked={correction.isWicket} onChange={(event) => setCorrection((current) => ({
                      ...current, isWicket: event.target.checked,
                    }))} /> Wicket</label>
                </div>
                <input className="w-full rounded border px-3 py-2" aria-label="Correction reason"
                  placeholder="Correction reason" value={correctionReason}
                  onChange={(event) => setCorrectionReason(event.target.value)} />
                <div className="flex gap-2">
                  <Button disabled={busy || !correctionReason.trim()} onClick={() => void saveCorrection()}>
                    <RotateCcw className="mr-2 h-4 w-4" /> Save correction
                  </Button>
                  <Button variant="outline" onClick={() => setCorrecting(null)}>Cancel</Button>
                </div>
              </CardContent>
            </Card>
          )}
        </>
      )}
    </div>
  );
}

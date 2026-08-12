'use client';

import { useState } from 'react';
import { Award, RefreshCw, Save, Trophy } from 'lucide-react';
import { ConnectedSportScoring } from '@/components/scoring/connected-sport-scoring';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { adminApi } from '@/lib/admin-api';

type RaceParticipant = {
  id: string;
  name: string;
  lane: number;
  status: string;
  qualificationStatus: string | null;
  qualificationReason: string | null;
  timeMilliseconds: number | null;
};
type Race = {
  id: string;
  name: string;
  contestShape: string;
  status: string;
  qualifyByPlace: number | null;
  qualifyByTime: number | null;
  qualificationApplied: boolean;
  participants: RaceParticipant[];
};
type FieldParticipant = {
  id: string;
  name: string;
  attempts: (number | null)[];
  bestMeasurement: number | null;
  disqualificationReason: string | null;
  isRecord: boolean;
};
type FieldEvent = {
  id: string;
  name: string;
  status: string;
  metricType: 'Distance' | 'Height';
  maxAttempts: number;
  recordThresholdMeters: number | null;
  participants: FieldParticipant[];
};
type AthleticsMeet = {
  id: string;
  name: string;
  status: string;
  races: Race[];
  fieldEvents: FieldEvent[];
};
type RaceDraft = Record<string, { timeSeconds: string; disqualified: boolean; reason: string }>;
type FieldDraft = Record<string, { attempts: string[]; disqualified: boolean; reason: string }>;

function formatTime(milliseconds: number) {
  return `${(milliseconds / 1000).toFixed(3)}s`;
}

export default function AthleticsScoringPage() {
  const [competitionId, setCompetitionId] = useState('');
  const [meet, setMeet] = useState<AthleticsMeet | null>(null);
  const [raceDrafts, setRaceDrafts] = useState<Record<string, RaceDraft>>({});
  const [fieldDrafts, setFieldDrafts] = useState<Record<string, FieldDraft>>({});
  const [message, setMessage] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  async function loadMeet() {
    if (!competitionId.trim()) return;
    setBusy(true);
    setMessage(null);
    try {
      const loaded = await adminApi<AthleticsMeet>(
        `/api/competitions/${competitionId.trim()}/athletics/meet`,
      );
      setMeet(loaded);
      setRaceDrafts(Object.fromEntries(loaded.races.map((race) => [race.id,
        Object.fromEntries(race.participants.map((participant) => [participant.id, {
          timeSeconds: participant.timeMilliseconds
            ? (participant.timeMilliseconds / 1000).toFixed(3)
            : '',
          disqualified: participant.status === 'Disqualified',
          reason: participant.qualificationReason ?? '',
        }]))])));
      setFieldDrafts(Object.fromEntries(loaded.fieldEvents.map((event) => [event.id,
        Object.fromEntries(event.participants.map((participant) => [participant.id, {
          attempts: Array.from({ length: event.maxAttempts }, (_, index) =>
            participant.attempts[index]?.toString() ?? ''),
          disqualified: Boolean(participant.disqualificationReason),
          reason: participant.disqualificationReason ?? '',
        }]))])));
    } catch (reason) {
      setMessage(reason instanceof Error ? reason.message : 'Could not load the athletics meet.');
    } finally {
      setBusy(false);
    }
  }

  function updateRaceParticipant(
    raceId: string,
    participantId: string,
    values: Partial<RaceDraft[string]>,
  ) {
    setRaceDrafts((current) => ({
      ...current,
      [raceId]: {
        ...current[raceId],
        [participantId]: { ...current[raceId][participantId], ...values },
      },
    }));
  }

  async function submitRace(race: Race) {
    if (!meet) return;
    const draft = raceDrafts[race.id];
    setBusy(true);
    setMessage(null);
    try {
      await adminApi(`/api/competitions/${meet.id}/athletics/races/${race.id}/results`, {
        method: 'POST',
        body: JSON.stringify({
          results: race.participants.map((participant) => ({
            participantId: participant.id,
            timeMilliseconds: draft[participant.id].disqualified
              ? null
              : Math.round(Number(draft[participant.id].timeSeconds) * 1000),
            disqualified: draft[participant.id].disqualified,
            disqualificationReason: draft[participant.id].disqualified
              ? draft[participant.id].reason
              : null,
          })),
        }),
      });
      setMessage(`${race.name} results recorded.`);
      await loadMeet();
    } catch (reason) {
      setMessage(reason instanceof Error ? reason.message : 'Could not record race results.');
      setBusy(false);
    }
  }

  async function applyQualification() {
    if (!meet) return;
    setBusy(true);
    setMessage(null);
    try {
      const result = await adminApi<{ nextRound: string; qualifiedCount: number }>(
        `/api/competitions/${meet.id}/athletics/qualify`,
        { method: 'POST' },
      );
      setMessage(`${result.qualifiedCount} athletes advanced to the ${result.nextRound}.`);
      await loadMeet();
    } catch (reason) {
      setMessage(reason instanceof Error ? reason.message : 'Could not apply qualification.');
      setBusy(false);
    }
  }

  function updateFieldParticipant(
    eventId: string,
    participantId: string,
    values: Partial<FieldDraft[string]>,
  ) {
    setFieldDrafts((current) => ({
      ...current,
      [eventId]: {
        ...current[eventId],
        [participantId]: { ...current[eventId][participantId], ...values },
      },
    }));
  }

  function updateAttempt(eventId: string, participantId: string, index: number, value: string) {
    const current = fieldDrafts[eventId][participantId];
    const attempts = [...current.attempts];
    attempts[index] = value;
    updateFieldParticipant(eventId, participantId, { attempts });
  }

  async function submitFieldEvent(event: FieldEvent) {
    if (!meet) return;
    const draft = fieldDrafts[event.id];
    setBusy(true);
    setMessage(null);
    try {
      await adminApi(`/api/competitions/${meet.id}/athletics/field-events/${event.id}/results`, {
        method: 'POST',
        body: JSON.stringify({
          results: event.participants.map((participant) => ({
            participantId: participant.id,
            attempts: draft[participant.id].disqualified
              ? []
              : draft[participant.id].attempts.map((measurement) => ({
                measurementMeters: measurement.trim() ? Number(measurement) : null,
              })),
            disqualified: draft[participant.id].disqualified,
            disqualificationReason: draft[participant.id].disqualified
              ? draft[participant.id].reason
              : null,
          })),
        }),
      });
      setMessage(`${event.name} measurements recorded.`);
      await loadMeet();
    } catch (reason) {
      setMessage(reason instanceof Error ? reason.message : 'Could not record field-event results.');
      setBusy(false);
    }
  }

  const canQualify = meet?.races.some((race) =>
    race.contestShape !== 'AthleticsFinal'
    && race.status === 'Completed'
    && !race.qualificationApplied) ?? false;

  return (
    <div className="space-y-6">
      <ConnectedSportScoring config={{
        name: 'Athletics', icon: '🏃',
        sportId: '00000000-0000-0000-0000-000000000103',
      }} />
      <Card>
        <CardHeader>
          <CardTitle>Athletics officials console</CardTitle>
          <CardDescription>
            Enter track times, field measurements, disqualifications, qualification, and records.
          </CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="flex flex-wrap gap-2">
            <input className="min-w-72 flex-1 rounded-md border px-3 py-2"
              placeholder="Competition ID" value={competitionId}
              onChange={(event) => setCompetitionId(event.target.value)} />
            <Button disabled={busy || !competitionId.trim()} onClick={() => void loadMeet()}>
              <RefreshCw className="mr-2 h-4 w-4" /> Load meet
            </Button>
          </div>
          {message && <p role="status" className="text-sm text-gray-600">{message}</p>}
          {meet && (
            <>
              <div className="rounded bg-gray-50 p-3 text-sm">
                <strong>{meet.name}</strong> · {meet.status}
              </div>
              <div className="space-y-4">
                {meet.races.map((race) => (
                  <section key={race.id} className="space-y-3 rounded border p-4">
                    <div>
                      <h2 className="font-semibold">{race.name}</h2>
                      <p className="text-xs text-gray-500">
                        {race.contestShape} · {race.status} · Qualify {race.qualifyByPlace ?? 0} by place
                        {' '}and {race.qualifyByTime ?? 0} fastest remaining
                      </p>
                    </div>
                    <div className="overflow-x-auto">
                      <table className="w-full text-sm">
                        <thead><tr className="border-b text-left">
                          <th className="p-2">Lane</th><th className="p-2">Athlete</th>
                          <th className="p-2">Time (seconds)</th><th className="p-2">DQ</th>
                          <th className="p-2">Reason / qualification</th>
                        </tr></thead>
                        <tbody>{race.participants.map((participant) => {
                          const draft = raceDrafts[race.id]?.[participant.id];
                          return <tr key={participant.id} className="border-b">
                            <td className="p-2 font-medium">{participant.lane}</td>
                            <td className="p-2">{participant.name}</td>
                            <td className="p-2">
                              {race.status === 'Completed' && participant.timeMilliseconds
                                ? formatTime(participant.timeMilliseconds)
                                : <input type="number" min="8" max="60" step="0.001"
                                  aria-label={`${participant.name} finish time`}
                                  disabled={draft?.disqualified || race.status === 'Completed'}
                                  className="w-28 rounded border px-2 py-1"
                                  value={draft?.timeSeconds ?? ''}
                                  onChange={(event) => updateRaceParticipant(
                                    race.id, participant.id, { timeSeconds: event.target.value })} />}
                            </td>
                            <td className="p-2"><input type="checkbox"
                              aria-label={`Disqualify ${participant.name}`}
                              checked={draft?.disqualified ?? false}
                              disabled={race.status === 'Completed'}
                              onChange={(event) => updateRaceParticipant(
                                race.id, participant.id, { disqualified: event.target.checked })} /></td>
                            <td className="p-2">
                              {participant.qualificationStatus ? (
                                <span className={participant.qualificationStatus === 'Qualified'
                                  ? 'font-medium text-green-700' : 'text-amber-700'}>
                                  {participant.qualificationStatus}: {participant.qualificationReason}
                                </span>
                              ) : <input className="w-full rounded border px-2 py-1"
                                placeholder="Required for DQ" value={draft?.reason ?? ''}
                                disabled={!draft?.disqualified || race.status === 'Completed'}
                                onChange={(event) => updateRaceParticipant(
                                  race.id, participant.id, { reason: event.target.value })} />}
                            </td>
                          </tr>;
                        })}</tbody>
                      </table>
                    </div>
                    {race.status !== 'Completed' && (
                      <Button disabled={busy} onClick={() => void submitRace(race)}>
                        <Save className="mr-2 h-4 w-4" /> Record race results
                      </Button>
                    )}
                  </section>
                ))}
              </div>
              {canQualify && <Button disabled={busy} onClick={() => void applyQualification()}>
                <Trophy className="mr-2 h-4 w-4" /> Apply qualification
              </Button>}

              <div className="space-y-4">
                {meet.fieldEvents.map((event) => (
                  <section key={event.id} className="space-y-3 rounded border p-4">
                    <div>
                      <h2 className="font-semibold">{event.name}</h2>
                      <p className="text-xs text-gray-500">
                        {event.metricType} · Higher is better · {event.maxAttempts} attempts
                        {event.recordThresholdMeters && ` · Record ${event.recordThresholdMeters.toFixed(3)}m`}
                      </p>
                    </div>
                    {event.participants.map((participant) => {
                      const draft = fieldDrafts[event.id]?.[participant.id];
                      return <div key={participant.id} className="space-y-2 rounded bg-gray-50 p-3">
                        <div className="flex flex-wrap items-center gap-3">
                          <strong className="min-w-40">{participant.name}</strong>
                          {event.status === 'Completed' ? participant.attempts.map((attempt, index) => (
                            <span key={index} className="rounded border bg-white px-2 py-1">
                              {attempt === null ? 'Foul' : `${attempt.toFixed(3)}m`}
                            </span>
                          )) : draft?.attempts.map((attempt, index) => (
                            <input key={index} type="number" min="0" step="0.001"
                              aria-label={`${participant.name} attempt ${index + 1}`}
                              placeholder={`Attempt ${index + 1} / foul`}
                              disabled={draft.disqualified}
                              className="w-32 rounded border px-2 py-1" value={attempt}
                              onChange={(change) => updateAttempt(
                                event.id, participant.id, index, change.target.value)} />
                          ))}
                          {participant.bestMeasurement !== null && (
                            <span className="font-semibold">Best {participant.bestMeasurement.toFixed(3)}m</span>
                          )}
                          {participant.isRecord && (
                            <span className="inline-flex items-center rounded bg-yellow-100 px-2 py-1 font-semibold text-yellow-800">
                              <Award className="mr-1 h-4 w-4" /> Record
                            </span>
                          )}
                        </div>
                        {event.status !== 'Completed' && <div className="flex items-center gap-2 text-sm">
                          <label><input type="checkbox" checked={draft?.disqualified ?? false}
                            onChange={(change) => updateFieldParticipant(event.id, participant.id, {
                              disqualified: change.target.checked,
                            })} /> Disqualified</label>
                          <input className="flex-1 rounded border px-2 py-1" placeholder="DQ reason"
                            disabled={!draft?.disqualified} value={draft?.reason ?? ''}
                            onChange={(change) => updateFieldParticipant(event.id, participant.id, {
                              reason: change.target.value,
                            })} />
                        </div>}
                        {participant.disqualificationReason && (
                          <p className="text-sm text-red-700">Disqualified: {participant.disqualificationReason}</p>
                        )}
                      </div>;
                    })}
                    {event.status !== 'Completed' && (
                      <Button disabled={busy} onClick={() => void submitFieldEvent(event)}>
                        <Save className="mr-2 h-4 w-4" /> Record field results
                      </Button>
                    )}
                  </section>
                ))}
              </div>
            </>
          )}
        </CardContent>
      </Card>
    </div>
  );
}

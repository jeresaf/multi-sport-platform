'use client';

import { useCallback, useEffect, useState } from 'react';
import { AlertTriangle, CalendarClock, CheckCircle2, RefreshCw } from 'lucide-react';
import { adminApi, getStoredUser } from '@/lib/admin-api';

type Competition = { id: string; name: string; status: string | number };
type Conflict = {
  key: string;
  type: string;
  message: string;
  contestId: string;
  conflictingContestId?: string | null;
  isOverridden: boolean;
  overrideReason?: string | null;
};
type Contest = {
  id: string;
  name: string;
  venue?: string | null;
  startTime?: string | null;
  durationMinutes: number;
};
type Validation = {
  canPublish: boolean;
  conflicts: Conflict[];
  contests: Contest[];
  policy: {
    defaultDurationMinutes: number;
    minimumRestMinutes: number;
    venueTurnaroundMinutes: number;
    windowStartMinutes: number;
    windowEndMinutes: number;
    blackoutDatesJson: string;
  };
};
type FixtureAdjustment = {
  id: string;
  contestId: string;
  venue: string;
  startTime: string;
  durationMinutes: number;
  reason: string;
  status: number | string;
  requestedAt: string;
  reviewNotes?: string | null;
};

export default function SchedulePage() {
  const [tenantId, setTenantId] = useState('');
  const [competitions, setCompetitions] = useState<Competition[]>([]);
  const [competitionId, setCompetitionId] = useState('');
  const [validation, setValidation] = useState<Validation | null>(null);
  const [overrideReason, setOverrideReason] = useState('');
  const [adjustmentReason, setAdjustmentReason] = useState('');
  const [reviewNotes, setReviewNotes] = useState('');
  const [adjustments, setAdjustments] = useState<FixtureAdjustment[]>([]);
  const [blackoutDates, setBlackoutDates] = useState('');
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => setTenantId(getStoredUser()?.tenantId ?? ''), []);
  useEffect(() => {
    if (!tenantId) return;
    adminApi<Competition[]>(`/api/competitions?tenantId=${tenantId}`)
      .then((items) => {
        setCompetitions(items);
        setCompetitionId((current) => current || items[0]?.id || '');
      })
      .catch((reason) => setError(reason instanceof Error ? reason.message : 'Could not load competitions.'));
  }, [tenantId]);
  const loadValidation = useCallback(async () => {
    if (!competitionId) return;
    setBusy(true);
    setError(null);
    try {
      const [result, history] = await Promise.all([
        adminApi<Validation>(`/api/competitions/${competitionId}/schedule/validation`),
        adminApi<FixtureAdjustment[]>(`/api/competitions/${competitionId}/schedule/adjustments`),
      ]);
      setValidation(result);
      setAdjustments(history);
      setBlackoutDates((JSON.parse(result.policy.blackoutDatesJson) as string[]).join(', '));
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'Could not validate the schedule.');
    } finally {
      setBusy(false);
    }
  }, [competitionId]);
  useEffect(() => {
    void loadValidation();
  }, [loadValidation]);

  async function saveConstraints(form: FormData) {
    setBusy(true);
    setError(null);
    try {
      await adminApi(`/api/competitions/${competitionId}/schedule/constraints`, {
        method: 'PUT',
        body: JSON.stringify({
          defaultDurationMinutes: Number(form.get('duration')),
          minimumRestMinutes: Number(form.get('rest')),
          venueTurnaroundMinutes: Number(form.get('turnaround')),
          windowStartMinutes: Number(form.get('windowStart')),
          windowEndMinutes: Number(form.get('windowEnd')),
          blackoutDates: blackoutDates.split(',').map((item) => item.trim()).filter(Boolean),
        }),
      });
      setMessage('Scheduling constraints saved. Previous overrides were cleared.');
      await loadValidation();
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'Could not save constraints.');
    } finally {
      setBusy(false);
    }
  }

  async function overrideConflict(conflictKey: string) {
    if (!overrideReason.trim()) {
      setError('Enter an audit reason before overriding a conflict.');
      return;
    }
    setBusy(true);
    try {
      await adminApi(`/api/competitions/${competitionId}/schedule/overrides`, {
        method: 'POST',
        body: JSON.stringify({ conflictKey, reason: overrideReason }),
      });
      setMessage('Conflict override recorded.');
      await loadValidation();
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'Could not override the conflict.');
    } finally {
      setBusy(false);
    }
  }

  async function reschedule(contest: Contest, form: FormData) {
    if (!adjustmentReason.trim()) {
      setError('Enter a reason before requesting a fixture adjustment.');
      return;
    }
    setBusy(true);
    try {
      await adminApi(`/api/competitions/${competitionId}/schedule/contests/${contest.id}`, {
        method: 'PUT',
        body: JSON.stringify({
          venue: String(form.get('venue')),
          startTime: new Date(String(form.get('startTime'))).toISOString(),
          durationMinutes: Number(form.get('duration')),
          reason: adjustmentReason,
        }),
      });
      setMessage('Fixture adjustment submitted for independent approval.');
      await loadValidation();
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'Could not reschedule the contest.');
    } finally {
      setBusy(false);
    }
  }

  async function reviewAdjustment(adjustmentId: string, decision: 'approve' | 'reject') {
    setBusy(true);
    setError(null);
    try {
      await adminApi(`/api/competitions/${competitionId}/schedule/adjustments/${adjustmentId}/${decision}`, {
        method: 'POST',
        body: JSON.stringify({ notes: reviewNotes || null }),
      });
      setMessage(decision === 'approve' ? 'Fixture adjustment approved and applied.' : 'Fixture adjustment rejected.');
      await loadValidation();
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'Could not review the fixture adjustment.');
    } finally {
      setBusy(false);
    }
  }

  async function publish() {
    setBusy(true);
    try {
      await adminApi(`/api/competitions/${competitionId}/publish`, { method: 'POST' });
      setMessage('Competition published.');
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'Could not publish the competition.');
    } finally {
      setBusy(false);
    }
  }

  const numberInput = 'rounded-lg border border-gray-200 px-3 py-2';
  return (
    <main className="space-y-6">
      <div className="flex items-center justify-between">
        <div><h1 className="text-3xl font-bold text-gray-900">Schedule control</h1><p className="mt-1 text-gray-500">Validate fixtures before publication and keep overrides auditable.</p></div>
        <button onClick={() => void loadValidation()} disabled={busy || !competitionId} className="flex items-center gap-2 rounded-lg border px-4 py-2 font-semibold"><RefreshCw className="h-4 w-4" /> Refresh</button>
      </div>
      {error && <p className="rounded-lg bg-red-50 p-3 text-red-700">{error}</p>}
      {message && <p className="rounded-lg bg-emerald-50 p-3 text-emerald-700">{message}</p>}
      {!tenantId && <p className="rounded-xl border bg-white p-6">Sign in with a tenant administrator account to manage schedules.</p>}
      <section className="rounded-xl border bg-white p-5">
        <label className="text-sm font-semibold">Competition</label>
        <select value={competitionId} onChange={(event) => setCompetitionId(event.target.value)} className="mt-2 w-full rounded-lg border px-3 py-2">
          {competitions.map((item) => <option key={item.id} value={item.id}>{item.name}</option>)}
        </select>
      </section>
      {validation && <>
        <section className="rounded-xl border bg-white p-5">
          <div className="flex items-center justify-between"><h2 className="flex items-center gap-2 text-xl font-bold"><CalendarClock className="h-5 w-5" /> Constraints</h2><span className={`rounded-full px-3 py-1 text-sm font-semibold ${validation.canPublish ? 'bg-emerald-100 text-emerald-700' : 'bg-amber-100 text-amber-800'}`}>{validation.canPublish ? 'Ready to publish' : `${validation.conflicts.filter((item) => !item.isOverridden).length} unresolved`}</span></div>
          <form action={(data) => void saveConstraints(data)} className="mt-4 grid gap-3 md:grid-cols-3">
            <label className="text-sm">Default duration (minutes)<input name="duration" type="number" defaultValue={validation.policy.defaultDurationMinutes} className={`mt-1 w-full ${numberInput}`} /></label>
            <label className="text-sm">Minimum rest (minutes)<input name="rest" type="number" defaultValue={validation.policy.minimumRestMinutes} className={`mt-1 w-full ${numberInput}`} /></label>
            <label className="text-sm">Venue turnaround (minutes)<input name="turnaround" type="number" defaultValue={validation.policy.venueTurnaroundMinutes} className={`mt-1 w-full ${numberInput}`} /></label>
            <label className="text-sm">Daily start (minutes after midnight)<input name="windowStart" type="number" defaultValue={validation.policy.windowStartMinutes} className={`mt-1 w-full ${numberInput}`} /></label>
            <label className="text-sm">Daily end (minutes after midnight)<input name="windowEnd" type="number" defaultValue={validation.policy.windowEndMinutes} className={`mt-1 w-full ${numberInput}`} /></label>
            <label className="text-sm">Blackout dates (YYYY-MM-DD, comma separated)<input value={blackoutDates} onChange={(event) => setBlackoutDates(event.target.value)} className={`mt-1 w-full ${numberInput}`} /></label>
            <button disabled={busy} className="rounded-lg bg-primary px-4 py-2 font-semibold text-white md:col-span-3">Save constraints</button>
          </form>
        </section>
        <section className="rounded-xl border bg-white p-5">
          <h2 className="text-xl font-bold">Conflicts and overrides</h2>
          <input value={overrideReason} onChange={(event) => setOverrideReason(event.target.value)} placeholder="Required reason for an override" className="mt-3 w-full rounded-lg border px-3 py-2" />
          <div className="mt-4 space-y-3">{validation.conflicts.length === 0 && <p className="flex items-center gap-2 text-emerald-700"><CheckCircle2 className="h-5 w-5" /> No scheduling conflicts.</p>}{validation.conflicts.map((item) => <article key={item.key} className="flex items-center justify-between gap-4 rounded-lg border p-3"><div><p className="flex items-center gap-2 font-semibold"><AlertTriangle className="h-4 w-4 text-amber-600" /> {item.type}</p><p className="text-sm text-gray-600">{item.message}</p>{item.overrideReason && <p className="mt-1 text-xs text-emerald-700">Override: {item.overrideReason}</p>}</div>{!item.isOverridden && <button onClick={() => void overrideConflict(item.key)} disabled={busy} className="rounded-lg border px-3 py-2 text-sm font-semibold">Override</button>}</article>)}</div>
          <button onClick={() => void publish()} disabled={busy || !validation.canPublish} className="mt-4 rounded-lg bg-primary px-4 py-2 font-semibold text-white disabled:opacity-40">Publish competition</button>
        </section>
        <section className="rounded-xl border bg-white p-5"><h2 className="text-xl font-bold">Fixtures</h2><p className="mt-1 text-sm text-gray-500">Changes are applied only after approval by a different administrator.</p><input value={adjustmentReason} onChange={(event) => setAdjustmentReason(event.target.value)} placeholder="Required adjustment reason" className="mt-3 w-full rounded-lg border px-3 py-2" /><div className="mt-4 space-y-3">{validation.contests.map((contest) => <form key={contest.id} action={(data) => void reschedule(contest, data)} className="grid items-end gap-3 rounded-lg border p-3 md:grid-cols-4"><div><p className="font-semibold">{contest.name}</p><p className="text-xs text-gray-500">{contest.id}</p></div><label className="text-sm">Venue<input name="venue" defaultValue={contest.venue ?? ''} required className={`mt-1 w-full ${numberInput}`} /></label><label className="text-sm">Start<input name="startTime" type="datetime-local" defaultValue={contest.startTime?.slice(0, 16) ?? ''} required className={`mt-1 w-full ${numberInput}`} /></label><div className="flex gap-2"><input aria-label="Duration minutes" name="duration" type="number" defaultValue={contest.durationMinutes} className={`w-24 ${numberInput}`} /><button disabled={busy} className="rounded-lg border px-3 py-2 font-semibold">Request</button></div></form>)}</div></section>
        <section className="rounded-xl border bg-white p-5"><h2 className="text-xl font-bold">Adjustment review</h2><input value={reviewNotes} onChange={(event) => setReviewNotes(event.target.value)} placeholder="Optional reviewer notes" className="mt-3 w-full rounded-lg border px-3 py-2" /><div className="mt-4 space-y-3">{adjustments.length === 0 && <p className="text-sm text-gray-500">No fixture adjustment requests.</p>}{adjustments.map((item) => { const pending = item.status === 0 || item.status === 'Pending'; return <article key={item.id} className="rounded-lg border p-3"><div className="flex flex-wrap items-start justify-between gap-3"><div><p className="font-semibold">{item.venue} · {new Date(item.startTime).toLocaleString()}</p><p className="text-sm text-gray-600">{item.reason} · {item.durationMinutes} minutes</p><p className="text-xs text-gray-500">Status: {String(item.status)} · requested {new Date(item.requestedAt).toLocaleString()}</p>{item.reviewNotes && <p className="text-xs text-gray-500">Review: {item.reviewNotes}</p>}</div>{pending && <div className="flex gap-2"><button type="button" disabled={busy} onClick={() => void reviewAdjustment(item.id, 'approve')} className="rounded-lg bg-primary px-3 py-2 text-sm font-semibold text-white">Approve</button><button type="button" disabled={busy} onClick={() => void reviewAdjustment(item.id, 'reject')} className="rounded-lg border px-3 py-2 text-sm font-semibold">Reject</button></div>}</div></article>; })}</div></section>
      </>}
    </main>
  );
}

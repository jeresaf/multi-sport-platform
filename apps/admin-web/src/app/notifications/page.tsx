'use client';

import { useCallback, useEffect, useState } from 'react';
import { Bell, Megaphone, RefreshCw, Send } from 'lucide-react';
import { adminApi, getStoredUser } from '@/lib/admin-api';

type Competition = { id: string; name: string };
type Contest = { id: string; name: string; startTime?: string | null; venue?: string | null };
type Schedule = { contests: Contest[] };
type Campaign = {
  id: string;
  type: string;
  status: string;
  title: string;
  body: string;
  reason: string;
  competitionId?: string | null;
  contestId?: string | null;
  recipientCount: number;
  createdAt: string;
  dispatchedAt?: string | null;
};

export default function NotificationsPage() {
  const [tenantId, setTenantId] = useState('');
  const [competitions, setCompetitions] = useState<Competition[]>([]);
  const [competitionId, setCompetitionId] = useState('');
  const [contests, setContests] = useState<Contest[]>([]);
  const [contestId, setContestId] = useState('');
  const [type, setType] = useState<'FixtureReminder' | 'Announcement'>('FixtureReminder');
  const [campaigns, setCampaigns] = useState<Campaign[]>([]);
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  const loadCampaigns = useCallback(async (scope: string) => {
    if (!scope) return;
    setCampaigns(await adminApi<Campaign[]>(`/api/notifications/campaigns?tenantId=${scope}`));
  }, []);

  useEffect(() => {
    const scope = getStoredUser()?.tenantId ?? '';
    setTenantId(scope);
    if (!scope) return;
    Promise.all([
      adminApi<Competition[]>(`/api/competitions?tenantId=${scope}`),
      loadCampaigns(scope),
    ]).then(([items]) => {
      setCompetitions(items);
      setCompetitionId(items[0]?.id ?? '');
    }).catch((reason) => setError(reason instanceof Error ? reason.message : 'Could not load notification tools.'));
  }, [loadCampaigns]);

  useEffect(() => {
    if (!competitionId) { setContests([]); setContestId(''); return; }
    adminApi<Schedule>(`/api/competitions/${competitionId}/schedule/validation`)
      .then((schedule) => {
        setContests(schedule.contests);
        setContestId((current) => schedule.contests.some((item) => item.id === current)
          ? current : schedule.contests[0]?.id ?? '');
      })
      .catch(() => { setContests([]); setContestId(''); });
  }, [competitionId]);

  async function dispatch(form: FormData) {
    if (type === 'FixtureReminder' && !contestId) {
      setError('Select a fixture before sending a reminder.');
      return;
    }
    setBusy(true);
    setError(null);
    setMessage(null);
    try {
      const campaign = await adminApi<Campaign>('/api/notifications/campaigns', {
        method: 'POST',
        body: JSON.stringify({
          tenantId,
          type,
          title: String(form.get('title')),
          body: String(form.get('body')),
          reason: String(form.get('reason')) || null,
          competitionId: competitionId || null,
          contestId: type === 'FixtureReminder' ? contestId : (contestId || null),
          idempotencyKey: crypto.randomUUID(),
        }),
      });
      setMessage(`${campaign.type} dispatched to ${campaign.recipientCount} active users.`);
      await loadCampaigns(tenantId);
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'Could not dispatch the notification.');
    } finally {
      setBusy(false);
    }
  }

  return <main className="space-y-6">
    <div className="flex flex-wrap items-start justify-between gap-3">
      <div><h1 className="flex items-center gap-2 text-3xl font-bold"><Bell className="h-7 w-7" /> Notifications</h1><p className="mt-1 text-gray-500">Author tenant announcements and fixture reminders with durable duplicate protection.</p></div>
      <button onClick={() => void loadCampaigns(tenantId)} disabled={busy || !tenantId} className="flex items-center gap-2 rounded-lg border px-4 py-2 font-semibold"><RefreshCw className="h-4 w-4" />Refresh</button>
    </div>
    {error && <p role="alert" className="rounded-lg bg-red-50 p-3 text-red-700">{error}</p>}
    {message && <p role="status" className="rounded-lg bg-emerald-50 p-3 text-emerald-700">{message}</p>}
    {!tenantId && <p className="rounded-xl border bg-white p-6">Sign in with a tenant administrator account to author notifications.</p>}
    {tenantId && <section className="rounded-xl border bg-white p-5">
      <h2 className="flex items-center gap-2 text-xl font-bold"><Megaphone className="h-5 w-5" />Compose</h2>
      <div className="mt-4 grid gap-3 md:grid-cols-2">
        <label className="text-sm">Type<select value={type} onChange={(event) => setType(event.target.value as typeof type)} className="mt-1 w-full rounded-lg border px-3 py-2"><option value="FixtureReminder">Fixture reminder</option><option value="Announcement">Announcement</option></select></label>
        <label className="text-sm">Competition<select value={competitionId} onChange={(event) => setCompetitionId(event.target.value)} className="mt-1 w-full rounded-lg border px-3 py-2"><option value="">Tenant-wide</option>{competitions.map((item) => <option key={item.id} value={item.id}>{item.name}</option>)}</select></label>
        <label className="text-sm md:col-span-2">Fixture<select value={contestId} onChange={(event) => setContestId(event.target.value)} required={type === 'FixtureReminder'} className="mt-1 w-full rounded-lg border px-3 py-2"><option value="">{type === 'FixtureReminder' ? 'Select fixture' : 'No fixture deep link'}</option>{contests.map((item) => <option key={item.id} value={item.id}>{item.name}{item.startTime ? ` · ${new Date(item.startTime).toLocaleString()}` : ''}{item.venue ? ` · ${item.venue}` : ''}</option>)}</select></label>
      </div>
      <form action={(data) => void dispatch(data)} className="mt-3 grid gap-3">
        <label className="text-sm">Title<input name="title" required maxLength={160} className="mt-1 w-full rounded-lg border px-3 py-2" /></label>
        <label className="text-sm">Message<textarea name="body" required maxLength={1000} rows={4} className="mt-1 w-full rounded-lg border px-3 py-2" /></label>
        <label className="text-sm">Audit reason<input name="reason" required maxLength={500} className="mt-1 w-full rounded-lg border px-3 py-2" /></label>
        <button disabled={busy} className="flex items-center justify-center gap-2 rounded-lg bg-primary px-4 py-2 font-semibold text-white disabled:opacity-50"><Send className="h-4 w-4" />{busy ? 'Dispatching…' : 'Dispatch now'}</button>
      </form>
    </section>}
    <section className="rounded-xl border bg-white p-5"><h2 className="text-xl font-bold">Dispatch history</h2><div className="mt-4 space-y-3">{campaigns.length === 0 && <p className="text-sm text-gray-500">No authored campaigns.</p>}{campaigns.map((item) => <article key={item.id} className="rounded-lg border p-3"><div className="flex flex-wrap justify-between gap-2"><div><p className="font-semibold">{item.title}</p><p className="text-sm text-gray-600">{item.body}</p><p className="mt-1 text-xs text-gray-500">Reason: {item.reason}</p></div><span className="h-fit rounded-full bg-emerald-100 px-3 py-1 text-xs font-semibold text-emerald-700">{item.status}</span></div><p className="mt-2 text-xs text-gray-500">{item.type} · {item.recipientCount} recipients · {new Date(item.createdAt).toLocaleString()}</p></article>)}</div></section>
  </main>;
}

'use client';

import { useCallback, useEffect, useMemo, useState } from 'react';
import { Bell, Megaphone, RefreshCw, Send } from 'lucide-react';
import { adminApi, getEffectiveTenantId } from '@/lib/admin-api';

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

  const metrics = useMemo(() => {
    const announcements = campaigns.filter(item => item.type === 'Announcement').length;
    const reminders = campaigns.filter(item => item.type === 'FixtureReminder').length;
    const recipients = campaigns.reduce((sum, item) => sum + item.recipientCount, 0);
    return {
      campaigns: campaigns.length,
      announcements,
      reminders,
      recipients,
    };
  }, [campaigns]);

  const loadCampaigns = useCallback(async (scope: string) => {
    if (!scope) return;
    setCampaigns(await adminApi<Campaign[]>(`/api/notifications/campaigns?tenantId=${scope}`));
  }, []);

  useEffect(() => {
    const scope = getEffectiveTenantId() ?? '';
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
    <section className="overflow-hidden rounded-3xl bg-gradient-to-br from-slate-950 via-slate-900 to-slate-800 p-6 text-white shadow-sm">
      <div className="flex flex-col gap-5 xl:flex-row xl:items-end xl:justify-between">
        <div className="max-w-3xl">
          <p className="text-xs font-bold uppercase tracking-[0.2em] text-slate-400">Audience operations</p>
          <h1 className="mt-2 flex items-center gap-3 text-3xl font-black tracking-tight"><Bell className="h-8 w-8" />Notification operations</h1>
          <p className="mt-2 max-w-2xl text-sm leading-6 text-slate-300">
            Author tenant announcements and fixture reminders with auditable reasons, deep links, recipient counts, and durable duplicate protection.
          </p>
        </div>
        <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
          <NotificationMetric label="Campaigns" value={metrics.campaigns} />
          <NotificationMetric label="Announcements" value={metrics.announcements} />
          <NotificationMetric label="Reminders" value={metrics.reminders} />
          <NotificationMetric label="Recipients" value={metrics.recipients} />
        </div>
      </div>
      <div className="mt-5">
        <button onClick={() => void loadCampaigns(tenantId)} disabled={busy || !tenantId} className="flex items-center gap-2 rounded-lg border border-white/15 bg-white/10 px-4 py-2 text-sm font-bold text-white transition hover:bg-white/15 disabled:opacity-50"><RefreshCw className="h-4 w-4" />Refresh history</button>
      </div>
    </section>
    {error && <p role="alert" className="rounded-lg bg-red-50 p-3 text-red-700">{error}</p>}
    {message && <p role="status" className="rounded-lg bg-emerald-50 p-3 text-emerald-700">{message}</p>}
    {!tenantId && <p className="rounded-2xl border border-slate-200 bg-white p-6 text-sm text-slate-600 shadow-sm">Sign in with a tenant administrator account to author notifications.</p>}
    {tenantId && <section className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
      <div>
        <p className="text-xs font-bold uppercase tracking-[0.16em] text-slate-400">Composer</p>
        <h2 className="mt-1 flex items-center gap-2 text-xl font-extrabold text-slate-950"><Megaphone className="h-5 w-5" />Compose campaign</h2>
        <p className="mt-1 text-sm text-slate-500">Choose the audience context, then write the message and audit reason.</p>
      </div>
      <div className="mt-4 grid gap-3 md:grid-cols-2">
        <label className="text-sm font-semibold text-slate-700">Type<select value={type} onChange={(event) => setType(event.target.value as typeof type)} className="mt-1 w-full rounded-lg border border-slate-300 bg-white px-3 py-2 outline-none transition focus:border-primary focus:ring-2 focus:ring-primary/10"><option value="FixtureReminder">Fixture reminder</option><option value="Announcement">Announcement</option></select></label>
        <label className="text-sm font-semibold text-slate-700">Competition<select value={competitionId} onChange={(event) => setCompetitionId(event.target.value)} className="mt-1 w-full rounded-lg border border-slate-300 bg-white px-3 py-2 outline-none transition focus:border-primary focus:ring-2 focus:ring-primary/10"><option value="">Tenant-wide</option>{competitions.map((item) => <option key={item.id} value={item.id}>{item.name}</option>)}</select></label>
        <label className="text-sm font-semibold text-slate-700 md:col-span-2">Fixture<select value={contestId} onChange={(event) => setContestId(event.target.value)} required={type === 'FixtureReminder'} className="mt-1 w-full rounded-lg border border-slate-300 bg-white px-3 py-2 outline-none transition focus:border-primary focus:ring-2 focus:ring-primary/10"><option value="">{type === 'FixtureReminder' ? 'Select fixture' : 'No fixture deep link'}</option>{contests.map((item) => <option key={item.id} value={item.id}>{item.name}{item.startTime ? ` · ${new Date(item.startTime).toLocaleString()}` : ''}{item.venue ? ` · ${item.venue}` : ''}</option>)}</select></label>
      </div>
      <form action={(data) => void dispatch(data)} className="mt-3 grid gap-3">
        <label className="text-sm font-semibold text-slate-700">Title<input name="title" required maxLength={160} className="mt-1 w-full rounded-lg border border-slate-300 bg-white px-3 py-2 outline-none transition focus:border-primary focus:ring-2 focus:ring-primary/10" /></label>
        <label className="text-sm font-semibold text-slate-700">Message<textarea name="body" required maxLength={1000} rows={4} className="mt-1 w-full rounded-lg border border-slate-300 bg-white px-3 py-2 outline-none transition focus:border-primary focus:ring-2 focus:ring-primary/10" /></label>
        <label className="text-sm font-semibold text-slate-700">Audit reason<input name="reason" required maxLength={500} className="mt-1 w-full rounded-lg border border-slate-300 bg-white px-3 py-2 outline-none transition focus:border-primary focus:ring-2 focus:ring-primary/10" /></label>
        <button disabled={busy} className="flex items-center justify-center gap-2 rounded-lg bg-primary px-4 py-2 font-bold text-white shadow-sm transition hover:brightness-95 disabled:opacity-50"><Send className="h-4 w-4" />{busy ? 'Dispatching…' : 'Dispatch now'}</button>
      </form>
    </section>}
    <section className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm"><div><p className="text-xs font-bold uppercase tracking-[0.16em] text-slate-400">Delivery log</p><h2 className="mt-1 text-xl font-extrabold text-slate-950">Dispatch history</h2><p className="mt-1 text-sm text-slate-500">Recent authored campaigns and their delivery audience sizes.</p></div><div className="mt-4 space-y-3">{campaigns.length === 0 && <p className="rounded-xl border border-dashed border-slate-200 bg-slate-50 p-6 text-center text-sm text-slate-500">No authored campaigns yet.</p>}{campaigns.map((item) => <article key={item.id} className="rounded-2xl border border-slate-200 bg-white p-4 transition hover:border-slate-300 hover:shadow-sm"><div className="flex flex-wrap justify-between gap-2"><div><p className="font-semibold">{item.title}</p><p className="text-sm text-slate-600">{item.body}</p><p className="mt-1 text-xs text-slate-500">Reason: {item.reason}</p></div><span className="h-fit rounded-full bg-emerald-100 px-3 py-1 text-xs font-semibold text-emerald-700">{item.status}</span></div><p className="mt-2 text-xs text-slate-500">{item.type} · {item.recipientCount} recipients · {new Date(item.createdAt).toLocaleString()}</p></article>)}</div></section>
  </main>;
}

function NotificationMetric({ label, value }: { label: string; value: number }) {
  return <div className="min-w-24 rounded-2xl border border-white/10 bg-white/5 px-4 py-3 backdrop-blur">
    <div className="text-[10px] font-bold uppercase tracking-[0.16em] text-slate-400">{label}</div>
    <div className="mt-1 text-2xl font-black text-white">{value}</div>
  </div>;
}


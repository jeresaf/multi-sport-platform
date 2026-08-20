'use client';

import { useEffect, useMemo, useState } from 'react';
import { Check, CircleAlert, DatabaseZap, RefreshCw, Send, ShieldCheck, Trophy, X } from 'lucide-react';
import { adminApi, getStoredUser } from '@/lib/admin-api';
import { Button } from '@/components/ui/button';
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from '@/components/ui/card';

type Tenant = { id: string; name: string; slug: string };
type Contest = {
  id: string;
  name?: string | null;
  contestShape: string;
  participants: Array<{ participantId: string }>;
};
type Participant = { id: string; name: string };
type Result = {
  id: string;
  participantId: string;
  status: string | number;
  rank?: number | null;
  score?: number | null;
  scoreDisplay?: string | null;
  remarks?: string | null;
};
type ContestSnapshot = {
  version: number;
  sourceHash: string;
  rebuiltAt: string;
  reason: string;
  state: {
    currentScores: unknown[];
    segmentScores: unknown[];
    results: unknown[];
    standings: unknown[];
    brackets: unknown[];
    leaderboards: unknown[];
  };
};

const statusNames: Record<number, string> = {
  0: 'Pending',
  1: 'Draft',
  2: 'Submitted',
  3: 'Approved',
  4: 'Rejected',
  5: 'Published',
  6: 'Official',
  7: 'Under Review',
  8: 'Corrected',
};

function statusName(status: string | number) {
  return typeof status === 'number' ? statusNames[status] ?? String(status) : status;
}

export default function ResultReviewPage() {
  const [tenants, setTenants] = useState<Tenant[]>([]);
  const [tenantId, setTenantId] = useState('');
  const [contests, setContests] = useState<Contest[]>([]);
  const [contestId, setContestId] = useState('');
  const [participants, setParticipants] = useState<Participant[]>([]);
  const [results, setResults] = useState<Result[]>([]);
  const [snapshot, setSnapshot] = useState<ContestSnapshot | null>(null);
  const [busyId, setBusyId] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);

  const user = getStoredUser();
  const canChooseTenant =
    user?.roles.includes('Admin') || user?.roles.includes('Platform Owner');
  const hasPermission = (name: string) => user?.permissions.some((permission) =>
    permission === name || permission.endsWith(`.${name}`)) ?? false;
  const canApprove = Boolean(canChooseTenant || hasPermission('results.approve'));
  const canCorrect = Boolean(canChooseTenant ||
    (hasPermission('scoring.correct') && hasPermission('results.approve')));
  const participantNames = useMemo(
    () => new Map(participants.map((participant) => [participant.id, participant.name])),
    [participants],
  );

  useEffect(() => {
    if (!user) {
      setError('Sign in before reviewing results.');
      return;
    }
    if (canChooseTenant) {
      adminApi<Tenant[]>('/api/tenants')
        .then((items) => {
          setTenants(items);
          setTenantId(user.tenantId ?? items[0]?.id ?? '');
        })
        .catch((reason: Error) => setError(reason.message));
    } else {
      setTenantId(user.tenantId ?? '');
    }
    // The stored user is stable for this mounted route.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  useEffect(() => {
    if (!tenantId) return;
    let cancelled = false;
    setError(null);
    Promise.all([
      adminApi<Contest[]>(`/api/contests?tenantId=${tenantId}`),
      adminApi<Participant[]>(`/api/participants?tenantId=${tenantId}`),
    ])
      .then(([contestItems, participantItems]) => {
        if (cancelled) return;
        setContests(contestItems);
        setParticipants(participantItems);
        setContestId((current) =>
          contestItems.some((contest) => contest.id === current)
            ? current
            : contestItems[0]?.id ?? '',
        );
      })
      .catch((reason: Error) => {
        if (!cancelled) setError(reason.message);
      });
    return () => {
      cancelled = true;
    };
  }, [tenantId]);

  async function loadResults() {
    if (!contestId) {
      setResults([]);
      return;
    }
    try {
      setResults(await adminApi<Result[]>(`/api/results/contest/${contestId}`));
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'Unable to load results.');
    }
  }

  async function loadSnapshot() {
    if (!contestId) {
      setSnapshot(null);
      return;
    }
    try {
      setSnapshot(await adminApi<ContestSnapshot>(
        `/api/livescoring/contests/${contestId}/snapshot`,
      ));
    } catch {
      setSnapshot(null);
    }
  }

  useEffect(() => {
    void loadResults();
    void loadSnapshot();
    // Reload whenever the selected contest changes.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [contestId]);

  async function rebuildSnapshot() {
    if (!contestId) return;
    const reason = window.prompt('Reason for rebuilding the contest snapshot');
    if (!reason?.trim()) return;
    setBusyId(contestId);
    setError(null);
    try {
      const rebuilt = await adminApi<ContestSnapshot>(
        `/api/livescoring/contests/${contestId}/snapshot/rebuild`,
        { method: 'POST', body: JSON.stringify({ reason: reason.trim() }) },
      );
      setSnapshot(rebuilt);
      setNotice(`Derived snapshot v${rebuilt.version} rebuilt and audited.`);
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'Unable to rebuild snapshot.');
    } finally {
      setBusyId('');
    }
  }

  async function action(result: Result, next: 'approve' | 'reject' | 'publish') {
    let body = '{}';
    if (next === 'reject') {
      const reason = window.prompt('Reason for rejection');
      if (!reason?.trim()) return;
      body = JSON.stringify({ reason: reason.trim() });
    }
    setBusyId(result.id);
    setError(null);
    setNotice(null);
    try {
      await adminApi(`/api/results/${result.id}/${next}`, {
        method: 'POST',
        body,
      });
      setNotice(
        next === 'approve'
          ? 'Result approved and standings rebuilt.'
          : next === 'publish'
            ? 'Result published to public clients.'
            : 'Result rejected with its review reason.',
      );
      await Promise.all([loadResults(), loadSnapshot()]);
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : `Unable to ${next} result.`);
    } finally {
      setBusyId('');
    }
  }

  async function correctResult(result: Result) {
    const scoreText = window.prompt('Corrected score', String(result.score ?? ''));
    if (scoreText === null || (scoreText !== '' && !Number.isFinite(Number(scoreText)))) return;
    const rankText = window.prompt('Corrected rank', String(result.rank ?? ''));
    if (rankText === null || (rankText !== '' && !Number.isInteger(Number(rankText)))) return;
    const reason = window.prompt('Reason for result correction');
    if (!reason?.trim()) return;
    setBusyId(result.id);
    setError(null);
    try {
      const score = scoreText === '' ? null : Number(scoreText);
      const rank = rankText === '' ? null : Number(rankText);
      await adminApi(`/api/results/${result.id}/correct`, {
        method: 'POST',
        body: JSON.stringify({
          score,
          scoreDisplay: score == null ? null : String(score),
          rank,
          position: rank == null ? null : String(rank),
          reason: reason.trim(),
        }),
      });
      setNotice('Result corrected, audited, and dependent standings rebuilt.');
      await Promise.all([loadResults(), loadSnapshot()]);
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'Unable to correct result.');
    } finally {
      setBusyId('');
    }
  }

  async function beginReview(result: Result) {
    setBusyId(result.id);
    setError(null);
    try {
      await adminApi(`/api/results/${result.id}/review`, { method: 'POST' });
      setNotice('Result is now under review.');
      await Promise.all([loadResults(), loadSnapshot()]);
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'Unable to start review.');
    } finally {
      setBusyId('');
    }
  }

  async function reopenResult(result: Result) {
    const reason = window.prompt('Reason for reopening this contest result');
    if (!reason?.trim()) return;
    setBusyId(result.id);
    setError(null);
    try {
      await adminApi(`/api/results/${result.id}/reopen`, {
        method: 'POST',
        body: JSON.stringify({ reason: reason.trim() }),
      });
      setNotice('Contest results reopened for correction.');
      await Promise.all([loadResults(), loadSnapshot()]);
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'Unable to reopen result.');
    } finally {
      setBusyId('');
    }
  }

  async function resubmitContest() {
    if (!contestId) return;
    setBusyId(contestId);
    setError(null);
    try {
      await adminApi(`/api/results/contest/${contestId}/submit`, { method: 'POST' });
      setNotice('Corrected contest results resubmitted for review.');
      await Promise.all([loadResults(), loadSnapshot()]);
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'Unable to resubmit results.');
    } finally {
      setBusyId('');
    }
  }

  const statusCounts = useMemo(() => {
    const counts = new Map<string, number>();
    for (const result of results) {
      const name = statusName(result.status);
      counts.set(name, (counts.get(name) ?? 0) + 1);
    }
    return counts;
  }, [results]);

  const submittedCount = (statusCounts.get('Submitted') ?? 0) + (statusCounts.get('Under Review') ?? 0);
  const publishedCount = (statusCounts.get('Published') ?? 0) + (statusCounts.get('Official') ?? 0);
  const issueCount =
    (statusCounts.get('Rejected') ?? 0) +
    (statusCounts.get('Corrected') ?? 0);

  const resultStatusTone = (status: string) => {
    if (['Published', 'Official'].includes(status)) return 'bg-emerald-50 text-emerald-700 ring-emerald-200';
    if (status === 'Approved') return 'bg-blue-50 text-blue-700 ring-blue-200';
    if (['Submitted', 'Under Review'].includes(status)) return 'bg-amber-50 text-amber-700 ring-amber-200';
    if (status === 'Rejected') return 'bg-rose-50 text-rose-700 ring-rose-200';
    if (status === 'Corrected') return 'bg-violet-50 text-violet-700 ring-violet-200';
    return 'bg-gray-100 text-gray-600 ring-gray-200';
  };

  return (
    <div className="space-y-6">
      <section className="overflow-hidden rounded-2xl border border-gray-200 bg-white shadow-sm">
        <div className="border-b border-gray-100 bg-gradient-to-br from-gray-950 via-gray-900 to-gray-800 px-6 py-7 text-white lg:px-8">
          <div className="flex flex-wrap items-end justify-between gap-5">
            <div className="max-w-2xl">
              <p className="text-xs font-bold uppercase tracking-[0.22em] text-white/60">Results operations</p>
              <h1 className="mt-2 text-3xl font-bold tracking-tight sm:text-4xl">Results control centre</h1>
              <p className="mt-2 max-w-xl text-sm leading-6 text-white/70">
                Review submitted outcomes, resolve corrections, rebuild derived data, and publish official results.
              </p>
            </div>

            {canChooseTenant && (
              <label className="min-w-[240px] text-sm font-semibold text-white/80">
                Tenant
                <select
                  aria-label="Tenant"
                  value={tenantId}
                  onChange={(event) => setTenantId(event.target.value)}
                  className="mt-1 h-11 w-full rounded-xl border border-white/15 bg-white/10 px-3 font-normal text-white outline-none"
                >
                  {tenants.map((tenant) => (
                    <option key={tenant.id} value={tenant.id} className="text-gray-900">{tenant.name}</option>
                  ))}
                </select>
              </label>
            )}
          </div>
        </div>

        <div className="grid gap-px bg-gray-200 sm:grid-cols-2 xl:grid-cols-4">
          <ResultMetric label="Results" value={results.length} hint="Selected contest" />
          <ResultMetric label="In review" value={submittedCount} hint="Need action" />
          <ResultMetric label="Published" value={publishedCount} hint="Public / official" />
          <ResultMetric label="Issues" value={issueCount} hint="Rejected / corrected" />
        </div>
      </section>

      {error && <State tone="error">{error}</State>}
      {notice && <State tone="success">{notice}</State>}

      <Card>
        <CardHeader className="border-b border-gray-100 bg-gray-50/70">
          <div className="flex flex-wrap items-center justify-between gap-3">
            <div>
              <p className="text-xs font-semibold uppercase tracking-wider text-gray-400">Selected contest</p>
              <CardTitle className="mt-1 flex items-center gap-2">
                <Trophy className="h-5 w-5 text-primary" />
                Contest results
              </CardTitle>
              <CardDescription>Only tenant-authorized results are available.</CardDescription>
            </div>
            <div className="flex flex-wrap gap-2">
              <select
                aria-label="Contest"
                value={contestId}
                onChange={(event) => setContestId(event.target.value)}
                className="rounded-lg border border-gray-200 bg-white px-3 py-2 text-sm"
              >
                {contests.map((contest) => (
                  <option key={contest.id} value={contest.id}>
                    {contest.name ?? contest.contestShape}
                  </option>
                ))}
              </select>
              <Button variant="outline" onClick={() => void loadResults()} disabled={!contestId}>
                <RefreshCw className="mr-2 h-4 w-4" /> Refresh
              </Button>
              {canApprove && (
                <Button
                  variant="outline"
                  onClick={() => void rebuildSnapshot()}
                  disabled={!contestId || busyId === contestId}
                >
                  <RefreshCw className="mr-2 h-4 w-4" /> Rebuild snapshot
                </Button>
              )}
            </div>
          </div>
        </CardHeader>
        <CardContent>
          {snapshot && (
            <div className="mb-5 overflow-hidden rounded-2xl border border-blue-100 bg-blue-50">
              <div className="flex flex-wrap items-start justify-between gap-3 border-b border-blue-100 px-4 py-3">
                <div className="flex items-center gap-3">
                  <span className="flex h-9 w-9 items-center justify-center rounded-xl bg-white text-blue-700">
                    <DatabaseZap className="h-4 w-4" />
                  </span>
                  <div>
                    <p className="font-semibold text-blue-950">
                      Derived snapshot v{snapshot.version}
                    </p>
                    <p className="text-xs text-blue-600">{snapshot.sourceHash.slice(0, 12)}</p>
                  </div>
                </div>
                <span className="rounded-full bg-white px-2.5 py-1 text-xs font-semibold text-blue-700">
                  {new Date(snapshot.rebuiltAt).toLocaleString()}
                </span>
              </div>

              <div className="grid gap-px bg-blue-100 sm:grid-cols-2 xl:grid-cols-5">
                <SnapshotMetric label="Totals" value={snapshot.state.currentScores.length} />
                <SnapshotMetric label="Segments" value={snapshot.state.segmentScores.length} />
                <SnapshotMetric label="Standings" value={snapshot.state.standings.length} />
                <SnapshotMetric label="Brackets" value={snapshot.state.brackets.length} />
                <SnapshotMetric label="Leaderboards" value={snapshot.state.leaderboards.length} />
              </div>

              <p className="px-4 py-3 text-xs text-blue-700">{snapshot.reason}</p>
            </div>
          )}
          {!contestId && <p className="rounded-xl bg-gray-50 p-4 text-sm text-gray-500">No contests are available.</p>}
          {contestId && results.length === 0 && (
            <p className="rounded-xl bg-gray-50 p-4 text-sm text-gray-500">No submitted results for this contest.</p>
          )}
          <div className="space-y-3">
            {results.map((result) => {
              const status = statusName(result.status);
              return (
                <article
                  key={result.id}
                  className="flex flex-wrap items-center justify-between gap-4 rounded-2xl border border-gray-200 bg-white p-4 transition hover:border-gray-300 hover:shadow-sm"
                >
                  <div>
                    <p className="font-semibold text-gray-900">
                      {participantNames.get(result.participantId) ?? result.participantId}
                    </p>
                    <p className="mt-1 text-sm text-gray-500">
                      Rank {result.rank ?? '—'} · {result.scoreDisplay ?? result.score ?? 'No score'}
                    </p>
                    {result.remarks && (
                      <p className="mt-1 text-sm text-red-600">{result.remarks}</p>
                    )}
                  </div>
                  <div className="flex items-center gap-2">
                    <span className={`rounded-full px-3 py-1 text-xs font-semibold ring-1 ${resultStatusTone(status)}`}>
                      {status}
                    </span>
                    {status === 'Submitted' && canApprove && (
                      <Button
                        size="sm"
                        onClick={() => void beginReview(result)}
                        disabled={busyId === result.id}
                      >
                        Start review
                      </Button>
                    )}
                    {status === 'Under Review' && canApprove && (
                      <>
                        <Button
                          size="sm"
                          onClick={() => void action(result, 'approve')}
                          disabled={busyId === result.id}
                        >
                          <Check className="mr-1 h-4 w-4" /> Approve
                        </Button>
                        <Button
                          variant="destructive"
                          size="sm"
                          onClick={() => void action(result, 'reject')}
                          disabled={busyId === result.id}
                        >
                          <X className="mr-1 h-4 w-4" /> Reject
                        </Button>
                      </>
                    )}
                    {status === 'Approved' && (
                      <Button
                        size="sm"
                        onClick={() => void action(result, 'publish')}
                        disabled={busyId === result.id}
                      >
                        <Send className="mr-1 h-4 w-4" /> Publish
                      </Button>
                    )}
                    {canCorrect && ['Approved', 'Published', 'Official'].includes(status) && (
                      <Button
                        variant="outline"
                        size="sm"
                        onClick={() => void reopenResult(result)}
                        disabled={busyId === result.id}
                      >
                        Reopen
                      </Button>
                    )}
                    {canCorrect && status === 'Corrected' && (
                      <Button
                        variant="outline"
                        size="sm"
                        onClick={() => void correctResult(result)}
                        disabled={busyId === result.id}
                      >
                        Correct result
                      </Button>
                    )}
                    {canApprove && ['Corrected', 'Rejected'].includes(status) && (
                      <Button
                        size="sm"
                        onClick={() => void resubmitContest()}
                        disabled={busyId === contestId}
                      >
                        Resubmit contest
                      </Button>
                    )}
                  </div>
                </article>
              );
            })}
          </div>
        </CardContent>
      </Card>
    </div>
  );
}

function State({
  children,
  tone,
}: {
  children: React.ReactNode;
  tone: 'error' | 'success';
}) {
  return (
    <div
      className={`rounded-lg border p-4 text-sm ${
        tone === 'error'
          ? 'border-red-200 bg-red-50 text-red-700'
          : 'border-green-200 bg-green-50 text-green-700'
      }`}
    >
      {children}
    </div>
  );
}

function ResultMetric({
  label,
  value,
  hint,
}: {
  label: string;
  value: number | string;
  hint: string;
}) {
  return (
    <div className="bg-white px-5 py-4">
      <p className="text-xs font-semibold uppercase tracking-wider text-gray-400">{label}</p>
      <div className="mt-1 flex items-end justify-between gap-3">
        <strong className="truncate text-2xl font-bold tracking-tight text-gray-950">{value}</strong>
        <span className="shrink-0 text-xs text-gray-500">{hint}</span>
      </div>
    </div>
  );
}

function SnapshotMetric({ label, value }: { label: string; value: number }) {
  return (
    <div className="bg-blue-50 px-4 py-3">
      <p className="text-xs font-semibold uppercase tracking-wider text-blue-500">{label}</p>
      <p className="mt-1 text-lg font-bold text-blue-950">{value}</p>
    </div>
  );
}


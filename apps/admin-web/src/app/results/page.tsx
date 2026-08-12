'use client';

import { useEffect, useMemo, useState } from 'react';
import { Check, RefreshCw, Send, X } from 'lucide-react';
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

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-end justify-between gap-4">
        <div>
          <p className="text-sm font-semibold uppercase tracking-wider text-primary">
            Controlled workflow
          </p>
          <h1 className="text-3xl font-bold text-gray-950">Result Review</h1>
          <p className="mt-1 text-gray-500">
            Approve submitted scores, rebuild standings, and publish official output.
          </p>
        </div>
        {canChooseTenant && (
          <label className="text-sm font-medium text-gray-700">
            Tenant
            <select
              aria-label="Tenant"
              value={tenantId}
              onChange={(event) => setTenantId(event.target.value)}
              className="ml-2 rounded-lg border border-gray-200 bg-white px-3 py-2"
            >
              {tenants.map((tenant) => (
                <option key={tenant.id} value={tenant.id}>{tenant.name}</option>
              ))}
            </select>
          </label>
        )}
      </div>

      {error && <State tone="error">{error}</State>}
      {notice && <State tone="success">{notice}</State>}

      <Card>
        <CardHeader>
          <div className="flex flex-wrap items-center justify-between gap-3">
            <div>
              <CardTitle>Contest results</CardTitle>
              <CardDescription>Only tenant-authorized results are available.</CardDescription>
            </div>
            <div className="flex gap-2">
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
            <div className="mb-4 rounded-xl border border-blue-100 bg-blue-50 p-4 text-sm text-blue-950">
              <p className="font-semibold">
                Derived snapshot v{snapshot.version} · {snapshot.sourceHash.slice(0, 12)}
              </p>
              <p className="mt-1 text-blue-700">
                {snapshot.state.currentScores.length} totals ·{' '}
                {snapshot.state.segmentScores.length} segment totals ·{' '}
                {snapshot.state.standings.length} standings ·{' '}
                {snapshot.state.brackets.length} bracket links ·{' '}
                {snapshot.state.leaderboards.length} leaderboards
              </p>
              <p className="mt-1 text-xs text-blue-600">
                {new Date(snapshot.rebuiltAt).toLocaleString()} · {snapshot.reason}
              </p>
            </div>
          )}
          {!contestId && <p className="text-sm text-gray-500">No contests are available.</p>}
          {contestId && results.length === 0 && (
            <p className="text-sm text-gray-500">No submitted results for this contest.</p>
          )}
          <div className="space-y-3">
            {results.map((result) => {
              const status = statusName(result.status);
              return (
                <article
                  key={result.id}
                  className="flex flex-wrap items-center justify-between gap-4 rounded-xl border border-gray-200 p-4"
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
                    <span className="rounded-full bg-gray-100 px-3 py-1 text-xs font-semibold">
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

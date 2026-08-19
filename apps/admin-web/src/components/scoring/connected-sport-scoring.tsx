'use client';

import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { Pause, Play, RefreshCw, Square } from 'lucide-react';
import { HubConnectionBuilder } from '@microsoft/signalr';
import {
  ADMIN_API_URL,
  adminApi,
  getAccessToken,
  getStoredUser,
} from '@/lib/admin-api';
import { Button } from '@/components/ui/button';
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from '@/components/ui/card';

export type ScoreAction = {
  code: string;
  label: string;
  metric: string;
  defaultValue: number;
  inputType: 'fixed' | 'number';
  minimum?: number | null;
  maximum?: number | null;
  step?: number | null;
  unit?: string | null;
  tone?: 'primary' | 'secondary' | 'danger';
  requiresPlayer?: boolean;
  eventType?: string | null;
};

export type SportScoringConfig = {
  name: string;
  icon: string;
  sportId?: string;
};

type ScoringSchema = {
  sportCode: string;
  name: string;
  primaryMetric: string;
  scoreLabel: string;
  lowerIsBetter: boolean;
  supportsClock: boolean;
  clockCountsDown: boolean;
  actions: ScoreAction[];
  eventDefinitions: EventDefinition[];
  segments: Array<{
    id: string;
    name: string;
    type: string;
    sortOrder: number;
    status: string;
    parentSegmentId?: string | null;
    depth: number;
    isOptional: boolean;
    activationCondition?: string | null;
  }>;
};

type EventDefinition = {
  code: string;
  name: string;
  segmentApplicability: string[];
  requiresParticipant: boolean;
  requiresSecondaryParticipant: boolean;
  requiredMetric?: string | null;
  timingRequirement: 'None' | 'Timestamp' | 'Clock' | 'TimestampOrClock';
  correctionPolicy: 'Prohibited' | 'ApprovalRequired';
  validation: {
    minimumValue?: number | null;
    maximumValue?: number | null;
    minimumClockSeconds?: number | null;
    maximumClockSeconds?: number | null;
  };
};

type Tenant = { id: string; name: string };
type Participant = { id: string; name: string; shortName?: string | null };
type ContestParticipant = {
  id: string;
  participantId: string;
  participantType: string;
  sortOrder: number;
};
type Contest = {
  id: string;
  tenantId: string;
  sportId: string;
  name?: string | null;
  contestShape: string;
  status: string | number;
  participants: ContestParticipant[];
};
type LiveScore = {
  id: string;
  participantId: string;
  secondaryParticipantId?: string | null;
  metricName: string;
  value: number;
  sequence: number;
  createdAt: string;
  segmentId?: string | null;
  clockSeconds?: number | null;
};
type RosterPlayer = {
  athleteId: string;
  name: string;
  shortName?: string | null;
  jerseyNumber?: string | null;
  position?: string | null;
};
type ContestRoster = { teamId: string; players: RosterPlayer[] };
type GolfScorecardReview = {
  id: string;
  participantId: string;
  roundNumber: number;
  holesJson: string;
  status: number | 'Pending' | 'Approved' | 'Rejected';
  submittedAt: string;
  reviewedAt?: string | null;
  reviewNotes?: string | null;
};
type ScoreCorrection = {
  id: string;
  liveScoreId: string;
  kind: number | string;
  reason: string;
  status: 'Pending' | 'Approved' | 'Rejected';
  requestedBy?: string | null;
  requestedAt: string;
  reviewedBy?: string | null;
  reviewedAt?: string | null;
  reviewReason?: string | null;
  oldValue?: string | null;
  newValue?: string | null;
};
type ScoringSession = {
  id: string;
  name: string;
  status: number;
  activeSegmentId?: string | null;
  currentClockSeconds?: number;
  clockCountsDown?: boolean;
  isClockRunning?: boolean;
};
type LifecycleState = {
  contestId: string;
  status: number;
  canonicalStatus: number;
  canonicalStatusCode: string;
  statusReason?: string | null;
  lastChangedAt: string;
  allowedTransitions: number[];
  usesSportPolicy: boolean;
};
type LifecycleHistory = {
  id: string;
  userId?: string | null;
  originalValue?: string | null;
  newValue?: string | null;
  reason?: string | null;
  timestamp: string;
};
type CheckInParticipantState = {
  participantId: string;
  participantType: string;
  status: number;
};
type CheckInOfficialState = {
  officialId: string;
  role: string;
  displayName?: string | null;
  isConfirmed: boolean;
};
type ReadinessState = {
  contestId: string;
  contestStatus: number;
  isOpen: boolean;
  totalParticipants: number;
  checkedIn: number;
  withdrawn: number;
  disqualified: number;
  pending: number;
  participantCheckInPassed: boolean;
  rosterConfirmationPassed: boolean;
  officialConfirmationPassed: boolean;
  venueConfirmationPassed: boolean;
  canStart: boolean;
  isOverridden: boolean;
  overrideReason?: string | null;
  blockingChecks: string[];
  participants: CheckInParticipantState[];
  officials: CheckInOfficialState[];
};
type CheckInHistory = {
  id: string;
  action: string;
  subjectType: string;
  reason?: string | null;
  isOverride: boolean;
  occurredAt: string;
};

const sessionLabels: Record<number, string> = {
  0: 'Pending',
  1: 'Active',
  2: 'Paused',
  3: 'Ended',
  4: 'Cancelled',
};
const lifecycleLabels = [
  'Draft', 'Scheduled', 'Check-in open', 'Ready', 'In progress', 'Paused',
  'Suspended', 'Completed', 'Pending approval', 'Approved', 'Corrected',
  'Published', 'Cancelled', 'Abandoned',
];
const reasonedLifecycleStatuses = new Set([6, 10, 12, 13]);
const participantStatusLabels = ['Entered', 'Checked in', 'Withdrawn', 'Disqualified'];
const formatCanonicalStatus = (value: string) => value
  .toLowerCase()
  .split('_')
  .map(part => part ? `${part[0].toUpperCase()}${part.slice(1)}` : part)
  .join(' ');

export function ConnectedSportScoring({ config }: { config: SportScoringConfig }) {
  const [tenants, setTenants] = useState<Tenant[]>([]);
  const [tenantId, setTenantId] = useState('');
  const [contests, setContests] = useState<Contest[]>([]);
  const [participants, setParticipants] = useState<Participant[]>([]);
  const [contestId, setContestId] = useState('');
  const [scores, setScores] = useState<LiveScore[]>([]);
  const [rosters, setRosters] = useState<ContestRoster[]>([]);
  const [selectedPlayers, setSelectedPlayers] = useState<Record<string, string>>({});
  const [selectedIncomingPlayers, setSelectedIncomingPlayers] = useState<Record<string, string>>({});
  const [golfScorecards, setGolfScorecards] = useState<GolfScorecardReview[]>([]);
  const [golfScorecardParticipantId, setGolfScorecardParticipantId] = useState('');
  const [golfScorecardRoundId, setGolfScorecardRoundId] = useState('');
  const [golfHoleValues, setGolfHoleValues] = useState<Record<string, number>>({});
  const [corrections, setCorrections] = useState<ScoreCorrection[]>([]);
  const [schema, setSchema] = useState<ScoringSchema | null>(null);
  const [segmentId, setSegmentId] = useState('');
  const [clockSeconds, setClockSeconds] = useState(0);
  const [actionValues, setActionValues] = useState<Record<string, number>>({});
  const [session, setSession] = useState<ScoringSession | null>(null);
  const [lifecycle, setLifecycle] = useState<LifecycleState | null>(null);
  const [lifecycleHistory, setLifecycleHistory] = useState<LifecycleHistory[]>([]);
  const [readiness, setReadiness] = useState<ReadinessState | null>(null);
  const [checkInHistory, setCheckInHistory] = useState<CheckInHistory[]>([]);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const refreshRequestId = useRef(0);
  const [liveStatus, setLiveStatus] = useState<
    'connecting' | 'live' | 'reconnecting' | 'offline'
  >('offline');

  const user = getStoredUser();
  const canChooseTenant =
    user?.roles.includes('Admin') || user?.roles.includes('Platform Owner');
  const hasPermission = (permission: string) =>
    user?.permissions.some((item) =>
      item === permission || item.endsWith(`.${permission}`)) ?? false;
  const canCorrect = Boolean(canChooseTenant || hasPermission('scoring.correct'));
  const canApproveCorrections = Boolean(
    canChooseTenant || hasPermission('results.approve'));
  const activeContest = contests.find((contest) => contest.id === contestId);
  const participantNames = useMemo(
    () => new Map(participants.map((participant) => [participant.id, participant.name])),
    [participants],
  );
  const golfRounds = useMemo(
    () => schema?.sportCode === 'golf'
      ? schema.segments.filter((segment) => segment.type === 'Round')
      : [],
    [schema],
  );

  useEffect(() => {
    const storedUser = getStoredUser();
    if (!storedUser) {
      setError('Sign in before opening a scoring console.');
      setLoading(false);
      return;
    }

    if (storedUser.roles.includes('Admin') ||
        storedUser.roles.includes('Platform Owner')) {
      adminApi<Tenant[]>('/api/tenants')
        .then((items) => {
          setTenants(items);
          setTenantId(storedUser.tenantId ?? items[0]?.id ?? '');
        })
        .catch((reason: Error) => setError(reason.message))
        .finally(() => setLoading(false));
    } else {
      setTenantId(storedUser.tenantId ?? '');
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    if (!tenantId) return;
    let disposed = false;
    setError(null);
    Promise.all([
      adminApi<Contest[]>(`/api/contests?tenantId=${tenantId}`),
      adminApi<Participant[]>(`/api/participants?tenantId=${tenantId}`),
    ])
      .then(([allContests, allParticipants]) => {
        if (disposed) return;
        const sportContests = config.sportId
          ? allContests.filter(
              (contest) =>
                contest.sportId.toLowerCase() === config.sportId?.toLowerCase(),
            )
          : allContests;
        setContests(sportContests);
        setParticipants(allParticipants);
        setContestId((current) =>
          sportContests.some((contest) => contest.id === current)
            ? current
            : sportContests[0]?.id ?? '',
        );
      })
      .catch((reason: Error) => {
        if (!disposed) setError(reason.message);
      });
    return () => {
      disposed = true;
    };
  }, [config.sportId, tenantId]);

  const refresh = useCallback(async () => {
    const requestId = ++refreshRequestId.current;
    if (!contestId) {
      setScores([]);
      setCorrections([]);
      setRosters([]);
      setGolfScorecards([]);
      setSelectedPlayers({});
      setSelectedIncomingPlayers({});
      setSession(null);
      setSchema(null);
      return;
    }

    try {
      const [scoreData, schemaData, rosterData, scorecardData] = await Promise.all([
        adminApi<LiveScore[]>(`/api/livescoring/contests/${contestId}/scores`),
        adminApi<ScoringSchema>(`/api/livescoring/contests/${contestId}/schema`),
        adminApi<ContestRoster[]>(`/api/livescoring/contests/${contestId}/rosters`),
        adminApi<GolfScorecardReview[]>(
          `/api/livescoring/contests/${contestId}/golf-scorecards`,
        ),
      ]);
      if (requestId !== refreshRequestId.current) return;
      setScores(scoreData);
      setSchema(schemaData);
      setRosters(rosterData);
      setGolfScorecards(scorecardData);
      const defaultSegment = schemaData.segments.find(
        (candidate) =>
          !schemaData.segments.some(
            (segment) => segment.parentSegmentId === candidate.id,
          ),
      );
      setSegmentId((current) =>
        schemaData.segments.some((segment) => segment.id === current)
          ? current
          : defaultSegment?.id ?? '',
      );
      try {
        const activeSession = await adminApi<ScoringSession>(
            `/api/livescoring/contests/${contestId}/session`,
          );
        setSession(activeSession);
        if (activeSession.activeSegmentId) {
          setSegmentId(activeSession.activeSegmentId);
        }
        setClockSeconds(activeSession.currentClockSeconds ?? 0);
      } catch {
        setSession(null);
      }
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'Unable to load scoring data.');
    }
  }, [contestId]);

  useEffect(() => {
    if (!activeContest) {
      setGolfScorecardParticipantId('');
      return;
    }
    setGolfScorecardParticipantId((current) =>
      activeContest.participants.some((item) => item.participantId === current)
        ? current
        : activeContest.participants[0]?.participantId ?? '',
    );
  }, [activeContest]);

  useEffect(() => {
    setGolfScorecardRoundId((current) =>
      golfRounds.some((round) => round.id === current)
        ? current
        : golfRounds[0]?.id ?? '',
    );
  }, [golfRounds]);

  const refreshCorrections = useCallback(async () => {
    if (!contestId || !canCorrect) {
      setCorrections([]);
      return;
    }
    try {
      setCorrections(await adminApi<ScoreCorrection[]>(
        `/api/livescoring/contests/${contestId}/corrections`));
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'Unable to load correction audit.');
    }
  }, [canCorrect, contestId]);

  useEffect(() => {
    void refreshCorrections();
  }, [refreshCorrections]);

  useEffect(() => {
    void refresh();
  }, [refresh]);

  const refreshLifecycle = useCallback(async () => {
    if (!contestId) {
      setLifecycle(null);
      setLifecycleHistory([]);
      return;
    }
    try {
      const [state, history] = await Promise.all([
        adminApi<LifecycleState>(`/api/contests/${contestId}/lifecycle`),
        adminApi<LifecycleHistory[]>(`/api/contests/${contestId}/lifecycle/history`),
      ]);
      setLifecycle(state);
      setLifecycleHistory(history);
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'Unable to load contest lifecycle.');
    }
  }, [contestId]);

  useEffect(() => {
    void refreshLifecycle();
  }, [refreshLifecycle]);

  const refreshReadiness = useCallback(async () => {
    if (!contestId) {
      setReadiness(null);
      setCheckInHistory([]);
      return;
    }
    try {
      const [state, history] = await Promise.all([
        adminApi<ReadinessState>(`/api/checkin/contests/${contestId}/status`),
        adminApi<CheckInHistory[]>(`/api/checkin/contests/${contestId}/history`),
      ]);
      setReadiness(state);
      setCheckInHistory(history);
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'Unable to load check-in readiness.');
    }
  }, [contestId]);

  useEffect(() => {
    void refreshReadiness();
  }, [refreshReadiness]);

  useEffect(() => {
    const token = getAccessToken();
    if (!contestId || !token) {
      setLiveStatus('offline');
      return;
    }

    let disposed = false;
    const connection = new HubConnectionBuilder()
      .withUrl(`${ADMIN_API_URL}/hubs/contest`, {
        accessTokenFactory: () => token,
        withCredentials: false,
      })
      .withAutomaticReconnect()
      .build();

    const refreshContest = () => {
      void refresh();
      void refreshCorrections();
    };
    connection.on('ContestSnapshot', refreshContest);
    connection.on('LiveScoreUpdated', refreshContest);
    connection.on('ContestStatusChanged', () => {
      void refresh();
      void refreshLifecycle();
    });
    connection.onreconnecting(() => setLiveStatus('reconnecting'));
    connection.onreconnected(async () => {
      setLiveStatus('live');
      await connection.invoke('JoinContestGroup', contestId);
    });
    connection.onclose(() => setLiveStatus('offline'));

    setLiveStatus('connecting');
    void connection.start()
      .then(async () => {
        if (disposed) return;
        await connection.invoke('JoinContestGroup', contestId);
        if (!disposed) setLiveStatus('live');
      })
      .catch(() => {
        if (!disposed) setLiveStatus('offline');
      });

    return () => {
      disposed = true;
      connection.off('ContestSnapshot', refreshContest);
      connection.off('LiveScoreUpdated', refreshContest);
      void connection.stop();
    };
  }, [contestId, refresh, refreshCorrections, refreshLifecycle]);

  async function checkInAction(path: string, noticeText: string, reason?: string) {
    if (!activeContest) return;
    setBusy(true);
    setError(null);
    try {
      await adminApi(`/api/checkin/contests/${activeContest.id}/${path}`, {
        method: 'POST',
        body: reason === undefined ? undefined : JSON.stringify({ reason }),
      });
      setNotice(noticeText);
      await Promise.all([refreshReadiness(), refreshLifecycle()]);
    } catch (failure) {
      setError(failure instanceof Error ? failure.message : 'Check-in action failed.');
    } finally {
      setBusy(false);
    }
  }

  function overrideReadiness() {
    const reason = window.prompt('Reason for overriding incomplete readiness checks');
    if (!reason?.trim()) return;
    void checkInAction('override', 'Readiness override recorded.', reason.trim());
  }

  async function transitionContest(target: number) {
    if (!activeContest) return;
    const reason = reasonedLifecycleStatuses.has(target)
      ? window.prompt(`Reason for ${lifecycleLabels[target].toLowerCase()}`)
      : null;
    if (reasonedLifecycleStatuses.has(target) && !reason?.trim()) return;
    setBusy(true);
    setError(null);
    try {
      await adminApi(`/api/contests/${activeContest.id}/transition`, {
        method: 'POST',
        body: JSON.stringify({
          targetStatus: lifecycleLabels[target].replaceAll('-', '').replaceAll(' ', ''),
          reason: reason?.trim() || null,
        }),
      });
      setContests((current) => current.map((contest) =>
        contest.id === activeContest.id ? { ...contest, status: target } : contest));
      setNotice(`Contest moved to ${lifecycleLabels[target]}.`);
      await refreshLifecycle();
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'Lifecycle transition failed.');
    } finally {
      setBusy(false);
    }
  }

  const metricTotals = useMemo(() => {
    const totals = new Map<string, Map<string, number>>();
    for (const score of scores) {
      const participantTotals =
        totals.get(score.participantId) ?? new Map<string, number>();
      participantTotals.set(
        score.metricName,
        (participantTotals.get(score.metricName) ?? 0) + Number(score.value),
      );
      totals.set(score.participantId, participantTotals);
    }
    return totals;
  }, [scores]);

  async function submit(participantId: string, action: ScoreAction) {
    if (!activeContest) return;
    const valueKey = `${participantId}:${action.code}`;
    const value = action.inputType === 'number'
      ? actionValues[valueKey] ?? action.defaultValue
      : action.defaultValue;
    setBusy(true);
    setError(null);
    try {
      const eventId = crypto.randomUUID();
      const eventType = action.eventType ?? (schema?.sportCode === 'football'
        ? action.code.toUpperCase().replaceAll('-', '_')
        : undefined);
      const primaryPlayerId = selectedPlayers[participantId] || null;
      const secondaryPlayerId = eventType === 'SUBSTITUTION'
        ? selectedIncomingPlayers[participantId] || null
        : null;
      await adminApi(`/api/livescoring/contests/${activeContest.id}/events`, {
        method: 'POST',
        headers: { 'X-Tenant-Id': activeContest.tenantId },
        body: JSON.stringify({
          eventId,
          participantId,
          secondaryParticipantId: primaryPlayerId,
          primaryPlayerId,
          secondaryPlayerId,
          eventType,
          metricName: action.metric,
          value,
          scoredBy: user?.email ?? 'admin-web',
          segmentId: session ? null : segmentId || null,
          clockSeconds: session ? null : schema?.supportsClock ? clockSeconds : null,
          deviceTimestamp: new Date().toISOString(),
          idempotencyKey: eventId,
        }),
      });
      await refresh();
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'Unable to submit event.');
    } finally {
      setBusy(false);
    }
  }

  async function updateContext(nextSegmentId: string, nextClock: number) {
    if (!activeContest || !session) return;
    setBusy(true);
    setError(null);
    try {
      await adminApi(
        `/api/livescoring/contests/${activeContest.id}/session/context`,
        {
          method: 'PUT',
          headers: { 'X-Tenant-Id': activeContest.tenantId },
          body: JSON.stringify({
            segmentId: nextSegmentId,
            clockSeconds: nextClock,
          }),
        },
      );
      await refresh();
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'Unable to update scoring context.');
    } finally {
      setBusy(false);
    }
  }

  async function clockAction(action: 'start' | 'pause') {
    if (!activeContest || !session) return;
    setBusy(true);
    setError(null);
    try {
      await adminApi(
        `/api/livescoring/contests/${activeContest.id}/session/clock/${action}`,
        {
          method: 'POST',
          headers: { 'X-Tenant-Id': activeContest.tenantId },
        },
      );
      await refresh();
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'Clock action failed.');
    } finally {
      setBusy(false);
    }
  }

  async function advanceSegment() {
    if (!activeContest || !session) return;
    setBusy(true);
    setError(null);
    try {
      await adminApi(
        `/api/livescoring/contests/${activeContest.id}/session/segment/next`,
        {
          method: 'POST',
          headers: { 'X-Tenant-Id': activeContest.tenantId },
        },
      );
      await refresh();
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'Unable to advance the segment.');
    } finally {
      setBusy(false);
    }
  }

  useEffect(() => {
    if (!session?.isClockRunning || !schema?.supportsClock) return;
    const timer = window.setInterval(() => {
      setClockSeconds((current) =>
        session.clockCountsDown ? Math.max(0, current - 1) : current + 1,
      );
    }, 1000);
    return () => window.clearInterval(timer);
  }, [schema?.supportsClock, session?.clockCountsDown, session?.isClockRunning]);

  async function correct(score: LiveScore) {
    if (!activeContest || !canCorrect) return;
    const valueText = window.prompt('Corrected value', String(score.value));
    if (valueText === null) return;
    const newValue = Number(valueText);
    if (!Number.isFinite(newValue)) {
      setError('Enter a valid corrected value.');
      return;
    }
    const reason = window.prompt('Reason for correction');
    if (!reason?.trim()) return;
    setBusy(true);
    try {
      await adminApi(
        `/api/livescoring/contests/${activeContest.id}/events/${score.id}/corrections`,
        {
          method: 'POST',
          headers: { 'X-Tenant-Id': activeContest.tenantId },
          body: JSON.stringify({
            kind: 'Correct',
            value: newValue,
            reason: reason.trim(),
          }),
        },
      );
      setNotice('Correction submitted for approval.');
      await refreshCorrections();
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'Correction failed.');
    } finally {
      setBusy(false);
    }
  }

  async function reverse(score: LiveScore) {
    if (!activeContest || !canCorrect) return;
    const reason = window.prompt('Reason for reversal');
    if (!reason?.trim()) return;
    setBusy(true);
    try {
      await adminApi(
        `/api/livescoring/contests/${activeContest.id}/events/${score.id}/corrections`,
        {
          method: 'POST',
          headers: { 'X-Tenant-Id': activeContest.tenantId },
          body: JSON.stringify({
            kind: 'Reverse',
            reason: reason.trim(),
          }),
        },
      );
      setNotice('Reversal submitted for approval.');
      await refreshCorrections();
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'Reversal failed.');
    } finally {
      setBusy(false);
    }
  }

  async function replace(score: LiveScore) {
    if (!activeContest || !canCorrect) return;
    const metricName = window.prompt('Replacement metric', score.metricName);
    if (!metricName?.trim()) return;
    const valueText = window.prompt('Replacement value', String(score.value));
    if (valueText === null || !Number.isFinite(Number(valueText))) return;
    const reason = window.prompt('Reason for replacement');
    if (!reason?.trim()) return;
    setBusy(true);
    try {
      await adminApi(
        `/api/livescoring/contests/${activeContest.id}/events/${score.id}/corrections`,
        {
          method: 'POST',
          headers: { 'X-Tenant-Id': activeContest.tenantId },
          body: JSON.stringify({
            kind: 'Replace',
            reason: reason.trim(),
            value: Number(valueText),
            metricName: metricName.trim(),
            eventType: metricName.trim(),
            participantId: score.participantId,
            secondaryParticipantId: score.secondaryParticipantId,
            segmentId: score.segmentId,
            clockSeconds: score.clockSeconds,
          }),
        },
      );
      setNotice('Replacement submitted for approval.');
      await refreshCorrections();
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'Replacement failed.');
    } finally {
      setBusy(false);
    }
  }

  async function reviewCorrection(correction: ScoreCorrection, approve: boolean) {
    if (!activeContest) return;
    const reason = window.prompt(
      approve ? 'Approval note (optional)' : 'Reason for rejection');
    if (!approve && !reason?.trim()) return;
    setBusy(true);
    try {
      await adminApi(
        `/api/livescoring/contests/${activeContest.id}/corrections/${correction.id}/${approve ? 'approve' : 'reject'}`,
        {
          method: 'POST',
          body: JSON.stringify({ reason: reason?.trim() || null }),
        },
      );
      setNotice(approve ? 'Correction approved.' : 'Correction rejected.');
      await Promise.all([refresh(), refreshCorrections()]);
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'Correction review failed.');
    } finally {
      setBusy(false);
    }
  }

  async function sessionAction(action: 'start' | 'pause' | 'resume' | 'end') {
    if (!activeContest) return;
    setBusy(true);
    setError(null);
    try {
      await adminApi(
        `/api/livescoring/contests/${activeContest.id}/session/${action}`,
        {
          method: 'POST',
          headers: { 'X-Tenant-Id': activeContest.tenantId },
          body:
            action === 'start'
              ? JSON.stringify({
                  eventId: crypto.randomUUID(),
                  name: `${config.name} scoring`,
                })
              : undefined,
        },
      );
      await refresh();
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'Session action failed.');
    } finally {
      setBusy(false);
    }
  }

  async function submitResults() {
    if (!activeContest) return;
    if (!window.confirm('Submit the current contest scores for approval?')) return;
    setBusy(true);
    setError(null);
    setNotice(null);
    try {
      await adminApi(`/api/results/contest/${activeContest.id}/submit`, {
        method: 'POST',
        headers: { 'X-Tenant-Id': activeContest.tenantId },
      });
      setNotice('Results submitted for approval.');
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'Result submission failed.');
    } finally {
      setBusy(false);
    }
  }

  async function submitGolfScorecard() {
    if (!activeContest || !golfScorecardParticipantId || !golfScorecardRoundId) return;
    const round = golfRounds.find((item) => item.id === golfScorecardRoundId);
    if (!round) return;
    const holes = schema?.segments
      .filter((item) => item.parentSegmentId === round.id)
      .sort((left, right) => left.sortOrder - right.sortOrder) ?? [];
    if (holes.length !== 18) {
      setError('The selected round does not contain 18 holes.');
      return;
    }
    setBusy(true);
    setError(null);
    setNotice(null);
    try {
      await adminApi(`/api/livescoring/contests/${activeContest.id}/golf-scorecards`, {
        method: 'POST',
        headers: { 'X-Tenant-Id': activeContest.tenantId },
        body: JSON.stringify({
          participantId: golfScorecardParticipantId,
          roundNumber: round.sortOrder,
          holes: holes.map((hole) => ({
            holeNumber: hole.sortOrder,
            strokes: golfHoleValues[`${round.id}:${hole.id}:strokes`]
              ?? parFromHoleName(hole.name),
            penalties: golfHoleValues[`${round.id}:${hole.id}:penalties`] ?? 0,
          })),
        }),
      });
      setNotice('Scorecard submitted and is pending validation.');
      await refresh();
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'Scorecard submission failed.');
    } finally {
      setBusy(false);
    }
  }

  async function reviewGolfScorecard(
    scorecard: GolfScorecardReview,
    approve: boolean,
  ) {
    if (!activeContest) return;
    const notes = window.prompt(
      approve ? 'Approval notes (optional)' : 'Reason for rejection',
    );
    if (notes === null || (!approve && !notes.trim())) return;
    setBusy(true);
    setError(null);
    try {
      await adminApi(
        `/api/livescoring/contests/${activeContest.id}/golf-scorecards/${scorecard.id}/${approve ? 'approve' : 'reject'}`,
        {
          method: 'POST',
          headers: { 'X-Tenant-Id': activeContest.tenantId },
          body: JSON.stringify({ notes: notes.trim() || null }),
        },
      );
      setNotice(approve ? 'Scorecard approved and leaderboard updated.' : 'Scorecard rejected.');
      await refresh();
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'Scorecard review failed.');
    } finally {
      setBusy(false);
    }
  }

  if (loading) return <p className="text-gray-500">Loading {config.name} scoring…</p>;

  return (
    <div
      className="space-y-6"
      data-testid="dynamic-scoring-console"
      data-configured-sport={config.name}
    >
      <div className="flex flex-wrap items-end justify-between gap-4">
        <div>
          <p className="text-4xl" aria-hidden="true">{config.icon}</p>
          <h1 className="mt-2 text-3xl font-bold text-gray-900">
            {config.name} Scoring
          </h1>
          <p className="mt-1 text-gray-500">
            Connected to tenant contests, live-scoring persistence, and SignalR broadcasts.
          </p>
        </div>
        {canChooseTenant && (
          <label className="text-sm font-medium text-gray-700">
            Tenant
            <select
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

      {error && (
        <div className="rounded-lg border border-red-200 bg-red-50 p-4 text-sm text-red-700">
          {error}
        </div>
      )}
      {notice && (
        <div className="rounded-lg border border-green-200 bg-green-50 p-4 text-sm text-green-700">
          {notice}
        </div>
      )}

      <div className="grid gap-6 lg:grid-cols-[320px_1fr]">
        <Card>
          <CardHeader>
            <CardTitle>{config.name} contests</CardTitle>
            <CardDescription>Only contests for this sport and tenant are shown.</CardDescription>
          </CardHeader>
          <CardContent className="space-y-2">
            {contests.length === 0 && (
              <p className="text-sm text-gray-500">
                No {config.name.toLowerCase()} contests are available for this tenant.
              </p>
            )}
            {contests.map((contest) => (
              <button
                key={contest.id}
                data-contest-id={contest.id}
                onClick={() => setContestId(contest.id)}
                className={`w-full rounded-lg border p-3 text-left ${
                  contest.id === contestId
                    ? 'border-primary bg-blue-50'
                    : 'border-gray-200 hover:bg-gray-50'
                }`}
              >
                <p className="font-medium text-gray-900">
                  {contest.name ?? contest.contestShape}
                </p>
                <p className="mt-1 text-xs text-gray-500">
                  {contest.participants.length} participants
                </p>
              </button>
            ))}
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <div className="flex flex-wrap items-center justify-between gap-3">
              <div>
                <CardTitle>
                  {activeContest?.name ?? activeContest?.contestShape ?? 'Select a contest'}
                </CardTitle>
                <CardDescription>
                  Session: {session ? sessionLabels[session.status] ?? session.status : 'Not started'}
                  {' · '}
                  <span data-testid="contest-live-status">
                    {liveStatus === 'live'
                      ? 'Live'
                      : liveStatus === 'connecting'
                        ? 'Connecting'
                        : liveStatus === 'reconnecting'
                          ? 'Reconnecting'
                          : 'Offline'}
                  </span>
                </CardDescription>
              </div>
              <div className="flex flex-wrap gap-2">
                <Button variant="outline" size="sm" onClick={refresh} disabled={!activeContest || busy}>
                  <RefreshCw className="mr-1 h-4 w-4" /> Refresh
                </Button>
                <Button
                  variant="outline"
                  size="sm"
                  onClick={submitResults}
                  disabled={!activeContest || scores.length === 0 || busy}
                >
                  Submit results
                </Button>
                {!session && (
                  <Button size="sm" onClick={() => sessionAction('start')} disabled={!activeContest || busy}>
                    <Play className="mr-1 h-4 w-4" /> Start
                  </Button>
                )}
                {session?.status === 1 && (
                  <Button variant="outline" size="sm" onClick={() => sessionAction('pause')} disabled={busy}>
                    <Pause className="mr-1 h-4 w-4" /> Pause
                  </Button>
                )}
                {session?.status === 2 && (
                  <Button size="sm" onClick={() => sessionAction('resume')} disabled={busy}>
                    <Play className="mr-1 h-4 w-4" /> Resume
                  </Button>
                )}
                {session && session.status !== 3 && (
                  <Button variant="destructive" size="sm" onClick={() => sessionAction('end')} disabled={busy}>
                    <Square className="mr-1 h-4 w-4" /> End
                  </Button>
                )}
              </div>
            </div>
          </CardHeader>
          <CardContent>
            {activeContest && schema && (
              <div
                className="mb-5 rounded-lg border border-blue-100 bg-blue-50/60 p-3"
                data-testid="scoring-schema-source"
                data-sport-code={schema.sportCode}
                data-action-count={schema.actions.length}
              >
                <p className="text-sm font-semibold text-gray-900">
                  {schema.name} sport configuration
                </p>
                <p className="mt-1 text-xs text-gray-600">
                  {schema.actions.length} scoring controls · {schema.segments.length} contest segments
                  {' · '}{schema.eventDefinitions?.length ?? 0} event definitions
                  {' · '}primary metric: {schema.primaryMetric}
                </p>
                <p
                  className={`mt-1 text-xs ${canCorrect ? 'text-emerald-700' : 'text-gray-500'}`}
                  data-testid="scoring-correction-access"
                >
                  Corrections: {canCorrect ? 'permitted' : 'not permitted'}
                </p>
              </div>
            )}
            {activeContest && lifecycle && (
              <div className="mb-5 rounded-lg border border-gray-200 p-3">
                <div className="flex flex-wrap items-center justify-between gap-3">
                  <div>
                    <p className="flex flex-wrap items-center gap-2 text-sm font-semibold text-gray-900">
                      <span>Lifecycle:</span>
                      <span className="rounded-full bg-blue-50 px-2 py-1 text-xs text-blue-800">
                        {formatCanonicalStatus(lifecycle.canonicalStatusCode)}
                      </span>
                    </p>
                    <p className="text-xs text-gray-500">
                      {lifecycle.usesSportPolicy ? 'Tenant sport policy' : 'Platform transition policy'}
                      {' · '}internal workflow: {lifecycleLabels[lifecycle.status] ?? lifecycle.status}
                    </p>
                    {lifecycle.statusReason && <p className="mt-1 text-xs text-amber-700">Reason: {lifecycle.statusReason}</p>}
                  </div>
                  <div className="flex flex-wrap gap-2">
                    {lifecycle.allowedTransitions.map((target) => (
                      <Button
                        key={target}
                        variant="outline"
                        size="sm"
                        disabled={busy}
                        onClick={() => void transitionContest(target)}
                      >
                        {lifecycleLabels[target] ?? target}
                      </Button>
                    ))}
                  </div>
                </div>
                {lifecycleHistory.length > 0 && (
                  <details className="mt-3 text-xs text-gray-600">
                    <summary className="cursor-pointer font-semibold">Transition history ({lifecycleHistory.length})</summary>
                    <ol className="mt-2 space-y-1">
                      {lifecycleHistory.slice().reverse().map((item) => (
                        <li key={item.id}>
                          {new Date(item.timestamp).toLocaleString()}
                          {item.reason ? ` — ${item.reason}` : ''}
                        </li>
                      ))}
                    </ol>
                  </details>
                )}
              </div>
            )}
            {activeContest && readiness && (
              <div className="mb-5 rounded-lg border border-gray-200 p-3">
                <div className="flex flex-wrap items-start justify-between gap-3">
                  <div>
                    <p className="text-sm font-semibold text-gray-900">Contest check-in</p>
                    <p className="text-xs text-gray-500">
                      {readiness.isOpen
                        ? `${readiness.checkedIn}/${readiness.totalParticipants} checked in`
                        : 'Check-in has not been opened'}
                      {readiness.isOverridden ? ` — overridden: ${readiness.overrideReason}` : ''}
                    </p>
                  </div>
                  <div className="flex flex-wrap gap-2">
                    {!readiness.isOpen && lifecycle?.status === 1 && (
                      <Button
                        size="sm"
                        disabled={busy}
                        onClick={() => void checkInAction('open', 'Contest check-in opened.')}
                      >
                        Open check-in
                      </Button>
                    )}
                    {readiness.isOpen && lifecycle?.status === 2 && (
                      <>
                        <Button
                          size="sm"
                          disabled={busy || readiness.rosterConfirmationPassed}
                          onClick={() => void checkInAction('roster/confirm', 'Roster confirmed.')}
                        >
                          Confirm roster
                        </Button>
                        <Button
                          size="sm"
                          disabled={busy || readiness.venueConfirmationPassed}
                          onClick={() => void checkInAction('venue/confirm', 'Venue confirmed.')}
                        >
                          Confirm venue
                        </Button>
                        <Button
                          size="sm"
                          disabled={busy || !readiness.canStart}
                          onClick={() => void checkInAction('complete', 'Check-in completed; contest is ready.')}
                        >
                          Mark ready
                        </Button>
                        {canChooseTenant && !readiness.canStart && (
                          <Button variant="outline" size="sm" disabled={busy} onClick={overrideReadiness}>
                            Override readiness
                          </Button>
                        )}
                      </>
                    )}
                  </div>
                </div>

                {readiness.isOpen && (
                  <>
                    <div className="mt-3 grid gap-2 text-xs sm:grid-cols-4">
                      {[
                        ['Participants', readiness.participantCheckInPassed],
                        ['Roster', readiness.rosterConfirmationPassed],
                        ['Officials', readiness.officialConfirmationPassed],
                        ['Venue', readiness.venueConfirmationPassed],
                      ].map(([label, passed]) => (
                        <div
                          key={String(label)}
                          className={`rounded-md px-2 py-1.5 ${
                            passed ? 'bg-emerald-50 text-emerald-700' : 'bg-amber-50 text-amber-800'
                          }`}
                        >
                          {String(label)}: {passed ? 'Ready' : 'Pending'}
                        </div>
                      ))}
                    </div>
                    <div className="mt-3 grid gap-2 sm:grid-cols-2">
                      {readiness.participants.map((participant) => (
                        <div
                          key={participant.participantId}
                          className="flex items-center justify-between rounded-md border border-gray-100 px-2 py-1.5 text-xs"
                        >
                          <span>
                            {participantNames.get(participant.participantId) ?? participant.participantId}
                            {' '}({participant.participantType}) — {participantStatusLabels[participant.status] ?? participant.status}
                          </span>
                          {participant.status === 0 && lifecycle?.status === 2 && (
                            <Button
                              variant="outline"
                              size="sm"
                              disabled={busy}
                              onClick={() => void checkInAction(
                                `participants/${participant.participantId}/checkin`,
                                'Participant checked in.',
                              )}
                            >
                              Check in
                            </Button>
                          )}
                        </div>
                      ))}
                      {readiness.officials.map((official) => (
                        <div
                          key={official.officialId}
                          className="flex items-center justify-between rounded-md border border-gray-100 px-2 py-1.5 text-xs"
                        >
                          <span>{official.displayName ?? official.role} — {official.isConfirmed ? 'Confirmed' : 'Pending'}</span>
                          {!official.isConfirmed && lifecycle?.status === 2 && (
                            <Button
                              variant="outline"
                              size="sm"
                              disabled={busy}
                              onClick={() => void checkInAction(
                                `officials/${official.officialId}/confirm`,
                                'Official confirmed.',
                              )}
                            >
                              Confirm
                            </Button>
                          )}
                        </div>
                      ))}
                    </div>
                  </>
                )}
                {checkInHistory.length > 0 && (
                  <details className="mt-3 text-xs text-gray-600">
                    <summary className="cursor-pointer font-semibold">
                      Check-in history ({checkInHistory.length})
                    </summary>
                    <ol className="mt-2 space-y-1">
                      {checkInHistory.slice().reverse().map((item) => (
                        <li key={item.id}>
                          {new Date(item.occurredAt).toLocaleString()} — {item.action}
                          {item.reason ? ` — ${item.reason}` : ''}
                          {item.isOverride ? ' (override)' : ''}
                        </li>
                      ))}
                    </ol>
                  </details>
                )}
              </div>
            )}
            {activeContest && schema && (
              <div className="mb-5 flex flex-wrap items-end gap-4 rounded-lg bg-gray-50 p-3">
                <label className="text-sm font-medium text-gray-700">
                  Segment
                  <select
                    data-testid="contest-segment"
                    value={segmentId}
                    onChange={(event) => {
                      const value = event.target.value;
                      setSegmentId(value);
                      void updateContext(value, clockSeconds);
                    }}
                    disabled={!session || busy}
                    className="ml-2 rounded-md border border-gray-200 bg-white px-2 py-1.5"
                  >
                    {schema.segments.map((segment) => (
                      <option
                        key={segment.id}
                        value={segment.id}
                        disabled={schema.segments.some(
                          (candidate) => candidate.parentSegmentId === segment.id,
                        )}
                      >
                        {segment.depth > 0 ? '— '.repeat(segment.depth) : ''}
                        {segment.name}
                        {segment.isOptional ? ' (optional)' : ''}
                      </option>
                    ))}
                  </select>
                </label>
                <Button
                  type="button"
                  variant="outline"
                  size="sm"
                  onClick={advanceSegment}
                  disabled={!session || busy}
                >
                  Next segment
                </Button>
                {schema.supportsClock && (
                  <label className="text-sm font-medium text-gray-700">
                    Clock seconds {schema.clockCountsDown ? '(remaining)' : '(elapsed)'}
                    <input
                      type="number"
                      min={0}
                      step="0.1"
                      value={clockSeconds}
                      onChange={(event) => setClockSeconds(Number(event.target.value))}
                      onBlur={() => void updateContext(segmentId, clockSeconds)}
                      disabled={!session || busy || session.isClockRunning}
                      className="ml-2 w-28 rounded-md border border-gray-200 bg-white px-2 py-1.5"
                    />
                    {session?.isClockRunning ? (
                      <Button
                        className="ml-2"
                        variant="outline"
                        size="sm"
                        onClick={() => clockAction('pause')}
                        disabled={busy}
                      >
                        Pause clock
                      </Button>
                    ) : (
                      <Button
                        className="ml-2"
                        size="sm"
                        onClick={() => clockAction('start')}
                        disabled={!session || busy}
                      >
                        Start clock
                      </Button>
                    )}
                  </label>
                )}
              </div>
            )}
            {activeContest && schema?.sportCode === 'golf' && (() => {
              const selectedRound = golfRounds.find(
                (round) => round.id === golfScorecardRoundId,
              );
              const holes = schema.segments
                .filter((item) => item.parentSegmentId === selectedRound?.id)
                .sort((left, right) => left.sortOrder - right.sortOrder);
              const currentRoundTotal = holes.reduce((total, hole) =>
                total
                + (golfHoleValues[`${selectedRound?.id}:${hole.id}:strokes`]
                  ?? parFromHoleName(hole.name))
                + (golfHoleValues[`${selectedRound?.id}:${hole.id}:penalties`] ?? 0), 0);
              const golferTotal = scores
                .filter((score) => score.participantId === golfScorecardParticipantId)
                .reduce((total, score) => total + Number(score.value), 0);
              return (
                <div className="mb-6 rounded-xl border border-green-200 bg-green-50/50 p-5">
                  <div className="flex flex-wrap items-start justify-between gap-4">
                    <div>
                      <h3 className="font-semibold text-gray-900">Scorecard validation</h3>
                      <p className="mt-1 text-sm text-gray-600">
                        Enter all 18 holes, submit for review, then approve to publish the scores.
                      </p>
                    </div>
                    <div className="text-right text-sm">
                      <p data-testid="golf-current-round-total" className="font-semibold text-gray-900">
                        Current round: {currentRoundTotal}
                      </p>
                      <p data-testid="golf-tournament-total" className="text-gray-600">
                        Published tournament total: {golferTotal || '—'}
                      </p>
                    </div>
                  </div>
                  <div className="mt-4 grid gap-3 sm:grid-cols-2">
                    <label className="text-sm font-medium text-gray-700">
                      Golfer
                      <select
                        aria-label="Scorecard golfer"
                        value={golfScorecardParticipantId}
                        onChange={(event) => setGolfScorecardParticipantId(event.target.value)}
                        className="mt-1 block w-full rounded-md border border-gray-200 bg-white px-2 py-1.5"
                      >
                        {activeContest.participants.map((participant) => (
                          <option key={participant.id} value={participant.participantId}>
                            {participantNames.get(participant.participantId)
                              ?? participant.participantId.slice(0, 8)}
                          </option>
                        ))}
                      </select>
                    </label>
                    <label className="text-sm font-medium text-gray-700">
                      Current round
                      <select
                        aria-label="Scorecard round"
                        value={golfScorecardRoundId}
                        onChange={(event) => setGolfScorecardRoundId(event.target.value)}
                        className="mt-1 block w-full rounded-md border border-gray-200 bg-white px-2 py-1.5"
                      >
                        {golfRounds.map((round) => (
                          <option key={round.id} value={round.id}>{round.name}</option>
                        ))}
                      </select>
                    </label>
                  </div>
                  <div className="mt-4 grid gap-2 sm:grid-cols-3 lg:grid-cols-6">
                    {holes.map((hole) => {
                      const strokesKey = `${selectedRound?.id}:${hole.id}:strokes`;
                      const penaltiesKey = `${selectedRound?.id}:${hole.id}:penalties`;
                      return (
                        <div key={hole.id} className="rounded-lg border border-green-100 bg-white p-2">
                          <p className="text-xs font-semibold text-gray-700">{hole.name}</p>
                          <label className="mt-1 block text-xs text-gray-500">
                            Strokes
                            <input
                              data-testid={`golf-hole-${hole.sortOrder}-strokes`}
                              type="number"
                              min={1}
                              max={20}
                              value={golfHoleValues[strokesKey] ?? parFromHoleName(hole.name)}
                              onChange={(event) => setGolfHoleValues((current) => ({
                                ...current,
                                [strokesKey]: Number(event.target.value),
                              }))}
                              className="mt-0.5 w-full rounded border border-gray-200 px-1.5 py-1"
                            />
                          </label>
                          <label className="mt-1 block text-xs text-gray-500">
                            Penalties
                            <input
                              type="number"
                              min={0}
                              max={20}
                              value={golfHoleValues[penaltiesKey] ?? 0}
                              onChange={(event) => setGolfHoleValues((current) => ({
                                ...current,
                                [penaltiesKey]: Number(event.target.value),
                              }))}
                              className="mt-0.5 w-full rounded border border-gray-200 px-1.5 py-1"
                            />
                          </label>
                        </div>
                      );
                    })}
                  </div>
                  <Button
                    className="mt-4"
                    onClick={submitGolfScorecard}
                    disabled={busy || holes.length !== 18 || !golfScorecardParticipantId}
                  >
                    Submit scorecard for validation
                  </Button>

                  <div className="mt-5 border-t border-green-200 pt-4">
                    <h4 className="text-sm font-semibold text-gray-900">Scorecard review queue</h4>
                    {golfScorecards.length === 0 ? (
                      <p className="mt-2 text-sm text-gray-500">No submitted scorecards.</p>
                    ) : (
                      <div className="mt-2 space-y-2">
                        {golfScorecards.map((scorecard) => {
                          const status = golfScorecardStatus(scorecard.status);
                          return (
                            <div
                              key={scorecard.id}
                              data-testid={`golf-scorecard-${scorecard.id}`}
                              className="flex flex-wrap items-center justify-between gap-2 rounded-lg bg-white px-3 py-2 text-sm"
                            >
                              <span>
                                {participantNames.get(scorecard.participantId)
                                  ?? scorecard.participantId.slice(0, 8)}
                                {' · '}Round {scorecard.roundNumber} · {status}
                              </span>
                              {status === 'Pending' && canApproveCorrections && (
                                <span className="flex gap-2">
                                  <Button size="sm" onClick={() => reviewGolfScorecard(scorecard, true)} disabled={busy}>
                                    Approve
                                  </Button>
                                  <Button size="sm" variant="destructive" onClick={() => reviewGolfScorecard(scorecard, false)} disabled={busy}>
                                    Reject
                                  </Button>
                                </span>
                              )}
                            </div>
                          );
                        })}
                      </div>
                    )}
                  </div>
                </div>
              );
            })()}
            {!activeContest ? (
              <p className="py-12 text-center text-gray-500">Choose a contest to score.</p>
            ) : activeContest.participants.length === 0 ? (
              <p className="py-12 text-center text-gray-500">
                Add participants before starting this contest.
              </p>
            ) : (
              <div className="grid gap-4 md:grid-cols-2">
                {[...activeContest.participants]
                  .sort((left, right) => left.sortOrder - right.sortOrder)
                  .map((participant, index) => {
                    const totals =
                      metricTotals.get(participant.participantId) ?? new Map<string, number>();
                    const rosterPlayers = rosters.find(
                      (roster) => roster.teamId === participant.participantId,
                    )?.players ?? [];
                    const primaryTotal = schema
                      ? totals.get(schema.primaryMetric) ?? 0
                      : 0;
                    return (
                      <div
                        key={participant.id}
                        data-testid={`participant-card-${participant.participantId}`}
                        className="rounded-xl border border-gray-200 p-5"
                      >
                        <p className="text-sm font-semibold text-gray-900">
                          {participantNames.get(participant.participantId) ??
                            `${participant.participantType} ${index + 1}`}
                        </p>
                        <p
                          data-testid={`participant-score-${participant.participantId}`}
                          className="mt-3 text-5xl font-bold text-gray-900"
                        >
                          {primaryTotal}
                        </p>
                        <p className="mt-1 text-xs text-gray-500">
                          {schema?.scoreLabel ?? 'score'}
                          {schema?.lowerIsBetter ? ' · lower is better' : ''}
                        </p>
                        {schema?.actions.some((action) => action.requiresPlayer) && (
                          <label className="mt-4 block text-sm font-medium text-gray-700">
                            Player attribution
                            <select
                              data-testid={`player-select-${participant.participantId}`}
                              value={selectedPlayers[participant.participantId] ?? ''}
                              onChange={(event) => setSelectedPlayers((current) => ({
                                ...current,
                                [participant.participantId]: event.target.value,
                              }))}
                              className="mt-1 block w-full rounded-md border border-gray-200 bg-white px-2 py-1.5"
                            >
                              <option value="">Team only / unassigned</option>
                              {rosterPlayers.map((player) => (
                                <option key={player.athleteId} value={player.athleteId}>
                                  {player.jerseyNumber ? `#${player.jerseyNumber} · ` : ''}
                                  {player.name}
                                  {player.position ? ` · ${player.position}` : ''}
                                </option>
                              ))}
                            </select>
                            {rosterPlayers.length === 0 && (
                              <span className="mt-1 block text-xs font-normal text-amber-700">
                                No active roster players are available; events will be team-only.
                              </span>
                            )}
                          </label>
                        )}
                        {schema?.sportCode === 'football' &&
                          schema.actions.some((action) => action.eventType === 'SUBSTITUTION') && (
                          <label className="mt-3 block text-sm font-medium text-gray-700">
                            Substitution player entering
                            <select
                              data-testid={`incoming-player-select-${participant.participantId}`}
                              value={selectedIncomingPlayers[participant.participantId] ?? ''}
                              onChange={(event) => setSelectedIncomingPlayers((current) => ({
                                ...current,
                                [participant.participantId]: event.target.value,
                              }))}
                              className="mt-1 block w-full rounded-md border border-gray-200 bg-white px-2 py-1.5"
                            >
                              <option value="">Select player entering</option>
                              {rosterPlayers.map((player) => (
                                <option key={player.athleteId} value={player.athleteId}>
                                  {player.jerseyNumber ? `#${player.jerseyNumber} · ` : ''}
                                  {player.name}
                                </option>
                              ))}
                            </select>
                          </label>
                        )}
                        <div className="mt-4 flex flex-wrap gap-2">
                          {schema?.actions.map((action) => {
                            const valueKey = `${participant.participantId}:${action.code}`;
                            return (
                              <div key={action.code} className="flex items-center gap-1">
                                {action.inputType === 'number' && (
                                  <label className="sr-only" htmlFor={valueKey}>
                                    {action.label} value
                                  </label>
                                )}
                                {action.inputType === 'number' && (
                                  <input
                                    id={valueKey}
                                    data-action-value-code={action.code}
                                    data-participant-id={participant.participantId}
                                    type="number"
                                    min={action.minimum ?? undefined}
                                    max={action.maximum ?? undefined}
                                    step={action.step ?? undefined}
                                    value={actionValues[valueKey] ?? action.defaultValue}
                                    onChange={(event) => setActionValues((current) => ({
                                      ...current,
                                      [valueKey]: Number(event.target.value),
                                    }))}
                                    className="w-24 rounded-md border border-gray-200 px-2 py-1.5 text-sm"
                                  />
                                )}
                                <Button
                                  data-action-code={action.code}
                                  data-participant-id={participant.participantId}
                                  size="sm"
                                  variant={
                                    action.tone === 'danger'
                                      ? 'destructive'
                                      : action.tone === 'secondary'
                                        ? 'secondary'
                                        : 'default'
                                  }
                                  onClick={() => submit(participant.participantId, action)}
                                  disabled={busy || session?.status !== 1 ||
                                    (action.requiresPlayer && !selectedPlayers[participant.participantId]) ||
                                    (action.eventType === 'SUBSTITUTION' &&
                                      !selectedIncomingPlayers[participant.participantId])}
                                >
                                  {action.label}
                                  {action.unit ? ` (${action.unit})` : ''}
                                </Button>
                              </div>
                            );
                          })}
                        </div>
                        {totals.size > 0 && (
                          <div className="mt-4 flex flex-wrap gap-2 text-xs text-gray-600">
                            {Array.from(totals.entries()).map(([metric, value]) => (
                              <span key={metric} className="rounded-full bg-gray-100 px-2 py-1">
                                {metric}: {value}
                              </span>
                            ))}
                          </div>
                        )}
                        {rosterPlayers.length > 0 && (
                          <div className="mt-4 border-t border-gray-100 pt-3">
                            <p className="text-xs font-semibold uppercase tracking-wide text-gray-500">
                              Player statistics
                            </p>
                            <div className="mt-2 space-y-1 text-sm">
                              {rosterPlayers.map((player) => {
                                const playerEvents = scores.filter(
                                  (score) => score.secondaryParticipantId === player.athleteId,
                                );
                                const playerTotals = playerEvents.reduce<Record<string, number>>(
                                  (current, score) => ({
                                    ...current,
                                    [score.metricName]:
                                      (current[score.metricName] ?? 0) + Number(score.value),
                                  }),
                                  {},
                                );
                                return (
                                  <div
                                    key={player.athleteId}
                                    data-testid={`player-stats-${player.athleteId}`}
                                    className="flex flex-wrap items-center justify-between gap-2 rounded bg-gray-50 px-2 py-1.5"
                                  >
                                    <span>
                                      {player.jerseyNumber ? `#${player.jerseyNumber} ` : ''}
                                      {player.name}
                                    </span>
                                    <span className="text-xs text-gray-600">
                                      {Object.entries(playerTotals).length > 0
                                        ? Object.entries(playerTotals)
                                            .map(([metric, value]) => `${metric}: ${value}`)
                                            .join(' · ')
                                        : 'No events'}
                                    </span>
                                  </div>
                                );
                              })}
                            </div>
                          </div>
                        )}
                      </div>
                    );
                  })}
              </div>
            )}

            {scores.length > 0 && (
              <div className="mt-6 border-t pt-5">
                <h3 className="font-semibold text-gray-900">Recent persisted events</h3>
                <div className="mt-3 space-y-2">
                  {[...scores].reverse().slice(0, 10).map((score) => (
                    <div
                      key={score.id}
                      data-testid={`score-event-${score.id}`}
                      data-metric-name={score.metricName}
                      data-participant-id={score.participantId}
                      className="flex flex-wrap items-center justify-between gap-2 rounded-lg bg-gray-50 px-3 py-2 text-sm"
                    >
                      <span>
                        {participantNames.get(score.participantId) ?? score.participantId.slice(0, 8)}
                        {score.secondaryParticipantId && (
                          <> · {participantNames.get(score.secondaryParticipantId) ?? 'Roster player'}</>
                        )}
                        {' · '}{score.metricName}
                        {score.segmentId && (
                          <> · {schema?.segments.find((segment) => segment.id === score.segmentId)?.name}</>
                        )}
                        {score.clockSeconds != null && <> · {score.clockSeconds}s</>}
                      </span>
                      <span className="flex items-center gap-2">
                        <span className="font-semibold">+{score.value}</span>
                        {canCorrect && score.value !== 0 && (
                          <>
                            <Button variant="outline" size="sm" onClick={() => correct(score)} disabled={busy}>
                              Correct
                            </Button>
                            <Button variant="destructive" size="sm" onClick={() => reverse(score)} disabled={busy}>
                              Reverse
                            </Button>
                            <Button variant="outline" size="sm" onClick={() => replace(score)} disabled={busy}>
                              Replace
                            </Button>
                          </>
                        )}
                      </span>
                    </div>
                  ))}
                </div>
              </div>
            )}

            {canCorrect && corrections.length > 0 && (
              <div className="mt-6 border-t pt-5">
                <h3 className="font-semibold text-gray-900">Correction review and audit</h3>
                <p className="mt-1 text-sm text-gray-500">
                  Original and proposed values remain recorded for every decision.
                </p>
                <div className="mt-3 space-y-2">
                  {corrections.map((correction) => (
                    <div
                      key={correction.id}
                      data-testid={`score-correction-${correction.id}`}
                      className="rounded-lg border bg-white p-3 text-sm"
                    >
                      <div className="flex flex-wrap items-center justify-between gap-2">
                        <span className="font-medium">
                          {typeof correction.kind === 'number'
                            ? ['Correct', 'Reverse', 'Replace'][correction.kind]
                            : correction.kind}
                          {' · '}{correction.reason}
                        </span>
                        <span className="rounded-full bg-gray-100 px-2 py-1 text-xs">
                          {correction.status}
                        </span>
                      </div>
                      <div className="mt-1 text-xs text-gray-500">
                        Requested {new Date(correction.requestedAt).toLocaleString()}
                        {correction.reviewedAt && ` · Reviewed ${new Date(correction.reviewedAt).toLocaleString()}`}
                        {correction.reviewReason && ` · ${correction.reviewReason}`}
                      </div>
                      {correction.status === 'Pending' && canApproveCorrections && (
                        <div className="mt-3 flex gap-2">
                          <Button size="sm" onClick={() => reviewCorrection(correction, true)} disabled={busy}>
                            Approve
                          </Button>
                          <Button variant="destructive" size="sm" onClick={() => reviewCorrection(correction, false)} disabled={busy}>
                            Reject
                          </Button>
                        </div>
                      )}
                    </div>
                  ))}
                </div>
              </div>
            )}
          </CardContent>
        </Card>
      </div>
    </div>
  );
}

function parFromHoleName(name: string) {
  const match = name.match(/Par\s+(\d+)/i);
  return match ? Number(match[1]) : 4;
}

function golfScorecardStatus(status: GolfScorecardReview['status']) {
  if (typeof status === 'string') return status;
  return ['Pending', 'Approved', 'Rejected'][status] ?? String(status);
}

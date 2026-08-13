import { contestPhase, statusLabel } from '@/lib/presentation';

export function ContestStatus({ status }: { status: string }) {
  const phase = contestPhase(status);
  return (
    <span
      className="inline-flex items-center gap-1.5 rounded-full px-2.5 py-1 text-xs font-bold uppercase tracking-wide"
      style={{
        color: `var(--status-${phase})`,
        background: `color-mix(in srgb, var(--status-${phase}) 12%, transparent)`,
      }}
    >
      {phase === 'live' && <span className="live-dot" aria-hidden="true" />}
      {statusLabel(status)}
    </span>
  );
}

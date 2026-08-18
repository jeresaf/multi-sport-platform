import Link from 'next/link';
import { tenantPath } from '@/lib/public-api';
import type { CompetitionView } from './competition-types';

export function CompetitionNavigation({ id, views, active, labels, tenantSlug, onChange }: {
  id: string; views: CompetitionView[]; active: CompetitionView;
  labels: Record<string, string>; tenantSlug: string;
  onChange: (view: CompetitionView) => void;
}) {
  return <nav className="mt-6 flex gap-1 overflow-x-auto border-b border-[var(--color-border)] pb-px" aria-label="Competition sections">
    {views.map(view => <Link
      key={view}
      href={tenantPath(`/competition/${id}/${view}`, tenantSlug)}
      onClick={() => onChange(view)}
      data-testid={`competition-tab-${view}`}
      aria-current={active === view ? 'page' : undefined}
      className={`shrink-0 border-b-2 px-4 py-3 text-sm font-semibold capitalize ${active === view ? 'border-[var(--brand-primary)] text-[var(--color-text)]' : 'border-transparent text-[var(--color-text-muted)] hover:text-[var(--color-text)]'}`}
    >{labels[view] ?? view}</Link>)}
  </nav>;
}

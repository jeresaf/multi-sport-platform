'use client';

import type { ContestFilter } from '@/lib/presentation';

const FILTERS: Array<{ id: ContestFilter; label: string }> = [
  { id: 'all', label: 'All' },
  { id: 'live', label: 'Live' },
  { id: 'upcoming', label: 'Upcoming' },
  { id: 'finished', label: 'Finished' },
  { id: 'favourites', label: 'Favourites' },
];

export function ContestFilterBar({ value, onChange }: { value: ContestFilter; onChange: (filter: ContestFilter) => void }) {
  return (
    <div className="flex gap-2 overflow-x-auto pb-1" role="group" aria-label="Filter contests">
      {FILTERS.map(filter => (
        <button
          key={filter.id}
          className={`shrink-0 rounded-full border px-4 py-2 text-sm font-semibold transition ${value === filter.id ? 'border-transparent text-white' : 'border-[var(--color-border)] bg-[var(--color-surface)] hover:bg-[var(--color-surface-muted)]'}`}
          style={value === filter.id ? { backgroundColor: 'var(--brand-primary)' } : undefined}
          onClick={() => onChange(filter.id)}
          aria-pressed={value === filter.id}
        >
          {filter.label}
        </button>
      ))}
    </div>
  );
}

'use client';

import { CalendarDays, ChevronLeft, ChevronRight } from 'lucide-react';
import { localDateKey } from '@/lib/presentation';

export function DateNavigator({ value, onChange }: { value: string; onChange: (date: string) => void }) {
  const selected = new Date(`${value}T12:00:00`);
  const today = localDateKey(new Date());
  const move = (days: number) => {
    const next = new Date(selected);
    next.setDate(next.getDate() + days);
    onChange(localDateKey(next));
  };
  const days = Array.from({ length: 7 }, (_, index) => {
    const offset = index - 3;
    const date = new Date(selected);
    date.setDate(date.getDate() + offset);
    return { offset, date, key: localDateKey(date) };
  });

  return (
    <div className="surface rounded-2xl p-2 shadow-sm" aria-label="Contest date">
      <div className="flex items-center gap-1">
        <button className="shrink-0 rounded-xl p-2 hover:bg-[var(--color-surface-muted)]" onClick={() => move(-1)} aria-label="Previous day">
          <ChevronLeft aria-hidden="true" />
        </button>
        <div className="grid min-w-0 flex-1 grid-cols-3 gap-1 md:grid-cols-5 xl:grid-cols-7">
          {days.map(day => (
            <button
              key={day.key}
              type="button"
              onClick={() => onChange(day.key)}
              aria-current={day.key === value ? 'date' : undefined}
              className={`${Math.abs(day.offset) > 1 ? 'hidden md:block' : ''} ${Math.abs(day.offset) > 2 ? 'md:hidden xl:block' : ''} rounded-xl px-1 py-2 text-center hover:bg-[var(--color-surface-muted)] ${day.key === value ? 'bg-[var(--brand-primary)] text-white' : ''}`}
            >
              <span className="block text-[10px] font-bold uppercase tracking-wide">{day.key === today ? 'Today' : day.date.toLocaleDateString(undefined, { weekday: 'short' })}</span>
              <span className="mt-0.5 block text-sm font-semibold">{day.date.getDate()}</span>
            </button>
          ))}
        </div>
        <button className="shrink-0 rounded-xl p-2 hover:bg-[var(--color-surface-muted)]" onClick={() => move(1)} aria-label="Next day">
          <ChevronRight aria-hidden="true" />
        </button>
        <label className="relative shrink-0 cursor-pointer rounded-xl p-2 hover:bg-[var(--color-surface-muted)]" title="Open calendar">
          <CalendarDays className="h-5 w-5" aria-hidden="true" />
          <input className="absolute inset-0 cursor-pointer opacity-0" type="date" value={value} onChange={event => onChange(event.target.value)} aria-label="Choose date" />
        </label>
      </div>
    </div>
  );
}

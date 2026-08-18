'use client';

import { BellRing, X } from 'lucide-react';
import { useState } from 'react';
import { alertOptions, loadAlerts, saveAlerts, type AlertKind } from '@/lib/alert-preferences';

export function AlertPreferencesDialog({ open, type, id, title, sportCode, onClose, onChange }: {
  open: boolean; type: string; id: string; title: string; sportCode?: string | null;
  onClose: () => void; onChange?: (enabled: boolean) => void;
}) {
  return open ? <OpenAlertPreferencesDialog key={`${type}:${id}`} type={type} id={id} title={title} sportCode={sportCode} onClose={onClose} onChange={onChange} /> : null;
}

function OpenAlertPreferencesDialog({ type, id, title, sportCode, onClose, onChange }: {
  type: string; id: string; title: string; sportCode?: string | null;
  onClose: () => void; onChange?: (enabled: boolean) => void;
}) {
  const [selected, setSelected] = useState<Set<AlertKind>>(() => loadAlerts(type, id));
  const toggle = (kind: AlertKind) => setSelected(current => {
    const next = new Set(current); if (next.has(kind)) next.delete(kind); else next.add(kind); return next;
  });
  const save = () => { saveAlerts(type, id, selected); onChange?.(selected.size > 0); onClose(); };
  return <div className="fixed inset-0 z-50 grid place-items-end bg-black/50 p-0 sm:place-items-center sm:p-4" role="presentation" onMouseDown={event => event.target === event.currentTarget && onClose()}>
    <section role="dialog" aria-modal="true" aria-labelledby={`alerts-${id}`} className="surface w-full rounded-t-3xl p-5 shadow-2xl sm:max-w-md sm:rounded-3xl">
      <div className="flex items-start justify-between gap-4"><div><p className="text-sm font-bold text-[var(--brand-primary)]"><BellRing className="mr-2 inline h-4 w-4" />Alert preferences</p><h2 id={`alerts-${id}`} className="mt-1 text-xl font-bold">{title}</h2></div><button onClick={onClose} aria-label="Close alert preferences" className="rounded-full p-2 hover:bg-[var(--color-surface-muted)]"><X className="h-5 w-5" /></button></div>
      <div className="mt-5 space-y-2">{alertOptions(sportCode).map(option => <label key={option.id} className="flex cursor-pointer items-center justify-between rounded-xl border border-[var(--color-border)] p-3"><span className="font-medium">{option.label}</span><input type="checkbox" checked={selected.has(option.id)} onChange={() => toggle(option.id)} className="h-5 w-5 accent-[var(--brand-primary)]" /></label>)}</div>
      <div className="mt-5 flex gap-3"><button onClick={() => { setSelected(new Set()); }} className="flex-1 rounded-xl border border-[var(--color-border)] px-4 py-3 font-semibold">Clear</button><button onClick={save} className="flex-1 rounded-xl bg-[var(--brand-primary)] px-4 py-3 font-semibold text-white">Save alerts</button></div>
    </section>
  </div>;
}

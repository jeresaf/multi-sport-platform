export type AlertKind = 'start' | 'score' | 'periodEnd' | 'final' | 'postponement' | 'goal' | 'wicket' | 'roundResult' | 'teeTime' | 'fightResult';

export const basicAlerts: Array<{ id: AlertKind; label: string }> = [
  { id: 'start', label: 'Start' },
  { id: 'score', label: 'Score changes' },
  { id: 'periodEnd', label: 'Period end' },
  { id: 'final', label: 'Final result' },
  { id: 'postponement', label: 'Postponement' },
];

export function alertOptions(_sportCode?: string | null) {
  return basicAlerts;
}

export function alertStorageKey(type: string, id: string) {
  return `sports-platform:alerts:${type}:${id}`;
}

export function loadAlerts(type: string, id: string): Set<AlertKind> {
  if (typeof window === 'undefined') return new Set();
  try { return new Set(JSON.parse(localStorage.getItem(alertStorageKey(type, id)) ?? '[]') as AlertKind[]); }
  catch { return new Set(); }
}

export function saveAlerts(type: string, id: string, values: Set<AlertKind>) {
  localStorage.setItem(alertStorageKey(type, id), JSON.stringify([...values]));
}

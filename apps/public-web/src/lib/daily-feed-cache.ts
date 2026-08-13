import type { PublicContest } from './public-api';

const ttlMs = 2 * 60 * 1000;
const maxEntries = 14;
const cache = new Map<string, { value: PublicContest[]; storedAt: number }>();
const pending = new Map<string, Promise<PublicContest[]>>();

const keyFor = (tenantSlug: string, date: string) => `${tenantSlug}:${date}`;

export function readDailyFeed(tenantSlug: string, date: string) {
  const key = keyFor(tenantSlug, date);
  const entry = cache.get(key);
  if (!entry || Date.now() - entry.storedAt >= ttlMs) {
    if (entry) cache.delete(key);
    return null;
  }
  // Refresh insertion order so frequently revisited days remain cached.
  cache.delete(key);
  cache.set(key, entry);
  return entry.value;
}

export async function loadDailyFeed(
  tenantSlug: string,
  date: string,
  loader: () => Promise<PublicContest[]>,
) {
  const cached = readDailyFeed(tenantSlug, date);
  if (cached) return cached;
  const key = keyFor(tenantSlug, date);
  const existing = pending.get(key);
  if (existing) return existing;
  const request = loader().then(value => {
    cache.set(key, { value, storedAt: Date.now() });
    while (cache.size > maxEntries) cache.delete(cache.keys().next().value!);
    return value;
  }).finally(() => pending.delete(key));
  pending.set(key, request);
  return request;
}

export function updateDailyFeed(
  tenantSlug: string,
  date: string,
  updater: (current: PublicContest[]) => PublicContest[],
) {
  const key = keyFor(tenantSlug, date);
  const entry = cache.get(key);
  if (entry) cache.set(key, { value: updater(entry.value), storedAt: entry.storedAt });
}

export function invalidateDailyFeed(tenantSlug: string, date: string) {
  cache.delete(keyFor(tenantSlug, date));
}

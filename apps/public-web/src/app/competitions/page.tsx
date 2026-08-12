'use client';

import Link from 'next/link';
import { Search, Trophy } from 'lucide-react';
import { useEffect, useMemo, useState } from 'react';
import {
  formatSport,
  publicApi,
  PublicCompetition,
  resolveTenantSlug,
  TENANT_SLUG,
  tenantPath,
} from '@/lib/public-api';

export default function CompetitionsPage() {
  const [competitions, setCompetitions] = useState<PublicCompetition[]>([]);
  const [search, setSearch] = useState('');
  const [status, setStatus] = useState('');
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [tenantSlug, setTenantSlug] = useState(TENANT_SLUG);

  useEffect(() => {
    const resolvedTenant = resolveTenantSlug();
    publicApi<PublicCompetition[]>('/competitions', undefined, resolvedTenant)
      .then(items => {
        setTenantSlug(resolvedTenant);
        setCompetitions(items);
      })
      .catch((reason: Error) => setError(reason.message))
      .finally(() => setLoading(false));
  }, []);

  const filtered = useMemo(
    () =>
      competitions.filter((competition) => {
        const matchesSearch = competition.name
          .toLowerCase()
          .includes(search.toLowerCase());
        return matchesSearch && (!status || competition.status === status);
      }),
    [competitions, search, status]
  );

  return (
    <div className="container mx-auto px-4 py-10">
      <div className="mb-8">
        <p className="mb-2 text-sm font-semibold uppercase tracking-wider text-primary">
          Public directory
        </p>
        <h1 className="text-3xl font-bold text-gray-900">Competitions</h1>
        <p className="mt-2 text-gray-600">
          Only competitions published by the selected tenant appear here.
        </p>
      </div>

      <div className="mb-8 flex flex-col gap-3 rounded-xl border border-gray-200 bg-gray-50 p-4 md:flex-row">
        <label className="relative flex-1">
          <span className="sr-only">Search competitions</span>
          <Search className="absolute left-3 top-3 h-4 w-4 text-gray-400" />
          <input
            value={search}
            onChange={(event) => setSearch(event.target.value)}
            placeholder="Search competitions"
            className="w-full rounded-lg border border-gray-200 bg-white py-2 pl-10 pr-3"
          />
        </label>
        <select
          value={status}
          onChange={(event) => setStatus(event.target.value)}
          className="rounded-lg border border-gray-200 bg-white px-4 py-2"
          aria-label="Filter by status"
        >
          <option value="">All statuses</option>
          <option value="Published">Published</option>
          <option value="InProgress">In progress</option>
          <option value="Closed">Closed</option>
        </select>
      </div>

      {loading && <Message>Loading competitions…</Message>}
      {error && <Message tone="error">Could not load competitions: {error}</Message>}
      {!loading && !error && filtered.length === 0 && (
        <Message>No published competitions match those filters.</Message>
      )}

      <div className="grid gap-5 md:grid-cols-2 lg:grid-cols-3">
        {filtered.map((competition) => (
          <Link
            key={competition.id}
            href={tenantPath(`/competitions/${competition.id}`, tenantSlug)}
            className="rounded-xl border border-gray-200 bg-white p-6 transition hover:shadow-md"
          >
            <div className="mb-5 flex items-center justify-between">
              <Trophy className="h-8 w-8 text-primary" />
              <span className="rounded-full bg-green-50 px-3 py-1 text-xs font-semibold text-green-700">
                {competition.status}
              </span>
            </div>
            <h2 className="text-xl font-bold text-gray-900">{competition.name}</h2>
            <p className="mt-2 text-sm text-gray-500">
              {formatSport(competition.sportId)}
            </p>
            {competition.description && (
              <p className="mt-4 line-clamp-3 text-sm text-gray-600">
                {competition.description}
              </p>
            )}
          </Link>
        ))}
      </div>
    </div>
  );
}

function Message({
  children,
  tone = 'neutral',
}: {
  children: React.ReactNode;
  tone?: 'neutral' | 'error';
}) {
  return (
    <div
      className={`mb-6 rounded-xl border p-6 text-center ${
        tone === 'error'
          ? 'border-red-200 bg-red-50 text-red-700'
          : 'border-dashed border-gray-300 bg-gray-50 text-gray-500'
      }`}
    >
      {children}
    </div>
  );
}

'use client';

import Image from 'next/image';
import Link from 'next/link';
import { Calendar, Medal, Radio, Trophy } from 'lucide-react';
import { useEffect, useState } from 'react';
import {
  formatSport,
  publicApi,
  PublicHome,
  resolveTenantSlug,
  TENANT_SLUG,
  tenantPath,
} from '@/lib/public-api';

export default function PublicHomePage() {
  const [data, setData] = useState<PublicHome | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [tenantSlug, setTenantSlug] = useState(TENANT_SLUG);

  useEffect(() => {
    const resolvedTenant = resolveTenantSlug();
    publicApi<PublicHome>('/home', undefined, resolvedTenant)
      .then(home => {
        setTenantSlug(resolvedTenant);
        setData(home);
      })
      .catch((reason: Error) => setError(reason.message));
  }, []);

  const branding = data?.tenant.branding;
  const primary = validHex(branding?.primaryColor) ?? '#0e7490';
  const secondary = validHex(branding?.secondaryColor) ?? '#312e81';
  const accent = validHex(branding?.accentColor) ?? '#f59e0b';

  return (
    <div className="container mx-auto px-4 py-6 sm:py-8">
      <section
        className="overflow-hidden rounded-3xl px-6 py-14 text-center text-white shadow-xl sm:px-10 md:py-20"
        style={{ background: `linear-gradient(135deg, ${primary}, ${secondary})` }}
        data-testid="tenant-home-hero"
      >
        {branding?.logoUrl && (
          <Image
            unoptimized
            width={80}
            height={80}
            src={branding.logoUrl}
            alt={`${data?.tenant.name ?? 'Tenant'} logo`}
            className="mx-auto mb-5 h-16 w-16 rounded-2xl bg-white object-contain p-2 sm:h-20 sm:w-20"
          />
        )}
        <p className="mb-3 text-sm font-semibold uppercase tracking-[0.25em] text-white/80">
          {data?.tenant.name ?? 'Sports Platform'}
        </p>
        <h1 className="mb-4 text-4xl font-bold md:text-6xl">
          Live sport, open to everyone
        </h1>
        <p className="mx-auto mb-8 max-w-2xl text-base text-white/80 sm:text-lg">
          Follow published competitions, live contests, official results and
          standings. No account is required.
        </p>
        <div className="flex flex-col justify-center gap-3 sm:flex-row">
          <Link
            className="rounded-lg px-6 py-3 font-semibold text-gray-950"
            style={{ backgroundColor: accent }}
            href={tenantPath('/competitions', tenantSlug)}
          >
            Browse competitions
          </Link>
          <Link
            className="rounded-lg border border-white/40 bg-white/10 px-6 py-3 font-semibold"
            href={tenantPath('/results', tenantSlug)}
          >
            Official results
          </Link>
        </div>
      </section>

      {error && (
        <div role="alert" className="mt-8 rounded-lg border border-red-200 bg-red-50 p-4 text-red-700">
          Public data is temporarily unavailable: {error}
        </div>
      )}

      {!data && !error && (
        <div role="status" className="py-16 text-center text-gray-500">
          Loading live coverage…
        </div>
      )}

      {data && (
        <div className="space-y-6 py-10 sm:space-y-10 sm:py-12">
          <HomeSection
            title="Live now"
            aside={(
              <span className="flex items-center gap-2 rounded-full bg-red-50 px-3 py-1 text-sm font-medium text-red-700">
                <Radio className="h-4 w-4" /> {data.liveContests.length} live
              </span>
            )}
          >
            {data.liveContests.length === 0 ? (
              <EmptyState message="There are no live contests right now." />
            ) : (
              <div className="grid gap-5 md:grid-cols-2 xl:grid-cols-3">
                {data.liveContests.map(contest => (
                  <ContestCard key={contest.id} contest={contest} tenantSlug={tenantSlug} />
                ))}
              </div>
            )}
          </HomeSection>

          <HomeSection
            title="Featured competitions"
            aside={(
              <Link href={tenantPath('/competitions', tenantSlug)} className="font-medium hover:underline" style={{ color: primary }}>
                View all
              </Link>
            )}
          >
            {data.featuredCompetitions.length === 0 ? (
              <EmptyState message="No featured competitions are published yet." />
            ) : (
              <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
                {data.featuredCompetitions.map(competition => (
                  <Link
                    key={competition.id}
                    href={tenantPath(`/competitions/${competition.id}`, tenantSlug)}
                    className="rounded-2xl border border-gray-200 bg-white p-5 transition hover:-translate-y-0.5 hover:shadow-md"
                  >
                    <Trophy className="mb-4 h-7 w-7" style={{ color: primary }} />
                    <h3 className="font-bold text-gray-900">{competition.name}</h3>
                    <p className="mt-1 text-sm text-gray-500">
                      {formatSport(competition.sportId)} · {competition.status}
                    </p>
                    {competition.description && (
                      <p className="mt-3 line-clamp-2 text-sm text-gray-600">{competition.description}</p>
                    )}
                  </Link>
                ))}
              </div>
            )}
          </HomeSection>

          <HomeSection title="Upcoming contests">
            {data.upcomingContests.length === 0 ? (
              <EmptyState message="No upcoming contests have been published." />
            ) : (
              <div className="grid gap-4 md:grid-cols-2">
                {data.upcomingContests.map(contest => (
                  <ContestCard key={contest.id} contest={contest} tenantSlug={tenantSlug} />
                ))}
              </div>
            )}
          </HomeSection>

          <HomeSection
            title="Recent results"
            aside={(
              <Link href={tenantPath('/results', tenantSlug)} className="font-medium hover:underline" style={{ color: primary }}>
                All results
              </Link>
            )}
          >
            {data.recentResults.length === 0 ? (
              <EmptyState message="Official results will appear here after publication." />
            ) : (
              <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
                {data.recentResults.map(result => (
                  <Link
                    key={result.id}
                    href={tenantPath(`/contest/${result.contestId}`, tenantSlug)}
                    className="rounded-xl border border-gray-200 bg-white p-4 hover:shadow-md"
                  >
                    <div className="flex items-start justify-between gap-3">
                      <Medal className="h-5 w-5 shrink-0" style={{ color: accent }} />
                      <span className="text-xs font-semibold uppercase text-gray-500">{result.status}</span>
                    </div>
                    <p className="mt-3 text-sm text-gray-500">{result.contestName}</p>
                    <h3 className="mt-1 font-bold text-gray-900">{result.participantName}</h3>
                    <p className="mt-2 text-lg font-semibold" style={{ color: primary }}>
                      {result.scoreDisplay ?? result.time ?? result.score ?? result.position ?? 'Official'}
                    </p>
                  </Link>
                ))}
              </div>
            )}
          </HomeSection>

          <HomeSection title="Sponsors">
            {data.sponsors.length === 0 ? (
              <EmptyState message="Sponsor partners will be shown here." />
            ) : (
              <div className="grid gap-4 grid-cols-2 md:grid-cols-3 xl:grid-cols-6">
                {data.sponsors.map(sponsor => {
                  const content = (
                    <>
                      {sponsor.logoUrl ? (
                        <Image
                          unoptimized
                          width={120}
                          height={64}
                          src={sponsor.logoUrl}
                          alt={`${sponsor.name} logo`}
                          className="mx-auto h-12 w-full object-contain"
                        />
                      ) : (
                        <span className="flex h-12 items-center justify-center text-center font-bold text-gray-800">
                          {sponsor.name}
                        </span>
                      )}
                      <span className="mt-3 block text-center text-xs uppercase tracking-wide text-gray-500">
                        {sponsor.tier}
                      </span>
                    </>
                  );
                  return sponsor.website ? (
                    <a key={sponsor.id} href={sponsor.website} target="_blank" rel="noreferrer" className="rounded-xl border bg-white p-4 hover:shadow-md">
                      {content}
                    </a>
                  ) : (
                    <div key={sponsor.id} className="rounded-xl border bg-white p-4">{content}</div>
                  );
                })}
              </div>
            )}
          </HomeSection>
        </div>
      )}
    </div>
  );
}

function HomeSection({
  title,
  aside,
  children,
}: {
  title: string;
  aside?: React.ReactNode;
  children: React.ReactNode;
}) {
  return (
    <section aria-labelledby={`section-${title.toLowerCase().replaceAll(' ', '-')}`}>
      <div className="mb-5 flex items-center justify-between gap-4">
        <h2 id={`section-${title.toLowerCase().replaceAll(' ', '-')}`} className="text-2xl font-bold text-gray-900">
          {title}
        </h2>
        {aside}
      </div>
      {children}
    </section>
  );
}

function ContestCard({
  contest,
  tenantSlug,
}: {
  contest: PublicHome['liveContests'][number];
  tenantSlug: string;
}) {
  return (
    <Link
      href={tenantPath(`/contest/${contest.id}`, tenantSlug)}
      className="rounded-xl border border-gray-200 bg-white p-5 transition hover:-translate-y-0.5 hover:shadow-md"
    >
      <div className="mb-4 flex items-center justify-between gap-3 text-sm">
        <span className="font-medium" style={{ color: 'var(--brand-primary)' }}>
          {formatSport(contest.sportId)}
        </span>
        <span className="rounded-full bg-gray-100 px-2 py-1">{contest.status}</span>
      </div>
      <h3 className="text-lg font-bold text-gray-900">{contest.name || contest.contestShape}</h3>
      <div className="mt-4 flex items-start gap-2 text-sm text-gray-500">
        <Calendar className="mt-0.5 h-4 w-4 shrink-0" />
        {contest.startTime ? new Date(contest.startTime).toLocaleString() : 'Time to be confirmed'}
      </div>
      {contest.venue && <p className="mt-2 text-sm text-gray-500">{contest.venue}</p>}
    </Link>
  );
}

function EmptyState({ message }: { message: string }) {
  return (
    <div className="rounded-xl border border-dashed border-gray-300 bg-gray-50 p-8 text-center text-gray-500">
      {message}
    </div>
  );
}

function validHex(value?: string | null) {
  return value && /^#[0-9a-fA-F]{6}$/.test(value) ? value : null;
}

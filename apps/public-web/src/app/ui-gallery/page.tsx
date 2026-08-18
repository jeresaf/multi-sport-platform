'use client';

import { useState } from 'react';
import { ContestCard } from '@/components/contest-card';
import { ContestFilterBar } from '@/components/contest-filter-bar';
import { ContestStatus } from '@/components/contest-status';
import { DateNavigator } from '@/components/date-navigator';
import { localDateKey, type ContestFilter } from '@/lib/presentation';
import type { ContestRenderer, PublicContest } from '@/lib/public-api';

const renderers: ContestRenderer[] = ['head-to-head', 'multi-participant-leaderboard', 'race-heat', 'fight-card-bout', 'cricket-scorecard'];
const sportVariants = ['FOOTBALL', 'BASKETBALL', 'RUGBY_15', 'RUGBY_7', 'NETBALL', 'HOCKEY', 'VOLLEYBALL', 'TENNIS'];
const statuses = [
  'NOT_STARTED', 'STARTING_SOON', 'LIVE', 'DELAYED', 'INTERRUPTED',
  'SUSPENDED', 'POSTPONED', 'ENDED', 'CLOSED', 'CANCELLED', 'ABANDONED',
];

export default function UiGalleryPage() {
  const [date, setDate] = useState(localDateKey(new Date()));
  const [filter, setFilter] = useState<ContestFilter>('all');
  const [favourites, setFavourites] = useState(new Set<string>());
  const toggle = (id: string) => setFavourites(current => { const next = new Set(current); if (next.has(id)) next.delete(id); else next.add(id); return next; });
  return (
    <div className="app-container space-y-10 py-8">
      <header><p className="text-sm font-bold uppercase tracking-wider text-[var(--brand-primary)]">Development</p><h1 className="text-3xl font-extrabold">UI component gallery</h1></header>
      <section className="space-y-4"><h2 className="text-xl font-bold">Controls & statuses</h2><DateNavigator value={date} onChange={setDate} /><ContestFilterBar value={filter} onChange={setFilter} /><div className="flex flex-wrap gap-2">{statuses.map(status => <ContestStatus key={status} status={status} />)}</div></section>
      <section><h2 className="mb-4 text-xl font-bold">Contest renderers</h2><div className="contest-grid">{renderers.map((renderer, index) => { const contest = fixture(renderer, index); return <ContestCard key={renderer} contest={contest} tenantSlug="ui-gallery" favourite={favourites.has(contest.id)} onToggleFavourite={toggle} />; })}</div></section>
      <section><h2 className="mb-4 text-xl font-bold">Head-to-head sport adapters</h2><div className="contest-grid">{sportVariants.map((variant, index) => { const contest = sportFixture(variant, index); return <ContestCard key={variant} contest={contest} tenantSlug="ui-gallery" favourite={favourites.has(contest.id)} onToggleFavourite={toggle} />; })}</div></section>
    </div>
  );
}

function sportFixture(variant: string, index: number): PublicContest {
  const rugby = variant.startsWith('RUGBY_');
  const setBased = variant === 'VOLLEYBALL' || variant === 'TENNIS';
  return {
    id: `sport-${variant}`,
    competitionId: 'sport-gallery',
    competitionName: rugby ? 'Kampala Rugby Championship' : `${variant[0]}${variant.slice(1).toLowerCase()} League`,
    name: rugby ? 'Kobs RFC vs Heathens RFC' : 'City fixture',
    sportId: '00000000-0000-0000-0000-000000000100',
    sportCode: variant.startsWith('RUGBY') ? 'RUGBY' : variant,
    sportName: variant.startsWith('RUGBY') ? 'Rugby Union (7s & 15s)' : variant[0] + variant.slice(1).toLowerCase(),
    contestShape: 'TeamVsTeam',
    renderer: 'head-to-head',
    status: index % 3 === 0 ? 'Completed' : 'InProgress',
    startTime: new Date().toISOString(),
    venue: 'Riverside Sports Centre',
    presentation: {
      scoreVariant: variant,
      stageLabel: variant === 'RUGBY_7' ? 'Pool A' : 'Semifinal',
      periodLabel: setBased ? (variant === 'TENNIS' ? 'SET 3' : 'SET 4') : variant === 'BASKETBALL' || variant === 'NETBALL' || variant === 'HOCKEY' ? 'Q3' : '2H',
      clock: setBased ? '14–10' : variant === 'BASKETBALL' ? '04:12' : "63'",
      eventSummary: rugby ? [
        { participantId: `${variant}-1`, eventType: 'TRY', count: 3 },
        { participantId: `${variant}-2`, eventType: 'TRY', count: 2 },
        { participantId: `${variant}-1`, eventType: 'PENALTY_GOAL', count: 2 },
        { participantId: `${variant}-2`, eventType: 'PENALTY_GOAL', count: 2 },
      ] : [],
    },
    participants: [
      { participantId: `${variant}-1`, name: rugby ? 'Kobs RFC' : 'Riverside Falcons', scoreDisplay: setBased && variant === 'TENNIS' ? '6-4, 3-6, 4-2' : '24' },
      { participantId: `${variant}-2`, name: rugby ? 'Heathens RFC' : 'Highland United', scoreDisplay: setBased && variant === 'TENNIS' ? '4-6, 6-3, 2-4' : '19' },
    ],
  };
}

function fixture(renderer: ContestRenderer, index: number): PublicContest {
  return {
    id: renderer,
    competitionId: 'gallery',
    competitionName: 'UI Gallery League',
    name: renderer.replaceAll('-', ' '),
    sportId: index === 4 ? '00000000-0000-0000-0000-000000000104' : '00000000-0000-0000-0000-000000000100',
    contestShape: renderer,
    renderer,
    status: statuses[index],
    startTime: new Date().toISOString(),
    venue: 'Riverside Sports Centre',
    participants: [
      { participantId: `${renderer}-1`, name: 'Riverside Falcons', rank: 1, scoreDisplay: index === 4 ? '164/6' : '24' },
      { participantId: `${renderer}-2`, name: 'Highland United', rank: 2, scoreDisplay: index === 4 ? '158/9' : '17' },
      ...(index === 1 || index === 2 ? [{ participantId: `${renderer}-3`, name: 'Lakeside Athletics', rank: 3, scoreDisplay: '10.42' }] : []),
    ],
  };
}

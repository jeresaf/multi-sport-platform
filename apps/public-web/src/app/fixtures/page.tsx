import { MatchCentrePage } from '../page';

export default function FixturesPage() {
  return (
    <MatchCentrePage
      initialFilter="upcoming"
      eyebrow="Schedule"
      title="Upcoming fixtures"
      description="Browse scheduled contests by date, sport and competition, with tenant-local venue and start-time details."
    />
  );
}

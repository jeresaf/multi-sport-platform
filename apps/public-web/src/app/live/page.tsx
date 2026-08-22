import { MatchCentrePage } from '../page';

export default function LivePage() {
  return (
    <MatchCentrePage
      initialFilter="live"
      eyebrow="Live centre"
      title="Live scores"
      description="Follow every published contest in progress with automatic score and status updates."
    />
  );
}

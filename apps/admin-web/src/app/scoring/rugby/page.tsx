import { ConnectedSportScoring } from '@/components/scoring/connected-sport-scoring';

export default function RugbyScoringPage() {
  return <ConnectedSportScoring config={{
    name: 'Rugby Union — 7s & 15s', icon: '🏉',
    sportId: '00000000-0000-0000-0000-000000000106',
  }} />;
}

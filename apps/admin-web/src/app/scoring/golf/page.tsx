import { ConnectedSportScoring } from '@/components/scoring/connected-sport-scoring';

export default function GolfScoringPage() {
  return <ConnectedSportScoring config={{
    name: 'Golf', icon: '⛳',
    sportId: '00000000-0000-0000-0000-000000000102',
  }} />;
}

import { ConnectedSportScoring } from '@/components/scoring/connected-sport-scoring';

export default function BasketballScoringPage() {
  return <ConnectedSportScoring config={{
    name: 'Basketball', icon: '🏀',
    sportId: '00000000-0000-0000-0000-000000000101',
  }} />;
}

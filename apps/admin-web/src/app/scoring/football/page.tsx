import { ConnectedSportScoring } from '@/components/scoring/connected-sport-scoring';

export default function FootballScoringPage() {
  return <ConnectedSportScoring config={{
    name: 'Football', icon: '⚽',
    sportId: '00000000-0000-0000-0000-000000000100',
  }} />;
}

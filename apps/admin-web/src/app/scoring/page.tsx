import { ConnectedSportScoring } from '@/components/scoring/connected-sport-scoring';

export default function ScoringPage() {
  return (
    <ConnectedSportScoring
      config={{
        name: 'Tenant',
        icon: '🏟️',
      }}
    />
  );
}

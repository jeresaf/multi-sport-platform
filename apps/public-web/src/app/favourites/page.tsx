import { MatchCentrePage } from '../page';

export default function FavouritesPage() {
  return (
    <MatchCentrePage
      initialFilter="favourites"
      eyebrow="Your sport"
      title="Favourites"
      description="Keep followed contests, competitions, teams and athletes together for faster access and alerts."
    />
  );
}

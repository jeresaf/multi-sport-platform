'use client';

import { useParams } from 'next/navigation';
import { MatchCentrePage } from '../../page';

export default function SportPage() {
  const { slug } = useParams<{ slug: string }>();
  return <MatchCentrePage initialSportSlug={slug} />;
}

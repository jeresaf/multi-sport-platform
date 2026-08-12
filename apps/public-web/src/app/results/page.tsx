'use client';

import { Medal } from 'lucide-react';
import { useEffect, useState } from 'react';
import { publicApi } from '@/lib/public-api';

type Result = {
  id: string;
  contestId: string;
  participantId: string;
  participantName: string;
  rank?: number | null;
  position?: string | null;
  score?: number | null;
  scoreDisplay?: string | null;
  time?: string | null;
  status: string;
  approvedAt?: string | null;
};

export default function ResultsPage() {
  const [results, setResults] = useState<Result[]>([]);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    publicApi<Result[]>('/results')
      .then(setResults)
      .catch((reason: Error) => setError(reason.message));
  }, []);

  return (
    <div className="container mx-auto max-w-5xl px-4 py-10">
      <h1 className="text-3xl font-bold text-gray-900">Official results</h1>
      <p className="mt-2 text-gray-600">
        Approved and published results from this tenant.
      </p>
      {error && <Message>Could not load results: {error}</Message>}
      {!error && results.length === 0 && <Message>No official results are published yet.</Message>}
      <div className="mt-8 overflow-hidden rounded-xl border border-gray-200">
        {results.map((result) => (
          <div
            key={result.id}
            className="flex items-center gap-4 border-b border-gray-100 bg-white p-4 last:border-0"
          >
            <Medal className="h-6 w-6 text-primary" />
            <div className="min-w-0 flex-1">
              <p className="truncate font-semibold text-gray-900">{result.participantName}</p>
              <p className="text-sm text-gray-500">{result.status}</p>
            </div>
            <div className="text-right">
              <p className="text-xl font-bold">{result.scoreDisplay ?? result.time ?? result.score ?? '—'}</p>
              {result.rank && <p className="text-xs text-gray-500">Rank {result.rank}</p>}
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}

function Message({ children }: { children: React.ReactNode }) {
  return <div className="mt-8 rounded-xl border border-dashed border-gray-300 bg-gray-50 p-8 text-center text-gray-500">{children}</div>;
}

'use client';

import Link from 'next/link';
import { useSearchParams } from 'next/navigation';
import { Suspense, useEffect, useState } from 'react';
import { verifyEmail } from '@/lib/admin-api';

function VerifyEmailResult() {
  const token = useSearchParams().get('token') ?? '';
  const [status, setStatus] = useState('Verifying your email…');
  const [failed, setFailed] = useState(false);

  useEffect(() => {
    if (!token) {
      setFailed(true);
      setStatus('This verification link is incomplete.');
      return;
    }
    void verifyEmail(token)
      .then(() => setStatus('Your email address has been verified.'))
      .catch((reason: unknown) => {
        setFailed(true);
        setStatus(reason instanceof Error ? reason.message : 'Email verification failed.');
      });
  }, [token]);

  return <div className={`rounded-lg p-4 text-sm ${failed ? 'bg-red-50 text-red-700' : 'bg-green-50 text-green-800'}`}>{status}</div>;
}

export default function VerifyEmailPage() {
  return (
    <main className="mx-auto flex min-h-screen max-w-md items-center px-5 py-12">
      <section className="w-full rounded-2xl bg-white p-8 shadow-2xl">
        <h1 className="mb-6 text-3xl font-bold text-gray-950">Email verification</h1>
        <Suspense fallback={<p>Loading…</p>}><VerifyEmailResult /></Suspense>
        <Link href="/login" className="mt-5 block text-center text-sm text-gray-500 hover:text-primary">Continue to sign in</Link>
      </section>
    </main>
  );
}

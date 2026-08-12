'use client';

import Link from 'next/link';
import { useRouter } from 'next/navigation';
import { FormEvent, useState } from 'react';
import { login } from '@/lib/admin-api';

export default function LoginPage() {
  const router = useRouter();
  const [email, setEmail] = useState('admin@sportsplatform.com');
  const [password, setPassword] = useState('');
  const [mfaCode, setMfaCode] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  async function submit(event: FormEvent) {
    event.preventDefault();
    setLoading(true);
    setError(null);
    try {
      await login(email, password, mfaCode);
      router.replace('/');
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'Sign in failed.');
    } finally {
      setLoading(false);
    }
  }

  return (
    <div className="mx-auto flex min-h-screen max-w-md items-center px-5 py-12">
      <form onSubmit={submit} className="w-full rounded-2xl bg-white p-8 shadow-2xl">
        <p className="text-sm font-semibold uppercase tracking-wider text-primary">
          Sports Platform
        </p>
        <h1 className="mt-2 text-3xl font-bold text-gray-950">Admin sign in</h1>
        <p className="mt-2 text-sm text-gray-500">
          Administration and scoring actions require an authorized account.
        </p>
        <label className="mt-8 block text-sm font-medium text-gray-700">
          Email
          <input
            type="email"
            required
            value={email}
            onChange={(event) => setEmail(event.target.value)}
            className="mt-2 w-full rounded-lg border border-gray-200 px-3 py-2"
          />
        </label>
        <label className="mt-4 block text-sm font-medium text-gray-700">
          Authentication code <span className="font-normal text-gray-400">(if enabled)</span>
          <input
            type="text"
            inputMode="numeric"
            autoComplete="one-time-code"
            pattern="[0-9]{6}"
            maxLength={6}
            value={mfaCode}
            onChange={(event) => setMfaCode(event.target.value.replace(/\D/g, '').slice(0, 6))}
            className="mt-2 w-full rounded-lg border border-gray-200 px-3 py-2 tracking-[0.35em]"
          />
        </label>
        <label className="mt-4 block text-sm font-medium text-gray-700">
          Password
          <input
            type="password"
            required
            value={password}
            onChange={(event) => setPassword(event.target.value)}
            className="mt-2 w-full rounded-lg border border-gray-200 px-3 py-2"
          />
        </label>
        {error && (
          <div className="mt-4 rounded-lg bg-red-50 p-3 text-sm text-red-700">{error}</div>
        )}
        <button
          disabled={loading}
          className="mt-6 w-full rounded-lg bg-primary px-4 py-3 font-semibold text-white disabled:opacity-60"
        >
          {loading ? 'Signing in…' : 'Sign in'}
        </button>
        <Link
          href="/forgot-password"
          className="mt-4 block text-center text-sm font-medium text-primary hover:underline"
        >
          Forgot your password?
        </Link>
        <Link
          href="http://localhost:3001"
          className="mt-5 block text-center text-sm font-medium text-gray-500 hover:text-primary"
        >
          Return to public website
        </Link>
      </form>
    </div>
  );
}

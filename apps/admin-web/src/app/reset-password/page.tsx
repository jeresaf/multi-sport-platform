'use client';

import Link from 'next/link';
import { useSearchParams } from 'next/navigation';
import { FormEvent, Suspense, useState } from 'react';
import { resetPassword } from '@/lib/admin-api';

function ResetPasswordForm() {
  const search = useSearchParams();
  const email = search.get('email') ?? '';
  const token = search.get('token') ?? '';
  const [password, setPassword] = useState('');
  const [message, setMessage] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  async function submit(event: FormEvent) {
    event.preventDefault();
    setError(null);
    try {
      await resetPassword(email, token, password);
      setMessage('Your password has been reset. You can now sign in.');
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'Password reset failed.');
    }
  }

  const validLink = Boolean(email && token);
  return (
    <form onSubmit={submit} className="w-full rounded-2xl bg-white p-8 shadow-2xl">
      <h1 className="text-3xl font-bold text-gray-950">Choose a new password</h1>
      {!validLink && <div className="mt-4 rounded-lg bg-red-50 p-3 text-sm text-red-700">This reset link is incomplete.</div>}
      <label className="mt-8 block text-sm font-medium text-gray-700">
        New password
        <input type="password" required minLength={8} value={password} onChange={(event) => setPassword(event.target.value)} className="mt-2 w-full rounded-lg border border-gray-200 px-3 py-2" />
      </label>
      {message && <div className="mt-4 rounded-lg bg-green-50 p-3 text-sm text-green-800">{message}</div>}
      {error && <div className="mt-4 rounded-lg bg-red-50 p-3 text-sm text-red-700">{error}</div>}
      <button disabled={!validLink || Boolean(message)} className="mt-6 w-full rounded-lg bg-primary px-4 py-3 font-semibold text-white disabled:opacity-60">Reset password</button>
      <Link href="/login" className="mt-5 block text-center text-sm text-gray-500 hover:text-primary">Back to sign in</Link>
    </form>
  );
}

export default function ResetPasswordPage() {
  return <main className="mx-auto flex min-h-screen max-w-md items-center px-5 py-12"><Suspense fallback={<p>Loading…</p>}><ResetPasswordForm /></Suspense></main>;
}

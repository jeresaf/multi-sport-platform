'use client';

import Link from 'next/link';
import { FormEvent, useState } from 'react';
import { requestPasswordReset } from '@/lib/admin-api';

export default function ForgotPasswordPage() {
  const [email, setEmail] = useState('');
  const [message, setMessage] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  async function submit(event: FormEvent) {
    event.preventDefault();
    setLoading(true);
    setError(null);
    try {
      await requestPasswordReset(email);
      setMessage('If that account exists, a password reset link has been sent.');
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'Unable to request a reset.');
    } finally {
      setLoading(false);
    }
  }

  return (
    <main className="mx-auto flex min-h-screen max-w-md items-center px-5 py-12">
      <form onSubmit={submit} className="w-full rounded-2xl bg-white p-8 shadow-2xl">
        <p className="text-sm font-semibold uppercase tracking-wider text-primary">Sports Platform</p>
        <h1 className="mt-2 text-3xl font-bold text-gray-950">Reset your password</h1>
        <p className="mt-2 text-sm text-gray-500">Enter the email address attached to your account.</p>
        <label className="mt-8 block text-sm font-medium text-gray-700">
          Email
          <input type="email" required value={email} onChange={(event) => setEmail(event.target.value)} className="mt-2 w-full rounded-lg border border-gray-200 px-3 py-2" />
        </label>
        {message && <div className="mt-4 rounded-lg bg-green-50 p-3 text-sm text-green-800">{message}</div>}
        {error && <div className="mt-4 rounded-lg bg-red-50 p-3 text-sm text-red-700">{error}</div>}
        <button disabled={loading} className="mt-6 w-full rounded-lg bg-primary px-4 py-3 font-semibold text-white disabled:opacity-60">
          {loading ? 'Sending…' : 'Send reset link'}
        </button>
        <Link href="/login" className="mt-5 block text-center text-sm font-medium text-gray-500 hover:text-primary">Back to sign in</Link>
      </form>
    </main>
  );
}

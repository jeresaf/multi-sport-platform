const API_URL = process.env.NEXT_PUBLIC_API_URL?.replace(/\/$/, '') ?? '';
const SESSION_KEY = 'sports-platform:applicant-session';

export type ApplicantSession = {
  accessToken: string;
  refreshToken: string;
  expiresAt: string;
  user: {
    id: string; email: string; firstName: string; lastName: string;
    tenantId?: string | null; roles: string[]; permissions: string[];
  };
};

export function getApplicantSession(): ApplicantSession | null {
  if (typeof window === 'undefined') return null;
  try {
    return JSON.parse(localStorage.getItem(SESSION_KEY) ?? 'null') as ApplicantSession | null;
  } catch { return null; }
}

export function saveApplicantSession(session: ApplicantSession) {
  localStorage.setItem(SESSION_KEY, JSON.stringify(session));
  window.dispatchEvent(new Event('sports-platform:applicant-session'));
}

export function clearApplicantSession() {
  localStorage.removeItem(SESSION_KEY);
  window.dispatchEvent(new Event('sports-platform:applicant-session'));
}

export async function authenticateApplicant(
  mode: 'login' | 'register',
  body: Record<string, unknown>,
): Promise<ApplicantSession> {
  const response = await fetch(`${API_URL}/api/auth/${mode}`, {
    method: 'POST', headers: { 'Content-Type': 'application/json', Accept: 'application/json' },
    body: JSON.stringify(body),
  });
  if (!response.ok) throw new Error(await errorMessage(response));
  const session = await response.json() as ApplicantSession;
  saveApplicantSession(session);
  return session;
}

export async function applicantApi<T>(path: string, options?: RequestInit): Promise<T> {
  const session = getApplicantSession();
  if (!session) throw new Error('Sign in to continue.');
  const response = await fetch(`${API_URL}${path}`, {
    ...options,
    headers: {
      Accept: 'application/json',
      Authorization: `Bearer ${session.accessToken}`,
      ...(options?.body instanceof FormData ? {} : { 'Content-Type': 'application/json' }),
      ...options?.headers,
    },
  });
  if (response.status === 401) {
    clearApplicantSession();
    throw new Error('Your session expired. Sign in again.');
  }
  if (!response.ok) throw new Error(await errorMessage(response));
  if (response.status === 204) return undefined as T;
  return response.json() as Promise<T>;
}

export async function platformApi<T>(path: string, options?: RequestInit): Promise<T> {
  const response = await fetch(`${API_URL}${path}`, {
    ...options, headers: { Accept: 'application/json', ...options?.headers },
  });
  if (!response.ok) throw new Error(await errorMessage(response));
  return response.json() as Promise<T>;
}

async function errorMessage(response: Response) {
  try {
    const payload = await response.json() as { error?: string; errors?: string[] };
    return [payload.error, ...(payload.errors ?? [])].filter(Boolean).join(' ') || `Request failed (${response.status}).`;
  } catch { return `Request failed (${response.status}).`; }
}

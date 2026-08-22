export const ADMIN_API_URL =
  process.env.NEXT_PUBLIC_API_URL?.replace(/\/$/, '') ?? '';

export type AdminUser = {
  id: string;
  email: string;
  firstName: string;
  lastName: string;
  roles: string[];
  permissions: string[];
  tenantId?: string | null;
};

export type AuthResponse = {
  accessToken: string;
  refreshToken: string;
  expiresAt: string;
  user: AdminUser;
};

export type SelectedTenant = {
  id: string;
  name: string;
  shortName: string;
  slug: string;
};

const PLATFORM_ROLES = new Set(['Admin', 'Platform Owner', 'Platform Support']);
const UNRESTRICTED_ROLES = new Set(['Admin', 'Platform Owner']);

export function isPlatformOperator(user: AdminUser | null | undefined) {
  return user?.roles.some((role) => PLATFORM_ROLES.has(role)) ?? false;
}

export function hasAnyPermission(
  user: AdminUser | null | undefined,
  permissions: string[],
) {
  return (user?.roles.some((role) => UNRESTRICTED_ROLES.has(role)) ?? false) || permissions.some((required) =>
    user?.permissions.some((permission) => permission === required) ?? false,
  );
}

const ACCESS_TOKEN_KEY = 'sports-platform.access-token';
const REFRESH_TOKEN_KEY = 'sports-platform.refresh-token';
const USER_KEY = 'sports-platform.user';
const SELECTED_TENANT_KEY = 'sports-platform.selected-tenant';
export const TENANT_SCOPE_CHANGED_EVENT = 'admin:tenant-scope-changed';

export async function login(email: string, password: string, mfaCode?: string) {
  const response = await fetch(`${ADMIN_API_URL}/api/auth/login`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', Accept: 'application/json' },
    body: JSON.stringify({
      email,
      password,
      deviceId: 'admin-web',
      deviceName: 'Admin web',
      deviceType: 'web',
      ipAddress: null,
      mfaCode: mfaCode || null,
    }),
  });
  if (!response.ok) throw new Error(await readError(response));
  const auth = (await response.json()) as AuthResponse;
  localStorage.setItem(ACCESS_TOKEN_KEY, auth.accessToken);
  localStorage.setItem(REFRESH_TOKEN_KEY, auth.refreshToken);
  localStorage.setItem(USER_KEY, JSON.stringify(auth.user));
  return auth;
}

export function logout() {
  const accessToken = localStorage.getItem(ACCESS_TOKEN_KEY);
  const refreshToken = localStorage.getItem(REFRESH_TOKEN_KEY);
  localStorage.removeItem(ACCESS_TOKEN_KEY);
  localStorage.removeItem(REFRESH_TOKEN_KEY);
  localStorage.removeItem(USER_KEY);
  localStorage.removeItem(SELECTED_TENANT_KEY);
  if (accessToken && refreshToken) {
    void fetch(`${ADMIN_API_URL}/api/auth/logout`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        Authorization: `Bearer ${accessToken}`,
      },
      body: JSON.stringify({ refreshToken, deviceId: 'admin-web' }),
    });
  }
}

export function getSelectedTenant(): SelectedTenant | null {
  if (typeof window === 'undefined') return null;
  const value = localStorage.getItem(SELECTED_TENANT_KEY);
  if (!value) return null;
  try {
    return JSON.parse(value) as SelectedTenant;
  } catch {
    localStorage.removeItem(SELECTED_TENANT_KEY);
    return null;
  }
}

export function setSelectedTenant(tenant: SelectedTenant | null) {
  if (typeof window === 'undefined') return;
  if (tenant) localStorage.setItem(SELECTED_TENANT_KEY, JSON.stringify(tenant));
  else localStorage.removeItem(SELECTED_TENANT_KEY);
  window.dispatchEvent(new CustomEvent(TENANT_SCOPE_CHANGED_EVENT, { detail: tenant }));
}

export function getEffectiveTenantId() {
  const user = getStoredUser();
  return isPlatformOperator(user)
    ? getSelectedTenant()?.id ?? null
    : user?.tenantId ?? null;
}

export function getStoredUser(): AdminUser | null {
  if (typeof window === 'undefined') return null;
  const value = localStorage.getItem(USER_KEY);
  if (!value) return null;
  try {
    return JSON.parse(value) as AdminUser;
  } catch {
    return null;
  }
}

export function isSignedIn() {
  return typeof window !== 'undefined' && Boolean(localStorage.getItem(ACCESS_TOKEN_KEY));
}

export function getAccessToken() {
  return typeof window === 'undefined' ? null : localStorage.getItem(ACCESS_TOKEN_KEY);
}

export async function adminApi<T>(path: string, options?: RequestInit): Promise<T> {
  const response = await authenticatedFetch(path, options);
  if (response.status === 401) {
    logout();
    throw new Error('Your session expired. Sign in again.');
  }
  if (!response.ok) throw new Error(await readError(response));
  if (response.status === 204) return undefined as T;
  return response.json() as Promise<T>;
}

export async function adminDownload(path: string) {
  const response = await authenticatedFetch(path);
  if (response.status === 401) {
    logout();
    throw new Error('Your session expired. Sign in again.');
  }
  if (!response.ok) throw new Error(await readError(response));
  const disposition = response.headers.get('content-disposition') ?? '';
  const encoded = disposition.match(/filename\*=UTF-8''([^;]+)/i)?.[1];
  const quoted = disposition.match(/filename="([^"]+)"/i)?.[1];
  const fileName = encoded ? decodeURIComponent(encoded) : quoted ?? 'report';
  const url = URL.createObjectURL(await response.blob());
  const link = document.createElement('a');
  link.href = url;
  link.download = fileName;
  document.body.appendChild(link);
  link.click();
  link.remove();
  URL.revokeObjectURL(url);
}

export async function requestPasswordReset(email: string) {
  const response = await fetch(`${ADMIN_API_URL}/api/auth/forgot-password`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', Accept: 'application/json' },
    body: JSON.stringify({ email }),
  });
  if (!response.ok) throw new Error(await readError(response));
}

export async function resetPassword(email: string, token: string, newPassword: string) {
  const response = await fetch(`${ADMIN_API_URL}/api/auth/reset-password`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', Accept: 'application/json' },
    body: JSON.stringify({ email, token, newPassword }),
  });
  if (!response.ok) throw new Error(await readError(response));
}

export async function verifyEmail(token: string) {
  const response = await fetch(`${ADMIN_API_URL}/api/auth/verify-email`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', Accept: 'application/json' },
    body: JSON.stringify({ token }),
  });
  if (!response.ok) throw new Error(await readError(response));
}

async function authenticatedFetch(path: string, options?: RequestInit, retried = false) {
  const token = localStorage.getItem(ACCESS_TOKEN_KEY);
  if (!token) throw new Error('Sign in is required.');
  const isFormData = typeof FormData !== 'undefined' && options?.body instanceof FormData;
  const selectedTenant = isPlatformOperator(getStoredUser())
    ? getSelectedTenant()
    : null;
  const response = await fetch(`${ADMIN_API_URL}${path}`, {
    ...options,
    headers: {
      Accept: 'application/json',
      ...(!isFormData ? { 'Content-Type': 'application/json' } : {}),
      Authorization: `Bearer ${token}`,
      ...(selectedTenant ? { 'X-Tenant-Id': selectedTenant.id } : {}),
      ...options?.headers,
    },
  });
  if (response.status !== 401 || retried || !(await refreshAdminSession())) return response;
  return authenticatedFetch(path, options, true);
}

async function refreshAdminSession() {
  const refreshToken = localStorage.getItem(REFRESH_TOKEN_KEY);
  if (!refreshToken) return false;
  const response = await fetch(`${ADMIN_API_URL}/api/auth/refresh`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', Accept: 'application/json' },
    body: JSON.stringify({ refreshToken, deviceId: 'admin-web' }),
  });
  if (!response.ok) return false;
  const auth = (await response.json()) as AuthResponse;
  localStorage.setItem(ACCESS_TOKEN_KEY, auth.accessToken);
  localStorage.setItem(REFRESH_TOKEN_KEY, auth.refreshToken);
  localStorage.setItem(USER_KEY, JSON.stringify(auth.user));
  return true;
}

async function readError(response: Response) {
  const body = await response.text();
  try {
    const parsed = JSON.parse(body) as { error?: string; message?: string; detail?: string };
    return parsed.message ?? parsed.detail ?? parsed.error ?? `Request failed (${response.status})`;
  } catch {
    return body || `Request failed (${response.status})`;
  }
}

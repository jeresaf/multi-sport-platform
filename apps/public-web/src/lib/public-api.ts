const API_URL =
  process.env.NEXT_PUBLIC_API_URL?.replace(/\/$/, '') ?? 'http://localhost:8080';

export const PUBLIC_CONTEST_HUB_URL = `${API_URL}/hubs/contest`;

export const TENANT_SLUG =
  process.env.NEXT_PUBLIC_TENANT_SLUG ?? 'premier-league';

export type PublicCompetition = {
  id: string;
  name: string;
  description?: string | null;
  sportId: string;
  status: string;
  publishedAt?: string | null;
};

export type PublicContest = {
  id: string;
  name?: string | null;
  sportId: string;
  contestShape: string;
  status: string;
  venue?: string | null;
  startTime?: string | null;
  endTime?: string | null;
};

export type PublicHome = {
  tenant: {
    id: string;
    name: string;
    shortName: string;
    slug: string;
    branding?: {
      logoUrl?: string | null;
      primaryColor?: string | null;
      secondaryColor?: string | null;
      accentColor?: string | null;
      publicSlug?: string | null;
      contactEmail?: string | null;
      contactPhone?: string | null;
      website?: string | null;
    } | null;
  };
  competitions: PublicCompetition[];
  featuredCompetitions: PublicCompetition[];
  liveContests: PublicContest[];
  upcomingContests: PublicContest[];
  recentResults: Array<{
    id: string;
    contestId: string;
    contestName: string;
    participantId: string;
    participantName: string;
    rank?: number | null;
    position?: string | null;
    score?: number | null;
    scoreDisplay?: string | null;
    time?: string | null;
    status: string;
    approvedAt?: string | null;
  }>;
  sponsors: Array<{
    id: string;
    name: string;
    description?: string | null;
    logoUrl?: string | null;
    website?: string | null;
    tier: string;
  }>;
};

export function resolveTenantSlug() {
  if (typeof window === 'undefined') return TENANT_SLUG;
  const routeTenant = new URLSearchParams(window.location.search).get('tenant');
  if (routeTenant?.trim()) return routeTenant.trim();

  const hostname = window.location.hostname.toLowerCase();
  const configuredDomains = (process.env.NEXT_PUBLIC_TENANT_DOMAIN_MAP ?? '')
    .split(',')
    .map(entry => entry.trim().split('='))
    .find(([domain, slug]) => domain?.toLowerCase() === hostname && slug);
  return configuredDomains?.[1] ?? TENANT_SLUG;
}

export function tenantPath(path: string, tenantSlug: string) {
  const separator = path.includes('?') ? '&' : '?';
  return `${path}${separator}tenant=${encodeURIComponent(tenantSlug)}`;
}

export async function publicApi<T>(
  path: string,
  options?: RequestInit,
  tenantSlug = TENANT_SLUG,
): Promise<T> {
  const response = await fetch(
    `${API_URL}/api/public/tenants/${encodeURIComponent(tenantSlug)}${path}`,
    {
      ...options,
      headers: { Accept: 'application/json', ...options?.headers },
    }
  );
  if (!response.ok) {
    const message = await response.text();
    throw new Error(message || `Request failed (${response.status})`);
  }
  return response.json() as Promise<T>;
}

export function formatSport(sportId: string) {
  return ({
    '00000000-0000-0000-0000-000000000100': 'Football',
    '00000000-0000-0000-0000-000000000101': 'Basketball',
    '00000000-0000-0000-0000-000000000102': 'Golf',
    '00000000-0000-0000-0000-000000000103': 'Athletics',
    '00000000-0000-0000-0000-000000000104': 'Cricket',
    '00000000-0000-0000-0000-000000000105': 'Combat',
    '00000000-0000-0000-0000-000000000106': 'Rugby Union (7s & 15s)',
  } as Record<string, string>)[sportId.toLowerCase()] ?? `Sport ${sportId.slice(0, 8)}`;
}

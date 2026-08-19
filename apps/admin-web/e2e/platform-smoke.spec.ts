import { expect, test } from '@playwright/test';
import { mkdirSync } from 'node:fs';
import path from 'node:path';

const adminUrl = process.env.E2E_ADMIN_URL ?? 'http://localhost:3000';
const publicUrl = process.env.E2E_PUBLIC_URL ?? 'http://localhost:3001';

async function signIn(page: import('@playwright/test').Page, email: string, password: string) {
  await page.goto(`${adminUrl}/login`);
  await page.getByLabel('Email').fill(email);
  await page.getByLabel('Password').fill(password);
  await page.getByRole('button', { name: 'Sign in' }).click();
  await expect(page).toHaveURL(`${adminUrl}/`);
  await page.waitForFunction(
    () =>
      Boolean(localStorage.getItem('sports-platform.access-token')) &&
      Boolean(localStorage.getItem('sports-platform.user')),
  );
}

test('admin and public web responses enforce browser security policies', async ({ request }) => {
  for (const url of [adminUrl, publicUrl]) {
    const response = await request.get(url);
    expect(response.status()).toBe(200);
    const headers = response.headers();
    expect(headers['content-security-policy']).toContain("frame-ancestors 'none'");
    expect(headers['x-frame-options']).toBe('DENY');
    expect(headers['x-content-type-options']).toBe('nosniff');
    expect(headers['referrer-policy']).toBe('strict-origin-when-cross-origin');
    expect(headers['permissions-policy']).toContain('camera=()');
    expect(headers['x-powered-by']).toBeUndefined();
  }
});

test('public site opens without authentication and exposes core navigation', async ({ page }) => {
  const response = await page.goto(publicUrl);

  expect(response?.status()).toBe(200);
  await expect(page.getByTestId('match-centre-header')).toContainText('Match centre');
  await expect(
    page.getByRole('heading', { name: 'Scores & schedule', exact: true }),
  ).toBeVisible();
});

test('rugby flagship page presents distinct Sevens rules and completed tournament', async ({ page }) => {
  const response = await page.goto(
    `${publicUrl}/rugby?tenant=national-sports-demo&competitionId=70000000-0000-0000-0000-000000000100`,
  );

  expect(response?.status()).toBe(200);
  await expect(page.getByText('Rugby Sevens', { exact: true })).toBeVisible();
  await expect(page.getByRole('heading', {
    name: 'National Rugby Sevens Flagship Demo',
  })).toBeVisible();
  await expect(page.getByText(/7-minute halves/)).toBeVisible();
  await expect(page.getByRole('link', { name: 'Explore Rugby 15s' })).toBeVisible();
  await expect(page.getByRole('link', { name: 'Explore Rugby Sevens' })).toBeVisible();
  await expect(page.getByText('Rugby Sevens Cup Final')).toBeVisible();
  await expect(page.getByRole('columnheader', { name: 'BP' })).toHaveCount(0);
});

test('tenant public homepage is branded, complete, responsive, and safely cached', async ({ page }) => {
  const homeResponse = await page.request.get(
    'http://localhost:8080/api/public/tenants/nba/home',
  );
  expect(homeResponse.status()).toBe(200);
  expect(homeResponse.headers()['cache-control'])
    .toBe('public, max-age=30, stale-while-revalidate=120');
  const home = await homeResponse.json() as Record<string, unknown>;
  for (const field of [
    'featuredCompetitions',
    'liveContests',
    'upcomingContests',
    'recentResults',
    'sponsors',
  ]) {
    expect(Array.isArray(home[field])).toBeTruthy();
  }

  await page.goto(`${publicUrl}/?tenant=nba`);
  await expect(page.getByRole('link', { name: 'NBA home' }))
    .toHaveAttribute('href', /tenant=nba/);
  await expect(page.getByTestId('match-centre-header')).toContainText('Scores & schedule');

  await page.setViewportSize({ width: 390, height: 844 });
  await expect(page.getByRole('navigation', { name: 'Mobile navigation' }))
    .toContainText('Competitions');
  await expect(page.getByRole('heading', { name: 'Scores & schedule' }))
    .toBeVisible();
  const dimensions = await page.evaluate(() => ({
    viewport: window.innerWidth,
    document: document.documentElement.scrollWidth,
  }));
  expect(dimensions.document).toBeLessThanOrEqual(dimensions.viewport);
});

test('public match centre renders the required visual device matrix', async ({ page }) => {
  const targets = [
    [1440, 1000], [1280, 900], [1024, 1366], [768, 1024],
    [430, 932], [390, 844], [360, 800],
  ] as const;
  const output = path.resolve(process.cwd(), '../../artifacts/ui-matrix');
  mkdirSync(output, { recursive: true });
  for (const [width, height] of targets) {
    await page.setViewportSize({ width, height });
    await page.goto(`${publicUrl}/?tenant=uganda-sports-demo&date=2026-08-13`);
    await expect(page.getByRole('heading', { name: 'Scores & schedule' })).toBeVisible();
    await expect(page.getByRole('status', { name: 'Loading contests' })).toHaveCount(0);
    await expect.poll(() => page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
    await page.screenshot({
      path: path.join(output, `public-home-${width}x${height}.png`),
      fullPage: true,
      animations: 'disabled',
    });
  }
});

test('public competition detail exposes only relevant labelled tabs and empty states', async ({ page }) => {
  type CompetitionDetail = {
    id: string;
    name: string;
    availableTabs: string[];
    labels: Record<string, string>;
  };

  const listResponse = await page.request.get(
    'http://localhost:8080/api/public/tenants/premier-league/competitions',
  );
  expect(listResponse.status()).toBe(200);
  const competitions = await listResponse.json() as Array<{ id: string }>;
  expect(competitions.length).toBeGreaterThan(0);

  let selected: CompetitionDetail | null = null;
  for (const competition of competitions) {
    const response = await page.request.get(
      `http://localhost:8080/api/public/tenants/premier-league/competitions/${competition.id}`,
    );
    if (!response.ok()) continue;
    const detail = await response.json() as CompetitionDetail;
    if (!selected || detail.availableTabs.length > selected.availableTabs.length) {
      selected = detail;
    }
  }
  expect(selected).not.toBeNull();

  await page.goto(`${publicUrl}/competitions/${selected!.id}?tenant=premier-league`);
  await expect(page.getByRole('heading', { name: selected!.name })).toBeVisible();
  const allTabs = [
    'overview', 'fixtures', 'results', 'standings', 'bracket',
    'leaderboard', 'participants', 'statistics',
  ];
  for (const tab of allTabs) {
    const control = page.getByTestId(`competition-tab-${tab}`);
    if (selected!.availableTabs.includes(tab)) {
      await expect(control).toHaveText(selected!.labels[tab]);
      await control.click();
      await expect(page.getByTestId(`competition-panel-${tab}`)).toBeVisible();
    } else {
      await expect(control).toHaveCount(0);
    }
  }

  const golfId = '00000000-0000-0000-0000-000000000302';
  const golfResponse = await page.request.get(
    `http://localhost:8080/api/public/tenants/pga-tour/competitions/${golfId}`,
  );
  expect(golfResponse.status()).toBe(200);
  const golf = await golfResponse.json() as {
    labels: Record<string, string>;
    availableTabs: string[];
    leaderboard: unknown[];
  };
  expect(golf.labels.fixtures).toBe('Tee times');
  expect(golf.labels.participants).toBe('Golfers');
  expect(golf.availableTabs).toContain('leaderboard');
  await page.goto(`${publicUrl}/competitions/${golfId}?tenant=pga-tour`);
  await page.getByTestId('competition-tab-leaderboard').click();
  await expect(page.getByTestId('competition-panel-leaderboard')).toBeVisible();
  if (golf.leaderboard.length === 0) {
    await expect(page.getByText('Leaderboard positions will appear after official scores are published.'))
      .toBeVisible();
  }
});

test('public contest selects its renderer and restores live coverage after reconnect', async ({ page }) => {
  const competitionId = '00000000-0000-0000-0000-000000000300';
  const competitionResponse = await page.request.get(
    `http://localhost:8080/api/public/tenants/premier-league/competitions/${competitionId}`,
  );
  expect(competitionResponse.status()).toBe(200);
  const competition = await competitionResponse.json() as {
    fixtures: Array<{ id: string }>;
  };
  expect(competition.fixtures.length).toBeGreaterThan(0);
  const contestId = competition.fixtures[0].id;
  const contestResponse = await page.request.get(
    `http://localhost:8080/api/public/tenants/premier-league/contests/${contestId}`,
  );
  expect(contestResponse.status()).toBe(200);
  expect(contestResponse.headers()['cache-control']).toContain('no-store');
  const contest = await contestResponse.json() as { name?: string | null; renderer: string };

  await page.goto(`${publicUrl}/contest/${contestId}?tenant=premier-league`);
  await expect(page.getByRole('heading', { name: contest.name ?? 'Published contest' })).toBeVisible();
  await expect(page.getByTestId(`contest-renderer-${contest.renderer}`)).toBeVisible();
  await expect(page.getByTestId('contest-live-state')).toContainText('live', { timeout: 15_000 });

  await page.reload();
  await expect(page.getByTestId(`contest-renderer-${contest.renderer}`)).toBeVisible();
  await expect(page.getByTestId('contest-live-state')).toContainText('live', { timeout: 20_000 });
});

test('public team and athlete profiles show schedules and results without private fields', async ({ page }) => {
  const profiles = [
    {
      tenant: 'premier-league',
      id: '00000000-0000-0000-0000-000000000200',
      kind: 'Team',
      testId: 'participant-profile-team',
    },
    {
      tenant: 'world-athletics',
      id: '00000000-0000-0000-0000-000000000230',
      kind: 'Athlete',
      testId: 'participant-profile-athlete',
    },
  ];
  for (const profile of profiles) {
    const response = await page.request.get(
      `http://localhost:8080/api/public/tenants/${profile.tenant}/participants/${profile.id}`,
    );
    expect(response.status()).toBe(200);
    expect(response.headers()['cache-control']).toContain('public');
    const raw = await response.text();
    for (const field of [
      'dateOfBirth', 'gender', 'contactEmail', 'contactPhone',
      'emergencyContact', 'medicalClearance',
    ]) {
      expect(raw).not.toContain(field);
    }
    const participant = JSON.parse(raw) as { name: string; profile: { kind: string } };
    expect(participant.profile.kind).toBe(profile.kind);

    await page.goto(`${publicUrl}/participants/${profile.id}?tenant=${profile.tenant}`);
    await expect(page.getByRole('heading', { name: participant.name })).toBeVisible();
    await expect(page.getByTestId(profile.testId)).toBeVisible();
    await expect(page.getByRole('heading', { name: 'Upcoming contests' })).toBeVisible();
    await expect(page.getByRole('heading', { name: 'Results' })).toBeVisible();
  }
});

test('public search is tenant scoped, paginated, filtered, and usable on mobile', async ({ page }) => {
  const premierResponse = await page.request.get(
    'http://localhost:8080/api/public/tenants/premier-league/search?q=Manchester%20City&pageSize=1',
  );
  expect(premierResponse.status()).toBe(200);
  expect(premierResponse.headers()['cache-control']).toContain('public');
  const premier = await premierResponse.json() as {
    total: number;
    items: Array<{ title: string; type: string }>;
    filters: { sports: unknown[]; competitions: unknown[]; statuses: string[] };
  };
  expect(premier.total).toBeGreaterThan(0);
  expect(premier.items[0].title).toContain('Manchester City');
  expect(Array.isArray(premier.filters.sports)).toBeTruthy();
  expect(Array.isArray(premier.filters.competitions)).toBeTruthy();
  expect(Array.isArray(premier.filters.statuses)).toBeTruthy();
  const nbaResponse = await page.request.get(
    'http://localhost:8080/api/public/tenants/nba/search?q=Manchester%20City',
  );
  expect(nbaResponse.status()).toBe(200);
  expect((await nbaResponse.json() as { total: number }).total).toBe(0);

  await page.goto(`${publicUrl}/search?tenant=premier-league`);
  await expect(page.getByRole('heading', { name: 'Search' })).toBeVisible();
  await expect(page.getByRole('navigation', { name: 'Search result pages' })).toBeVisible();
  await page.getByRole('button', { name: /Next/ }).click();
  await expect(page.getByText(/Page 2 of/)).toBeVisible();

  await page.getByPlaceholder('Search names, contests, or venues').fill('Manchester City');
  await page.getByLabel('Result type').selectOption('team');
  await expect(page.getByTestId('search-results')).toContainText('Manchester City');
  await expect(page.getByTestId('search-results')).toContainText('Team');

  await page.setViewportSize({ width: 390, height: 844 });
  await expect(page.getByLabel('Sport', { exact: true })).toBeVisible();
  await expect(page.getByLabel('Status', { exact: true })).toBeVisible();
  await expect(page.getByLabel('Competition', { exact: true })).toBeVisible();
  await expect(page.getByLabel('From date', { exact: true })).toBeVisible();
  await expect(page.getByLabel('To date', { exact: true })).toBeVisible();
  const dimensions = await page.evaluate(() => ({
    viewport: window.innerWidth,
    document: document.documentElement.scrollWidth,
  }));
  expect(dimensions.document).toBeLessThanOrEqual(dimensions.viewport);
});

test('administrator can sign in and open the connected scoring workspace', async ({ page }) => {
  await signIn(page, 'admin@sportsplatform.com', 'Admin123!');
  await page.goto(`${adminUrl}/scoring`);
  await expect(page.getByRole('heading', { name: 'Tenant Scoring' })).toBeVisible();
  await expect(page.getByRole('heading', { name: 'Tenant contests' })).toBeVisible();
});

test('platform administrator creates a tenant that is immediately available on public web', async ({ page }) => {
  await signIn(page, 'admin@sportsplatform.com', 'Admin123!');
  const accessToken = await page.evaluate(() =>
    localStorage.getItem('sports-platform.access-token'));
  expect(accessToken).toBeTruthy();

  const suffix = Date.now().toString();
  const tenantName = `Browser Tenant ${suffix}`;
  const tenantSlug = `browser-tenant-${suffix}`;
  let createdTenant: { id: string; name: string; slug: string } | null = null;

  try {
    await page.goto(`${adminUrl}/tenants`);
    await expect(page.getByRole('heading', { name: 'Tenants' })).toBeVisible();
    await page.getByRole('button', { name: 'New tenant' }).click();
    await page.getByLabel('Tenant name').fill(tenantName);
    await page.getByLabel('Short name').fill('BT');
    await page.getByLabel('Public slug').fill(tenantSlug);

    const createResponsePromise = page.waitForResponse(response =>
      new URL(response.url()).pathname === '/api/tenants' &&
      response.request().method() === 'POST');
    await page.getByRole('button', { name: 'Create tenant' }).click();
    const createResponse = await createResponsePromise;
    expect(createResponse.status()).toBe(201);
    createdTenant = await createResponse.json() as typeof createdTenant;

    await expect(page.getByRole('status')).toContainText(
      'Tenant created with its standard role catalogue.',
    );
    const selectedTenantHeading = page.getByRole('heading', { name: tenantName });
    await expect(selectedTenantHeading).toBeVisible();
    await expect(selectedTenantHeading.locator('..')).toContainText(`/${tenantSlug}`);
    await expect(page.getByText('Active', { exact: true }).first()).toBeVisible();

    const publicResponse = await page.request.get(
      `http://localhost:8080/api/public/tenants/${tenantSlug}/home`,
    );
    expect(publicResponse.status()).toBe(200);
    const publicHome = await publicResponse.json() as {
      tenant: { id: string; name: string; slug: string };
    };
    expect(publicHome.tenant).toMatchObject({
      id: createdTenant!.id,
      name: tenantName,
      slug: tenantSlug,
    });

    await page.goto(`${publicUrl}/?tenant=${tenantSlug}`);
    await expect(page.getByRole('link', { name: `${tenantName} home` }))
      .toHaveAttribute('href', new RegExp(`tenant=${tenantSlug}`));
  } finally {
    const tenantToDelete = createdTenant as { id: string } | null;
    if (tenantToDelete && accessToken) {
      const cleanupResponse = await page.request.delete(
        `http://localhost:8080/api/tenants/${tenantToDelete.id}`,
        { headers: { Authorization: `Bearer ${accessToken}` } },
      );
      expect(cleanupResponse.status()).toBe(204);
    }
  }
});

test('dynamic scoring console renders every prototype sport configuration', async ({ page }) => {
  test.setTimeout(180_000);
  await signIn(page, 'admin@sportsplatform.com', 'Admin123!');

  const prototypes = [
    {
      route: 'football', code: 'football',
      sportId: '00000000-0000-0000-0000-000000000100',
      actions: ['goal', 'own-goal', 'penalty-goal', 'penalty-scored', 'penalty-missed',
        'yellow-card', 'second-yellow', 'red-card', 'substitution-in'],
    },
    {
      route: 'basketball', code: 'basketball',
      sportId: '00000000-0000-0000-0000-000000000101',
      actions: ['free-throw', 'field-goal', 'three-pointer', 'team-foul',
        'personal-foul', 'timeout'],
    },
    {
      route: 'golf', code: 'golf',
      sportId: '00000000-0000-0000-0000-000000000102',
      actions: ['hole-strokes', 'penalty'],
    },
    {
      route: 'athletics', code: 'athletics',
      sportId: '00000000-0000-0000-0000-000000000103',
      actions: ['time', 'distance', 'qualification', 'disqualification'],
    },
    {
      route: 'cricket', code: 'cricket',
      sportId: '00000000-0000-0000-0000-000000000104',
      actions: ['single', 'four', 'six', 'wicket', 'extra'],
    },
    {
      route: 'combat', code: 'combat',
      sportId: '00000000-0000-0000-0000-000000000105',
      actions: ['judge-score', 'significant-strike', 'takedown', 'knockdown', 'stoppage'],
    },
    {
      route: 'rugby', code: 'rugby',
      sportId: '00000000-0000-0000-0000-000000000106',
      actions: ['try', 'conversion', 'penalty-try', 'penalty-goal', 'drop-goal',
        'yellow-card', 'red-card', 'substitution'],
    },
  ];

  const fixture = await page.evaluate(async (sports) => {
    const token = localStorage.getItem('sports-platform.access-token');
    if (!token) throw new Error('Administrator session is unavailable.');
    const headers = {
      Authorization: `Bearer ${token}`,
      Accept: 'application/json',
      'Content-Type': 'application/json',
    };
    async function request<T>(path: string, method = 'GET', body?: unknown) {
      const response = await fetch(`${path}`, {
        method,
        headers,
        body: body === undefined ? undefined : JSON.stringify(body),
      });
      const text = await response.text();
      if (!response.ok) {
        throw new Error(`${method} ${path} failed (${response.status}): ${text}`);
      }
      return text ? JSON.parse(text) as T : undefined as T;
    }

    const suffix = Date.now().toString();
    const tenant = await request<{ id: string; name: string }>('/api/tenants', 'POST', {
      name: `Dynamic Scoring ${suffix}`,
      shortName: 'DS',
      slug: `dynamic-scoring-${suffix}`,
    });
    const subscription = await request<{ id: string }>('/api/subscriptions', 'POST', {
      tenantId: tenant.id,
      plan: 'Professional',
      startDate: new Date().toISOString(),
      endDate: new Date(Date.now() + 86_400_000).toISOString(),
    });
    await request(`/api/subscriptions/${subscription.id}/activate`, 'POST');
    const participants = await Promise.all([1, 2].map(index =>
      request<{ id: string }>('/api/participants/teams', 'POST', {
        tenantId: tenant.id,
        name: `Schema Team ${suffix} ${index}`,
        shortName: `DS${index}`,
      }),
    ));
    for (const sport of sports) {
      const contest = await request<{ id: string }>('/api/contests', 'POST', {
        tenantId: tenant.id,
        sportId: sport.sportId,
        contestShape: 'TeamVsTeam',
        name: `${sport.code} schema acceptance`,
      });
      for (const participant of participants) {
        await request(`/api/contests/${contest.id}/participants`, 'POST', {
          participantId: participant.id,
          participantType: 'Team',
        });
      }
    }
    return tenant;
  }, prototypes.map(({ sportId, code }) => ({ sportId, code })));

  for (const prototype of prototypes) {

    await page.goto(`${adminUrl}/scoring/${prototype.route}`);
    await page.getByTestId('dynamic-scoring-console')
      .locator('select')
      .first()
      .selectOption({ label: fixture.name });

    const schema = page.getByTestId('scoring-schema-source');
    await expect(schema).toHaveAttribute('data-sport-code', prototype.code);
    const schemaActionCount = Number(await schema.getAttribute('data-action-count'));
    expect(schemaActionCount).toBeGreaterThanOrEqual(prototype.actions.length);
    await expect(page.getByTestId('contest-live-status')).toHaveText('Live');
    await expect(page.getByTestId('scoring-correction-access'))
      .toHaveText('Corrections: permitted');

    for (const action of prototype.actions) {
      await expect(page.locator(`[data-action-code="${action}"]`).first()).toBeVisible();
    }
  }
});

test('scoring correction controls follow the signed-in user permissions', async ({ page }) => {
  await signIn(page, 'scorer@premier.com', 'Scorer123!');
  await page.goto(`${adminUrl}/scoring/football`);

  await expect(page.getByTestId('scoring-schema-source'))
    .toHaveAttribute('data-sport-code', 'football');
  await expect(page.getByTestId('scoring-correction-access'))
    .toHaveText('Corrections: not permitted');
  await expect(page.getByRole('button', { name: 'Correct', exact: true })).toHaveCount(0);
  await expect(page.getByRole('button', { name: 'Reverse', exact: true })).toHaveCount(0);
  await expect(page.getByRole('button', { name: 'Replace', exact: true })).toHaveCount(0);
});

test('platform administrator reviews operations and confirms an audited support action', async ({ page }) => {
  await signIn(page, 'admin@sportsplatform.com', 'Admin123!');
  const tenant = await page.evaluate(async () => {
    const token = localStorage.getItem('sports-platform.access-token');
    const suffix = Date.now().toString();
    const response = await fetch('/api/tenants', {
      method: 'POST',
      headers: {
        Authorization: `Bearer ${token}`,
        Accept: 'application/json',
        'Content-Type': 'application/json',
      },
      body: JSON.stringify({
        name: `Browser Support ${suffix}`,
        shortName: 'BS',
        slug: `browser-support-${suffix}`,
      }),
    });
    if (!response.ok) throw new Error(await response.text());
    return response.json() as Promise<{ id: string; name: string; slug: string }>;
  });

  await page.getByRole('button', { name: 'Refresh' }).click();
  await expect(page.getByRole('heading', { name: 'Platform administration' })).toBeVisible();
  await expect(page.getByRole('heading', { name: 'System health' })).toBeVisible();
  await expect(page.getByRole('heading', { name: 'Support investigation' })).toBeVisible();
  await expect(page.getByRole('heading', { name: 'Tenant operations' })).toBeVisible();
  await expect(page.getByRole('heading', { name: 'Failed jobs' })).toBeVisible();
  await expect(page.getByRole('heading', { name: 'Mobile sync conflicts' })).toBeVisible();
  await expect(page.getByRole('heading', { name: 'Contest event history' })).toBeVisible();
  await expect(page.getByRole('heading', { name: 'Recent audit review' })).toBeVisible();

  let tenantCard = page.getByTestId(`platform-tenant-${tenant.slug}`);
  await expect(tenantCard).toContainText('Active');
  await tenantCard.getByLabel(`Support reason for ${tenant.name}`)
    .fill('Browser support suspension verification');
  page.once('dialog', (dialog) => dialog.accept());
  await tenantCard.getByRole('button', { name: 'Suspend tenant' }).click();
  await expect(page.getByRole('status')).toContainText('completed and audited');
  tenantCard = page.getByTestId(`platform-tenant-${tenant.slug}`);
  await expect(tenantCard).toContainText('Suspended');
  await expect(page.getByText('Browser support suspension verification')).toBeVisible();

  await tenantCard.getByLabel(`Support reason for ${tenant.name}`)
    .fill('Browser support verification complete');
  page.once('dialog', (dialog) => dialog.accept());
  await tenantCard.getByRole('button', { name: 'Activate tenant' }).click();
  tenantCard = page.getByTestId(`platform-tenant-${tenant.slug}`);
  await expect(tenantCard).toContainText('Active');

  await tenantCard.getByLabel(`Notification reason for ${tenant.name}`)
    .fill('Browser notification pause verification');
  page.once('dialog', (dialog) => dialog.accept());
  await tenantCard.getByRole('button', { name: 'Pause 4 hours' }).click();
  await expect(page.getByRole('status')).toContainText('notification delivery paused and audited');
  tenantCard = page.getByTestId(`platform-tenant-${tenant.slug}`);
  await expect(tenantCard).toContainText('Paused until');
  await tenantCard.getByLabel(`Notification reason for ${tenant.name}`)
    .fill('Browser notification pause verification complete');
  page.once('dialog', (dialog) => dialog.accept());
  await tenantCard.getByRole('button', { name: 'Resume delivery' }).click();
  await expect(page.getByRole('status')).toContainText('notification delivery resumed and audited');
});

test('tenant administration applies branding and remains usable on desktop and tablet', async ({ page }) => {
  await page.setViewportSize({ width: 1440, height: 900 });
  await signIn(page, 'scorer@premier.com', 'Scorer123!');

  const dashboard = page.getByTestId('tenant-administration-dashboard');
  await expect(dashboard).toBeVisible();
  await expect(dashboard.getByText('Live contests', { exact: true }).first()).toBeVisible();
  await expect(dashboard.getByText('Results', { exact: true }).first()).toBeVisible();
  await expect(dashboard.getByText('Sponsors', { exact: true }).first()).toBeVisible();

  const expectedPrimary = await page.evaluate(async () => {
    const token = localStorage.getItem('sports-platform.access-token');
    const response = await fetch('/api/tenant-admin/dashboard', {
      headers: { Authorization: `Bearer ${token}`, Accept: 'application/json' },
    });
    if (!response.ok) throw new Error(await response.text());
    const payload = await response.json() as { branding: { primaryColor?: string | null } };
    const color = payload.branding.primaryColor ?? '#0EA5E9';
    return `${parseInt(color.slice(1, 3), 16)} ${parseInt(color.slice(3, 5), 16)} ${parseInt(color.slice(5, 7), 16)}`;
  });
  await expect.poll(() => page.evaluate(() =>
    document.documentElement.style.getPropertyValue('--brand-primary').trim(),
  )).toBe(expectedPrimary);

  const sidebar = page.locator('aside');
  await expect(sidebar.getByText('Tenants', { exact: true })).toHaveCount(0);
  await expect(sidebar.getByText('Branding', { exact: true })).toHaveCount(0);
  expect(await page.evaluate(() => document.body.scrollWidth <= window.innerWidth)).toBe(true);

  await page.setViewportSize({ width: 768, height: 1024 });
  await expect(dashboard).toBeVisible();
  await expect(page.getByRole('heading', { name: 'Upcoming fixtures' })).toBeVisible();
  expect(await page.evaluate(() => document.body.scrollWidth <= window.innerWidth)).toBe(true);
});

test('tenant scorer can start a session and persist a score event', async ({ page }) => {
  await signIn(page, 'scorer@premier.com', 'Scorer123!');
  await page.goto(`${adminUrl}/scoring`);
  await expect(page.getByRole('heading', { name: 'Tenant contests' })).toBeVisible();
  await expect(page.getByText(/participants/).first()).toBeVisible();

  const start = page.getByRole('button', { name: 'Start', exact: true });
  if (await start.isVisible()) {
    await start.click();
  }

  const addScore = page.getByRole('button', { name: 'Goal' }).first();
  await expect(addScore).toBeEnabled();
  await addScore.click();
  await expect(page.getByText('Recent persisted events')).toBeVisible();

  const end = page.getByRole('button', { name: 'End', exact: true });
  if (await end.isVisible()) {
    await end.click();
  }
});

test('tenant scorer UI fails closed when its tenant identifier is tampered with', async ({ page }) => {
  const nbaTenantId = '00000000-0000-0000-0000-000000000003';

  await signIn(page, 'scorer@premier.com', 'Scorer123!');
  await page.goto(`${adminUrl}/competitions`);
  await expect(page.getByRole('heading', { name: 'Competitions' })).toBeVisible();

  const denied = page.waitForResponse(
    (response) =>
      response.url().includes(`/api/competitions?tenantId=${nbaTenantId}`) &&
      response.status() === 403,
  );
  await page.getByPlaceholder('Tenant ID').fill(nbaTenantId);
  await denied;
  await expect(page.getByText('Request failed (403)')).toBeVisible();
});

test('tenant administrator is isolated and publishes tenant branding', async ({ page }) => {
  await signIn(page, 'admin@premier.com', 'TenantAdmin123!');
  await page.goto(`${adminUrl}/tenants`);
  await expect(page.getByRole('heading', { name: 'Premier League' })).toBeVisible();
  await expect(page.getByRole('heading', { name: 'NBA' })).toHaveCount(0);

  const deniedStatus = await page.evaluate(async () => {
    const token = localStorage.getItem('sports-platform.access-token');
    return (await fetch(
      '/api/competitions?tenantId=00000000-0000-0000-0000-000000000003',
      { headers: { Authorization: `Bearer ${token}` } },
    )).status;
  });
  expect(deniedStatus).toBe(403);

  await page.goto(`${adminUrl}/subscription`);
  await expect(page.getByRole('heading', { name: 'Subscription' })).toBeVisible();
  await expect(page.locator('#subscription-tenant option')).toHaveCount(1);
  await expect(page.getByText('Professional', { exact: true })).toBeVisible();
  await expect(page.getByText('Feature entitlements')).toBeVisible();
  await expect(page.getByText('Competitions', { exact: true }).last()).toBeVisible();

  await page.goto(`${adminUrl}/branding`);
  await expect(page.getByRole('heading', { name: 'Branding' })).toBeVisible();
  await expect(page.locator('#tenant option')).toHaveCount(1);
  await page.getByLabel('Tenant name').fill('Premier League');
  await page.getByLabel('Short name').fill('PL');
  const primaryHex = page.getByLabel('Primary color hex');
  const secondaryHex = page.getByLabel('Secondary color hex');
  const accentHex = page.getByLabel('Accent color hex');

  await primaryHex.fill('#3d195b');
  await secondaryHex.fill('#00ff85');
  await accentHex.fill('#04f5ff');

  await expect(primaryHex).toHaveValue('#3d195b');
  await expect(secondaryHex).toHaveValue('#00ff85');
  await expect(accentHex).toHaveValue('#04f5ff');

  const saved = page.waitForResponse(response =>
    response.url().includes('/api/branding/tenant/') &&
    response.request().method() === 'PUT' && response.status() === 200,
  );
  await page.getByRole('button', { name: 'Save branding' }).click();
  const savedResponse = await saved;
  const savedBranding = await savedResponse.json() as {
    primaryColor?: string | null;
    secondaryColor?: string | null;
    accentColor?: string | null;
  };

  expect(savedBranding.primaryColor).toBe('#3D195B');
  expect(savedBranding.secondaryColor).toBe('#00FF85');
  expect(savedBranding.accentColor).toBe('#04F5FF');

  await expect(page.getByText('Branding saved and available to public and mobile clients.')).toBeVisible();

  const publicBrandingResponse = await page.request.get(
    'http://localhost:8080/api/public/tenants/premier-league',
  );
  expect(publicBrandingResponse.status()).toBe(200);
  const publicBranding = await publicBrandingResponse.json() as {
    branding?: {
      primaryColor?: string | null;
      secondaryColor?: string | null;
      accentColor?: string | null;
    } | null;
  };
  expect(publicBranding.branding?.primaryColor).toBe('#3D195B');
  expect(publicBranding.branding?.secondaryColor).toBe('#00FF85');
  expect(publicBranding.branding?.accentColor).toBe('#04F5FF');

  await page.goto(`${publicUrl}/?tenant=premier-league`);
  await expect(page.getByText('Premier League').first()).toBeVisible();
  await expect(page.locator('html')).toHaveCSS('--brand-primary', '#3D195B');
});

test('football console uses the tenant contest and persists a goal', async ({ page }) => {
  await signIn(page, 'scorer@premier.com', 'Scorer123!');
  await page.goto(`${adminUrl}/scoring/football`);
  await expect(page.getByRole('heading', { name: 'Football Scoring' })).toBeVisible();
  await expect(page.getByRole('heading', { name: 'Football contests' })).toBeVisible();
  await expect(page.getByText(/participants/).first()).toBeVisible();

  const start = page.getByRole('button', { name: 'Start', exact: true });
  if (await start.isVisible()) {
    await start.click();
  }

  const goal = page.getByRole('button', { name: 'Goal' }).first();
  await expect(goal).toBeEnabled();
  await goal.click();
  await expect(page.getByText('Recent persisted events')).toBeVisible();

  const end = page.getByRole('button', { name: 'End', exact: true });
  if (await end.isVisible()) {
    await end.click();
  }
});

test('football console synchronizes every match action and approved corrections across clients', async ({ browser, page }) => {
  test.setTimeout(180_000);
  await signIn(page, 'admin@sportsplatform.com', 'Admin123!');

  const match = await page.evaluate(async () => {
    const apiUrl = '';
    const token = localStorage.getItem('sports-platform.access-token');
    if (!token) throw new Error('Administrator session is unavailable.');
    const headers = {
      Authorization: `Bearer ${token}`,
      Accept: 'application/json',
      'Content-Type': 'application/json',
    };
    async function request<T>(path: string, method = 'GET', body?: unknown) {
      const response = await fetch(`${apiUrl}${path}`, {
        method,
        headers,
        body: body === undefined ? undefined : JSON.stringify(body),
      });
      const text = await response.text();
      if (!response.ok) {
        throw new Error(`${method} ${path} failed (${response.status}): ${text}`);
      }
      return text ? JSON.parse(text) as T : undefined as T;
    }

    const tenantId = '00000000-0000-0000-0000-000000000002';
    const sportId = '00000000-0000-0000-0000-000000000100';
    const suffix = Date.now().toString();
    const competition = await request<{ id: string }>('/api/competitions', 'POST', {
      name: `Live Football Browser ${suffix}`,
      tenantId,
      sportId,
    });
    const teams = await Promise.all([1, 2].map(index =>
      request<{ id: string; name: string }>('/api/participants/teams', 'POST', {
        name: `Live Browser ${suffix} Team ${index}`,
        shortName: `LB${suffix.slice(-6)}${index}`,
        tenantId,
      }),
    ));

    for (const [teamIndex, team] of teams.entries()) {
      const athletes = await Promise.all([1, 2].map(playerIndex =>
        request<{ id: string }>('/api/participants/athletes', 'POST', {
          name: `Live Browser ${suffix} T${teamIndex + 1} Player ${playerIndex}`,
          shortName: `LP${suffix.slice(-4)}${teamIndex + 1}${playerIndex}`,
          tenantId,
        }),
      ));

      const profile = await request<{
        rosters: Array<{ id: string }>;
      }>(`/api/participants/teams/${team.id}/rosters`, 'POST', {
        name: `Live Browser ${suffix} Match Roster`,
        season: '2026/27',
        startsAt: '2026-08-01T00:00:00Z',
        endsAt: '2027-06-30T23:59:59Z',
        competitionId: competition.id,
        reason: 'Two-client football acceptance',
      });
      const rosterId = profile.rosters[0]?.id;
      if (!rosterId) throw new Error(`Roster was not created for ${team.name}.`);

      for (const [playerIndex, athlete] of athletes.entries()) {
        await request(
          `/api/participants/teams/${team.id}/rosters/${rosterId}/athletes/${athlete.id}/transfer`,
          'POST',
          {
            jerseyNumber: String(playerIndex + 9),
            position: playerIndex === 0 ? 'Forward' : 'Midfielder',
            reason: 'Two-client football acceptance',
          },
        );
      }
    }

    const registrations = await Promise.all(teams.map(team =>
      request<{ id: string }>(
        `/api/registrations/competition/${competition.id}`,
        'POST',
        { participantId: team.id, type: 1 },
      ),
    ));
    await Promise.all(registrations.map(registration =>
      request(`/api/registrations/${registration.id}/submit`, 'POST'),
    ));
    await Promise.all(registrations.map(registration =>
      request(`/api/registrations/${registration.id}/approve`, 'POST', {
        notes: 'Two-client football acceptance',
      }),
    ));
    const schedule = await request<{
      fixtures: Array<{
        contestId: string;
        homeParticipantId: string;
        awayParticipantId: string;
      }>;
    }>(`/api/competitions/${competition.id}/fixtures/round-robin`, 'POST', {
      editionName: '2026/27',
      stageName: 'Live Match',
      startTimeUtc: '2026-09-01T15:00:00Z',
      venue: 'Browser Stadium',
      homeAndAway: false,
      daysBetweenRounds: 7,
    });
    if (schedule.fixtures.length !== 1) {
      throw new Error(`Expected one football fixture, got ${schedule.fixtures.length}.`);
    }
    return {
      contestId: schedule.fixtures[0].contestId,
      participantIds: [
        schedule.fixtures[0].homeParticipantId,
        schedule.fixtures[0].awayParticipantId,
      ],
    };
  });

  const firstContext = await browser.newContext();
  const secondContext = await browser.newContext();
  const scorer = await firstContext.newPage();
  const observer = await secondContext.newPage();
  try {
    await Promise.all([
      signIn(scorer, 'admin@premier.com', 'TenantAdmin123!'),
      signIn(observer, 'admin@premier.com', 'TenantAdmin123!'),
    ]);
    await Promise.all([
      scorer.goto(`${adminUrl}/scoring/football`),
      observer.goto(`${adminUrl}/scoring/football`),
    ]);
    await Promise.all([
      scorer.locator(`[data-contest-id="${match.contestId}"]`).click(),
      observer.locator(`[data-contest-id="${match.contestId}"]`).click(),
    ]);
    await Promise.all([
      expect(scorer.getByTestId('contest-live-status')).toHaveText('Live'),
      expect(observer.getByTestId('contest-live-status')).toHaveText('Live'),
    ]);

    await scorer.getByRole('button', { name: 'Start', exact: true }).click();
    await expect(observer.getByText('Session: Active')).toBeVisible();

    const [homeId, awayId] = match.participantIds;
    for (const participantId of [homeId, awayId]) {
      const playerSelect = scorer.getByTestId(`player-select-${participantId}`);
      const incomingSelect = scorer.getByTestId(`incoming-player-select-${participantId}`);
      await expect(playerSelect.locator('option')).toHaveCount(3);
      await expect(incomingSelect.locator('option')).toHaveCount(3);
      await playerSelect.selectOption({ index: 1 });
      await incomingSelect.selectOption({ index: 2 });
    }

    const action = (participantId: string, code: string) =>
      scorer.locator(
        `[data-action-code="${code}"][data-participant-id="${participantId}"]`,
      ).first();

    const ensurePlayerSelections = async (participantId: string) => {
      const playerSelect = scorer.getByTestId(`player-select-${participantId}`);
      if (await playerSelect.count()) {
        const selected = await playerSelect.inputValue();
        if (!selected) {
          await expect(playerSelect.locator('option')).toHaveCount(3);
          await playerSelect.selectOption({ index: 1 });
        }
      }

      const incomingSelect = scorer.getByTestId(`incoming-player-select-${participantId}`);
      if (await incomingSelect.count()) {
        const selectedIncoming = await incomingSelect.inputValue();
        if (!selectedIncoming) {
          await expect(incomingSelect.locator('option')).toHaveCount(3);
          await incomingSelect.selectOption({ index: 2 });
        }
      }
    };

    const clickAction = async (participantId: string, code: string) => {
      const control = action(participantId, code);
      await expect(
        control,
        `Expected ${code} action for participant ${participantId}`,
      ).toBeVisible({ timeout: 10_000 });

      if (await control.isDisabled()) {
        await ensurePlayerSelections(participantId);
      }

      await expect(
        control,
        `Expected ${code} action to become enabled for participant ${participantId}`,
      ).toBeEnabled({ timeout: 30_000 });

      const responsePromise = scorer.waitForResponse(response =>
        new URL(response.url()).pathname ===
          `/api/livescoring/contests/${match.contestId}/events`
        && response.request().method() === 'POST',
      );

      await control.click({ timeout: 15_000 });
      let response = await responsePromise;
      let retryCount = 0;

      while (response.status() === 429 && retryCount < 4) {
        retryCount += 1;

        const retryAfterHeader = response.headers()['retry-after'];
        const retryAfterSeconds = Number.parseFloat(retryAfterHeader ?? '');
        const retryDelayMs = Number.isFinite(retryAfterSeconds)
          ? Math.max(250, Math.ceil(retryAfterSeconds * 1000))
          : 1_000;

        await scorer.waitForTimeout(retryDelayMs);

        const retryResponsePromise = scorer.waitForResponse(candidate =>
          candidate.request().method() === 'POST' &&
          new URL(candidate.url()).pathname ===
            `/api/livescoring/contests/${match.contestId}/events`,
        );

        await expect(control).toBeEnabled({ timeout: 10_000 });
        await control.click({ timeout: 15_000 });
        response = await retryResponsePromise;
      }

      if (response.status() !== 200) {
        throw new Error(
          `${code} event failed (${response.status()}): ${await response.text()}`,
        );
      }
    };
    const score = (target: import('@playwright/test').Page, participantId: string) =>
      target.getByTestId(`participant-score-${participantId}`);

    const schema = await scorer.evaluate(async (contestId) => {
      const response = await fetch(`/api/livescoring/contests/${contestId}/schema`, {
        headers: {
          Authorization: `Bearer ${localStorage.getItem('sports-platform.access-token')}`,
        },
      });
      if (!response.ok) {
        throw new Error(`Unable to load football scoring schema (${response.status}).`);
      }
      return await response.json() as {
        primaryMetric: string;
        actions: Array<{ code: string; metric: string; defaultValue: number }>;
      };
    }, match.contestId);

    const persistedPrimaryTotals = async () => {
      return await scorer.evaluate(async ({ contestId, primaryMetric }) => {
        let response: Response | null = null;
        for (let attempt = 0; attempt < 6; attempt += 1) {
          response = await fetch(`/api/livescoring/contests/${contestId}/scores`, {
            headers: {
              Authorization: `Bearer ${localStorage.getItem('sports-platform.access-token')}`,
            },
          });
          if (response.status !== 429) break;

          const retryAfter = Number(response.headers.get('Retry-After') ?? '1');
          const delayMs = Number.isFinite(retryAfter)
            ? Math.max(250, retryAfter * 1000)
            : 1000;
          await new Promise(resolve => setTimeout(resolve, delayMs));
        }

        if (!response?.ok) {
          throw new Error(
            `Unable to load persisted football scores (${response?.status ?? 'no response'}).`,
          );
        }

        const scores = await response.json() as Array<{
          participantId: string;
          metricName: string;
          value: number;
        }>;
        return scores
          .filter(item => item.metricName === primaryMetric)
          .reduce<Record<string, number>>((totals, item) => {
            totals[item.participantId] =
              (totals[item.participantId] ?? 0) + Number(item.value);
            return totals;
          }, {});
      }, { contestId: match.contestId, primaryMetric: schema.primaryMetric });
    };

    const homeCodes = [
      'goal',
      'own-goal',
      'penalty-goal',
      'yellow-card',
      'second-yellow',
      'red-card',
      'substitution-in',
    ];
    const awayCodes = ['goal', 'own-goal', 'penalty-goal'];

    for (const code of homeCodes) {
      await clickAction(homeId, code);
    }
    for (const code of awayCodes) {
      await clickAction(awayId, code);
    }
    await expect(
      observer.locator(`[data-participant-id="${homeId}"][data-metric-name="yellow_cards"]`).first(),
    ).toBeVisible();
    const regulationTotals = await persistedPrimaryTotals();
    await expect(score(observer, homeId)).toHaveText(String(regulationTotals[homeId] ?? 0));
    await expect(score(observer, awayId)).toHaveText(String(regulationTotals[awayId] ?? 0));

    await scorer.getByRole('button', { name: 'Next segment' }).click();
    await expect(observer.getByTestId('contest-segment').locator('option:checked'))
      .toContainText('Second Half');
    // Extra Time is conditional: regulation must finish level.
    // The earlier "every action" coverage intentionally exercises events such
    // as own goals, so balance the persisted primary score before activating it.
    let totalsBeforeExtraTime = await persistedPrimaryTotals();

    for (let attempt = 0; attempt < 8; attempt += 1) {
      const homeTotal = totalsBeforeExtraTime[homeId] ?? 0;
      const awayTotal = totalsBeforeExtraTime[awayId] ?? 0;
      if (homeTotal === awayTotal) break;

      await clickAction(
        homeTotal < awayTotal ? homeId : awayId,
        'goal',
      );
      totalsBeforeExtraTime = await persistedPrimaryTotals();
    }

    const homeBeforeExtraTime = totalsBeforeExtraTime[homeId] ?? 0;
    const awayBeforeExtraTime = totalsBeforeExtraTime[awayId] ?? 0;
    await expect(score(observer, homeId)).toHaveText(String(homeBeforeExtraTime));
    await expect(score(observer, awayId)).toHaveText(String(awayBeforeExtraTime));
    expect(
      homeBeforeExtraTime,
      `Regulation must be tied before Extra Time (home=${homeBeforeExtraTime}, away=${awayBeforeExtraTime})`,
    ).toBe(awayBeforeExtraTime);

    await scorer.getByTestId('contest-segment').selectOption({ label: 'Extra Time (optional)' });
    await expect(observer.getByTestId('contest-segment').locator('option:checked'))
      .toContainText('Extra Time');
    await scorer.getByTestId('contest-segment').selectOption({ label: 'Penalty Shootout (optional)' });
    await expect(observer.getByTestId('contest-segment').locator('option:checked'))
      .toContainText('Penalty Shootout');

    for (const participantId of [homeId, awayId]) {
      await clickAction(participantId, 'penalty-scored');
      await clickAction(participantId, 'penalty-missed');
    }
    const shootoutScoredMetric = schema.actions.find(
      action => action.code === 'penalty-scored',
    )?.metric;
    const shootoutMissedMetric = schema.actions.find(
      action => action.code === 'penalty-missed',
    )?.metric;
    expect(shootoutScoredMetric, 'penalty-scored metric must exist').toBeTruthy();
    expect(shootoutMissedMetric, 'penalty-missed metric must exist').toBeTruthy();

    await expect(
      observer.locator(`[data-metric-name="${shootoutScoredMetric}"]`),
    ).toHaveCount(2);
    await expect(
      observer.locator(`[data-metric-name="${shootoutMissedMetric}"]`),
    ).toHaveCount(2);

    await scorer.getByTestId('contest-segment').selectOption({ label: 'Extra Time (optional)' });
    await clickAction(homeId, 'goal');
    await clickAction(awayId, 'goal');
    const extraTimeTotals = await persistedPrimaryTotals();
    const homeAfterExtraTime = extraTimeTotals[homeId] ?? 0;
    const awayAfterExtraTime = extraTimeTotals[awayId] ?? 0;
    await expect(score(observer, homeId)).toHaveText(String(homeAfterExtraTime));
    await expect(score(observer, awayId)).toHaveText(String(awayAfterExtraTime));

    const correctedEvent = scorer
      .locator(`[data-metric-name="score"][data-participant-id="${homeId}"]`)
      .first();
    const correctionAnswers = ['1', 'Two-client correction'];
    scorer.on('dialog', async dialog => dialog.accept(correctionAnswers.shift() ?? ''));
    await correctedEvent.getByRole('button', { name: 'Correct' }).click();
    scorer.removeAllListeners('dialog');
    const pendingCorrection = scorer
      .locator('[data-testid^="score-correction-"]')
      .filter({ hasText: 'Pending' })
      .first();
    await expect(pendingCorrection).toBeVisible();
    scorer.once('dialog', dialog => dialog.accept('Approved in browser acceptance'));
    await pendingCorrection.getByRole('button', { name: 'Approve' }).click();
    await expect(score(observer, homeId)).toHaveText(String(homeAfterExtraTime));

    scorer.once('dialog', dialog => dialog.accept('Approved reversal coverage'));
    await correctedEvent.getByRole('button', { name: 'Reverse' }).click();
    const pendingApprovedReversal = scorer
      .locator('[data-testid^="score-correction-"]')
      .filter({ hasText: 'Pending' })
      .first();
    scorer.once('dialog', dialog => dialog.accept('Reversal approved'));
    await pendingApprovedReversal.getByRole('button', { name: 'Approve' }).click();
    await expect(score(observer, homeId)).toHaveText(String(homeAfterExtraTime - 1));

    const cardEvent = scorer.locator('[data-metric-name="shootout_misses"]').first();
    scorer.once('dialog', dialog => dialog.accept('Reverse action coverage'));
    await cardEvent.getByRole('button', { name: 'Reverse' }).click();
    const pendingReversal = scorer
      .locator('[data-testid^="score-correction-"]')
      .filter({ hasText: 'Pending' })
      .first();
    scorer.once('dialog', dialog => dialog.accept('Rejected for acceptance coverage'));
    await pendingReversal.getByRole('button', { name: 'Reject' }).click();

    const replaceableEvent = scorer
      .locator('[data-testid^="score-event-"]')
      .filter({ has: scorer.getByRole('button', { name: 'Replace' }) })
      .first();
    await expect(replaceableEvent).toBeVisible();
    await expect(
      replaceableEvent.getByRole('button', { name: 'Replace' }),
    ).toBeEnabled();
    const replacementAnswers = [
      'yellow_cards',
      '1',
      'Replacement action coverage',
    ];
    scorer.on('dialog', async dialog => dialog.accept(replacementAnswers.shift() ?? ''));
    await replaceableEvent.getByRole('button', { name: 'Replace' }).click();
    scorer.removeAllListeners('dialog');
    const pendingReplacement = scorer
      .locator('[data-testid^="score-correction-"]')
      .filter({ hasText: 'Pending' })
      .first();
    scorer.once('dialog', dialog => dialog.accept('Replacement approved'));
    await pendingReplacement.getByRole('button', { name: 'Approve' }).click();
    await expect(observer.locator('[data-metric-name="yellow_cards"]')).toBeVisible();
  } finally {
    await Promise.allSettled([
      firstContext.close(),
      secondContext.close(),
    ]);
  }
});

test('administrator creates registered teams and persisted double round-robin fixtures through the wizard', async ({ page }) => {
  test.setTimeout(300_000);
  const competitionName = `Browser Competition ${Date.now()}`;

  await signIn(page, 'admin@premier.com', 'TenantAdmin123!');
  await page.goto(`${adminUrl}/competitions/new`);
  await page.getByRole('button', { name: 'Validate and continue' }).click();
  await expect(page.locator('p[role="alert"]')).toContainText('competition name and edition');
  await page.getByLabel('Competition name').fill(competitionName);
  await page.getByLabel('Edition / season').fill('2026/27');
  await page.getByRole('button', { name: 'Save draft' }).click();
  await page.reload();
  await expect(page.getByLabel('Competition name')).toHaveValue(competitionName);
  await expect(page.getByRole('status')).toContainText('Saved draft restored');
  await page.getByRole('button', { name: 'Validate and continue' }).click();
  await page.getByRole('button', { name: /Football/ }).click();
  await page.getByRole('button', { name: 'Validate and continue' }).click();
  const publishedRuleset = page.getByRole('button', { name: /Published.*version/ }).first();
  await expect(publishedRuleset).toBeVisible();
  await publishedRuleset.click();
  await page.getByRole('button', { name: 'Validate and continue' }).click();
  await page.getByRole('button', { name: /Round robin/ }).click();
  await page.getByRole('button', { name: 'Validate and continue' }).click();
  await page.getByRole('button', { name: 'Validate and continue' }).click();
  await page.getByRole('button', { name: 'Validate and continue' }).click();
  await page.getByLabel('Start date').fill('2026-09-01');
  await page.getByLabel('End date').fill('2027-04-30');
  await page.getByLabel('Primary venue').fill('Browser Arena');
  await page.getByRole('button', { name: 'Validate and continue' }).click();
  await page.getByRole('button', { name: 'Validate and continue' }).click();
  await page.getByRole('button', { name: /Publish after creation/ }).click();
  await page.getByRole('button', { name: 'Validate and continue' }).click();
  await expect(page.getByText('Browser Arena')).toBeVisible();
  await expect(page.getByText('Round robin').first()).toBeVisible();
  const generated = page.waitForResponse(
    (response) =>
      response.url().includes('/fixtures/round-robin') &&
      response.request().method() === 'POST',
    { timeout: 60_000 },
  );
  await page.getByRole('button', { name: 'Create and publish', exact: true }).click();
await generated;
  const fixtureResponse = await generated;
  expect(
    fixtureResponse.status(),
    `Round-robin generation failed: ${await fixtureResponse.text()}`,
  ).toBe(200);
  const fixturePayload = await fixtureResponse.json() as {
    eligibleParticipantCount: number;
    fixtureCount: number;
  };
  expect(fixturePayload.eligibleParticipantCount).toBe(8);
  expect(fixturePayload.fixtureCount).toBe(56);

  await expect(page).toHaveURL(`${adminUrl}/competitions`);
  await expect(page.getByRole('heading', { name: competitionName })).toBeVisible();
});

test('fresh football league advances its top four and publishes a champion', async ({ page }) => {
  test.setTimeout(120_000);
  await signIn(page, 'admin@sportsplatform.com', 'Admin123!');

  const completed = await page.evaluate(async () => {
    const apiUrl = '';
    const token = localStorage.getItem('sports-platform.access-token');
    if (!token) throw new Error('Administrator session is unavailable.');
    const headers = {
      Authorization: `Bearer ${token}`,
      Accept: 'application/json',
      'Content-Type': 'application/json',
    };
    async function request<T>(path: string, method = 'GET', body?: unknown) {
      const response = await fetch(`${apiUrl}${path}`, {
        method,
        headers,
        body: body === undefined ? undefined : JSON.stringify(body),
      });
      const text = await response.text();
      if (!response.ok) {
        throw new Error(`${method} ${path} failed (${response.status}): ${text}`);
      }
      return text ? JSON.parse(text) as T : undefined as T;
    }

    const tenantId = '00000000-0000-0000-0000-000000000002';
    const sportId = '00000000-0000-0000-0000-000000000100';
    const suffix = Date.now().toString();
    const competition = await request<{ id: string }>('/api/competitions', 'POST', {
      name: `Browser Championship ${suffix}`,
      tenantId,
      sportId,
    });
    const teams = await Promise.all(Array.from({ length: 8 }, async (_, index) =>
      request<{ id: string; name: string }>('/api/participants/teams', 'POST', {
        name: `Browser Finals ${suffix} Team ${index + 1}`,
        shortName: `BF${suffix.slice(-6)}${index + 1}`,
        tenantId,
      }),
    ));
    const registrations = await Promise.all(teams.map(team =>
      request<{ id: string }>(
        `/api/registrations/competition/${competition.id}`,
        'POST',
        { participantId: team.id, type: 1 },
      ),
    ));
    await Promise.all(registrations.map(registration =>
      request(`/api/registrations/${registration.id}/submit`, 'POST'),
    ));
    await Promise.all(registrations.map(registration =>
      request(`/api/registrations/${registration.id}/approve`, 'POST', {
        notes: 'Browser acceptance',
      }),
    ));
    const schedule = await request<{
      fixtures: Array<{
        contestId: string;
        homeParticipantId: string;
        awayParticipantId: string;
      }>;
    }>(`/api/competitions/${competition.id}/fixtures/round-robin`, 'POST', {
      editionName: '2026/27',
      stageName: 'League',
      startTimeUtc: '2026-09-01T15:00:00Z',
      venue: 'Browser Stadium',
      homeAndAway: true,
      daysBetweenRounds: 7,
    });
    await request(`/api/competitions/${competition.id}/publish`, 'POST');

    async function scoreAndApprove(
      contestId: string,
      expectedParticipants?: [string, string],
    ) {
      const contest = await request<{
        participants: Array<{ participantId: string }>;
      }>(`/api/contests/${contestId}`);
      const participants = contest.participants.map(item => item.participantId);
      if (participants.length !== 2) {
        throw new Error(`Contest ${contestId} does not have two participants.`);
      }
      if (expectedParticipants &&
          !expectedParticipants.every(participant => participants.includes(participant))) {
        throw new Error(`Contest ${contestId} has unexpected participants.`);
      }
      for (const participantId of [participants[0], participants[0], participants[1]]) {
        await request(`/api/livescoring/contests/${contestId}/events`, 'POST', {
          eventId: crypto.randomUUID(),
          participantId,
          metricName: 'score',
          value: 1,
          scoredBy: 'browser-championship',
        });
      }
      const submitted = await request<{
        results: Array<{ id: string }>;
      }>(`/api/results/contest/${contestId}/submit`, 'POST');
      for (const result of submitted.results) {
        await request(`/api/results/${result.id}/approve`, 'POST', {
          approvedBy: crypto.randomUUID(),
        });
      }
      return participants[0];
    }

    for (const fixture of schedule.fixtures.slice(0, 4)) {
      await scoreAndApprove(
        fixture.contestId,
        [fixture.homeParticipantId, fixture.awayParticipantId],
      );
    }
    const playoffs = await request<{
      semiFinalContestIds: string[];
      finalContestId: string;
    }>(`/api/competitions/${competition.id}/playoffs/top-four`, 'POST', {
      bracketName: 'Browser Championship Playoffs',
      stageName: 'Top Four',
      startTimeUtc: '2027-06-01T15:00:00Z',
      venue: 'Browser Finals Stadium',
      daysUntilFinal: 7,
    });
    const finalists: string[] = [];
    for (const contestId of playoffs.semiFinalContestIds) {
      finalists.push(await scoreAndApprove(contestId));
    }
    const championId = await scoreAndApprove(
      playoffs.finalContestId,
      [finalists[0], finalists[1]],
    );
    return {
      competitionId: competition.id,
      championName: teams.find(team => team.id === championId)?.name,
    };
  });

  expect(completed.championName).toBeTruthy();
  await page.goto(
    `${publicUrl}/playoffs?tenant=premier-league&competitionId=${completed.competitionId}`,
  );
  await expect(page.getByRole('heading', { name: 'Playoffs' })).toBeVisible();
  await expect(page.getByTestId('playoff-champion')).toHaveText(
    `Champion: ${completed.championName}`,
  );
  await expect(page.getByRole('heading', { name: 'Semifinals' })).toBeVisible();
  await expect(page.getByRole('heading', { name: 'Final', exact: true })).toBeVisible();
});

test('fresh basketball league completes best-of-three semifinals with overtime', async ({ page }) => {
  test.setTimeout(180_000);
  await signIn(page, 'admin@sportsplatform.com', 'Admin123!');

  const completed = await page.evaluate(async () => {
    const apiUrl = '';
    const token = localStorage.getItem('sports-platform.access-token');
    if (!token) throw new Error('Administrator session is unavailable.');
    const headers = {
      Authorization: `Bearer ${token}`,
      Accept: 'application/json',
      'Content-Type': 'application/json',
    };
    async function request<T>(path: string, method = 'GET', body?: unknown) {
      const response = await fetch(`${apiUrl}${path}`, {
        method,
        headers,
        body: body === undefined ? undefined : JSON.stringify(body),
      });
      const text = await response.text();
      if (!response.ok) {
        throw new Error(`${method} ${path} failed (${response.status}): ${text}`);
      }
      return text ? JSON.parse(text) as T : undefined as T;
    }

    const tenantId = '00000000-0000-0000-0000-000000000003';
    const sportId = '00000000-0000-0000-0000-000000000101';
    const suffix = Date.now().toString();
    const competition = await request<{ id: string }>('/api/competitions', 'POST', {
      name: `Browser Basketball Championship ${suffix}`,
      tenantId,
      sportId,
    });
    const teams = await Promise.all(Array.from({ length: 6 }, async (_, index) =>
      request<{ id: string; name: string }>('/api/participants/teams', 'POST', {
        name: `Browser Basketball ${suffix} Team ${index + 1}`,
        shortName: `BB${suffix.slice(-6)}${index + 1}`,
        tenantId,
      }),
    ));
    const registrations = await Promise.all(teams.map(team =>
      request<{ id: string }>(
        `/api/registrations/competition/${competition.id}`,
        'POST',
        { participantId: team.id, type: 1 },
      ),
    ));
    await Promise.all(registrations.map(registration =>
      request(`/api/registrations/${registration.id}/submit`, 'POST'),
    ));
    await Promise.all(registrations.map(registration =>
      request(`/api/registrations/${registration.id}/approve`, 'POST', {
        notes: 'Basketball browser acceptance',
      }),
    ));
    const schedule = await request<{
      fixtures: Array<{ contestId: string }>;
    }>(`/api/competitions/${competition.id}/fixtures/round-robin`, 'POST', {
      editionName: '2026/27',
      stageName: 'Regular Season',
      startTimeUtc: '2026-09-01T15:00:00Z',
      venue: 'Browser Arena',
      homeAndAway: false,
      daysBetweenRounds: 1,
    });
    if (schedule.fixtures.length !== 15) {
      throw new Error(`Expected 15 regular-season games, got ${schedule.fixtures.length}.`);
    }
    await request(`/api/competitions/${competition.id}/publish`, 'POST');

    async function scoreAndApprove(
      contestId: string,
      desiredWinnerId?: string,
      useOvertime = false,
    ) {
      const contest = await request<{
        participants: Array<{ participantId: string }>;
        segments: Array<{ id: string; sortOrder: number }>;
      }>(`/api/contests/${contestId}`);
      const participants = contest.participants.map(item => item.participantId);
      if (participants.length !== 2 || contest.segments.length !== 5) {
        throw new Error(`Basketball contest ${contestId} is not fully configured.`);
      }
      const winnerId = desiredWinnerId ?? participants[0];
      const loserId = participants.find(id => id !== winnerId);
      if (!loserId) throw new Error('A second basketball participant is required.');
      const segments = [...contest.segments].sort((a, b) => a.sortOrder - b.sortOrder);
      let segmentId = segments[0].id;

      if (useOvertime) {
        await request(
          `/api/livescoring/contests/${contestId}/session/start`,
          'POST',
          { eventId: crypto.randomUUID(), name: 'Browser overtime' },
        );
        for (let quarter = 1; quarter < 4; quarter += 1) {
          await request(
            `/api/livescoring/contests/${contestId}/session/segment/next`,
            'POST',
          );
        }
        segmentId = segments[3].id;
      }
      for (const score of [
        { participantId: winnerId, value: 2 },
        { participantId: loserId, value: useOvertime ? 2 : 1 },
      ]) {
        await request(`/api/livescoring/contests/${contestId}/events`, 'POST', {
          eventId: crypto.randomUUID(),
          participantId: score.participantId,
          metricName: 'score',
          value: score.value,
          scoredBy: 'browser-basketball',
          segmentId,
          clockSeconds: 0,
        });
      }
      if (useOvertime) {
        await request(
          `/api/livescoring/contests/${contestId}/session/context`,
          'PUT',
          { segmentId: segments[4].id, clockSeconds: 300 },
        );
        await request(`/api/livescoring/contests/${contestId}/events`, 'POST', {
          eventId: crypto.randomUUID(),
          participantId: winnerId,
          metricName: 'score',
          value: 1,
          scoredBy: 'browser-basketball-overtime',
          segmentId: segments[4].id,
          clockSeconds: 1,
        });
      }
      const submitted = await request<{
        results: Array<{ id: string }>;
      }>(`/api/results/contest/${contestId}/submit`, 'POST');
      for (const result of submitted.results) {
        await request(`/api/results/${result.id}/approve`, 'POST', {
          approvedBy: crypto.randomUUID(),
        });
      }
      return winnerId;
    }

    for (const fixture of schedule.fixtures) {
      await scoreAndApprove(fixture.contestId);
    }
    const standings = await request<Array<{ played: number }>>(
      `/api/public/tenants/nba/standings?competitionId=${competition.id}`,
    );
    if (standings.length !== 6 || standings.some(row => row.played !== 5)) {
      throw new Error('The six-team regular season did not complete.');
    }

    const playoffs = await request<{
      semiFinalSeries: Array<{ contestIds: string[] }>;
      finalSeries: { contestIds: string[] };
    }>(`/api/competitions/${competition.id}/playoffs/top-four`, 'POST', {
      bracketName: 'Browser Basketball Playoffs',
      stageName: 'Top Four',
      startTimeUtc: '2027-05-01T15:00:00Z',
      venue: 'Browser Finals Arena',
      semifinalBestOf: 3,
      finalBestOf: 1,
      daysUntilFinal: 10,
      daysBetweenSeriesGames: 2,
    });
    const finalists: string[] = [];
    let overtimeHandled = false;
    for (const series of playoffs.semiFinalSeries) {
      const winner = await scoreAndApprove(
        series.contestIds[0],
        undefined,
        !overtimeHandled,
      );
      overtimeHandled = true;
      await scoreAndApprove(series.contestIds[1], winner);
      const unused = await request<{ status: number }>(
        `/api/contests/${series.contestIds[2]}`,
      );
      if (unused.status !== 12) throw new Error('Unneeded game three was not cancelled.');
      finalists.push(winner);
    }
    const championId = await scoreAndApprove(playoffs.finalSeries.contestIds[0]);
    if (!finalists.includes(championId)) {
      throw new Error('The champion did not advance from a semifinal series.');
    }
    return {
      competitionId: competition.id,
      championName: teams.find(team => team.id === championId)?.name,
    };
  });

  expect(completed.championName).toBeTruthy();
  await page.goto(
    `${publicUrl}/playoffs?tenant=nba&competitionId=${completed.competitionId}`,
  );
  await expect(page.getByTestId('playoff-champion')).toHaveText(
    `Champion: ${completed.championName}`,
  );
  await expect(page.getByText('Best of 3').first()).toBeVisible();
  await expect(page.getByRole('listitem').filter({ hasText: /Game 1.*won/ }).first())
    .toBeVisible();
  await expect(page.getByRole('listitem').filter({ hasText: /Game 3.*Cancelled/ }).first())
    .toBeVisible();
});

test('fresh golf tournament applies a tied cut and publishes four-round leaderboard', async ({ page }) => {
  test.setTimeout(240_000);
  await signIn(page, 'admin@sportsplatform.com', 'Admin123!');

  const completed = await page.evaluate(async () => {
    const apiUrl = '';
    const token = localStorage.getItem('sports-platform.access-token');
    if (!token) throw new Error('Administrator session is unavailable.');
    const headers = {
      Authorization: `Bearer ${token}`,
      Accept: 'application/json',
      'Content-Type': 'application/json',
    };
    async function request<T>(path: string, method = 'GET', body?: unknown) {
      const response = await fetch(`${apiUrl}${path}`, {
        method,
        headers,
        body: body === undefined ? undefined : JSON.stringify(body),
      });
      const text = await response.text();
      if (!response.ok) {
        throw new Error(`${method} ${path} failed (${response.status}): ${text}`);
      }
      return text ? JSON.parse(text) as T : undefined as T;
    }

    const tenantId = '00000000-0000-0000-0000-000000000006';
    const sportId = '00000000-0000-0000-0000-000000000102';
    const suffix = Date.now().toString();
    const competition = await request<{ id: string }>('/api/competitions', 'POST', {
      name: `Browser Golf Championship ${suffix}`,
      tenantId,
      sportId,
    });
    const golfers = await Promise.all(Array.from({ length: 24 }, async (_, index) => {
      const golfer = await request<{ id: string; name: string }>(
        '/api/participants/athletes',
        'POST',
        {
          name: `Browser Golfer ${suffix} ${String(index + 1).padStart(2, '0')}`,
          shortName: `BG${String(index + 1).padStart(2, '0')}`,
          tenantId,
        },
      );
      return { ...golfer, modifier: index === 12 ? 11 : index };
    }));
    const registrations = await Promise.all(golfers.map(golfer =>
      request<{ id: string }>(
        `/api/registrations/competition/${competition.id}`,
        'POST',
        { participantId: golfer.id, type: 0 },
      ),
    ));
    await Promise.all(registrations.map(registration =>
      request(`/api/registrations/${registration.id}/submit`, 'POST'),
    ));
    await Promise.all(registrations.map(registration =>
      request(`/api/registrations/${registration.id}/approve`, 'POST', {
        notes: 'Golf browser acceptance',
      }),
    ));
    const tournament = await request<{ contestId: string; participantCount: number }>(
      `/api/competitions/${competition.id}/golf/tournament`,
      'POST',
      {
        tournamentName: `Browser Four Round Championship ${suffix}`,
        editionName: '2026',
        stageName: 'Championship',
        startTimeUtc: '2026-10-01T08:00:00Z',
        venue: 'Browser Golf Club',
        expectedParticipants: 24,
        totalRounds: 4,
        holesPerRound: 18,
        parPerRound: 72,
        cutAfterRound: 2,
        cutToParticipants: 12,
      },
    );
    if (tournament.participantCount !== 24) {
      throw new Error('The golf field does not contain 24 players.');
    }
    await request(`/api/competitions/${competition.id}/publish`, 'POST');

    function scorecard(modifier: number) {
      return Array.from({ length: 18 }, (_, index) => {
        const hole = index + 1;
        return {
          holeNumber: hole,
          strokes: 4
            + Math.floor(modifier / 18)
            + (hole <= modifier % 18 ? 1 : 0),
          penalties: 0,
        };
      });
    }
    async function submitRound(golfer: typeof golfers[number], roundNumber: number) {
      await request(
        `/api/competitions/${competition.id}/golf/scorecards`,
        'POST',
        {
          participantId: golfer.id,
          roundNumber,
          holes: scorecard(golfer.modifier),
        },
      );
    }
    for (const golfer of golfers) {
      await submitRound(golfer, 1);
      await submitRound(golfer, 2);
    }
    const premature = await fetch(
      `${apiUrl}/api/competitions/${competition.id}/golf/scorecards`,
      {
        method: 'POST',
        headers,
        body: JSON.stringify({
          participantId: golfers[0].id,
          roundNumber: 3,
          holes: scorecard(golfers[0].modifier),
        }),
      },
    );
    if (premature.status !== 409) {
      throw new Error(`Premature round three returned ${premature.status}.`);
    }
    const cut = await request<{
      qualifiedCount: number;
      qualifiedParticipantIds: string[];
    }>(`/api/competitions/${competition.id}/golf/cut`, 'POST');
    if (cut.qualifiedCount !== 13) {
      throw new Error(`Expected 13 golfers including the cut tie, got ${cut.qualifiedCount}.`);
    }
    const qualified = new Set(cut.qualifiedParticipantIds);
    for (const golfer of golfers.filter(item => qualified.has(item.id))) {
      await submitRound(golfer, 3);
      await submitRound(golfer, 4);
    }
    const leaderboard = await request<{
      status: string;
      entries: Array<{
        name: string;
        madeCut: boolean;
        position: number;
        isTied: boolean;
      }>;
    }>(`/api/public/tenants/pga-tour/golf/leaderboard?competitionId=${competition.id}`);
    if (leaderboard.status !== 'Completed' || leaderboard.entries.length !== 24) {
      throw new Error('The public golf leaderboard is incomplete.');
    }
    if (leaderboard.entries.filter(entry => entry.madeCut).length !== 13) {
      throw new Error('The public cut field is inconsistent.');
    }
    const tied = leaderboard.entries.filter(entry => entry.isTied);
    if (tied.length < 2) throw new Error('The tied position is not represented.');
    return {
      competitionId: competition.id,
      leaderName: leaderboard.entries[0].name,
      tiedPosition: tied[0].position,
    };
  });

  await page.goto(
    `${publicUrl}/leaderboard?tenant=pga-tour&competitionId=${completed.competitionId}`,
  );
  await expect(page.getByRole('heading', { name: 'Golf leaderboard' })).toBeVisible();
  await expect(page.getByTestId('golf-leader')).toHaveText(completed.leaderName);
  await expect(page.getByText('Cut applied')).toBeVisible();
  await expect(page.getByText('Missed cut').last()).toBeVisible();
  await expect(page.getByText(`T${completed.tiedPosition}`).last()).toBeVisible();
});

test('fresh combat event publishes an eight-bout card with decision and stoppage results', async ({ page }) => {
  test.setTimeout(180_000);
  await signIn(page, 'admin@sportsplatform.com', 'Admin123!');

  const completed = await page.evaluate(async () => {
    const apiUrl = '';
    const token = localStorage.getItem('sports-platform.access-token');
    if (!token) throw new Error('Administrator session is unavailable.');
    const headers = {
      Authorization: `Bearer ${token}`,
      Accept: 'application/json',
      'Content-Type': 'application/json',
    };
    async function request<T>(path: string, method = 'GET', body?: unknown) {
      const response = await fetch(`${apiUrl}${path}`, {
        method,
        headers,
        body: body === undefined ? undefined : JSON.stringify(body),
      });
      const text = await response.text();
      if (!response.ok) {
        throw new Error(`${method} ${path} failed (${response.status}): ${text}`);
      }
      return text ? JSON.parse(text) as T : undefined as T;
    }

    const tenantId = '00000000-0000-0000-0000-000000000007';
    const sportId = '00000000-0000-0000-0000-000000000105';
    const suffix = Date.now().toString();
    const competition = await request<{ id: string }>('/api/competitions', 'POST', {
      name: `Browser Combat Night ${suffix}`,
      tenantId,
      sportId,
    });
    const fighters = await Promise.all(Array.from({ length: 16 }, async (_, index) =>
      request<{ id: string; name: string }>('/api/participants/athletes', 'POST', {
        name: `Browser Fighter ${suffix} ${String(index + 1).padStart(2, '0')}`,
        shortName: `BF${String(index + 1).padStart(2, '0')}`,
        tenantId,
      }),
    ));
    const registrations = await Promise.all(fighters.map(fighter =>
      request<{ id: string }>(
        `/api/registrations/competition/${competition.id}`,
        'POST',
        { participantId: fighter.id, type: 0 },
      ),
    ));
    await Promise.all(registrations.map(registration =>
      request(`/api/registrations/${registration.id}/submit`, 'POST'),
    ));
    await Promise.all(registrations.map(registration =>
      request(`/api/registrations/${registration.id}/approve`, 'POST', {
        notes: 'Combat browser acceptance',
      }),
    ));

    const officialSets = Array.from({ length: 8 }, (_, bout) =>
      Array.from({ length: 4 }, (_, official) => ({
        officialId: crypto.randomUUID(),
        displayName: official === 0
          ? `Browser Referee ${bout + 1}`
          : `Browser Judge ${bout + 1}-${official}`,
        role: official === 0 ? 'Referee' : 'Judge',
      })),
    );
    const weightClasses = [
      'Flyweight', 'Bantamweight', 'Featherweight', 'Lightweight',
      'Welterweight', 'Middleweight', 'Light Heavyweight', 'Heavyweight',
    ];
    const card = await request<{
      boutCount: number;
      bouts: Array<{ id: string; scheduledRounds: number }>;
    }>(`/api/competitions/${competition.id}/combat/card`, 'POST', {
      editionName: '2026',
      stageName: 'Fight Card',
      startTimeUtc: '2026-10-10T17:00:00Z',
      venue: 'Browser Combat Arena',
      minutesBetweenBouts: 30,
      bouts: Array.from({ length: 8 }, (_, index) => ({
        fighter1Id: fighters[index * 2].id,
        fighter2Id: fighters[index * 2 + 1].id,
        weightClass: weightClasses[index],
        cardSection: index < 4
          ? 'PreliminaryCard'
          : index < 6
            ? 'MainCard'
            : index === 6 ? 'CoMainEvent' : 'MainEvent',
        scheduledRounds: index === 7 ? 5 : 3,
        officials: officialSets[index],
      })),
    });
    if (card.boutCount !== 8 || card.bouts[7].scheduledRounds !== 5) {
      throw new Error('The generated combat card is incomplete.');
    }
    await request(`/api/competitions/${competition.id}/publish`, 'POST');

    const methods = [
      'Unanimous Decision',
      'Knockout',
      'Submission',
      'Technical Knockout',
      'Split Decision',
      'Knockout',
      'Submission',
      'Unanimous Decision',
    ];
    for (let index = 0; index < card.bouts.length; index += 1) {
      const method = methods[index];
      const rounds = card.bouts[index].scheduledRounds;
      const decision = method.endsWith('Decision');
      const split = method === 'Split Decision';
      const scorecards: unknown[] = decision
        ? officialSets[index].slice(1).map((official, judgeIndex) => ({
          officialId: official.officialId,
          rounds: Array.from({ length: rounds }, (_, roundIndex) => ({
            roundNumber: roundIndex + 1,
            fighter1Score: split && judgeIndex === 2 ? 9 : 10,
            fighter2Score: split && judgeIndex === 2 ? 10 : 9,
          })),
        }))
        : [];
      const winnerId = method === 'Submission'
        ? fighters[index * 2 + 1].id
        : fighters[index * 2].id;
      await request(
        `/api/competitions/${competition.id}/combat/bouts/${card.bouts[index].id}/result`,
        'POST',
        {
          winnerId,
          resultMethod: method,
          round: decision ? null : 2,
          timeInRoundSeconds: decision ? null : 145,
          scorecards,
        },
      );
    }

    const publicCard = await request<{
      boutCount: number;
      completedBoutCount: number;
      bouts: Array<{
        winnerName: string;
        resultMethod: string;
        officials: unknown[];
      }>;
    }>(`/api/public/tenants/ufc/combat/card?competitionId=${competition.id}`);
    if (publicCard.boutCount !== 8 || publicCard.completedBoutCount !== 8) {
      throw new Error('The public fight card is incomplete.');
    }
    if (publicCard.bouts.some(bout => bout.officials.length !== 4)) {
      throw new Error('The public official assignments are incomplete.');
    }
    return {
      competitionId: competition.id,
      mainEventWinner: publicCard.bouts[7].winnerName,
    };
  });

  await page.goto(
    `${publicUrl}/fight-card?tenant=ufc&competitionId=${completed.competitionId}`,
  );
  await expect(page.getByRole('heading', { name: 'Fight card' })).toBeVisible();
  await expect(page.getByText('8 of 8 bouts completed')).toBeVisible();
  await expect(page.getByTestId('fight-card').locator('article')).toHaveCount(8);
  await expect(page.getByText('Main Event · Bout 8')).toBeVisible();
  await expect(page.getByText(completed.mainEventWinner).first()).toBeVisible();
  await expect(page.getByText(/Unanimous Decision/).first()).toBeVisible();
  await expect(page.getByText(/Knockout/).first()).toBeVisible();
  await expect(page.getByText(/Submission/).first()).toBeVisible();
  await expect(page.getByText(/Browser Referee \d+ \(Referee\)/).first()).toBeVisible();
});

test('fresh athletics meet advances two heats and publishes final medals', async ({ page }) => {
  test.setTimeout(180_000);
  await signIn(page, 'admin@sportsplatform.com', 'Admin123!');

  const completed = await page.evaluate(async () => {
    const apiUrl = '';
    const token = localStorage.getItem('sports-platform.access-token');
    if (!token) throw new Error('Administrator session is unavailable.');
    const headers = {
      Authorization: `Bearer ${token}`,
      Accept: 'application/json',
      'Content-Type': 'application/json',
    };
    async function request<T>(path: string, method = 'GET', body?: unknown) {
      for (let attempt = 0; attempt < 6; attempt++) {
        const response = await fetch(`${apiUrl}${path}`, {
          method,
          headers,
          body: body === undefined ? undefined : JSON.stringify(body),
        });
        const text = await response.text();
        if (response.status === 429 && attempt < 5) {
          const retryAfter = Number(response.headers.get('retry-after') ?? '1');
          await new Promise(resolve =>
            setTimeout(resolve, Math.max(250, retryAfter * 1000)),
          );
          continue;
        }
        if (!response.ok) {
          throw new Error(`${method} ${path} failed (${response.status}): ${text}`);
        }
        return text ? JSON.parse(text) as T : undefined as T;
      }
      throw new Error(`${method} ${path} exhausted rate-limit retries.`);
    }

    const tenantId = '00000000-0000-0000-0000-000000000004';
    const sportId = '00000000-0000-0000-0000-000000000103';
    const suffix = Date.now().toString();
    const competition = await request<{ id: string }>('/api/competitions', 'POST', {
      name: `Browser Athletics Meet ${suffix}`,
      tenantId,
      sportId,
    });
    const athletes = await Promise.all(Array.from({ length: 16 }, async (_, index) =>
      request<{ id: string; name: string }>('/api/participants/athletes', 'POST', {
        name: `Browser Sprinter ${suffix} ${String(index + 1).padStart(2, '0')}`,
        shortName: `BS${String(index + 1).padStart(2, '0')}`,
        tenantId,
      }),
    ));
    const registrations = await Promise.all(athletes.map(athlete =>
      request<{ id: string }>(
        `/api/registrations/competition/${competition.id}`,
        'POST',
        { participantId: athlete.id, type: 0 },
      ),
    ));
    await Promise.all(registrations.map(registration =>
      request(`/api/registrations/${registration.id}/submit`, 'POST'),
    ));
    await Promise.all(registrations.map(registration =>
      request(`/api/registrations/${registration.id}/approve`, 'POST', {
        notes: 'Athletics browser acceptance',
      }),
    ));

    const meet = await request<{
      heatCount: number;
      heats: Array<{
        id: string;
        lanes: Array<{ participantId: string; lane: number }>;
      }>;
    }>(`/api/competitions/${competition.id}/athletics/meet`, 'POST', {
      eventName: "Men's 100m",
      editionName: '2026',
      stageName: 'Track',
      startTimeUtc: '2026-10-12T08:00:00Z',
      venue: 'Browser Athletics Track',
      distanceMeters: 100,
      expectedParticipants: 16,
      heatCount: 2,
      lanesPerHeat: 8,
      qualifyByPlace: 3,
      qualifyByTime: 2,
      advanceTies: true,
      minutesBetweenHeats: 15,
    });
    if (meet.heatCount !== 2 || meet.heats.some(heat => heat.lanes.length !== 8)) {
      throw new Error('The athletics heat assignment is incomplete.');
    }
    await request(`/api/competitions/${competition.id}/publish`, 'POST');

    const times = [
      [10_000, 10_200, 10_400, 10_550, 10_700, 10_900, 11_100, 11_300],
      [10_100, 10_300, 10_500, 10_600, 10_800, 11_000, 11_200, 11_400],
    ];
    for (let heatIndex = 0; heatIndex < meet.heats.length; heatIndex += 1) {
      await request(
        `/api/competitions/${competition.id}/athletics/races/${meet.heats[heatIndex].id}/results`,
        'POST',
        {
          results: meet.heats[heatIndex].lanes.map((lane, laneIndex) => ({
            participantId: lane.participantId,
            timeMilliseconds: heatIndex === 0 && laneIndex === 3
              ? null
              : times[heatIndex][laneIndex],
            disqualified: heatIndex === 0 && laneIndex === 3,
            disqualificationReason: heatIndex === 0 && laneIndex === 3
              ? 'False start'
              : null,
          })),
        },
      );
    }
    const qualification = await request<{
      finalContestId: string;
      requestedSlots: number;
      qualifiedCount: number;
      qualifiers: Array<{
        participantId: string;
        timeMilliseconds: number;
        qualificationReason: string;
      }>;
    }>(`/api/competitions/${competition.id}/athletics/qualify`, 'POST');
    if (qualification.requestedSlots !== 8 || qualification.qualifiedCount !== 8) {
      throw new Error('The athletics final does not contain eight qualifiers.');
    }
    if (qualification.qualifiers.filter(item =>
      item.qualificationReason.includes('by time')).length !== 2) {
      throw new Error('Fastest-remaining qualification is incorrect.');
    }
    const ordered = [...qualification.qualifiers].sort(
      (left, right) => left.timeMilliseconds - right.timeMilliseconds,
    );
    const finalTimes = [9_800, 9_900, 10_000, 10_100, 10_100, 10_300, 10_400, 10_500];
    await request(
      `/api/competitions/${competition.id}/athletics/races/${qualification.finalContestId}/results`,
      'POST',
      {
        results: ordered.map((qualifier, index) => ({
          participantId: qualifier.participantId,
          timeMilliseconds: finalTimes[index],
          disqualified: false,
        })),
      },
    );
    const publicMeet = await request<{
      heats: Array<{ entries: Array<{ status: string }> }>;
      final: {
        entries: Array<{
          name: string;
          place: number;
          isTied: boolean;
          medal?: string;
        }>;
      };
    }>(`/api/public/tenants/world-athletics/athletics/meet?competitionId=${competition.id}`);
    if (publicMeet.heats.flatMap(heat => heat.entries)
      .filter(entry => entry.status === 'Disqualified').length !== 1) {
      throw new Error('The heat disqualification is not public.');
    }
    if (publicMeet.final.entries.slice(0, 3).map(entry => entry.medal).join(',')
      !== 'Gold,Silver,Bronze') {
      throw new Error('The final medal assignment is incorrect.');
    }
    if (publicMeet.final.entries.filter(entry => entry.isTied && entry.place === 4)
      .length !== 2) {
      throw new Error('The final tie is not represented.');
    }
    return {
      competitionId: competition.id,
      winnerName: publicMeet.final.entries[0].name,
    };
  });

  await page.goto(`${adminUrl}/scoring/athletics`);
  await expect(page.getByRole('heading', { name: 'Athletics officials console' }))
    .toBeVisible();
  await page.getByPlaceholder('Competition ID').fill(completed.competitionId);
  await page.getByRole('button', { name: 'Load meet' }).click();
  await expect(page.getByText('Men\'s 100m — Final')).toBeVisible();
  await expect(page.getByText(/Qualified:/).first()).toBeVisible();

  await page.goto(
    `${publicUrl}/athletics?tenant=world-athletics&competitionId=${completed.competitionId}`,
  );
  await expect(page.getByRole('heading', { name: 'Athletics meet' })).toBeVisible();
  await expect(page.getByText('Qualification applied')).toBeVisible();
  await expect(page.getByTestId('athletics-final')).toContainText(completed.winnerName);
  await expect(page.getByTestId('medal-gold')).toBeVisible();
  await expect(page.getByTestId('medal-silver')).toBeVisible();
  await expect(page.getByTestId('medal-bronze')).toBeVisible();
  await expect(page.getByText('Disqualified')).toBeVisible();
  await expect(page.getByText('T4').first()).toBeVisible();
});

test('fresh T20 tournament advances group winners and publishes its champion', async ({ page }) => {
  test.setTimeout(240_000);
  await signIn(page, 'admin@sportsplatform.com', 'Admin123!');

  const completed = await page.evaluate(async () => {
    const apiUrl = '';
    const token = localStorage.getItem('sports-platform.access-token');
    if (!token) throw new Error('Administrator session is unavailable.');
    const headers = {
      Authorization: `Bearer ${token}`,
      Accept: 'application/json',
      'Content-Type': 'application/json',
    };
    async function request<T>(path: string, method = 'GET', body?: unknown) {
      const response = await fetch(`${apiUrl}${path}`, {
        method,
        headers,
        body: body === undefined ? undefined : JSON.stringify(body),
      });
      const text = await response.text();
      if (!response.ok) {
        throw new Error(`${method} ${path} failed (${response.status}): ${text}`);
      }
      return text ? JSON.parse(text) as T : undefined as T;
    }

    type Match = { id: string; participantIds: string[] };
    const tenantId = '00000000-0000-0000-0000-000000000005';
    const sportId = '00000000-0000-0000-0000-000000000104';
    const suffix = Date.now().toString();
    await request('/api/sports/cricket/configure', 'POST', {
      createdBy: 'browser-cricket-tournament',
    });
    const competition = await request<{ id: string }>('/api/competitions', 'POST', {
      name: `Browser T20 Cup ${suffix}`,
      tenantId,
      sportId,
    });
    const teams = await Promise.all(Array.from({ length: 4 }, async (_, index) =>
      request<{ id: string; name: string }>('/api/participants/teams', 'POST', {
        name: `Browser Cricket ${suffix} Team ${index + 1}`,
        shortName: `BC${suffix.slice(-6)}${index + 1}`,
        tenantId,
      }),
    ));
    const registrations = await Promise.all(teams.map(team =>
      request<{ id: string }>(
        `/api/registrations/competition/${competition.id}`,
        'POST',
        { participantId: team.id, type: 1 },
      ),
    ));
    await Promise.all(registrations.map(item =>
      request(`/api/registrations/${item.id}/submit`, 'POST'),
    ));
    await Promise.all(registrations.map(item =>
      request(`/api/registrations/${item.id}/approve`, 'POST', {
        notes: 'T20 browser acceptance',
      }),
    ));
    const tournament = await request<{ matches: Match[] }>(
      `/api/competitions/${competition.id}/cricket/tournament`,
      'POST',
      {
        editionName: '2026',
        groupStageName: 'Group stage',
        venue: 'Browser Cricket Ground',
        startTimeUtc: '2026-11-01T08:00:00Z',
      },
    );
    if (tournament.matches.length !== 6) {
      throw new Error('The T20 group stage does not contain six matches.');
    }

    const deliveries = (runs: number) => Array.from({ length: 120 }, (_, index) => ({
      overNumber: Math.floor(index / 6) + 1,
      ballNumber: (index % 6) + 1,
      runs: index === 119 ? runs - 119 : 1,
      extraRuns: 0,
      isWicket: false,
    }));
    async function completeMatch(match: Match) {
      await request(
        `/api/competitions/${competition.id}/cricket/matches/${match.id}/innings`,
        'POST',
        {
          inningsNumber: 1,
          battingTeamId: match.participantIds[0],
          deliveries: deliveries(120),
        },
      );
      await request(
        `/api/competitions/${competition.id}/cricket/matches/${match.id}/innings`,
        'POST',
        {
          inningsNumber: 2,
          battingTeamId: match.participantIds[1],
          deliveries: deliveries(121),
        },
      );
    }
    for (const match of tournament.matches) await completeMatch(match);

    const knockout = await request<{
      standings: unknown[];
      semifinals: Match[];
    }>(`/api/competitions/${competition.id}/cricket/qualify`, 'POST', {
      stageName: 'Knockout',
      venue: 'Browser Cricket Ground',
      startTimeUtc: '2026-11-08T08:00:00Z',
      daysUntilFinal: 2,
    });
    if (knockout.standings.length !== 4 || knockout.semifinals.length !== 2) {
      throw new Error('The T20 knockout qualification is incomplete.');
    }
    for (const semifinal of knockout.semifinals) await completeMatch(semifinal);

    const state = await request<{
      final: { id: string; teams: Array<{ participantId: string }> };
    }>(`/api/competitions/${competition.id}/cricket/tournament`);
    if (state.final.teams.length !== 2) {
      throw new Error('Semifinal winners did not advance to the final.');
    }
    await completeMatch({
      id: state.final.id,
      participantIds: state.final.teams.map(team => team.participantId),
    });
    await request(`/api/competitions/${competition.id}/publish`, 'POST');
    const publicTournament = await request<{
      standings: unknown[];
      groupMatches: unknown[];
      semifinals: unknown[];
      championName: string;
    }>(`/api/public/tenants/cricket-world/cricket/tournament?competitionId=${competition.id}`);
    if (publicTournament.groupMatches.length !== 6
      || publicTournament.semifinals.length !== 2
      || publicTournament.standings.length !== 4
      || !publicTournament.championName) {
      throw new Error('The public T20 tournament projection is incomplete.');
    }
    return {
      competitionId: competition.id,
      championName: publicTournament.championName,
    };
  });

  await page.goto(
    `${publicUrl}/cricket?tenant=cricket-world&competitionId=${completed.competitionId}`,
  );
  await expect(
    page.getByRole('heading', { name: 'T20 cricket tournament' }),
  ).toBeVisible();
  await expect(page.getByRole('heading', { name: 'Group standings' })).toBeVisible();
  await expect(page.getByRole('heading', { name: 'Semifinals' })).toBeVisible();
  await expect(page.getByRole('heading', { name: 'Final', exact: true })).toBeVisible();
  await expect(page.getByTestId('cricket-champion'))
    .toContainText(completed.championName);
  await expect(page.getByText('Won by 10 wickets').first()).toBeVisible();
});

test('cricket officials console records and corrects a delivery with rebuilt totals', async ({ page }) => {
  test.setTimeout(180_000);
  await signIn(page, 'admin@sportsplatform.com', 'Admin123!');
  const setup = await page.evaluate(async () => {
    const apiUrl = '';
    const token = localStorage.getItem('sports-platform.access-token');
    if (!token) throw new Error('Administrator session is unavailable.');
    const headers = {
      Authorization: `Bearer ${token}`,
      Accept: 'application/json',
      'Content-Type': 'application/json',
    };
    async function request<T>(path: string, method = 'GET', body?: unknown) {
      const response = await fetch(`${apiUrl}${path}`, {
        method,
        headers,
        body: body === undefined ? undefined : JSON.stringify(body),
      });
      const text = await response.text();
      if (!response.ok) throw new Error(`${method} ${path} failed (${response.status}): ${text}`);
      return text ? JSON.parse(text) as T : undefined as T;
    }

    const tenantId = '00000000-0000-0000-0000-000000000005';
    const sportId = '00000000-0000-0000-0000-000000000104';
    const suffix = Date.now().toString();
    await request('/api/sports/cricket/configure', 'POST', {
      createdBy: 'browser-cricket-scorecard',
    });
    const competition = await request<{ id: string }>('/api/competitions', 'POST', {
      name: `Scorecard T20 ${suffix}`,
      tenantId,
      sportId,
    });
    const teams = await Promise.all(Array.from({ length: 4 }, (_, index) =>
      request<{ id: string }>('/api/participants/teams', 'POST', {
        name: `Scorecard ${suffix} Team ${index + 1}`,
        shortName: `SC${suffix.slice(-6)}${index + 1}`,
        tenantId,
      })));
    const registrations = await Promise.all(teams.map((team) =>
      request<{ id: string }>(`/api/registrations/competition/${competition.id}`, 'POST', {
        participantId: team.id,
        type: 1,
      })));
    for (const registration of registrations) {
      await request(`/api/registrations/${registration.id}/submit`, 'POST');
      await request(`/api/registrations/${registration.id}/approve`, 'POST', {
        notes: 'Cricket scorecard browser acceptance',
      });
    }
    await request(`/api/competitions/${competition.id}/cricket/tournament`, 'POST', {
      editionName: '2026 scorecard',
      groupStageName: 'Group stage',
      venue: 'Scorecard Ground',
      startTimeUtc: '2026-12-01T08:00:00Z',
    });
    return competition;
  });

  await page.goto(`${adminUrl}/scoring/cricket`);
  await expect(page.getByRole('heading', { name: 'Cricket officials scorecard' })).toBeVisible();
  await page.getByLabel('Competition ID').fill(setup.id);
  await page.getByRole('button', { name: 'Load tournament' }).click();
  await expect(page.getByRole('heading', { name: 'Record ball 1.1' })).toBeVisible();
  await page.getByRole('button', { name: 'Record 1 runs' }).click();
  await expect(page.getByTestId('cricket-innings-1-score')).toHaveText('1/0');
  await expect(page.getByText('Over 1: 1 runs, 0 wickets')).toBeVisible();

  await page.getByRole('button', { name: 'Correct ball 1.1' }).click();
  await page.getByLabel('Runs', { exact: true }).fill('2');
  await page.getByLabel('Correction reason').fill('Umpire boundary signal reviewed');
  await page.getByRole('button', { name: 'Save correction' }).click();
  await expect(page.getByTestId('cricket-innings-1-score')).toHaveText('2/0');
  await expect(page.getByText('Over 1: 2 runs, 0 wickets')).toBeVisible();
  await expect(page.getByText(/corrected and innings rebuilt/)).toBeVisible();
});

test('submitted result is rejected, resubmitted, approved, rebuilt, and published', async ({ page }) => {
  test.setTimeout(180_000);
  let lifecycleContestId = '';

  async function findAvailableContest() {
    return page.evaluate(async () => {
      const apiUrl = '';
      const token = localStorage.getItem('sports-platform.access-token');
      const storedUser = localStorage.getItem('sports-platform.user');
      if (!token || !storedUser) throw new Error('Scorer session is unavailable.');
      const tenantId = (JSON.parse(storedUser) as { tenantId?: string }).tenantId;
      const headers = { Authorization: `Bearer ${token}`, Accept: 'application/json' };
      const contestResponse = await fetch(`${apiUrl}/api/contests?tenantId=${tenantId}`, { headers });
      const contests = await contestResponse.json() as Array<{ id: string }>;
      for (const contest of [...contests].reverse()) {
        const [resultsResponse, sessionResponse] = await Promise.all([
          fetch(`${apiUrl}/api/results/contest/${contest.id}`, { headers }),
          fetch(`${apiUrl}/api/livescoring/contests/${contest.id}/session`, { headers }),
        ]);
        const results = resultsResponse.ok
          ? await resultsResponse.json() as unknown[]
          : [];
        const session = sessionResponse.ok
          ? await sessionResponse.json() as { status: number }
          : null;
        if (results.length === 0 && session?.status !== 3) return contest.id;
      }
      throw new Error('No unused contest is available for the result lifecycle journey.');
    });
  }

  async function chooseScoringContest(startSession: boolean) {
    const sessionLoaded = page.waitForResponse((response) =>
      response.url().includes(`/api/livescoring/contests/${lifecycleContestId}/session`),
    );
    await page.goto(`${adminUrl}/scoring`);
    await expect(page.getByRole('heading', { name: 'Tenant contests' })).toBeVisible();
    await page.locator(`[data-contest-id="${lifecycleContestId}"]`).click();
    await sessionLoaded;
    if (startSession) {
      const start = page.getByRole('button', { name: 'Start', exact: true });
      await expect(start).toBeVisible();
      await start.click();
    }
    await expect(page.getByRole('button', { name: 'Goal' }).first()).toBeEnabled();
  }

  async function choosePremierResultContest() {
    await page.goto(`${adminUrl}/results`);
    await expect(page.getByRole('heading', { name: 'Result Review' })).toBeVisible();
    const tenantSelect = page.getByLabel('Tenant');
    const premierValue = await tenantSelect.locator('option', { hasText: 'Premier League' })
      .getAttribute('value');
    if (!premierValue) throw new Error('Premier League tenant option is unavailable.');
    if (await tenantSelect.inputValue() !== premierValue) {
      const contestsLoaded = page.waitForResponse((response) =>
        response.url().includes(`/api/contests?tenantId=${premierValue}`)
          && response.status() === 200,
      );
      await tenantSelect.selectOption(premierValue);
      await contestsLoaded;
    }
    const select = page.getByLabel('Contest');
    await expect(select.locator(`option[value="${lifecycleContestId}"]`))
      .toBeAttached({ timeout: 30_000 });
    await select.selectOption(lifecycleContestId);
    await expect(page.getByText('Submitted').first()).toBeVisible();
  }

  await signIn(page, 'scorer@premier.com', 'Scorer123!');
  lifecycleContestId = await findAvailableContest();
  await chooseScoringContest(true);
  await page.getByRole('button', { name: 'Goal' }).first().click();
  page.once('dialog', (dialog) => dialog.accept());
  await page.getByRole('button', { name: 'Submit results' }).click();
  await expect(page.getByText('Results submitted for approval.')).toBeVisible();

  await signIn(page, 'admin@sportsplatform.com', 'Admin123!');
  await choosePremierResultContest();
  await page.getByRole('button', { name: 'Start review' }).first().click();
  await expect(page.getByText('Under Review').first()).toBeVisible();
  page.once('dialog', (dialog) => dialog.accept('Browser review correction'));
  await page.getByRole('button', { name: 'Reject' }).first().click();
  await expect(page.getByText('Result rejected with its review reason.')).toBeVisible();
  await expect(page.getByText('Browser review correction')).toBeVisible();

  await signIn(page, 'scorer@premier.com', 'Scorer123!');
  await chooseScoringContest(false);
  page.once('dialog', (dialog) => dialog.accept());
  await page.getByRole('button', { name: 'Submit results' }).click();
  await expect(page.getByText('Results submitted for approval.')).toBeVisible();

  await signIn(page, 'admin@sportsplatform.com', 'Admin123!');
  await choosePremierResultContest();
  await page.getByRole('button', { name: 'Start review' }).first().click();
  await expect(page.getByText('Under Review').first()).toBeVisible();
  await page.getByRole('button', { name: 'Approve' }).first().click();
  await expect(page.getByText('Result approved and standings rebuilt.')).toBeVisible();
  await page.getByRole('button', { name: 'Publish' }).first().click();
  await expect(page.getByText('Result published to public clients.')).toBeVisible();

  await page.goto(`${publicUrl}/results`);
  await expect(page.getByText('Published').first()).toBeVisible();
  await page.goto(`${publicUrl}/standings`);
  const standingsTable = page.getByRole('table').first();
  await expect(standingsTable).toBeVisible();
  expect(await standingsTable.getByRole('row').count()).toBeGreaterThan(1);
});

test('tenant administrator generates and downloads a genuine isolated PDF report', async ({ page }) => {
  const reportName = `Browser PDF match sheet ${Date.now()}`;
  await signIn(page, 'admin@premier.com', 'TenantAdmin123!');
  await page.goto(`${adminUrl}/reports`);
  await expect(page.getByTestId('reports-workspace')).toBeVisible();
  await expect(page.getByRole('heading', { name: 'Reports', exact: true })).toBeVisible();
  await page.getByLabel('Report type').selectOption('MatchSheet');
  await page.getByLabel('Report name').fill(reportName);
  await page.getByLabel('Format').selectOption('Pdf');
  await page.getByRole('button', { name: 'Generate', exact: true }).click();
  await expect(page.getByRole('status')).toContainText('Report queued');
  const row = page.getByText(reportName, { exact: true }).locator('..').locator('..');
  await expect(row.getByText('Completed')).toBeVisible({ timeout: 30_000 });
  const downloadPromise = page.waitForEvent('download');
  await row.getByRole('button', { name: 'Download' }).click();
  const download = await downloadPromise;
  expect(download.suggestedFilename()).toBe(`${reportName}.pdf`);
});

test('tenant administrator archives and downloads a sport-aware printable contest sheet', async ({ page }) => {
  test.setTimeout(60_000);
  const archiveName = `Browser contest sheet ${Date.now()}`;
  await signIn(page, 'admin@premier.com', 'TenantAdmin123!');
  await page.goto(`${adminUrl}/reports`);
  const workspace = page.getByTestId('contest-sheet-workspace');
  await expect(workspace).toBeVisible();
  await workspace.getByLabel('Contest').selectOption({ index: 1 });
  await workspace.getByLabel('Archive name (optional)').fill(archiveName);
  await workspace.getByRole('button', { name: 'Generate and archive' }).click();
  await expect(page.getByRole('status')).toContainText('queued and archived');
  const row = workspace.getByText(archiveName, { exact: true })
    .locator('xpath=ancestor::div[contains(@class,"justify-between")][1]');
  await expect(row.getByText('Completed')).toBeVisible({ timeout: 45_000 });
  const downloadPromise = page.waitForEvent('download');
  await row.getByRole('button', { name: 'PDF' }).click();
  const download = await downloadPromise;
  expect(download.suggestedFilename()).toBe(`${archiveName}.pdf`);
});

test('tenant administrator generates and downloads an audited isolated tenant data export', async ({ page }) => {
  test.setTimeout(90_000);
  const exportName = `Browser tenant export ${Date.now()}`;
  await signIn(page, 'admin@premier.com', 'TenantAdmin123!');
  await page.goto(`${adminUrl}/reports`);
  const workspace = page.getByTestId('tenant-export-workspace');
  await expect(workspace).toBeVisible();
  await workspace.getByLabel('Export name (optional)').fill(exportName);
  await workspace.getByRole('button', { name: 'Generate tenant export' }).click();
  await expect(page.getByRole('status')).toContainText('export queued');
  const row = workspace.getByText(exportName, { exact: true })
    .locator('xpath=ancestor::div[contains(@class,"justify-between")][1]');
  await expect(row.getByText('Completed')).toBeVisible({ timeout: 60_000 });
  const downloadPromise = page.waitForEvent('download');
  await row.getByRole('button', { name: 'ZIP' }).click();
  const download = await downloadPromise;
  expect(download.suggestedFilename()).toBe(`${exportName}.zip`);
  await expect(page.getByRole('status')).toContainText('audit event recorded');
});

test('tenant administrator publishes a date-controlled sponsor placement to public web', async ({ page }) => {
  test.setTimeout(90_000);
  const suffix = Date.now().toString();
  const sponsorName = `Browser Sponsor ${suffix}`;
  const campaignName = `Homepage Campaign ${suffix}`;

  await signIn(page, 'admin@premier.com', 'TenantAdmin123!');
  await page.goto(`${adminUrl}/sponsors`);
  await expect(page.getByTestId('sponsor-management-workspace')).toBeVisible();
  const newProfile = page.getByRole('button', { name: 'New profile' });
  if (await newProfile.isVisible()) await newProfile.click();
  await expect(page.getByRole('button', { name: 'Create sponsor' })).toBeVisible();
  await page.getByLabel('Name', { exact: true }).fill(sponsorName);
  await page.getByLabel('Description').fill('Browser acceptance sponsor');
  await page.getByRole('button', { name: 'Create sponsor' }).click();
  await expect(page.getByText('Sponsor profile created and audited.')).toBeVisible();

  await page.getByLabel('Campaign name').fill(campaignName);
  await page.getByLabel('Value').fill('12500');
  await page.getByLabel('Currency').fill('KES');
  await page.getByLabel('Placement').selectOption('PublicHome');
  await page.getByLabel('Competition').selectOption({ index: 1 });
  await page.getByRole('button', { name: 'Create placement' }).click();
  await expect(page.getByText('Sponsorship placement created. Activate it when it is ready to publish.')).toBeVisible();
  await expect(page.getByText(campaignName)).toBeVisible();
  await page.getByRole('button', { name: 'Activate' }).last().click();
  await expect(page.getByText('Sponsorship activated and audited.')).toBeVisible();
  await expect(page.getByText('Active · Visible now').last()).toBeVisible();

  const publicResponse = await page.request.get(
    `http://localhost:8080/api/public/tenants/premier-league/home?fresh=${Date.now()}`,
  );
  expect(publicResponse.status()).toBe(200);
  const publicHome = await publicResponse.json() as {
    sponsors: Array<{ name: string }>;
  };
  expect(publicHome.sponsors.map(item => item.name)).toContain(sponsorName);

  await page.goto(`${publicUrl}/?tenant=premier-league&fresh=${Date.now()}`);
  await expect(page.getByText(sponsorName)).toBeVisible();
});

test('tenant administrator searches append-only audit history', async ({ page }) => {
  await signIn(page, 'admin@premier.com', 'TenantAdmin123!');
  await page.goto(`${adminUrl}/audit`);
  await expect(page.getByTestId('audit-history-workspace')).toBeVisible();
  await page.getByLabel('Action').fill('AuthenticationLoginSucceeded');
  await page.getByRole('button', { name: 'Search history' }).click();
  const entry = page.getByTestId('audit-entry').first();
  await expect(entry).toContainText('AuthenticationLoginSucceeded');
  await expect(entry).toContainText('admin@premier.com');
  await expect(entry).toContainText('Login succeeded');
});

test('tenant administrator controls privacy and runs tenant retention', async ({ page }) => {
  const suffix = Date.now().toString();
  const policyName = `Browser privacy retention ${suffix}`;
  await signIn(page, 'admin@premier.com', 'TenantAdmin123!');
  await page.goto(`${adminUrl}/privacy`);
  await expect(page.getByTestId('privacy-workspace')).toBeVisible();

  const analytics = page.getByText('Analytics', { exact: true }).locator('..').locator('..');
  await analytics.getByRole('button', { name: /Grant|Revoke/ }).click();
  await expect(page.getByRole('status')).toContainText('Analytics consent updated');

  await page.getByLabel('Profile visibility').selectOption('1');
  await page.getByLabel('Allow analytics').check();
  await page.getByRole('button', { name: 'Save preferences' }).click();
  await expect(page.getByRole('status')).toContainText('Visibility preferences saved');

  await page.getByLabel('Request details').fill(`Browser access request ${suffix}`);
  await page.getByRole('button', { name: 'Submit request' }).click();
  await expect(page.getByRole('status')).toContainText('Data access package generated');
  const downloadPromise = page.waitForEvent('download');
  await page.getByRole('button', { name: 'JSON' }).first().click();
  const download = await downloadPromise;
  expect(download.suggestedFilename()).toMatch(/^privacy-data-.*\.json$/);

  const administration = page.getByTestId('privacy-administration');
  const stalePolicies = administration.getByRole('button', { name: /^Delete Browser privacy retention/ });
  while (await stalePolicies.count()) {
    await stalePolicies.first().click();
    await expect(page.getByRole('status')).toContainText('Retention policy deleted');
  }
  await administration.getByLabel('Policy name').fill(policyName);
  await administration.getByLabel('Resource').selectOption('2');
  await administration.getByLabel('Retention days').fill('3650');
  await administration.getByLabel('Action').selectOption('1');
  await administration.getByLabel('Run every hours').fill('24');
  await administration.getByRole('button', { name: 'Add policy' }).click();
  await expect(page.getByRole('status')).toContainText('Retention schedule created');
  await administration.getByRole('button', { name: 'Run' }).last().click();
  await expect(page.getByRole('status')).toContainText('Retention policy executed');
  await administration.getByRole('button', { name: `Delete ${policyName}` }).click();
  await expect(page.getByRole('status')).toContainText('Retention policy deleted');
});

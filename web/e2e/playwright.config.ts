import { defineConfig, devices } from "@playwright/test";

// Smoke tests run against production builds (`next start`). Turbo builds both apps first (`pnpm e2e`).
// Set PLAYWRIGHT_CHROMIUM_EXECUTABLE_PATH to use a pre-installed Chromium instead of Playwright's own download.
const executablePath = process.env.PLAYWRIGHT_CHROMIUM_EXECUTABLE_PATH || undefined;
const isCi = !!process.env.CI;

const apps = [
  { name: "public", port: 3000 },
  { name: "admin", port: 3001 },
] as const;

export default defineConfig({
  testDir: "./tests",
  fullyParallel: true,
  forbidOnly: isCi,
  retries: 0,
  reporter: isCi ? [["github"], ["html", { open: "never" }]] : [["list"]],
  use: {
    trace: "retain-on-failure",
    launchOptions: { executablePath },
  },
  projects: apps.map((app) => ({
    name: app.name,
    testMatch: `${app.name}.spec.ts`,
    use: { ...devices["Desktop Chrome"], baseURL: `http://localhost:${app.port}` },
  })),
  webServer: apps.map((app) => ({
    command: `pnpm --filter @msp/${app.name} start`,
    url: `http://localhost:${app.port}`,
    reuseExistingServer: !isCi,
    timeout: 120_000,
  })),
});

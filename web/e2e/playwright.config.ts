import { randomBytes } from "node:crypto";
import { defineConfig, devices } from "@playwright/test";

// Smoke tests run against production builds (`next start`). Turbo builds both apps first (`pnpm e2e`).
// Both apps run against the real API (start-api.sh: migrate, seed DEMO, then the API in Development), so this
// needs Docker's PostgreSQL (`docker compose up -d`) and the .NET SDK.
// Set PLAYWRIGHT_CHROMIUM_EXECUTABLE_PATH to use a pre-installed Chromium instead of Playwright's own download.
const executablePath = process.env.PLAYWRIGHT_CHROMIUM_EXECUTABLE_PATH || undefined;

// Each run creates its own platform administrator (start-api.sh), with a generated password: no credentials are
// written anywhere (S03 D16), and runs on the same database never collide. Workers inherit these from the main process.
process.env.MSP_E2E_RUN ??= randomBytes(4).toString("hex");
process.env.MSP_E2E_ADMIN_EMAIL ??= `e2e-admin-${process.env.MSP_E2E_RUN}@msp-e2e.example`;
process.env.MSP_E2E_ADMIN_PASSWORD ??= randomBytes(24).toString("base64url");
const isCi = !!process.env.CI;

const apps = [
  // Tenant sites are visited by host (`kmsa.localhost:3000`); Chromium resolves `*.localhost` to loopback.
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
    testMatch: `${app.name}*.spec.ts`,
    use: { ...devices["Desktop Chrome"], baseURL: `http://localhost:${app.port}` },
  })),
  // Never reuse a running server: smoke tests must hit the production builds and a freshly seeded API.
  webServer: [
    {
      command: "./start-api.sh",
      url: "http://localhost:5080/health/live",
      reuseExistingServer: false,
      // The first run builds the backend and migrates the database.
      timeout: 300_000,
      stdout: "ignore",
      stderr: "pipe",
    },
    ...apps.map((app) => ({
      command: `pnpm --filter @msp/${app.name} start`,
      // A port, not a URL: the public app answers 404 on `localhost`, which serves no tenant.
      port: app.port,
      reuseExistingServer: false,
      timeout: 120_000,
    })),
  ],
});

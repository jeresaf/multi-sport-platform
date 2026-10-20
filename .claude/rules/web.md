---
paths:
  - "web/**"
---

# Web conventions (Next.js 16, TypeScript strict)

## Structure
- `web/apps/public` (tenant sites, SSR-first) and `web/apps/admin` (authenticated, mostly client components with
  TanStack Query). Shared code lives in `web/packages/*` (`ui`, `tokens`, `api-client`, `config`).
- Server Components by default in the public app; Client Components only where interaction or live updates need
  them.

## Data
- Call the API **only** through the generated client in `web/packages/api-client` (regenerate from
  `contracts/openapi.json`; never hand-edit generated files or duplicate DTO types).
- Admin app calls the API through same-origin rewrites with cookies (`createAdminClient` from
  `@msp/api-client/admin` adds `X-CSRF: 1` to mutations and `X-Tenant-Id` in a tenant workspace). Show errors with
  `describeProblem` (`@msp/api-client/errors`) and put `errors[]` under their fields.
- Errors: branch on the Problem Details `code`; show a friendly message plus the correlation ID.
- Capabilities: show/hide/disable actions from the capabilities endpoint and say why (e.g. "Requires PRO").

## Live updates
- Use the shared SignalR hook; apply versioned deltas to the query cache; on a version gap fetch
  `…?afterVersion=n`; never refetch whole pages after every event.

## UI
- Tailwind with tokens from `web/packages/tokens` only — no hex colours or ad-hoc palettes in components.
- Admin components are shadcn/ui in `web/packages/ui` (`@msp/ui/components/<name>`). Add one with the CLI from
  `web/packages/ui`: `pnpm dlx shadcn@4.21.5 add <name>`, then adapt it: `tablet:`/`desktop:` instead of `sm:`/`md:`,
  `rounded-sm|md|lg` only, `muted` (not `secondary`) for neutral fills, `text-destructive-foreground` and `bg-overlay`
  instead of white and black, 44 px controls. shadcn's colour names map onto the tokens in `@msp/config/theme.css`.
- Semantic colours (live, success, warning, danger, info) only for their meaning; never colour alone for status.
- Design phone (375 px), tablet (820 px) and desktop (1280 px) deliberately; attach screenshots to UI PRs.
- Accessibility: labels, roles, visible focus, keyboard paths, `aria-live="polite"` for score changes, touch
  targets ≥ 44–48 px.
- Times: format with the venue/tenant IANA time zone via `Intl`; never assume the browser's zone for fixtures.
- No sport-specific logic: renderers and scorer pads read definition metadata and `ScoreSummary`.
- Forms: react-hook-form + zod; the server is authoritative; show field errors from the API.

## Performance (public)
- Small client bundles, responsive images with sizes, lazy-load below the fold, avoid request waterfalls; budget
  LCP < 2.5 s on a mid-range phone.

## Tests
- Vitest (+ Testing Library) for components and logic; Playwright for user flows using role/label selectors
  (`data-testid` only when necessary).

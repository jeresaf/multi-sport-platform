# ADR-0011 — Web stack: pnpm + Turborepo, Next.js 16 (App Router), generated typed API client

**Status:** Accepted
**Date:** 2026-10-17
**Deciders:** Owner, Reviewer

## Context

Two web apps are needed: an admin app (authenticated, form- and table-heavy, tablet-friendly scorer) and a
public app (SEO, fast, responsive mobile/tablet/desktop, tenant-branded, live updates). The first summary chose
Next.js + TypeScript + Tailwind + TanStack Query + SignalR + Playwright.

## Decision

- **Monorepo tooling:** pnpm workspaces + Turborepo under `web/`.
- **Apps:** `web/apps/admin` and `web/apps/public`, both Next.js 16 (App Router), React 19, TypeScript
  `strict`.
- **Styling:** Tailwind CSS with design tokens from `web/packages/tokens` (CSS variables, light/dark,
  tenant-brandable within limits — docs/product/ux/design-system.md). Admin components built on shadcn/ui
  (Radix primitives) in `web/packages/ui`.
- **Data:** TanStack Query for server state. The API client is **generated from the backend OpenAPI
  document** into `web/packages/api-client` (openapi-typescript + openapi-fetch). Never hand-write request
  types that the backend already defines.
- **Forms:** react-hook-form + zod (zod schemas generated or hand-written to mirror the API; server is the
  final validator).
- **Realtime:** `@microsoft/signalr` client, wrapped in a small hook that applies versioned deltas to the
  TanStack Query cache.
- **Public rendering:** server-side rendering for public pages (SEO), client-side live updates on top.
- **Testing:** Vitest (+ Testing Library) for units, Playwright for end-to-end flows.
- **Language:** English-only UI in v1, no i18n framework yet (docs/build/open-questions.md Q7).

## Consequences

- Positive: one language (TypeScript) across both web apps; types flow from the backend.
- Negative: two Next.js apps to keep current; shared packages minimise duplication.

## Review trigger

Next.js major version changes that affect App Router conventions, or SEO/performance targets not met.

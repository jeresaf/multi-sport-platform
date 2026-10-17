# ADR-0012 — Mobile apps in Flutter, started after the API stabilises (Phase 8)

**Status:** Accepted
**Date:** 2026-10-17
**Deciders:** Owner, Reviewer

## Context

The owner wants the public UI on web and mobile (phone and tablet form factors). The first summary chose
Flutter (Riverpod/Bloc, Drift/SQLite, FCM) with supporter mode and an offline scorer mode, and white-label
builds per tenant. Building a third client in a third language (Dart) while the API is still changing
multiplies rework.

## Decision

- Keep **Flutter** for native apps (strong fit for white-label flavours and offline storage with Drift).
- **Start mobile in Phase 8**, after the football vertical slice, multi-sport proof and public web v1 have
  stabilised the API.
- Until then: the public web app is fully responsive (phone/tablet/desktop) and the **scorer UI lives in the
  admin web app**, designed tablet-first. It works online in v1 and shows connection state.
- The Dart API client is generated from the same OpenAPI document as the web client.
- Offline scoring (Phase 8) reuses the idempotent timeline API from ADR-0002; no server redesign is expected.
- Before Phase 8 starts, re-confirm Flutter vs React Native (sharing TypeScript with the web) in a short ADR.

## Consequences

- Positive: one client at a time; the hardest backend work is proven before mobile adds surface area.
- Negative: no app-store presence in early pilots; responsive web covers phones meanwhile.

## Review trigger

A pilot customer requires native apps or offline scoring earlier.

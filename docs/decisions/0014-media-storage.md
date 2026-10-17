# ADR-0014 — Media storage behind an abstraction (local disk in development, Azure Blob Storage in production)

**Status:** Accepted
**Date:** 2026-10-17 (production provider named before first merge, after ADR-0016)
**Deciders:** Owner, Reviewer

## Context

Photos, logos, badges, sponsor assets, consent evidence and import files are referenced everywhere
(`photo_asset_id`, `logo_asset_id`) but the previous plan had no owner for files.

## Decision

- The **Platform** module owns media metadata in `platform.media_assets`: `id, tenant_id, purpose
  (PERSON_PHOTO, TEAM_LOGO, TEAM_BADGE, ORG_LOGO, COMPETITION_LOGO, SPONSOR_ASSET, BRAND_ASSET,
  CONSENT_EVIDENCE, IMPORT_FILE, REPORT_FILE), content_type, size_bytes, storage_key, variants, visibility
  (PUBLIC | PRIVATE), uploaded_by, created_at, status`.
- Files are stored through `IFileStorage`: local filesystem in development/tests, **Azure Blob Storage** in
  production (ADR-0016). The interface stays provider-neutral; the Azure SDK lives only in the adapter (AR-33).
- Other modules store only asset IDs and ask Platform (via Contracts) for URLs. Public assets get cacheable
  URLs (cached at the edge by Cloudflare, ADR-0016); private assets get short-lived signed URLs after a
  permission check and are never cached.
- Images get resized variants (thumbnail, medium) using a permissively-licensed library (ADR-0013).
- Upload limits and allowed types are enforced server-side; storage use counts toward the `STORAGE_GB`
  entitlement.

## Consequences

- Positive: one place for storage policy, privacy and quotas.
- Negative: an extra hop for URLs; cached per request.

## Review trigger

Need for a CDN or image service at scale.

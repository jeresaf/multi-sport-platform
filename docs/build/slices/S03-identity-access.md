# S03 — Identity and access

**Goal:** real users log in, belong to tenants with scoped roles, and every admin request is authorised by
action code; the admin app has a working shell and the platform console can create tenants and invite owners.
**Modules touched:** Identity & Access (new), Tenancy (owner invitation entry point), admin web.
**Depends on:** S02.
**Demo:** platform admin logs in → creates a tenant → invites its owner (email in Mailpit) → owner accepts and
logs in → owner invites a VIEWER → viewer sees read-only screens; a denied action shows a clear message.

Read first: ADR-0005, docs/architecture/authorization.md, docs/product/ux/admin-app.md (shell, platform console,
tenant settings).

## Out of scope

Entitlements (S04), custom roles, SSO, public fan accounts (S21), mobile bearer flows beyond a smoke test.

## Tasks

### T1 — Users and credentials
Deliver:
- ASP.NET Core Identity with EF stores in `identity` schema (UUIDv7 keys); password policy (minimum length 12),
  lockout, email confirmation, password reset (emails via SMTP → Mailpit in dev).
- CLI `create-platform-admin --email <e>` reading the password from an environment variable; refuses to run if a
  platform admin with that email exists.

Acceptance:
- [ ] No default credentials anywhere in code, seeds or docs.
- [ ] Lockout after repeated failures (test).

### T2 — Authentication endpoints and web session
Deliver:
- Cookie authentication for web: `POST /api/auth/login`, `POST /api/auth/logout`, `GET /api/auth/me`.
  Cookies HttpOnly, Secure outside Development, `SameSite=Strict`.
- The admin app calls the API **through Next.js rewrites** (`/api/*` → backend) so cookies are first-party. State-
  changing requests must send header `X-CSRF: 1`; the API rejects cookie-authenticated mutations without it.
- Bearer token support configured for API/mobile clients with one smoke test (login → token → `me`).
- Rate limiting on login and password-reset endpoints.
- Note for S12: SignalR through the same origin works via a reverse proxy in deployed environments; in local dev
  connect directly with CORS + credentials if the dev proxy can't upgrade WebSockets.

Acceptance:
- [ ] CSRF test: mutation without header → `403 AUTH_CSRF_REQUIRED`.
- [ ] Login rate limit test.

### T3 — Memberships, invitations and role assignments
Deliver:
- `identity.tenant_memberships` (user, tenant, status `ACTIVE | SUSPENDED | REMOVED`, invited_by, joined_at);
  `identity.invitations` (email, tenant, roles, token hash, expiry, accepted_at);
  `identity.role_assignments` (membership, `role_code`, `scope_type`, `scope_id`, granted_by/at, revoked_at);
  `identity.platform_role_assignments`.
- **System roles defined in code** (role → action codes) per docs/architecture/authorization.md; stored
  assignments reference role codes. Custom roles later.
- Actions: `membership.invite`, `invitation.accept`, `membership.suspend`, `membership.remove`, `role.assign`,
  `role.revoke`; platform: `tenant.owner.invite` (`POST /api/platform/v1/tenants/{id}/owner-invitations`).
- Replace S02's test `ITenantMembershipChecker` with the real implementation.

Acceptance:
- [ ] Invitation tokens are single-use, expire, and are stored hashed.
- [ ] Removing a membership revokes access immediately (test).

### T4 — Permission evaluation and capabilities
Deliver:
- `IPermissionQueries` + `AuthorizationEvaluator` (Identity) combining access mode (from Tenancy) and scoped
  permissions; `IScopeChainProvider` registry (modules register providers in later slices; tests use a fake).
- `GET /api/admin/v1/me/capabilities?scopeType=&scopeId=` returns allowed action codes.
- Role/action matrix tests for all actions existing so far; a scoped assignment test with a fake chain
  (`CONTEST` → edition → competition → tenant).

Acceptance:
- [ ] Every admin endpoint returns 403 for a user without the action and succeeds with it (generated test over
      the endpoint catalogue where practical).

### T5 — Admin web shell and platform console
Deliver:
- Login, logout, session refresh, password reset pages; app shell with role-filtered navigation, tenant switcher,
  user menu, 403/404 pages, error display (code + correlation ID).
- Tenant settings → **Members & roles**: list, invite, change roles (scope `TENANT` for now), remove.
- Platform console: tenants list/create/detail with lifecycle actions (reason dialog) and owner invitation.
- Uses the generated API client and capabilities to show/hide actions.
- Playwright: the demo flow above, plus a viewer attempting a forbidden action via the API returns 403.

Acceptance:
- [ ] Screens work at tablet and desktop widths (screenshots).
- [ ] Demo flow passes in CI against the test stack.

## Slice acceptance

- [ ] Platform admin, tenant owner and viewer flows work end to end.
- [ ] Authorization is enforced server-side for every admin endpoint; capabilities drive the UI.
- [ ] Docs updated: authorization.md (any role changes), CLAUDE.md commands (CLI admin creation).

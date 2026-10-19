# S03 — Identity and access

> Rewritten by the S02 slice review (2026-10-19) to match what S01 and S02 actually built. It replaces the kit's
> version. Changes from the kit's version: the decisions below (D1–D18) are written out; the account endpoints get
> their own family (`/api/account/v1`) instead of `/api/auth/*`, because every endpoint must declare an action and
> belong to a family the Host checks; bearer tokens are deferred (D6, owner to confirm, Q18); emails go out through
> the outbox (D13); memberships, invitations and role assignments are tenant-owned under RLS, with one named platform
> service for the cross-tenant lookups (D8); the S02 follow-ups assigned to S03 are folded into the tasks ("Carried
> over from S02").

**Goal:** real people sign in, belong to tenants with roles, and every admin and platform request is authorised by
its action code; the admin app has a working shell, and the platform console can create tenants and invite their
owners.
**Modules touched:** Identity & Access (new module `Identity`, schema `identity`), Platform (email), Tenancy (access
mode refinements), Host, admin web.
**Depends on:** S02.
**Demo:** a platform admin created with the CLI signs in → creates a tenant → invites its owner (email in Mailpit) →
the owner accepts, sets a password and signs in → the owner changes the tenant settings and invites a VIEWER → the
viewer sees the settings read-only, and a forbidden change through the API returns `403 AUTH_FORBIDDEN`; the audit
trail shows every step with the real user.

Read first: ADR-0005, docs/architecture/authorization.md, docs/architecture/multi-tenancy.md ("Resolving the
tenant", as built), docs/product/ux/admin-app.md (shell, platform console, tenant settings), ADR-0015 (users' email is
personal data), docs/product/pilot-customer.md (people and roles).

## What S01 and S02 give you (use it, don't rebuild it)

- **Pipeline** (`SportsPlatformApp.UsePipeline`, the single place it is written): forwarded host → logging and
  correlation → routing → **actor** (`UseAuthentication`, then `CurrentActorMiddleware` sets the scoped
  `ActorContext` from the principal's `sub`) → **audit** → **tenant resolution** → **authorization** (step 1 access
  mode via `ITenantAccess`, then `IAuthorizationEvaluator`) → endpoint. The Host has no authentication scheme yet and
  its evaluator is `DenyAllAuthorizationEvaluator`; tests use a test scheme (`X-Test-User`, `X-Test-Tenants`) and
  `TestAuthorizationEvaluator`.
- **Tenant resolution:** tenant actions take `X-Tenant-Id`, checked by `ITenantMembershipChecker` (Host default:
  `NoTenantMemberships`); platform routes opt in with `.RunsUnderRouteTenant()`; public endpoints with
  `.RequirePublicTenant()`. `TenantContext.Set` once per scope, before the first tenant-data connection (S02 D1).
- **Isolation:** `ITenantOwned` + query filter + `TenantOwnershipInterceptor` + `TenantConnectionInterceptor`,
  `EnableTenantRls`, composite foreign keys between tenant-owned tables, the isolation harness (a sample per entity,
  `.claude/rules/testing.md`), the RLS coverage test (exceptions: `outbox_messages`, `tenant_domains`).
- **Platform services on `sp_platform`:** `AddPlatformServiceDbContext<T>` (never migrated; maps tables its module's
  migrated context owns) and the architecture test that names each one's allowed users (today only
  `PlatformAuditDbContext`).
- **Actions and audit:** `ActionDefinition` (scope `Platform | Tenant`, `ScopableBy`, audit), `.RequireAction`,
  `.AllowPublic`, the startup endpoint rules, catalogue export, `AuditScope`/`AuditMiddleware`, `IAuditLog`, audit
  under RLS. `actor_kind` allows `USER | SYSTEM` and `actor_id` is NOT NULL.
- **Tenancy:** `ITenantDirectory`, `ITenantAccess` (status-only access mode), the platform tenant API, admin settings
  API, DEMO tenants KMSA and ELSSC (`tenancy.demo.tenants`), `SeedContext.RunCommandAsync` (a command per scope, as
  `SYSTEM`, audited).
- **Web:** Next.js admin app (port 3001, an empty shell), generated API client, Playwright against the real API
  (`web/e2e/start-api.sh`), screenshots as a CI artifact. shadcn/ui is not initialised yet.

## Decisions (reviewer defaults; confirm or change in each task's plan)

- **D1 — An account family: `/api/account/v1/…`.** Sign-in, sign-out, the current user, password reset and change,
  and invitation acceptance work on the caller's own account: no tenant, never cacheable, never under
  `/api/public` (public endpoints are anonymous and cacheable) or `/api/admin` (needs `X-Tenant-Id`). api-conventions.md
  and `EndpointActionRules` gain the family: account endpoints require an `ACCOUNT` action (D2) and are never
  `.AllowPublic()`.
- **D2 — Account actions are a third action scope, `Account`.** Granted to every authenticated user for their own
  account; a few also allow an anonymous caller (`allowsAnonymous: true`, only valid on `Account` actions): `account.login`,
  `account.password.forgot`, `account.password.reset`, `invitation.read`, `invitation.accept`. Authenticated only:
  `account.logout`, `account.read` (`GET …/me`), `account.password.change`. Because they are actions, the audit
  middleware audits them like everything else (login success and failure included) and the catalogue lists them.
- **D3 — Anonymous actor.** `ActorKind.Anonymous` with a fixed `actor_id` (`00000000-0000-0000-0000-000000000000`),
  a migration widening `actor_kind` on `audit.audit_entries` and `tenancy.tenant_status_history`, and a test that an
  anonymous denial on an audited endpoint writes a `DENIED` entry (S01 #23 R1). Anonymous denials keep one row each;
  volume is limited at the edge (infrastructure task) and by D15's rate limits (decides S01 #23 R2).
- **D4 — Unauthenticated is 401, not 403.** An anonymous caller on an admin or platform endpoint, or an authenticated
  principal without a GUID `sub`, gets `401 AUTH_UNAUTHENTICATED` (S02 follow-ups). Tenant resolution answers 401
  before checking `X-Tenant-Id`; authorization answers 401 for platform actions.
- **D5 — Web session = ASP.NET Core Identity's application cookie.** Name `__Host-msp_session` outside Development
  (`msp_session` in Development, where the site is http), HttpOnly, `Secure` outside Development, `SameSite=Strict`,
  path `/`. 12 hours sliding, 7 days absolute (an `issued_at` claim checked on every request). The security stamp is
  validated every 5 minutes, so a password reset or change signs out every other session. Removing a membership or a
  role takes effect on the next request, because memberships and roles are read per request (no claims cache).
- **D6 — Bearer tokens are deferred (owner to confirm, Q18).** ADR-0005 gives bearer tokens to mobile and API clients;
  the first one is the Flutter scorer (Phase 8) or tenant API keys (PRO, `API_ACCESS`). S03 builds cookies only, so
  the API has one authentication scheme to secure and test. `MapIdentityApi` is not used (its endpoints declare no
  actions). When bearer tokens arrive, the account endpoints issue them through Identity's bearer scheme
  (`IdentityConstants.BearerScheme`), not `MapIdentityApi`; that keeps ADR-0005's decision. If the design changes in
  substance, supersede the ADR then.
- **D7 — CSRF.** Every non-GET request to `/api/account`, `/api/admin` and `/api/platform` must carry `X-CSRF: 1`,
  else `403 AUTH_CSRF_REQUIRED` — including sign-in (login CSRF). `SameSite=Strict` already blocks cross-site cookies;
  the header is defence in depth and costs one line in the web client.
- **D8 — Who owns which identity data, and RLS.**
  - `identity.users` (ASP.NET Core Identity's user table and its satellites) is **global**: a person signs in once and
    may belong to several tenants. Email is `PRIVATE` (ADR-0015): never logged, never in audit metadata, never in
    integration events.
  - `identity.tenant_memberships`, `identity.invitations` and `identity.role_assignments` are **tenant-owned**
    (`ITenantOwned`, `EnableTenantRls`, isolation samples; `role_assignments → tenant_memberships` is a composite key).
  - The cross-tenant reads a request needs before a tenant is set go through **one named platform service**,
    `IdentityDirectory` (`AddPlatformServiceDbContext`, read-only, on `sp_platform`): "is user U an active member of
    tenant T" (the real `ITenantMembershipChecker`), "U's tenants and roles" (`GET /api/account/v1/me`) and "the
    invitation with this token hash". This is the second platform service context, so S02's follow-up #33 N2 is due:
    make the architecture rule generic in the same task.
  - `identity.platform_role_assignments` is global (platform staff have no tenant).
  - No new RLS exceptions.
- **D9 — Roles are defined in code.** `IdentityRoles` maps each system role (authorization.md) to its action codes,
  written as code strings (`"tenant.settings.update"`): action definitions live in each module's assembly, which
  Identity may not reference (AR-02). The Host checks at startup that every code exists in the catalogue and that a role's actions fit its scope (tenant
  roles only `Tenant` actions, platform roles only `Platform` actions); `export-catalogues` writes
  `docs/generated/roles.md` and `contracts/roles.json`, so the catalogue shows who can do what. Assignments store the
  role code. Custom roles come later. ADR-0005's "system roles are seeded" means predefined: defining them in code
  meets it (the kit's S03 spec said the same). T3 changes authorization.md's heading to "System roles (defined in
  code; tenant-custom roles later)".
- **D10 — The evaluator lives in Identity.** `IdentityAuthorizationEvaluator : IAuthorizationEvaluator` replaces
  `DenyAllAuthorizationEvaluator` in the Host (tests keep their own). `AuthorizationRequest` gains the resolved tenant
  and an optional resource scope (`ScopeRef(type, id)`) for the scope-chain step:
  - **Platform action:** the user has an active platform role that includes it.
  - **Tenant action:** the user has an active membership in the request's tenant and an unrevoked assignment whose
    role includes the action and whose scope is `TENANT`, or (with a resource scope) appears in that resource's scope
    chain (`IScopeChainProvider` registry; no module provides chains until S06+, tests use a fake).
  - **Account action:** the user is authenticated, or the action allows anonymous callers.
  - Entitlements (step 2) arrive with S04. Platform staff get no tenant actions through platform roles.
- **D11 — Access mode refinements (need roles; S02 follow-ups #34 R1, T3a).** `PROVISIONING` is `FULL` only for
  `TENANT_OWNER` and `TENANT_ADMIN`; other members get `403 TENANT_PROVISIONING`. `BLOCKED` keeps refusing every
  tenant action except those marked `availableWhenBlocked` (new `ActionDefinition` flag), which only `TENANT_OWNER`
  holds: `tenant.access.read` (`GET /api/admin/v1/tenant/access`: status and why, so the owner sees "suspended" instead
  of a bare 403). state-machines.md "Access mode" is updated to say how.
- **D12 — Invitations.** An invitation names an email, a tenant and roles (`TENANT` scope only in S03). Its token is
  32 random bytes, sent once in the email link, stored as a SHA-256 hash; it expires after 7 days and works once.
  States `PENDING → ACCEPTED | REVOKED`, `EXPIRED` derived from the date (added to state-machines.md with the owner's
  approval). Accepting: if no user has the email, the invitee sets a password and the user is created with the
  email confirmed (holding the token proves it); if a user exists, the invitee must sign in as that user first. A
  re-invite to a pending email replaces the old invitation. There is no self-registration.
- **D13 — Email goes through the outbox.** `IEmailSender` in Platform Contracts with an SMTP adapter (MailKit, MIT —
  list it in the PR) that sends to Mailpit in development; the production provider is the infrastructure task's
  (AR-33: the adapter is the only provider-specific code). Identity never sends inside a request: it publishes an
  event with IDs only (S02 D10) and its own consumer loads what it needs, creates the token (invitation token or
  Identity's reset token) and sends. A rolled-back request sends nothing; a failed send is retried by the outbox; a
  retry creates a new token and the newest wins. Plain text plus simple HTML, English (Q7). `Email:From` is
  configuration (Q10 sets the domain). T1 adds to modules.md, Platform **Owns**: "transactional email transport
  (`IEmailSender` and its adapter)"; Engagement (S21) keeps notification definitions, templates and delivery tracking
  and sends through it.
- **D14 — Data protection keys** persist in `identity.data_protection_keys`
  (`Microsoft.AspNetCore.DataProtection.EntityFrameworkCore`, MIT), so cookies and reset tokens survive restarts and
  work on several API instances. Encrypting the key ring at rest (Key Vault) is the infrastructure task's.
- **D15 — Sign-in protection.** Lockout after 5 failures for 15 minutes. Unknown email, wrong password, a locked or
  unconfirmed account all answer `401 AUTH_INVALID_CREDENTIALS` (no account enumeration; the audit entry records the
  real reason without the email). `POST …/password/forgot` always answers `202`. Rate limits (ASP.NET Core's built-in
  limiter) on login and forgot-password, per client IP and per normalised email: `429 AUTH_RATE_LIMITED` with
  `Retry-After`. Password: at least 12 characters, no composition rules, checked against Identity's validators;
  common-password lists are a hardening item.
- **D16 — The first platform admin comes from the CLI.** `create-platform-admin --email <e>` reads the password from
  `MSP_ADMIN_PASSWORD` (refuses if it is missing or too short), creates a confirmed user with `PLATFORM_ADMIN`, and
  refuses if a platform admin with that email already exists. No default credentials anywhere in code, seeds, tests
  or docs (tests generate theirs).
- **D17 — Last owner.** A tenant always keeps at least one active `TENANT_OWNER`: removing, suspending or revoking
  the last one is `409 AUTH_LAST_OWNER`. Ownership transfer is out of scope.
- **D18 — Demo data invites, it never sets passwords.** The DEMO seeder (through Identity commands, as `SYSTEM`)
  invites KMSA's people from pilot-customer.md with their roles (Grace Nakato `TENANT_OWNER`, Daniel Kato
  `TENANT_ADMIN`, Brian Ssemakula `REGISTRAR`, Esther Nabirye `RESULTS_OFFICER`) and an ELSSC owner, at invented
  addresses on `kmsa.example` / `elssc.example`. The invitations land in Mailpit; whoever runs the demo accepts them
  and chooses the passwords. Idempotent: a pending or accepted invitation is not re-sent.

## Out of scope

Entitlements and quotas (S04, including `MAX_STAFF_USERS` on `membership.invite`), custom roles, scoped assignments
other than `TENANT` in the UI (the evaluator supports chains; modules provide them from S06), ownership transfer,
SSO, MFA (owner to confirm: before the first pilot, Q19), bearer tokens (D6), public fan accounts (Q3, S21), linking a
user to a Registry person, platform-user management screens (platform roles change through the CLI in S03), the
tenant-facing audit view.

## Tasks

### T1 — Identity module, users and email
Deliver:
- New module `Identity` (`SportsPlatform.Modules.Identity` + `.Contracts`), schema `identity`, in `ModuleRegistry`;
  architecture tests find it.
- ASP.NET Core Identity core (`AddIdentityCore<User>`, EF stores in `identity`, UUIDv7 keys, audit columns) without
  its UI or API endpoints. Password, lockout and token options per D15. Data protection keys in the database (D14).
  `identity.users` and the Identity satellite tables declared in `TableAccessPolicies`.
- `IEmailSender` (Platform Contracts) with the SMTP adapter (D13), configuration `Email:Smtp:*` and `Email:From`;
  Development points at Mailpit (`localhost:1025`). A test sends through a fake; one integration test against
  Mailpit's API is optional.
- CLI `create-platform-admin` (D16). It needs the platform role table from T3: in T1 the command creates the user and
  T3 adds the role, or T1 ships the table early — say which in the plan.
- The CLI logs EF Core commands at `Information` (DEMO seeding prints every SQL statement): give the CLI the Host's
  log levels (`Microsoft.EntityFrameworkCore` at `Warning`).

Acceptance:
- [x] No default credentials anywhere in code, seeds, tests or docs (a test scans for the obvious ones; the reviewer
      checks the PR).
- [x] `create-platform-admin` refuses a missing or short password and an existing platform admin (tests).

### T2 — Account API, sessions and the anonymous actor
Deliver:
- The account family and `Account` actions (D1, D2): `POST /api/account/v1/login`, `POST …/logout`, `GET …/me`
  (user, platform roles, tenants with their roles — through `IdentityDirectory` once T4 adds memberships; T2 returns
  platform roles and an empty tenant list), `POST …/password/forgot`, `POST …/password/reset`, `POST …/password/change`.
  Startup rules and api-conventions.md updated.
- The cookie scheme (D5) as the Host's only authentication scheme; `CurrentActorMiddleware` reads it. The test scheme
  stays test-only.
- CSRF (D7), sign-in protection and rate limits (D15), `401 AUTH_UNAUTHENTICATED` (D4), the anonymous actor and its
  migration (D3). Update `RequestLoggingTests.Request_is_logged_with_correlation_ID_and_actor` (S01 #17 R1).
- Errors (`IdentityErrors`, prefix `AUTH_`): `AUTH_UNAUTHENTICATED` (401), `AUTH_INVALID_CREDENTIALS` (401),
  `AUTH_CSRF_REQUIRED` (403), `AUTH_RATE_LIMITED` (429), `AUTH_PASSWORD_REJECTED` (400, with `errors` naming the
  rule), `AUTH_RESET_TOKEN_INVALID` (400).
- Password reset email through the outbox (D13).
- Startup check that every endpoint with `.ValidateRequest<T>()` has an `IValidator<T>` (S01 #22 N1: due with the
  next validator, which is here).
- docs: authorization.md (account scope, 401 vs 403), api-conventions.md (family, CSRF), modules.md (Identity
  authenticates; the actor context stays in BuildingBlocks).

Acceptance:
- [ ] A mutation without `X-CSRF` → `403 AUTH_CSRF_REQUIRED`; with it and a valid session → allowed by the evaluator.
- [ ] Lockout after 5 failures; the rate limit answers 429; a locked account and an unknown email answer the same.
- [ ] An anonymous denial on an audited endpoint writes a `DENIED` entry with actor kind `ANONYMOUS`.
- [ ] A password change signs out the user's other sessions (security stamp).

### T3 — Roles in code, platform roles and the evaluator
Deliver:
- `IdentityRoles` (D9) with the system roles of authorization.md for the actions that exist today, plus
  `PLATFORM_ADMIN` and `PLATFORM_SUPPORT` (read-only platform actions); startup validation; `roles.md`/`roles.json`
  in `export-catalogues` and the `Contracts` CI job.
- `identity.platform_role_assignments` (global; granted/revoked by and at); `create-platform-admin` grants
  `PLATFORM_ADMIN`.
- `IdentityAuthorizationEvaluator` (D10) for platform and account actions; tenant actions are denied until T4 (fail
  closed). `AuthorizationRequest` extended (tenant, optional `ScopeRef`).
- After T3 a real platform admin can run the S02 platform API (tenants, audit, diagnostics) end to end.

Acceptance:
- [ ] A platform admin may perform every platform action; `PLATFORM_SUPPORT` only the read ones; a user without a
      platform role gets `403 AUTH_FORBIDDEN` on each (generated over the action catalogue).
- [ ] A role naming an unknown action code, or an action of the wrong scope, stops the Host at startup (test).

### T4 — Memberships, invitations and tenant roles
Deliver:
- `identity.tenant_memberships` (user, status `ACTIVE | SUSPENDED | REMOVED`, invited_by, joined_at),
  `identity.invitations` (D12), `identity.role_assignments` (membership, role code, scope type, scope id,
  granted_by/at, revoked_by/at): tenant-owned (D8), each with an isolation sample; membership and invitation state
  machines added to state-machines.md (owner).
- `IdentityDirectory` (D8) and the real `ITenantMembershipChecker`; the generic platform-service-context rule (#33 N2).
- Actions (TENANT scope unless noted): `membership.read`, `membership.invite`, `membership.suspend` (reason),
  `membership.restore`, `membership.remove` (reason), `role.assign`, `role.revoke`, `invitation.revoke`;
  `invitation.read` and `invitation.accept` (ACCOUNT, anonymous allowed); platform: `tenant.owner.invite`
  (`POST /api/platform/v1/tenants/{tenantId}/owner-invitations`, runs under the route tenant). Endpoints under
  `/api/admin/v1/members`, `/api/admin/v1/invitations` and `/api/account/v1/invitations/{token}`.
- The evaluator's tenant step (D10); last-owner rule (D17); access-mode refinements (D11) with `tenant.access.read`
  in Tenancy and `TENANT_PROVISIONING` in `TenancyErrors`.
- Invitation email through the outbox (D13); `GET /api/account/v1/me` lists the user's tenants and roles.
- DEMO seeder `identity.demo.invitations` (D18), after `tenancy.demo.tenants`.
- Likely two PRs (T4a memberships, roles, evaluator, directory; T4b invitations, emails, access-mode refinements,
  DEMO); say so in the plan.

Acceptance:
- [ ] Invitation tokens are single-use, expire, and are stored hashed (tests read the table).
- [ ] Removing a membership, suspending it, or revoking the last role denies the next request (test).
- [ ] A member of tenant A can't read or change tenant B's members, invitations or roles through the API (`403` on
      the header, no data), and the isolation harness covers the three tables.
- [ ] A `PROVISIONING` tenant: owner and admin may work, a viewer gets `403 TENANT_PROVISIONING`; a suspended tenant:
      the owner reads `GET /api/admin/v1/tenant/access`, everything else is `403 TENANT_BLOCKED`.
- [ ] The last owner can't be removed, suspended or demoted (`409 AUTH_LAST_OWNER`).

### T5 — Capabilities and the authorization matrix
Deliver:
- `GET /api/admin/v1/me/capabilities` (tenant action `tenant.capabilities.read`, in every tenant role) and
  `GET /api/platform/v1/me/capabilities` (platform roles): the action codes the caller may perform, optionally for a
  `scopeType`/`scopeId`. S04 adds the reason for unavailable actions.
- `IScopeChainProvider` registry in BuildingBlocks (modules register providers from S06); a fake chain test
  (`CONTEST → edition → competition → tenant`).
- A generated test over the endpoint catalogue: every admin and platform endpoint answers `403` for a signed-in user
  whose roles lack its action and passes authorization with a role that has it (handlers may then fail on their own
  rules; the test checks the authorization step only).

Acceptance:
- [ ] The matrix test covers every mapped admin and platform endpoint; a new endpoint is covered without editing the
      test.
- [ ] Capabilities match the evaluator for every system role (table-driven).

### T6 — Admin web: sign-in, shell, members, platform console
Deliver:
- shadcn/ui initialised (ADR-0011; replaces the hand-written `Button`). The admin app calls the API through Next.js
  rewrites (`/api/*` → `MSP_API_URL`), so the session cookie is first-party; the client sends `X-CSRF: 1` and, in the
  tenant workspace, `X-Tenant-Id`.
- Pages: sign in, sign out, forgot and reset password, accept invitation (set password or sign in), 401/403/404 and an
  error display (message from the `code`, otherwise the message and correlation ID, admin-app.md).
- Shell (admin-app.md): navigation filtered by capabilities, tenant switcher from `me`, user menu; tenant workspace
  under `/t/{tenantSlug}`, platform console under `/platform`.
- Tenant settings: Organisation (the T4b settings form, read-only without `tenant.settings.update`), **Members &
  roles** (list, invite, change roles at `TENANT` scope, suspend, remove, revoke invitation; reasons where required).
- Platform console: tenants list, create, detail with lifecycle actions (reason dialog) and owner invitation.
- The OpenAPI fixes the generated client needs (S02 follow-up): optional request members not `required`, `If-Match`
  and `ETag` documented; conflict screens use them ("This record changed since you opened it").
- Playwright: the slice demo flow against the real API (emails read from Mailpit's API in e2e), plus a viewer's
  forbidden change returning `403`. Screenshots at tablet and desktop widths, light and dark, as the CI artifact.
- Likely two PRs (T6a auth pages, shell, client; T6b members, platform console, e2e).

Acceptance:
- [ ] The demo flow passes in CI against the real API.
- [ ] Every screen works at tablet and desktop widths (screenshots).
- [ ] No control is shown that the capabilities don't allow; the server still refuses when called directly (test).

## Carried over from S02 (slice review 2026-10-19)

Each is assigned above; remove it from PROGRESS.md "Tech debt" when its task merges.
- Anonymous actor kind and its audit migration, with the `DENIED` test (S01 #23 R1) — T2 (D3).
- Anonymous denials: one row each, edge limits later (S01 #23 R2) — T2 (D3, decided).
- 401 with a `code` (S01-T3a) — T2 (D4).
- Principal without a GUID `sub` refused with 401 (S02-T3a) — T2 (D4).
- Unauthenticated requests logged as anonymous; update `RequestLoggingTests` (S01 #17 R1) — T2.
- `PROVISIONING` limited to owners and admins (#34 R1) — T4 (D11).
- `BLOCKED` exception for the owner (S02-T3a) — T4 (D11).
- Real `ITenantMembershipChecker` replacing `NoTenantMemberships` — T4 (D8).
- Generic platform-service-context usage rule (#33 N2) — T4 (D8).
- Startup check for missing `IValidator<T>` (S01 #22 N1) — T2.
- OpenAPI optional members, `If-Match`/`ETag` headers (S02-T1b, T4b pre-reviews) — T6.
- shadcn/ui initialisation — T6.
- CLI log levels (S02 owner demo) — T1.

## Slice acceptance

- [ ] Platform admin, tenant owner and viewer flows work end to end, in CI.
- [ ] Every admin and platform endpoint is authorised server-side by its action (matrix test); capabilities drive the
      UI.
- [ ] Memberships, invitations and role assignments are covered by the isolation harness; the RLS exception list is
      unchanged.
- [ ] No default credentials anywhere; sign-in has lockout, rate limits and CSRF protection.
- [ ] Docs updated: authorization.md (account scope, roles as built, access-mode refinements), api-conventions.md
      (account family, CSRF, 401), multi-tenancy.md (membership check), state-machines.md (membership, invitation),
      CLAUDE.md commands (`create-platform-admin`), `docs/generated/roles.md`.
- [ ] The S02 carry-overs above are done and removed from PROGRESS.md "Tech debt / follow-ups".

## Implementation notes

(The builder adds notes here as tasks merge.)

- **T1 (two PRs, owner):** T1a Identity module, users, key ring and `create-platform-admin`; T1b `IEmailSender` and
  the SMTP adapter.
- **T1a:** `User : IdentityUser<Guid>` lives in `Infrastructure/Users` (Identity's base type is ASP.NET Core, which
  `Domain/` may not use); IDs are UUIDv7. `UserStore` (`UserOnlyStore`) never saves by itself: a handler's user
  changes, role assignments and audit entry commit in its one save. The user name is the email.
  `identity.platform_role_assignments` ships here (T3 adds the role definitions and the evaluator); `PlatformRoles`
  holds the two codes. `create-platform-admin` lives in the Host (`Administration/CreatePlatformAdminCommand`, so tests
  run it), is audited as `platform.admin.create` (Platform scope, no endpoint) by `SYSTEM`, and refuses any email a
  user already has. The CLI applies the Host's `Serilog:MinimumLevel` to its console logger. Because the store doesn't
  save, every `UserManager`/`SignInManager` change (failed-attempt counts, lockout, security stamp) is lost unless the
  handler saves: T2's login must save after a failed attempt too. Identity's satellite tables keep its own keys
  (`int` claim IDs, composite login and token keys), as does `data_protection_keys` (`int`), not `id uuid`.
- **T1b:** `IEmailSender` and `EmailMessage` (recipient `PRIVATE`) are in `Platform.Contracts/Email`; the MailKit
  adapter `SmtpEmailSender` is in `Platform/Infrastructure/Email`: one connection per email, failures throw (the
  outbox retries the consumer), logs carry only the Message-ID. `Email:From`, `Email:FromName` (default
  `SportsPlatform`) and `Email:Smtp:{Host, Port, Security (None|StartTls|SslOnConnect), UserName, Password,
  TimeoutSeconds}` are validated on start (owner: fail fast), so the API refuses to start without them outside
  Development and Testing; the CLI never starts its host and checks them only if something sends. Development and
  Testing point at Mailpit with `From` `no-reply@sportsplatform.localhost` (domain waits for Q10); a different
  `MAILPIT_SMTP_PORT` needs `Email__Smtp__Port`. `TestApp.Emails` (`RecordingEmailSender`) replaces the adapter in
  tests; `Email/SmtpEmailSenderTests` run it against a Mailpit container. MimeKit's `System.Security.Cryptography.Pkcs`
  10.0.0 has advisories: `Directory.Packages.props` pins 10.0.13.

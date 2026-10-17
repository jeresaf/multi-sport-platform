# ADR-0005 — Identity and access: ASP.NET Core Identity, permission = action code, scoped role assignments

**Status:** Accepted
**Date:** 2026-10-17
**Deciders:** Owner, Reviewer

## Context

The previous plan defined an action registry and the formula *lifecycle ∧ entitlement ∧ permission ∧ domain
rule*, but never specified users, login, roles, or how a team manager is limited to *their* team and a scorer
to *their* contests. Without this, the admin UI cannot be built.

## Decision

**Authentication**
- ASP.NET Core Identity with EF Core stores in the `identity` schema.
- Web apps (admin, public accounts later) use secure, HTTP-only, same-site cookies against the API.
- Mobile and API clients use bearer tokens (Identity API endpoints). Revisit OpenIddict only if third-party
  OIDC/SSO is required (Enterprise).
- Email confirmation, password reset, lockout and login rate limiting are on from the start.
- The first platform administrator is created by a CLI command from environment-provided values — never a
  default password in code or seed files.

**Authorization**
- **One catalogue: action codes** (`person.create`, `contest.start`, `result.approve`, …). A permission *is*
  an action code; there is no second permission vocabulary.
- A **role** is a named set of action codes. System roles are seeded (docs/architecture/authorization.md);
  tenant-custom roles come later.
- A **role assignment** links a tenant membership to a role **with a scope**: `TENANT`, `COMPETITION`,
  `TEAM` or `CONTEST` (+ scope id). A TEAM_MANAGER assignment scoped to Team X allows roster/lineup actions
  only for Team X.
- Owning modules expose a **scope chain** for their resources through Contracts (e.g. contest → edition →
  competition → tenant), so the evaluator can check scoped assignments without reading other modules'
  tables.
- Final decision for every state-changing request:
  `access mode (tenant + subscription) ∧ entitlement (if the action needs one) ∧ permission (scoped) ∧ domain rule`.
- Entitlements map to action codes as well (docs/architecture/authorization.md), satisfying the owner's
  requirement that "each action should be documented and added to the subscription model". The action
  catalogue is generated from code and checked in CI, so it cannot silently fall behind the system.

## Alternatives considered

- **External IdP (Auth0/Clerk/Entra External ID).** Fast to start; per-user pricing becomes expensive with
  public fan accounts; data-residency questions (ADR-0015). Keep as a later option for SSO.
- **Keycloak.** Capable but another service to run and secure.
- **Separate permission codes (pack).** Rejected: two vocabularies drift; 1:1 is simpler.

## Consequences

- Positive: the admin UI can show/hide actions from one source; audit, analytics and entitlements reuse the
  same codes.
- Negative: we own security-sensitive code; mitigated by using Identity's built-ins, not custom crypto, and a
  security review in hardening.

## Rules affected

AR-06, AR-07.

## Review trigger

A customer requires SSO (SAML/OIDC) or delegated federation identity.

# S06 — People registry

> Written by the S05 slice review (2026-11-16) against what S01–S05 actually built. It replaces the outline. Changes
> from the outline: the decisions below (D1–D24) are written out as reviewer defaults; the owner confirms or changes
> them in each task's plan. Photos and consent evidence go through a new Platform media service with a local-disk
> adapter only (the Azure Blob adapter comes with the first deployment, Q41); the image library is SkiaSharp
> (Q35); `STORAGE_GB` is counted through Tenancy and checked by the uploading module (Q36). The person page has no
> Audit tab until a tenant audit read exists (Q40). Merges stay in S25; nothing from team memberships (S08) or
> competitions (S09). `TEAM_MANAGER`'s resource scopes, which S03–S05 said would come "with S06", move to S08 (S08
> builds the `TEAM` scope chain; S06 adds no resource below the tenant). Task sizes are estimated and the likely PR
> splits named. Q35–Q41 were answered with these defaults (owner, 2026-11-16).

**Goal:** a tenant keeps one canonical record per real person — the owner's "Player Manager" — with strong duplicate
prevention, role profiles (athlete, staff, official) on top of it, minors protected by default with guardian consent,
and photos. It is built so it can grow into a national register (docs/architecture/national-registry.md): stable IDs,
external identifiers with an authority, aliases, normalised names, field classification.
**Modules touched:** Registry (new: people, aliases, external identifiers, duplicate check, profiles, consent, the
public person read), Platform (media: `platform.media_assets`, `IFileStorage`, `IMediaAssets`), Tenancy (the
`STORAGE_GB` counter, `tenancy.demo.sports`), Identity (roles for the new actions), Host (catalogues, startup rules),
admin web.
**Depends on:** S05.
**Demo:** Brian Ssemakula, KMSA's registrar, opens **People** → searches "Ssekandi" and finds John Ssekandi, a
seeded Ntinda Swifts player → starts **Add person**, types "Ssekandi John" with the same date of birth → the duplicate
review shows John Ssekandi as **Likely the same person** ("Same date of birth", "Same name, other order") → he opens
the existing record instead → next he adds Shamim Nankya with her twin sister Mariam's surname and birth date: the
review shows Mariam Nankya as **Possibly the same person**; he proceeds with the reason "Twin sister, different first
name", and the warning doesn't come back for the pair → he adds Shamim's athlete profile (Football, Central
midfielder) and a photo → he opens Brenda Akello, 15, an under-17 player: **Minor** badge, public profile off; he
records her mother's consent (Data processing, Public profile, Photo) with a scanned form → turns her public profile on
→ the public person read (the API; the public player page is S14's) shows only her display name and photo → he
withdraws the Photo scope with a reason and the public read drops the photo → Denis Opoka shows athlete, staff
(Assistant coach) and official (Referee, licence until 2027-06-30) profiles on one person → Ivan Mukasa, archived,
can't get a new profile. ELSSC's owner, Agnes Namutebi, sees none of KMSA's people. Duplicate rules, classification
and minors' rules are also shown by their tests.

Read first: ADR-0001, ADR-0004, ADR-0013, ADR-0014, ADR-0015, ADR-0016 (point 6), docs/architecture/modules.md
(Platform, Registry), rules.md (AR-02, AR-04, AR-05, AR-18, AR-21–AR-25, AR-30, AR-31, AR-33),
data-conventions.md (Identifiers, Personal-data classification, Serializing changes, Duplicate prevention, Seeding),
authorization.md (Entitlements: `STORAGE_GB`), api-conventions.md (Pagination, Concurrency, Caching public reads),
docs/domain/state-machines.md (Registry), docs/product/glossary.md, docs/product/pilot-customer.md,
docs/product/ux/admin-app.md (Registry), docs/reference/chatgpt-pack/specifications/08, 09, 12, 13 (background only;
the ADRs and this spec win).

## What S01–S05 give you (use it, don't rebuild it)

- **Modules and persistence:** `IModule` and registration in the Host; one schema and `ModuleDbContext` per module
  (snake_case, UUIDv7 keys, audit columns, `xmin` row version on aggregate roots, `SaveChangesResultAsync` →
  `<AREA>_CONCURRENCY_CONFLICT`); `ITenantOwned` + `EnableTenantRls` + composite tenant foreign keys (the context
  refuses others); the isolation harness (`IsolationSamples`, `TenantIsolationTests`, `RlsCoverageTests`) and
  `TableAccessPolicies`/`TableGrantsTests`. Advisory locks per rule (`<schema>.<purpose>:<id>`, data-conventions.md).
  Architecture tests: `ModuleBoundaryTests`, `LayeringTests`, `TenantIsolationRulesTests`, `SportLiteralTests`,
  `NoDefaultCredentialsTests`.
- **API:** `.RequireAction(...)`/`.AllowPublic()`, `.RequirePublicTenant()` (tenant from host, `Vary:
  X-Forwarded-Host`), `StrictJsonBody`, `.ValidateRequest<T>()` (FluentValidation, `errors: [{field, code}]`),
  `IfMatch`/`RowVersion`/`.AcceptsIfMatch()`, `PageInfo` and per-module cursors (`IdCursor`, `TenantCursor`,
  `AuditCursor`), `CacheableJson.Ok(value, maxAge)`, `.ProducesErrors(...)`, `QueryValues`. Problem Details with
  stable codes; `ErrorCatalogue`.
- **Actions, roles, audit:** `ActionDefinition` (scope, scopable-by, audit, entitlement, read-only/blocked
  availability; a startup rule requires `availableWhenReadOnly` on every tenant `.read`); `IdentityRoles` as explicit
  lists (`REGISTRAR` exists but holds only `_tenantContext` reads today); audit middleware and `AuditScope` (one entry
  per audited action, reason where `REQUIRED_WITH_REASON`), `AuditMetadata` with `[PersonalData(...)]` masking
  `PRIVATE`/`SENSITIVE` as `"[REDACTED]"` (`AuditMetadataTests`). Column classification as
  `.HasAnnotation("pii", "PRIVATE")` (Identity's users and invitations). There is **no** public-DTO classification
  test yet (AR-21 says "[test]"): S06 adds it with the first public person read.
- **Tenancy and Sports Configuration Contracts:** `ITenantAccess.GetAccessAsync` (status, mode, **time zone**: "today"
  for a tenant comes from its zone; `TenantCalendar` itself is internal to Tenancy), `IEnabledSports` (S05 D12: "for
  S06–S09"), `ISportDefinitions` (positions, `staff_roles`, `official_roles` with labels), `IEntitlements`
  (`CheckQuotaAsync`), `IQuotaUsage` + `AddQuotaUsage<TCounter>(code)`. Tenant settings already hold
  `MinorRegistrationConsent` (`REQUIRED`, `RECOMMENDED`, `OFF`; default `RECOMMENDED`, Q11) — S09 enforces it.
  Platform's `IReferenceData` checks country codes (nationality).
- **Seeding:** `ISeeder`, `SeedContext.RunCommandAsync(action, tenantId, …)` as the system actor with audit source
  `SYSTEM`, `QueryAsTenantAsync`, `DeterministicIds`, DEMO tenants `tenancy.demo.tenants` (KMSA, ELSSC). No sport is
  enabled by any seed yet (S05 D19 left `tenancy.demo.sports` to "the first slice whose demo data needs a sport": S06).
- **Data protection:** ASP.NET Core Data Protection is registered once by Identity (application name `msp`, keys in
  `identity.data_protection_keys`); any module may use `IDataProtectionProvider` with a purpose of its own.
- **Admin web:** shell and navigation filtered by capabilities (`lib/navigation.ts`), `useTenantWorkspace().can()` and
  `.unavailable()` with `UnavailableNote`, `ActionDialog` (reasons), `ConflictAlert`, `ProblemAlert` and
  `describeProblem`, `QuotaUsage`, `StatusBadge`, `SectionPage`, `PageCard`, `FormField`, `SelectField`, `EmptyState`,
  `Loading`, `lib/form-errors.ts` (field errors from `errors[]`), `lib/reference-data.ts` (`useCountries()`), the
  tenant's time zone (`useTenantTimeZoneOnceRead`), tabs (`@msp/ui/components/tabs`), `lib/version-param.ts`. The
  generated client (`@msp/api-client`). ESLint's sport-literal and colour-literal rules.
- **e2e:** Playwright against the real API; helpers in `web/e2e/tests/support` (`createTenant`, `assignSubscription`,
  `inviteOwnerOf`, `acceptByApi`, `signIn`, `auditEntries`, `shoot` at phone, tablet and desktop); slice demos on test
  tenants of their own (`admin-plans-demo`, `admin-sports-demo`).

## Decisions (reviewer defaults; confirm or change in each task's plan)

- **D1 — The module.** `Modules/Registry/SportsPlatform.Modules.Registry` (+ `.Contracts`), schema `registry`,
  depending on Platform, Tenancy and Sports Configuration Contracts only (modules.md). Every table is tenant-owned:
  `tenant_id`, RLS in the creating migration, an isolation sample each. Persons belong to one tenant (pack RULE-PER-001);
  nothing is shared across tenants (national-registry.md).
- **D2 — The person.** `registry.persons`: `first_name`, `middle_name`, `last_name`, `other_names` (free text, e.g.
  a clan name), `display_name` (defaults to "first last"), `gender` (`FEMALE`, `MALE`, or null = not recorded; used by
  competition categories later), `date_of_birth` (`date`, optional), `nationality` and `secondary_nationality`
  (ISO 3166-1 alpha-2, checked through `IReferenceData`), `height_cm`, `weight_kg` (optional, bounded), `photo_asset_id`,
  `public_profile_enabled`, `status`, `source` and `import_job_id` (provenance, data-conventions.md; `MANUAL` or
  `SEED` in S06). First and last name are required; everything else is optional. No contact details, no national ID
  numbers and no medical data in S06 (ADR-0015).
- **D3 — Classification.** Each column is annotated (`pii`) and each DTO property carries `[PersonalData]`:

  | Class | Fields |
  |---|---|
  | `PUBLIC` | display name, photo, nationality, an athlete profile's sport, position and public name |
  | `INTERNAL` | first, middle, last and other names; aliases; gender; birth year; minor or not; height; weight; status; public-profile flag; external identifiers; profile grades and licence numbers; consent scopes and dates |
  | `PRIVATE` | full date of birth; guardian name, relationship and contact; consent evidence |

  `PUBLIC` fields reach the public read only under D18's rules. `person.read` answers `INTERNAL` and `PUBLIC` fields
  (birth year and `isMinor`, never the full date); `person.read_private` adds the `PRIVATE` ones. Audit metadata and
  logs never carry `PRIVATE` values (redaction test); audit entries name people by ID.
- **D4 — Normalised names.** In code (Domain, unit-tested), stored in columns: each name part trimmed, inner
  whitespace collapsed, lower-cased (invariant), accents removed (NFD, non-spacing marks dropped), apostrophes, hyphens
  and dots removed. The **name key** is the normalised tokens of first, middle, last and other names, sorted and joined
  with one space, so "Ssekandi John" and "John Ssekandi" share `john ssekandi`. Each alias has its own key. The
  extension `pg_trgm` (`CREATE EXTENSION IF NOT EXISTS pg_trgm` in schema `public`, owned by `sp_migrator`; a trusted
  extension, no superuser needed) gives a GIN `gin_trgm_ops` index on the name keys; Npgsql's
  `EF.Functions.TrigramsSimilarity` reads it (no new package). ADR-0013 names `pg_trgm`; it isn't new infrastructure.
- **D5 — The duplicate check.** `POST /api/admin/v1/people/duplicate-check` (action `person.create`, not audited: it
  saves nothing) takes the candidate's names, date of birth, nationality and external identifiers, and answers each
  existing person (not `MERGED`; `ARCHIVED` included and marked) that matches, with a confidence and its evidence:
  - `EXACT_IDENTIFIER`: an external identifier with the same authority and value (blocks creation);
  - `LIKELY`: same date of birth and (same name key, or name-key similarity ≥ 0.8);
  - `POSSIBLE`: similarity ≥ 0.6 and (either date of birth missing, or birth years within one year, or same
    nationality); or same name key with a different date of birth.

  Names are compared on the person's name key and every alias key; the best match counts. Evidence is a list of codes
  with values that aren't `PRIVATE` (`SAME_DATE_OF_BIRTH`, `BIRTH_YEAR_WITHIN_ONE`, `SAME_NAME_KEY`,
  `NAME_SIMILARITY` with the percentage, `SAME_NATIONALITY`, `ALIAS_MATCH`, `SAME_EXTERNAL_ID` with its authority),
  never the other person's date of birth, so the check is safe for `person.create` holders without
  `person.read_private`. Each candidate carries id, display name, birth year, status, and the athlete profiles' sports.
  At most 20 candidates, strongest first. Pairs recorded as "not the same person" (D7) are left out. The thresholds are
  constants in one place, tested at their edges; the T2 plan confirms them against the DEMO data.
- **D6 — Creating requires a check.** The check answers a `checkToken`: Data Protection (purpose
  `registry.duplicate-check`, 30 minutes) over the tenant, the actor, a hash of the normalised inputs and the
  candidate IDs. `POST /api/admin/v1/people` (`person.create`, audit `REQUIRED`) needs it and holds
  `registry.people:<tenantId>` while it runs the check again and saves:
  - no token, an expired one, or one for other inputs or another actor → `422 PERSON_DUPLICATE_CHECK_REQUIRED`;
  - an `EXACT_IDENTIFIER` match → `409 PERSON_EXTERNAL_ID_EXISTS` (on the identifier's field);
  - a candidate that the token doesn't list (someone created a match meanwhile) → `409 PERSON_DUPLICATE_CHECK_STALE`;
  - `LIKELY` or `POSSIBLE` candidates and no `overrideReason` (1–500 characters) → `422
    PERSON_DUPLICATE_OVERRIDE_REQUIRED`.

  With a reason, the person is created and a "not the same person" decision is recorded for each candidate shown. The
  audit entry names the candidates and their confidence (IDs and codes only) and keeps the reason. Two registrars
  creating the same person at once: one succeeds, the other gets `PERSON_DUPLICATE_CHECK_STALE` (the lock serialises
  them; a retried create is caught the same way, so no `Idempotency-Key` is needed).
- **D7 — Decisions are remembered.** `registry.duplicate_decisions`: two person IDs (ordered, unique pair), decision
  `NOT_SAME`, reason, decided by and at. Written only by a create or an update that proceeds past candidates (D6, D8).
  The check never shows a decided pair again. Undoing a decision and reviewing existing pairs (rescans) come with merges
  (S25).
- **D8 — Changing a person.** `PUT /api/admin/v1/people/{id}` (`person.update`, audit `REQUIRED`, `If-Match`)
  replaces the biodata. A change to names, date of birth, nationality or external identifiers runs the check
  as D6 does (the same token, the person itself left out); other changes don't. Status changes (D9) have their own
  actions. Display name, height and weight need no check.
- **D9 — Person lifecycle.** Stored states from state-machines.md: `ACTIVE`, `INACTIVE`, `DECEASED`, `ARCHIVED`
  (`MERGED` is S25's). Transitions: `ACTIVE → INACTIVE` (`person.deactivate`) and back (`person.reactivate`);
  `ACTIVE | INACTIVE → ARCHIVED` (`person.archive`, `REQUIRED_WITH_REASON`) and back to `ACTIVE`
  (`person.unarchive`, `REQUIRED_WITH_REASON`); `ACTIVE | INACTIVE → DECEASED` (`person.mark_deceased`,
  `REQUIRED_WITH_REASON`; final in S06). History in `registry.person_status_changes` (from, to, reason, by, at). An
  `ARCHIVED` or `DECEASED` person accepts no new profile, alias, identifier, consent or photo (`409 PERSON_ARCHIVED`,
  `409 PERSON_DECEASED`); reads still show everything. No hard deletes (AR-18). state-machines.md gets the transitions
  "as built" (Q39).
- **D10 — Aliases and external identifiers.** `registry.person_aliases`: name, kind (`FORMER_NAME`, `NICKNAME`,
  `SPELLING`), its normalised key; removing one stamps removed by, at and why (history kept). External identifiers in
  `registry.external_identifiers` as data-conventions.md says (`entity_type` `PERSON` in S06; S07 adds its own types):
  authority (`^[A-Z][A-Z0-9_]{1,39}$`, free per tenant, e.g. `KMSA_REG_NO`, `SCHOOL_ADMISSION_NO`), value (trimmed,
  1–100), `valid_from`, `valid_to`; unique `(tenant_id, authority, entity_type, value)`, ended ones included (a value is
  never reused). Authorities that name national identity documents (`NATIONAL_ID`, `NIN`, `PASSPORT`) are refused
  (`422 PERSON_EXTERNAL_ID_AUTHORITY_RESERVED`): ADR-0015 makes them `SENSITIVE` and S06 doesn't collect them. Actions
  `person.alias.add`, `person.alias.remove` (`REQUIRED_WITH_REASON`), `person.external_id.add` (runs the exact check:
  `409 PERSON_EXTERNAL_ID_EXISTS`), `person.external_id.end` (sets `valid_to`). External IDs never change a person's ID
  (AR-23).
- **D11 — Child rows bump the version.** Aliases, identifiers, profile capabilities and consents are children of an
  aggregate; a change to only a child must bump the root's `xmin` (the S01-T2a tech-debt item, "S06 at the latest").
  T1a marks the root `Modified` when a dependent changes (in `ModuleDbContext`, BuildingBlocks.Persistence), with a test
  that two edits to different children of one person conflict.
- **D12 — Role profiles.** Separate from the person (glossary):
  - **Athlete profile** (`registry.athlete_profiles`): one open per person and sport variant; the variant must be
    enabled for the tenant (`IEnabledSports`, `422 PERSON_SPORT_NOT_ENABLED`); primary position (a code of the
    variant's latest definition, `422 PERSON_POSITION_UNKNOWN`), other positions, dominant side (`LEFT`, `RIGHT`,
    `BOTH`, or null), public name (e.g. "J. Ssekandi"; defaults to the display name).
  - **Staff profile** (`registry.staff_profiles`): one per person, with capabilities (variant + a `staff_roles` code
    of its definition, optional qualification text up to 100 characters).
  - **Official profile** (`registry.official_profiles`): one per person, with capabilities (variant + an
    `official_roles` code, optional grade text up to 50, licence number up to 50, licence valid until). An expired
    licence is shown as expired on the tenant's date; nothing blocks on it in S06 (S11's assignments may).

  Statuses from state-machines.md: profiles start `ACTIVE`; S06 moves them `ACTIVE ⇄ INACTIVE`, `→ RETIRED` and
  `→ ARCHIVED` (`*.status.change`, `REQUIRED_WITH_REASON`); `SUSPENDED` waits for discipline (S13+). A disabled
  variant keeps its profiles (S05 D12: disabling never touches history) but takes no new ones. Actions:
  `athlete_profile.create`, `athlete_profile.update`, `athlete_profile.status.change` and the same for
  `staff_profile` and `official_profile` (audit `REQUIRED`; status changes with reason). Labels come from the
  definition (AR-29); generic code never names a sport (AR-11).
- **D13 — Minors.** A person is a minor when under 18 on the tenant's date ("today" in the tenant's time zone,
  `ITenantAccess`), computed on every read, never stored (ADR-0015). A person without a date of birth is treated as a
  minor for public exposure ("Age unknown"; Q38). Eligibility's own reference date is S09's. The admin read answers
  `isMinor` and `ageStatus` (`ADULT`, `MINOR`, `UNKNOWN`).
- **D14 — Guardian consent.** `registry.guardian_consents`: guardian name, relationship (`MOTHER`, `FATHER`, `GUARDIAN`,
  `OTHER`), contact (phone or email, free text), scopes (`DATA_PROCESSING`, `PUBLIC_PROFILE`, `PHOTO`), the date given,
  an optional evidence asset (PDF or image, `CONSENT_EVIDENCE`, `PRIVATE`), recorded by and at; withdrawn by, at and
  why. Withdrawing a scope closes the record and records a new one with the remaining scopes, so the history stays
  (states `ACTIVE → WITHDRAWN`, Q39). Actions `person.consent.record` (`REQUIRED`), `person.consent.withdraw`
  (`REQUIRED_WITH_REASON`), `person.consent.evidence.upload` (`REQUIRED`). The person read shows the active scopes
  (`INTERNAL`); the records, with the guardian's details and the evidence, need `person.read_private`. Consent can be
  recorded for anyone whose age status isn't `ADULT`. The `DATA_PROCESSING` scope's wording on screen states where data
  is stored and names the sub-processors (ADR-0015, ADR-0016); the text is the owner's (Q13's lawyer), the screen shows
  a placeholder sentence marked as such until then. S06 doesn't enforce `MinorRegistrationConsent` (registration is
  S09).
- **D15 — The public profile.** `public_profile_enabled` defaults to off for everyone (Q37). Turning it on
  (`person.public_profile.enable`; `.disable`, both `REQUIRED`) for a person whose age status isn't `ADULT` needs an
  active consent with `PUBLIC_PROFILE` (`422 PERSON_CONSENT_REQUIRED`). The public read re-checks every time (D18), so
  a withdrawn scope hides at once without touching the flag.
- **D16 — Media in Platform (ADR-0014).** `platform.media_assets` (tenant-owned, RLS): purpose (`PERSON_PHOTO`,
  `CONSENT_EVIDENCE` in S06; the ADR's others later), content type, size in bytes (all variants), storage key,
  variants (`jsonb`: name, key, width, height, bytes), visibility (`PRIVATE` in S06), uploaded by and at, status
  (`ACTIVE → REMOVED`, Q39). `IFileStorage` (Platform, provider-neutral): put, open, delete, signed read URL. S06 builds
  the **local-disk adapter** only (`Media:LocalRoot`, a temp folder in tests, git-ignored `.media/` in Development);
  the Azure Blob adapter (and AR-33's allow-list of adapter assemblies) comes with the first deployment (Q41).
  `IMediaAssets` (Platform Contracts): store (stream, purpose, content type → asset ID or a `MEDIA_*` error), remove,
  open a variant, a signed URL for a variant, and the tenant's stored bytes. Platform checks size and type by the
  file's first bytes, not the client's content type: photos JPEG, PNG or WebP up to 5 MB; evidence PDF, JPEG or PNG up
  to 10 MB (`422 MEDIA_TYPE_NOT_ALLOWED`, `413 MEDIA_TOO_LARGE`). Photos are re-encoded into `thumbnail` (160 px),
  `medium` (640 px) and `large` (1600 px, the longest side), which drops EXIF data such as GPS positions; the original
  isn't kept. Evidence is stored as uploaded. The image library is SkiaSharp (MIT) with
  `SkiaSharp.NativeAssets.Linux.NoDependencies`, a new native dependency: S06-T5a starts with its ADR (Q35).
- **D17 — URLs.** Private files are served by short-lived signed URLs after a permission check (ADR-0014): the owning
  module checks its read action, then asks `IMediaAssets` for a URL. The local adapter's URL is
  `/api/public/v1/media/{token}` (`.AllowPublic()`, no tenant from the host; the token is Data Protection, purpose
  `platform.media`, 10 minutes, naming tenant, asset and variant; the endpoint sets the scope's tenant from the token
  before it reads, so RLS still applies; `Cache-Control: private, no-store`); Azure's will be a SAS URL. Admin reads
  carry `photo: { thumbnailUrl, mediumUrl, largeUrl, expiresAt }`; the consent read carries the evidence URL. The
  **public person read** (D18) serves its photo through `GET /api/public/v1/people/{id}/photo?variant=`
  (`.RequirePublicTenant()`), which re-checks D18's rules on every request and streams the variant with `Cache-Control:
  public, max-age=300`, so a withdrawn `PHOTO` scope leaves edge caches within five minutes. Person photos stay
  `PRIVATE` assets; nothing flips visibility.
- **D18 — The public person read.** `GET /api/public/v1/people/{id}` (`.RequirePublicTenant()`, `CacheableJson`, 60
  s): `404 PERSON_NOT_FOUND` unless the person is `ACTIVE`, the public profile is on and, for a person whose age
  status isn't `ADULT`, an active consent has `PUBLIC_PROFILE`. An adult: display name, nationality, photo, athlete
  profiles (variant, position, public name). A minor: display name only, plus the photo with `PHOTO` (ADR-0015). Team and
  age group come with teams (S08, S14). A test asserts every property of every public DTO is `[PersonalData(PUBLIC)]`
  (AR-21's "[test]", for all modules, as the public-DTO check in data-conventions.md's CI list).
- **D19 — Photos and evidence endpoints.** `PUT /api/admin/v1/people/{id}/photo` (multipart, one file;
  `person.photo.set`, audit `REQUIRED`) stores a new asset and marks the old one `REMOVED`; `DELETE …/photo`
  (`person.photo.remove`). `PUT …/people/{id}/consents/{consentId}/evidence` (multipart). JSON bodies stay JSON: a
  consent is recorded first, its evidence uploaded next (two calls, one dialog). Multipart endpoints keep the `X-CSRF:
  1` check and turn off ASP.NET Core's antiforgery for the form (`DisableAntiforgery()`), with a test that a request
  without `X-CSRF` is refused. Removed files stay on disk until a retention job (Later).
- **D20 — `STORAGE_GB` (Q36).** Platform counts the tenant's bytes (`ACTIVE` assets, every variant). Platform
  depends on no other module (modules.md), so it can neither implement Tenancy's `IQuotaUsage` nor call
  `IEntitlements`. Tenancy registers the `STORAGE_GB` counter (`AddQuotaUsage`), reading the bytes through Platform
  Contracts (Tenancy → Platform is allowed), in whole GB rounded down, so the Subscription page shows "0 of 10 GB" until
  the first full gigabyte. The uploading action names `STORAGE_GB`, and its handler (Registry in S06) checks
  `IEntitlements.CheckQuotaAsync` before storing: at the limit `403 ENTITLEMENT_QUOTA_EXCEEDED` with "Your plan allows
  N GB of storage.". The entitlement's `enforcedBy` ("Platform" since S04) becomes the uploading modules. No lock: two
  uploads at the limit may both pass, at most 10 MB over (accepted; the next one is refused).
- **D21 — Actions and roles.** Reads: `person.read` (directory, detail, profiles, aliases, identifiers; available while
  read-only; audit `NONE`) for `TENANT_OWNER`, `TENANT_ADMIN`, `REGISTRAR`, `COMPETITION_MANAGER`, `RESULTS_OFFICER`,
  `DATA_ENTRY`, `VIEWER`; `person.read_private` (date of birth, consents and their evidence; available while
  read-only; audit `REQUIRED`, one entry per read naming the person) for `TENANT_OWNER`, `TENANT_ADMIN`, `REGISTRAR`.
  Every change (create and duplicate check, update, lifecycle, aliases, identifiers, profiles, consent, public profile,
  photo) for `TENANT_OWNER`, `TENANT_ADMIN`, `REGISTRAR`. `SCORER` and `TEAM_MANAGER` get none in S06 (S08 decides
  what a team manager may do for their team's people). Merges (`person.merge`) are S25's. Names follow
  docs/generated/actions.md; the T1 plan confirms them.
- **D22 — Reads and Contracts.** `GET /api/admin/v1/people` (cursor, `limit` 1–100 default 25 typed as integers in
  OpenAPI; `q` matches a name or alias by prefix or similarity and an external identifier's value exactly; filters
  `status` (default every state but `ARCHIVED`), `ageStatus`, `profile` (`ATHLETE`, `STAFF`, `OFFICIAL`), `variant`;
  sorted by last name, first name, id); `GET …/people/{id}` (with `ETag`); `GET …/people/{id}/private`
  (`person.read_private`); `GET …/people/{id}/status-changes`. Registry Contracts: `IPeopleDirectory` (summaries by IDs:
  id, display name, status, age status; for S07–S09), no integration events until a consumer needs one (as S05 D20).
- **D23 — Seeds and test data.** `tenancy.demo.sports` (S05 D19's, pulled forward): football enabled for KMSA and ELSSC
  through `tenant.sport.enable` as `SYSTEM`, skipped when enabled. `registry.demo.people` through the Registry commands
  (duplicate check, create with reasons, profiles, consents; photos and evidence are generated placeholder images, not
  photos of anyone). KMSA: about 40 people — players of the Metro Football League clubs; John Ssekandi (the demo's
  likely match); Peter Ochieng and Peter Ochieng Okot, a possible pair already decided "not the same person"; Mariam
  Nankya (her twin Shamim is the demo's), and twins Babirye Lillian Namaganda and Nakato Irene Namaganda with one birth
  date; Denis Opoka (athlete, assistant coach and referee); about 10 under-17 players, Brenda Akello among them, with
  consents for some; Ivan Mukasa, archived ("Left the association"). ELSSC: about 10 pupils, all minors, most with
  consent. Every name is invented (pilot-customer.md); none is one of the pilot staff. Tests: `RegistryTestData` creates
  people through the handlers; the isolation samples cover every new table.
- **D24 — Docs.** modules.md (Registry and Platform media as built), data-conventions.md (the classification as
  built, `pg_trgm`, the name key, the external identifiers' table, the seeders), state-machines.md (person, profile,
  consent and media transitions, Q39), authorization.md (the person actions and roles; `STORAGE_GB` as built),
  api-conventions.md (multipart uploads, signed media URLs), admin-app.md (Registry: People as built),
  pilot-customer.md (the seeded people and S06's test tenants), glossary.md if a term is added, CLAUDE.md (`Media:`
  settings or any new command).

## Out of scope

Merges, duplicate rescans and undoing a decision (S25); team memberships, staff assignments and the team page (S08);
registrations and enforcing `MinorRegistrationConsent` (S09); contest officials' assignments (S11); the person page's
Teams & history, Competitions, Matches and Statistics tabs (their slices); the Audit tab (Q40); public player pages
(S14); bulk import of people (`BULK_IMPORT`, Data Exchange); contact details and national ID numbers; data-subject
export and correction tooling (Phase 9); the Azure Blob adapter (Q41); image or media screens for other purposes
(logos come with S07); search beyond the directory (S20); tenant-configurable duplicate thresholds.

## Tasks

### T1 — Registry module, the person and the reads (~2 PRs, about 1,100 lines)
Deliver:
- The module (D1), `registry.persons` with classification annotations (D2, D3), `person_aliases`,
  `external_identifiers`, `person_status_changes` (tables only where T3 adds the commands), normalisation and the name
  key (D4, unit tests with Luganda, Luo and English names, accents and reordering), the `pg_trgm` extension and indexes,
  isolation samples, table policies; D11's version bump in `ModuleDbContext`.
- `person.read`, `person.read_private` and the reads (D22) with `IPeopleDirectory`; the minors' computation (D13) in
  the reads; `RegistryTestData` creating people through the domain until T2's handler exists.
- The public-DTO classification test (scans every response type of the public endpoints) and a redaction test for the
  person's `PRIVATE` fields in logs and audit metadata.
- Likely T1a (module, model, normalisation, tables, D11) and T1b (reads, roles, classification and redaction tests,
  catalogues, OpenAPI, client).

Acceptance:
- [x] Every tenant table of the module has RLS and an isolation sample; `RlsCoverageTests`' exception list is
      unchanged.
- [x] "Ssekandi John" and "John Ssekandi" have one name key; "Nansubuga" and "Nänsubuga" normalise alike (unit tests).
- [x] A change to only a child row of an aggregate conflicts with a concurrent change to another child (test).
- [x] `person.read` answers no `PRIVATE` field; `person.read_private` is audited per read; a viewer gets `403` on the
      private read (tests).

### T2 — Duplicate check and create (~2 PRs, about 1,000 lines)
Deliver:
- The check (D5) with its endpoint, the token (D6), `POST /api/admin/v1/people` with the lock and the four refusals,
  `duplicate_decisions` (D7), the audit entry (candidate IDs, confidence, reason).
- Validation field names convert every path segment (`externalIdentifiers[0].value`, the "first nested request body"
  tech-debt item).
- Likely T2a (matching, thresholds, the check endpoint, table-driven tests) and T2b (token, create, decisions,
  concurrency test, errors).

Acceptance:
- [ ] Table-driven tests: exact identifier blocks; same birth date with reordered names is `LIKELY`; twins with one
      birth date and different first names are `POSSIBLE` or no match, never `LIKELY`; similarity 0.59 / 0.6 / 0.79 /
      0.8 on either side of each threshold; missing birth dates; an alias match; an archived person is found and
      marked; a decided pair isn't shown.
- [ ] Create without a check, with a stale check or past candidates without a reason is refused with its code; with a
      reason it succeeds, the audit entry keeps the reason and the pair isn't warned again (tests).
- [ ] Two creates of the same person at once: one succeeds, one gets `409 PERSON_DUPLICATE_CHECK_STALE` (test).
- [ ] The check's answer carries no `PRIVATE` value (test).

### T3 — Changing a person: biodata, lifecycle, aliases, identifiers (~1–2 PRs, about 800 lines)
Deliver:
- `PUT …/people/{id}` with `If-Match` and the check on identity fields (D8); lifecycle actions and history (D9);
  aliases and external identifiers (D10) with the reserved authorities; `PERSON_ARCHIVED`/`PERSON_DECEASED` guards.
- Likely one PR; split into T3a (update, lifecycle) and T3b (aliases, identifiers) if it passes ~600 lines.

Acceptance:
- [ ] Every transition in D9 is allowed, every other refused (`409 PERSON_STATUS_INVALID`), each with its history row
      and audit reason (table-driven).
- [ ] An identifier already used by another person, even ended, is refused with `PERSON_EXTERNAL_ID_EXISTS` (test).
- [ ] A name change that creates a `LIKELY` match needs a check and a reason (test).

### T4 — Role profiles (~2 PRs, about 1,000 lines)
Deliver:
- Athlete, staff and official profiles (D12) with their capabilities, status changes and reads; definition codes
  checked through `ISportDefinitions`, variants through `IEnabledSports`.
- Likely T4a (athlete profiles) and T4b (staff and official profiles).

Acceptance:
- [ ] A profile for a variant the tenant hasn't enabled, or a position or role code the definition doesn't have, is
      refused with its code (tests with S05's test-only definitions, `TestSportDefinitions`).
- [ ] One person holds athlete, staff and official profiles at once; an archived person gets none (tests).
- [ ] No sport code in the module's generic code (`SportLiteralTests` stays green).

### T5 — Media and photos (~2 PRs, about 1,100 lines)
Deliver:
- Platform: `platform.media_assets` (isolation sample), `IFileStorage` with the local-disk adapter, `IMediaAssets`,
  checks by the file's bytes, photo variants with the chosen library (Q35), signed URLs and the token endpoint (D16,
  D17), the stored-bytes count; Tenancy's `STORAGE_GB` counter (D20).
- Registry: the photo endpoints (D19), photo URLs in the admin reads.
- Likely T5a (Platform media, adapter, variants, URL endpoint, counter) and T5b (person photo, quota check).

Acceptance:
- [ ] A file whose bytes aren't an allowed type is refused whatever its content type; a 5 MB + 1 byte photo is `413`
      (tests).
- [ ] Variants have no EXIF data (test with a JPEG carrying a GPS tag).
- [ ] A signed URL works until it expires and never for another tenant's asset; the token endpoint never caches
      (tests with the fake clock).
- [ ] At the `STORAGE_GB` limit the upload is `403 ENTITLEMENT_QUOTA_EXCEEDED`; the usage reads count the stored
      bytes in whole GB (tests, with an override of 0 and a counter test over a known total).

### T6 — Minors, consent and the public read (~2 PRs, about 1,000 lines)
Deliver:
- Consent records, withdrawal, evidence upload (D14); the public-profile actions (D15); the public person read and its
  photo route (D17, D18).
- Likely T6a (consent and evidence) and T6b (public profile, public read, photo route).

Acceptance:
- [ ] A minor's public profile can't be turned on without `PUBLIC_PROFILE` consent; with it, the public read shows
      only the display name, and the photo only with `PHOTO` (tests).
- [ ] Withdrawing `PHOTO` drops the photo from the public read and makes the photo route `404` at once (test).
- [ ] A person turning 18 on the tenant's date is an adult that day, not the day before (test across midnight in
      `Africa/Kampala`, with the fake clock).
- [ ] The public read answers only `PUBLIC` fields (the T1 test covers it) and nothing for another tenant's host.

### T7 — Screens, demo data and slice close (~4 PRs, about 2,000 lines)
Deliver:
- **People** in the tenant workspace (navigation "People" under a Registry group, `person.read`): the directory
  (search, filters, cursor paging, status and Minor badges); **Add person**, search-first (type a name → existing
  matches → continue → biodata → the duplicate review: each candidate side by side with its evidence, "Open
  existing", or proceed with a reason); the person page: header (photo, display name, status, Minor or Age unknown,
  primary actions), tabs **Overview** (biodata; the date of birth only with `person.read_private`), **Roles**
  (profiles), **Consent** (shown when the age status isn't `ADULT` or a consent exists), **External IDs & aliases**;
  photo upload and removal; lifecycle dialogs with reasons (`ActionDialog`). Labels from the definitions (AR-29).
- `tenancy.demo.sports` and `registry.demo.people` (D23); pilot-customer.md lists them.
- The reused-database e2e item (PROGRESS.md, "From S04"): `admin-shell` and `admin-screenshots` stop adding owners to
  KMSA (tenants of their own, added to pilot-customer.md's test-only names).
- Playwright: the slice demo on a test tenant of its own (as S05's), screenshots of the new screens at phone, tablet and
  desktop, light and dark; `describeProblem` for the new codes; admin-app.md as built. Slice close.
- Likely T7a (directory and person page read-only, the e2e item), T7b (search-first create and the duplicate review),
  T7c (profiles, consent, photo, identifiers, lifecycle), T7d (seeds, demo e2e, screenshots, slice close).

Acceptance:
- [ ] The duplicate review shows evidence, never another person's full birth date (Vitest).
- [ ] A user without `person.read_private` sees the birth year, not the date, and the consent scopes without the
      guardian's details or the evidence (Vitest).
- [ ] Missing photos, long names (four name parts) and a person with no profile render at phone width (Vitest,
      screenshots).
- [ ] The slice demo runs end to end against the real API (Playwright).

## Carried over from S05 (slice review 2026-11-16)

Each is assigned above; remove it from PROGRESS.md "Tech debt" when its task merges.
- e2e on a reused local database: KMSA passes STANDARD's 100 staff users after enough runs (owner, #93 R3) — T7a.
- `xmin`/`updated_*` cover only the aggregate root's row (S01-T2a, "S06 at the latest") — T1a (D11).
- Validation field names camelCased by their first letter only (#22 re-review N2, "with the first nested request
  body") — T2b.
- OpenAPI list queries typed as strings (S03-T6c1): S06's new lists type `limit` and enums from the start (D22); the
  old endpoints keep their own trigger.
- `TEAM_MANAGER`'s capabilities and sport reads "with resource scopes (S06)" move to S08, which builds the `TEAM` scope
  chain; S08 corrects the `IdentityRoles` comment and authorization.md.
- `tenancy.demo.sports` (S05 D19, "the first slice whose demo data needs a sport") — T7d (D23).

## Slice acceptance

- [ ] Duplicate scenarios behave as D5–D8 say; override reasons are audited and decided pairs aren't warned again.
- [ ] The public read never answers an `INTERNAL` or `PRIVATE` field (classification test over every public DTO);
      minors follow ADR-0015 and consent (tests); `PRIVATE` values never reach logs or audit metadata (redaction test).
- [ ] Every new tenant table is covered by the isolation harness; the RLS exception list is unchanged.
- [ ] One person holds athlete, staff and official profiles; profiles follow the tenant's enabled sports and the
      definitions' codes; no sport code in generic backend or web code.
- [ ] Photos and consent evidence are stored through Platform media with signed URLs; `STORAGE_GB` counts them.
- [ ] Docs updated (D24); the S05 carry-overs above are done or moved, and PROGRESS.md says where.

## Implementation notes

(The builder adds notes here as tasks merge.)

- **T1a:** the module, the four tables and D11. Owner decisions (plan, 2026-11-16): external identifiers have no
  foreign key to `persons` and the command calls `context.MarkChanged(person)`; D11 moves the version only of a root
  that is loaded; D11 applies to every module's context. The conflict error is `PERSON_CONCURRENCY_CONFLICT`
  (api-conventions.md has no `REGISTRY_` area). `Person` has `Create` and `AddAlias` only; status transitions, alias
  removal and identifier commands are T3's. Bounds: height 50–260 cm, weight 10–300 kg (one decimal). `pg_trgm` is
  declared without a schema (Npgsql's snapshot drops one) and a test checks it lands in `public`. Name keys drop
  everything that isn't a letter or a digit, more than D4's apostrophes, hyphens and dots, so stray punctuation or an
  invisible character doesn't change a key. Review of #95 (owner's answers, 2026-11-16): T2's plan tests "Okello-Owino"
  against "Okello Owino" at the thresholds (hyphens stay removed, not turned into spaces); T2's validation refuses a
  name whose key is empty with a 422.
- **T1b:** the reads, the two actions, the roles and `IPeopleDirectory`. Owner decisions (plan, 2026-11-17): the
  directory's `profile` and `variant` filters come with T4 (the profile tables don't exist yet); the public-DTO test is
  strict (an unclassified property fails), so the site, reference and health responses are classified `PUBLIC`; `q`
  uses trigram similarity ≥ 0.3 (`ListPeopleHandler.SearchSimilarity`); one PR. As built: `q` matches a prefix of the
  normalised first name, last name, name key or an alias key, or similarity of the name key or an alias key, or an
  external identifier's value exactly; removed aliases don't match; results stay sorted by name, not by relevance.
  `isMinor` is true unless the age status is `ADULT`, so a person without a date of birth (`UNKNOWN`) is treated as a
  minor (owner, review #96 Q1). The detail
  has no `photo` property until T5 and no profiles or consent scopes until T4 and T6; `/private` answers the date of
  birth only until T6 adds consents, with `Cache-Control: no-store`. The status history is not paged. The cursor
  holds the last person's normalised names and ID (base64url). Tests move a person to `ARCHIVED` in the table until
  T3's commands exist.
- **T2a:** the duplicate check without the token (T2b). Owner decisions (plan, 2026-11-17), all three changing D5:
  a **sibling rule** — the same date of birth and the same normalised last name is `POSSIBLE`, with the evidence code
  `SAME_LAST_NAME` (D5's thresholds found neither demo twin pair: "Mariam Nankya" and "Shamim Nankya" score 0.33);
  the check has **its own action**, `person.duplicate_check` (audit `NONE`), because every request of an audited
  action is audited and `person.create` is `REQUIRED`; `registry.duplicate_decisions` comes with T2a and the check
  reads it. As built: the request takes an optional `personId` (the person being changed, D8), which leaves out that
  person and everyone decided "not the same person" as them; a new person has no decisions yet, so the "decided pair
  isn't shown" case is tested through `personId`. `PERSON_NAME_INVALID` (422) for a first or last name without a
  letter or a digit; `PERSON_EXTERNAL_ID_AUTHORITY_RESERVED` (422) already at the check. The nationality is checked
  for its format only (create checks it against the reference data). Candidates carry no athlete sports until T4.
  Scores against the demo data: "Ssekandi John" same key; Peter Ochieng and Peter Ochieng Okot 0.78; "Okello-Owino"
  and "Okello Owino" 0.82 with a first name (0.71 alone).
  Pre-review: a decided pair that shares an external identifier is still answered as `EXACT_IDENTIFIER`;
  `SAME_LAST_NAME` is given whenever the sibling rule holds and the name keys differ; the rows read before
  classifying keep identifier, same-key and same-birth-date matches first. Q42 (the check as an unaudited way to
  learn a date of birth) and Q43 (a future date of birth) are open for T2b.
- **T2b:** the check token, create and the decisions. Owner decisions (plan, 2026-11-17): **Q42** — the check stays
  unaudited and isn't rate-limited; it declares `grantedWith: person.read_private` and a startup rule (`RoleRules`)
  refuses a role that grants it without the private read. **Q43** — a date of birth after the tenant's date is `422
  PERSON_DATE_OF_BIRTH_IN_FUTURE` on `dateOfBirth`, in the check and create (T3's update uses the same
  `PersonDetailsRules`). **Q44** — `SAME_NATIONALITY` counts a nationality in common, primary or secondary on either
  side; the check takes `secondaryNationality`. As built: the check answers `checkToken`; the token's expiry is in
  the token and read with `TimeProvider` (Data Protection's own time limit can't be tested); the check and create
  share `PersonDetailsValidator<T>`, which now checks nationalities against the reference data in the check too
  (T2a: format only). The check takes only what it compares (no gender, height, weight, display name or `validFrom`).
  Create takes no aliases (T3's `person.alias.add`); identifiers may carry `validFrom`. `PERSON_EXTERNAL_ID_EXISTS`
  wins over a reason and names the first taken identifier's field. Refusals after the check runs again are audited
  with the candidates' IDs and confidence. `RegistryTestData.CreatePersonAsync` creates through the handlers.

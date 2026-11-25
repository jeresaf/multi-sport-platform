# S07 — Organisations, teams and venues

> Written by the S06 slice review (2026-11-21) against what S01–S06 actually built. It replaces the outline. Changes
> from the outline: the decisions below (D1–D24) are written out as reviewer defaults; the owner confirms or changes
> them in each task's plan. Only football is published until S15–S16, so the DEMO seed has football teams only and
> "an organisation runs teams in several sports" is shown by test definitions, as S05 D18 showed the sport quota
> (Q51). Duplicate warnings are lighter than people's: no check token and no remembered decisions (Q54). Logos and
> badges are the first `PUBLIC` media assets (D15). Venues come first because organisations and teams point at them.
> Task sizes are estimated and the likely PR splits named. Q50–Q55 carry reviewer defaults until the owner answers.

**Goal:** persistent identities for the clubs, schools and federations of a tenant, their teams, and the venues where
contests happen, so S08 can put people in teams and S09–S10 can enter teams in editions and play at venues.
**Modules touched:** Registry (organisations, teams, venues, their aliases and external identifiers, duplicate
warnings, the public reads, `ITeamDirectory`, `IVenueDirectory`, `IOrganisationDirectory`, the `MAX_TEAMS` counter),
Platform (media: logo and badge purposes, public assets), Tenancy (`MAX_TEAMS` through `IEntitlements`; enabled
sports through `IEnabledSports`), Identity (roles for the new actions), Host (catalogues), admin web.
**Depends on:** S06.
**Demo:** Brian Ssemakula, KMSA's registrar, opens **Organisations** → Ntinda Swifts SC (a multi-sport club) shows its
football teams: Senior Men, Women and Under-17 → he opens **Venues** and adds Kiwatule Meadow Ground (Football, natural
grass, 1,200 seats, `Africa/Kampala`) → he starts **Add team** for Kisaasi Kestrels SC with the name "Kisaasi
Kestrel FC", Senior Men: the form warns that **Kisaasi Kestrels FC** (Senior Men, Football) looks like the same team;
he opens it instead → he renames Bunga Breakers FC (named so since 2020) to "Bunga Lakeside FC" from 1 January this year: the
old name stays as a former name with its dates → he uploads Bunga Lakeside FC's badge and sets its home venue to Kiwatule Meadow Ground
→ the sport picker offers only the tenant's enabled sports → the public team read (the API; the public team page is
S14's) shows the new name, the badge and the home venue, and none of the internal fields → he disbands Seeta Sparrows
FC's Under-17 team with a reason; it stays in the directory under "Disbanded" and frees a team for the plan's limit.
ELSSC's owner, Agnes Namutebi, sees none of KMSA's organisations, teams or venues. The multi-sport rule, the team quota
and the cycle rule are also shown by their tests.

Read first: ADR-0001, ADR-0004, ADR-0013, ADR-0014, ADR-0015, docs/architecture/modules.md (Registry, Platform
media as built), rules.md (AR-02, AR-04, AR-05, AR-11, AR-18, AR-21–AR-25, AR-29, AR-31), data-conventions.md
(Identifiers, Personal-data classification, Serializing changes, Aggregate versions and child rows, Duplicate
prevention, Seeding), authorization.md (Entitlements: `MAX_TEAMS`, `STORAGE_GB`), api-conventions.md (Pagination,
Concurrency, Caching public reads, multipart uploads, signed media URLs), docs/domain/state-machines.md (Registry),
docs/product/glossary.md, docs/product/pilot-customer.md (Seed set), docs/product/ux/admin-app.md (Registry),
docs/product/ux/public-app.md (Team, player and venue pages), docs/reference/chatgpt-pack/specifications/10, 11, 14
(background only; the ADRs and this spec win).

## What S01–S06 give you (use it, don't rebuild it)

- **Registry (S06):** the module, schema `registry`, `RegistryDbContext`, `RegistryActions`, `RegistryErrors`, the
  `NameNormaliser` and name keys, `pg_trgm` with `EF.Functions.TrigramsSimilarity`, `registry.external_identifiers`
  (one table for every registry entity type, `entity_type` `PERSON` so far, unique `(tenant_id, authority,
  entity_type, value)` with ended rows), the alias pattern (removed with a reason, never deleted), the status-history
  pattern (`person_status_changes`, `profile_status_changes`), `Person.EnsureAcceptsNewChildren`, `SportCodes`
  (variants through `IEnabledSports`, codes through `ISportDefinitions`), `TenantToday`, `PublicExposure`, the public
  person read and its photo route, `IPeopleDirectory`, `RegistryTestData`, the `registry.demo.people` seeder and its
  JSON data file.
- **Persistence:** S06 D11's child-row version bump in `ModuleDbContext` (a change below a loaded root moves its version),
  composite tenant foreign keys, RLS and isolation samples (`IsolationSamples`, `TenantIsolationTests`,
  `RlsCoverageTests`), `TableAccessPolicies`, advisory locks (`<schema>.<purpose>:<id>`).
- **Media (S06-T5):** `platform.media_assets` (tenant-owned), `IFileStorage` with the local-disk adapter, `IMediaAssets`
  (store with purpose, remove, open a variant, signed URL, stored bytes), type checks by the file's first bytes,
  SkiaSharp WebP variants (ADR-0017), `MEDIA_*` errors, multipart endpoints with `DisableAntiforgery()` and the `X-CSRF`
  check, `STORAGE_GB` counted by Tenancy and checked by the uploading module.
- **API and authorization:** everything S06 lists (actions, `StrictJsonBody`, `.ValidateRequest<T>()`, `IfMatch`,
  cursors, `CacheableJson`, `.RequirePublicTenant()`, `.ProducesErrors(...)`), plus the public-DTO classification
  test (`PublicDtoClassificationTests`: every property of every public response type is `[PersonalData(PUBLIC)]`) and
  `AuditMetadataTests`.
- **Tenancy and Sports Configuration Contracts:** `IEnabledSports` (`ListAsync`, `IsEnabledAsync`),
  `ISportDefinitions` (`ListAsync` of published variants, `GetAsync`), `IEntitlements.CheckQuotaAsync`, `IQuotaUsage` +
  `AddQuotaUsage<TCounter>(code)`, `ITenantAccess` (the tenant's time zone and today). Platform's `IReferenceData`
  (`IsActiveCountryAsync`, `IsActiveTimeZoneAsync`).
- **Test sports:** `TestSportDefinitions` and `AppWithTestSports` (integration tests): test-only variants of a second
  sport, so a rule that needs two sports is tested without a sport word in code.
- **Admin web:** the Registry navigation group, the People screens and their parts (directory with search in the
  address and **Show more**, detail with tabs in `?tab=`, `ActionDialog`, `ConflictAlert`, `ProblemAlert`,
  `FormField`, `SelectField`, `StatusBadge`, `UnavailableNote`, the photo upload and its preview, the duplicate review
  screen, `lib/form-errors.ts`, `useCountries()`, the tenant's time zone, labels from definitions), the generated client.
- **e2e:** helpers in `web/e2e/tests/support` and the slice demos on test tenants of their own
  (`admin-people-demo` on Coucal Sports Club and Francolin Schools Sports Circle).

## Decisions (reviewer defaults; confirm or change in each task's plan)

- **D1 — Where it lives.** Registry, schema `registry`, `RegistryDbContext` (modules.md: Registry owns organisations,
  teams and venues). Every new table is tenant-owned: `tenant_id`, RLS in the creating migration, an isolation sample
  each. Foreign keys between organisations, teams and venues are composite `(tenant_id, id)` inside the schema; nothing
  points at another module's tables (sport variants are codes, media are asset IDs).
- **D2 — Conflict errors per aggregate (the S06 tech-debt item, #95 Q1).** `RegistryDbContext` answers the conflict of
  the aggregate root whose version failed: `PERSON_CONCURRENCY_CONFLICT`, `ORGANISATION_CONCURRENCY_CONFLICT`,
  `TEAM_CONCURRENCY_CONFLICT`, `VENUE_CONCURRENCY_CONFLICT` (the mapping in one place, tested per root; other modules
  keep one code each). T1 builds it before the first venue saves.
- **D3 — The venue.** `registry.venues`: `name` (1–120), `short_name` (optional, ≤ 40), `venue_type` (`STADIUM`,
  `ARENA`, `SPORTS_GROUND`, `FIELD`, `COURT`, `INDOOR_HALL`, `TRACK`, `MULTI_SPORT_COMPLEX`, `SCHOOL_GROUND`, `OTHER`),
  `address_line` (optional, ≤ 200), `city` (1–100), `region` (optional, ≤ 100), `country` (ISO alpha-2 through
  `IReferenceData`, default the tenant's country), `latitude`/`longitude` (optional, both or neither, `numeric(9,6)`,
  ranges checked), **`time_zone` (required, an active IANA zone through `IReferenceData`; the form defaults to the
  tenant's)**, `capacity` (optional, 0–200,000), `surfaces` (0–5 codes: `NATURAL_GRASS`, `ARTIFICIAL_TURF`,
  `HYBRID_GRASS`, `HARDWOOD`, `SYNTHETIC_COURT`, `CONCRETE`, `CLAY`, `SAND`, `OTHER`), `supported_variants` (0–20
  published variants through `ISportDefinitions`, not necessarily enabled for the tenant: a ground can host a sport
  the tenant doesn't run yet; `422 VENUE_SPORT_UNKNOWN`), `name_key`, `status`, `source`. Codes are generic (AR-11):
  no surface or type names a sport. Pitches inside a complex ("Pitch 2") are S10's to decide if scheduling needs them.
- **D4 — The organisation.** `registry.organisations`: `organisation_type` (`CLUB`, `SCHOOL`, `UNIVERSITY`, `ACADEMY`,
  `FEDERATION`, `ASSOCIATION`, `COMPANY`, `COMMUNITY`, `GOVERNMENT`, `OTHER`), `display_name` (1–120, required),
  `legal_name` (optional, ≤ 200), `short_name` (optional, ≤ 40), `country` (default the tenant's), `city`
  (optional, ≤ 100), `founded_year` (optional, 1800 to the tenant's year), `website` (optional, absolute `https://` or
  `http://` URL, ≤ 200), `email` and `phone` (optional, the body's own contacts, ≤ 200 and ≤ 30), `description`
  (optional, ≤ 2,000, plain text), `primary_venue_id` (optional, a venue of the tenant that isn't `ARCHIVED` or `CLOSED`, `422
  ORGANISATION_PRIMARY_VENUE_UNAVAILABLE`),
  `logo_asset_id`, `name_key`, `status`, `source`. No named contact person: people belong to the person registry,
  and S08 links staff to teams. Registration numbers are external identifiers (D10).
- **D5 — Parent organisation.** One current parent per organisation, kept with its history in
  `registry.organisation_parent_links` (child, parent, `valid_from`, `valid_to`, set by, reason): `POST
  …/organisations/{id}/parent` with `{parentId | null, reason}` (`organisation.parent.set`, `REQUIRED_WITH_REASON`)
  ends the open link on the tenant's today and opens the new one. The parent must be another organisation of the
  tenant, not `DISSOLVED` or `ARCHIVED` (`422 ORGANISATION_PARENT_UNAVAILABLE`), and not the child or any of its
  descendants (`422 ORGANISATION_PARENT_CYCLE`),
  checked under `registry.organisations:<tenantId>`. Depth isn't limited. A child's status never follows its parent's
  (pack RULE-ORG-005). The read shows the parent, the children and the history.
- **D6 — The team.** `registry.teams`: `variant` (required, **enabled for the tenant at creation**, `422
  TEAM_SPORT_NOT_ENABLED`; never changes: another sport is another team), the **category** (D7), `organisation_id`
  (optional; an organisation that isn't `DISSOLVED` or `ARCHIVED`, `422 TEAM_ORGANISATION_UNAVAILABLE`), `name`
  (1–120, the playing name, e.g. "Ntinda Swifts FC"), `short_name` (optional, ≤ 30, e.g. "Ntinda Swifts"), `slug`
  (D9), `code` (optional, `^[A-Z]{3}$`, for scoreboards; not unique: a club's men and women may both be "NTS"),
  `motto` (optional, ≤ 120), colours `primary_colour`, `secondary_colour`, `tertiary_colour` (optional,
  `#RRGGBB`; a secondary without a primary, or a tertiary without a secondary, is `422 TEAM_COLOURS_INVALID`),
  `home_venue_id` (optional, a venue that isn't `ARCHIVED` or `CLOSED`, `422 TEAM_HOME_VENUE_UNAVAILABLE`; a venue that
  doesn't list the variant is accepted, the screen notes it), `name_since` (D8), `logo_asset_id`, `badge_asset_id`, `founded_year`,
  `name_key`, `status`, `source`. A tenant may hold two teams with one name in other categories or variants (that's
  what categories are for). Competition entries are not on the team (pack RULE-TEAM-004; S09). References (organisation,
  home venue, an organisation's primary venue) are checked only when a request sets or changes them: an update that
  keeps a reference to an organisation dissolved since then succeeds.
- **D7 — Team category.** Three generic codes, stored as columns and shown together ("Senior Men", "Under-17 Women"):
  `gender_category` (`MEN`, `WOMEN`, `MIXED`), `age_group` (`SENIOR`, `U6` … `U23`, `VETERANS`), `level` (`FIRST`
  default, `RESERVE`, `DEVELOPMENT`, `ACADEMY`). Labels live in the web app's dictionary, not in definitions (they
  aren't sport words): `MEN`/`WOMEN` read "Boys"/"Girls" with `U6`…`U17` and "Men"/"Women" otherwise (Q53). The category is descriptive: S09's age limits use the edition's rules and a reference date, not
  the team's age group. Changing a team's category is an update (D11), not a new team. Q53 confirms the lists.
- **D8 — Team names keep history.** `POST …/teams/{id}/rename` (`team.rename`, audit `REQUIRED`) takes `name`,
  optional `shortName` and `code`, and `effectiveOn` (after the current name's start and not after the tenant's today,
  else `422 TEAM_RENAME_DATE_INVALID`): the old name becomes a `FORMER_NAME` alias with `valid_from` = the old name's
  start and `valid_to` = the day before `effectiveOn`. The first name's start is `name_since`, an optional date on
  create (not after the tenant's today; default the tenant's today), so a team registered now can carry the name it
  has used since 2020 and be renamed from any later date. `registry.team_aliases` holds
  former names, nicknames and spellings (`FORMER_NAME`, `NICKNAME`, `SPELLING`), each with its name key and, for
  former names, the dates; nicknames and spellings are added and removed as people's aliases are
  (`team.alias.add`, `team.alias.remove` with reason). `PUT …/teams/{id}` can't change the name (`422
  TEAM_RENAME_REQUIRED` points to rename). `ITeamDirectory` answers the name on a date (D18), so S13–S14 can show
  "Bunga Breakers FC" on a 2025 result; a date before `name_since` answers the first name. A future-dated rename waits for scheduled jobs (Later). Organisations and
  venues change names by update and keep the old one as an alias only when the user adds it (`organisation.alias.add`,
  `venue.alias.add`): their history isn't shown on results.
- **D9 — Slugs.** Teams, organisations and venues each have a `slug` (`^[a-z0-9]+(-[a-z0-9]+)*$`, 2–80), unique per
  tenant and entity type among every status (an archived team keeps its slug), proposed from the name and category
  (`ntinda-swifts-fc-senior-men`) with `-2`, `-3` on a clash when create sends none, editable on update; a slug that
  parses as a UUID is refused (`{idOrSlug}` must stay unambiguous) (`422 TEAM_SLUG_TAKEN`,
  `ORGANISATION_SLUG_TAKEN`, `VENUE_SLUG_TAKEN`). A rename doesn't change the slug. Old slugs aren't kept: S14 decides
  redirects with the public pages.
- **D10 — Aliases and external identifiers.** `registry.organisation_aliases`, `registry.team_aliases` (D8),
  `registry.venue_aliases` as people's: name, kind, name key, removed by, at and why; an alias whose key is already the
  record's own name key or a current alias's is refused (`409 ORGANISATION_ALIAS_EXISTS`, `TEAM_ALIAS_EXISTS`,
  `VENUE_ALIAS_EXISTS`). External identifiers go in the existing `registry.external_identifiers` with `entity_type`
  `ORGANISATION`, `TEAM` or `VENUE` and the same rules as people's (authority pattern, value 1–100, `valid_from`,
  `valid_to`, a value never reused within an authority and entity type, `…_EXTERNAL_ID_EXISTS`, ending sets
  `valid_to`); the reserved national-document authorities are a person rule only. T1 replaces
  `ck_external_identifiers_entity_type` (today `'PERSON'` only) in a new migration, and the handlers call
  `MarkChanged(root)` as `AddExternalIdentifierHandler` does (the table has no foreign key, so a new row moves no
  version by itself). An unknown or already removed alias or identifier is `404 <AREA>_ALIAS_NOT_FOUND` /
  `<AREA>_EXTERNAL_ID_NOT_FOUND`, an already removed or ended one `409 <AREA>_ALIAS_REMOVED` /
  `<AREA>_EXTERNAL_ID_ENDED` (people's codes, per area). Actions
  `<entity>.alias.add`, `<entity>.alias.remove` (`REQUIRED_WITH_REASON`), `<entity>.external_id.add`,
  `<entity>.external_id.end`. An `ARCHIVED` record (and a `DISSOLVED`, `DISBANDED` or `CLOSED` one) takes no new
  alias, identifier, logo, team or parent link (`409 ORGANISATION_ARCHIVED`, `ORGANISATION_DISSOLVED`, `TEAM_ARCHIVED`,
  `TEAM_DISBANDED`, `VENUE_ARCHIVED`, `VENUE_CLOSED`); removing and ending stay allowed (S06's
  `EnsureAcceptsNewChildren` pattern).
- **D11 — Changing a record.** `PUT …/organisations/{id}`, `PUT …/teams/{id}`, `PUT …/venues/{id}` (`*.update`, audit
  `REQUIRED`, `If-Match`) replace the editable fields (not the team's variant or name, not statuses, parents, aliases,
  identifiers or images, which have their own actions). A change to the compared fields (D13) runs the warnings again.
  Aliases, identifiers, parent links and names are children of their aggregate: a change to one moves its root's
  version (S06 D11).
- **D12 — Lifecycles (state-machines.md has the states; Q39's pattern for the transitions).** One endpoint per entity,
  `POST …/{id}/status` with `{status, reason}` (`organisation.status.change`, `team.status.change`,
  `venue.status.change`, `REQUIRED_WITH_REASON`), history in `registry.organisation_status_changes`,
  `team_status_changes`, `venue_status_changes` (from, to, reason, actor, time; the first row at creation; append-only):

  | Entity | From | To |
  |---|---|---|
  | Organisation | `ACTIVE` | `INACTIVE`, `SUSPENDED`, `DISSOLVED`, `ARCHIVED` |
  | | `INACTIVE` | `ACTIVE`, `SUSPENDED`, `DISSOLVED`, `ARCHIVED` |
  | | `SUSPENDED` | `ACTIVE`, `INACTIVE`, `DISSOLVED`, `ARCHIVED` |
  | | `DISSOLVED` | `ARCHIVED` (final otherwise) |
  | | `ARCHIVED` | the status it had before archiving (unarchive) |
  | Team | `ACTIVE` | `INACTIVE`, `SUSPENDED`, `DISBANDED`, `ARCHIVED` |
  | | `INACTIVE` | `ACTIVE`, `SUSPENDED`, `DISBANDED`, `ARCHIVED` |
  | | `SUSPENDED` | `ACTIVE`, `INACTIVE`, `DISBANDED`, `ARCHIVED` |
  | | `DISBANDED` | `ACTIVE` (reform), `ARCHIVED` |
  | | `ARCHIVED` | the status it had before archiving (unarchive) |
  | Venue | `ACTIVE` | `TEMPORARILY_UNAVAILABLE`, `CLOSED`, `ARCHIVED` |
  | | `TEMPORARILY_UNAVAILABLE` | `ACTIVE`, `CLOSED`, `ARCHIVED` |
  | | `CLOSED` | `ACTIVE` (reopen), `ARCHIVED` |
  | | `ARCHIVED` | the status it had before archiving (unarchive) |

  Unarchive sends the status before archiving (read from the history); any other target is refused, so archiving a
  dissolved organisation and unarchiving it never undoes the dissolution (Q55).

  `SUSPENDED` here is an administrative stop set by staff (discipline's own suspensions come later). Any other move is
  `409 ORGANISATION_STATUS_INVALID`, `TEAM_STATUS_INVALID`, `VENUE_STATUS_INVALID`. An organisation's status never
  changes its teams'. Nothing is deleted (AR-18). The pack's venue `INACTIVE` isn't in state-machines.md and isn't
  built. state-machines.md gets the machines "as built".
- **D13 — Lighter duplicate warnings (Q54).** Organisations, teams and venues use S06's normaliser and trigram
  similarity, but no check token and no remembered decisions:
  - **Organisation:** `LIKELY` with the same name key (display, legal, short name or a current alias, either side) and
    the same type or city; `POSSIBLE` with similarity ≥ 0.7 and the same city or type.
  - **Team:** `LIKELY` with the same name key (name, short name or a current alias) and the same variant and
    category; `POSSIBLE` with similarity ≥ 0.7, the same variant and (the same organisation, or either has none).
  - **Venue:** `LIKELY` with the same name key and city, or coordinates within 200 m; `POSSIBLE` with similarity ≥ 0.7
    and the same city.

  A null city or type never matches. Create, and an update or rename that changes a compared field, run the check
  under D21's lock and answer `422 <AREA>_DUPLICATE_REVIEW_REQUIRED` (422 as people's
  `PERSON_DUPLICATE_OVERRIDE_REQUIRED`) with
  `candidates` (id, name, the type or category and variant, city, status, the evidence codes) in the Problem Details,
  unless the body says `duplicatesReviewed: true` (with an optional `duplicateReason`, 1–500). The audit entry names
  the candidates shown and the reason. An exact external identifier is never a warning: it is `409
  <AREA>_EXTERNAL_ID_EXISTS`. `ARCHIVED` records are found and marked; at most 10 candidates, strongest first; the
  thresholds are constants in one place with edge tests. `POST …/organisations/duplicate-check`, `…/teams/…` and
  `…/venues/…` (`organisation.duplicate_check`, `team.duplicate_check`, `venue.duplicate_check`; audit `NONE`, as
  `person.duplicate_check`; granted with the create action) give the form its live warning before the save.
- **D14 — `MAX_TEAMS` (Q50).** Registry counts the tenant's teams in `ACTIVE`, `INACTIVE` or `SUSPENDED` (`TeamCount`,
  `AddQuotaUsage<TeamCount>("MAX_TEAMS")`); `team.create` names the entitlement, and create, reform (`DISBANDED →
  ACTIVE`) and unarchive check `IEntitlements.CheckQuotaAsync` under `registry.teams:<tenantId>` before saving: at the
  limit `403 ENTITLEMENT_QUOTA_EXCEEDED` with "Your plan allows N teams." Disbanding or archiving frees a place. The
  Subscription page shows the count with S04's `QuotaUsage`.
- **D15 — Logos and badges (ADR-0014).** New purposes `ORG_LOGO`, `TEAM_LOGO`, `TEAM_BADGE` in `MediaPurpose`, the
  first with visibility `PUBLIC`: PNG, JPEG or WebP up to 2 MB (no SVG: it can carry script), at least 64 px on the
  shorter side (`422 MEDIA_IMAGE_TOO_SMALL`, a new Platform code), re-encoded as WebP `thumbnail` (160 px) and `medium` (640 px) with transparency kept, no `large`.
  Endpoints `PUT/DELETE …/organisations/{id}/logo`, `…/teams/{id}/logo`, `…/teams/{id}/badge` (multipart;
  `organisation.logo.set`/`.remove`, `team.logo.set`/`.remove`, `team.badge.set`/`.remove`; audit `REQUIRED`; the set
  actions name `STORAGE_GB` and check it as S06 D20 does). A new image marks the old asset `REMOVED`. Admin reads carry
  signed URLs as photos do. Public reads carry `logoUrl`/`badgeUrl` on the tenant's host, built by a new
  `IMediaAssets.GetPublicPath(assetId, variant)` (Registry never hard-codes Platform's route, ADR-0014): `GET
  /api/public/v1/assets/{assetId}/{variant}` (Platform, `.RequirePublicTenant()`), which streams only `ACTIVE` assets
  with visibility `PUBLIC` of the host's tenant (else `404 MEDIA_NOT_FOUND`) with `Cache-Control: public,
  max-age=86400` (a day, no `immutable`: a removed logo, or a site whose public flag is turned off, leaves caches
  within a day; the IDs change with each new image, so a longer life buys little). The route doesn't know the owner: a
  logo of an archived team stays reachable by its URL, but no public read links it (D17). Person photos stay
  `PRIVATE`.
- **D16 — Classification.** Each column carries `pii` and each DTO property `[PersonalData]`. `PUBLIC`: names, short
  names, slug, type, category, variant, code, motto, colours, logo and badge, founded year, organisation (as id, name,
  slug), home or primary venue (as id, name, slug), city, region, country, website, description, a venue's address,
  coordinates, capacity, surfaces, supported variants and time zone, status, former names with their dates.
  `INTERNAL`: an organisation's email and phone, legal name, aliases other than former names, external identifiers,
  status reasons and history, parent-link reasons, `source`. No field is `PRIVATE` (no person's data is stored).
  `PublicDtoClassificationTests` covers the new public DTOs without change.
- **D17 — Public reads (Q52).** No per-record public switch: organisations, teams and venues aren't personal data.
  `.RequirePublicTenant()`, `CacheableJson` 60 s, every status but `ARCHIVED` (`404 ORGANISATION_NOT_FOUND`,
  `TEAM_NOT_FOUND`, `VENUE_NOT_FOUND`), `PUBLIC` fields only:
  - `GET /api/public/v1/organisations` (cursor; `type`, `q`) and `…/organisations/{idOrSlug}` (with its teams, its
    parent and its children, none of them archived);
  - `GET /api/public/v1/teams` (cursor; `variant`, `organisationId`, `genderCategory`, `ageGroup`, `q`) and
    `…/teams/{idOrSlug}` (organisation, home venue, former names);
  - `GET /api/public/v1/venues` (cursor; `variant`, `q`) and `…/venues/{idOrSlug}`.

  An embedded organisation or venue that is `ARCHIVED` is answered as `null` (it 404s on its own read), and an archived
  record's logo isn't linked. Directories default to `ACTIVE` and take `status`. The public site's pages are S14's; S07 builds the API only.
- **D18 — Contracts.** `ITeamDirectory.GetSummariesAsync(ids, onDate?)` (id, the name, short name and code on that date,
  variant, category codes, organisation id, status, slug, logo and badge asset IDs), `IVenueDirectory.GetSummariesAsync
  (ids)` (id, name, short name, time zone, city, country, supported variants, status), `IOrganisationDirectory
  .GetSummariesAsync(ids)` (id, display name, short name, type, status, logo asset ID): the scope's tenant only, no
  action checked (the caller checks its own, as `IPeopleDirectory`). No integration events until a consumer needs
  one (S06 D22).
- **D19 — Reads (admin).** `GET /api/admin/v1/organisations` (cursor, `limit` 1–100 default 25; `q` by name or alias
  prefix or similarity and an external identifier's value exactly; `type`, `status` default all but `ARCHIVED`,
  `parentId`; sorted by display name, id), `…/organisations/{id}` (with `ETag`: fields, parent, children, teams as
  summaries, aliases, identifiers, logo URLs), `…/organisations/{id}/status-changes`, `…/organisations/{id}/parent-links`;
  the same for teams (`variant`, `organisationId`, `genderCategory`, `ageGroup`, `level`, `status`; sorted by name,
  category, id; detail with aliases including former names, identifiers, images) and venues (`variant`, `city`,
  `status`; sorted by name, id). Query integers typed as integers in OpenAPI.
- **D20 — Actions and roles.** Reads `organisation.read`, `team.read`, `venue.read` (available while read-only; audit
  `NONE`) for `TENANT_OWNER`, `TENANT_ADMIN`, `REGISTRAR`, `COMPETITION_MANAGER`, `RESULTS_OFFICER`, `DATA_ENTRY`,
  `VIEWER` (as `person.read`). Every change — create (and its `*.duplicate_check`), update, rename, status, parent,
  aliases, identifiers, images — for `TENANT_OWNER`, `TENANT_ADMIN`, `REGISTRAR`. `SCORER` and `TEAM_MANAGER` get none
  in S07 (S08 gives a team manager their own team through the `TEAM` scope). Names follow docs/generated/actions.md;
  each task's plan confirms them.
- **D21 — Concurrency and locks.** Updates, renames, status changes and child changes take `If-Match`. Creates, and updates or renames
  that change a compared field (D13), hold `registry.<organisations|teams|venues>:<tenantId>` while they check identifiers, slugs and (teams) the quota and
  save; the unique indexes are the last guard (a clash after the lock is the `…_SLUG_TAKEN` or `…_EXTERNAL_ID_EXISTS`
  answer, never a 500).
- **D22 — Seeds and test data (Q51).** DEMO seeders through the Registry commands, from JSON files beside
  `demo-people.json`, in order venues → organisations → teams:
  - `registry.demo.venues`: KMSA's five venues (pilot-customer.md), `Africa/Kampala`, with surfaces and capacities;
    supported variants only where football is played (Bukoto Community Stadium, Kisaasi Recreation Ground, Seeta Hill
    Sports Field), the others with none until S15–S16 publish rugby and basketball. ELSSC: one invented venue
    (Lakeshore High School Playing Fields). No coordinates (the places are invented).
  - `registry.demo.organisations`: every KMSA club of the seed set (the four multi-sport clubs, the football, rugby
    and basketball clubs) with types, cities and primary venues, and ELSSC's four schools (`SCHOOL`).
  - `registry.demo.teams`: football only — the 10 Metro Football League teams (Senior Men), 6 women's and 8 under-17
    teams as the competitions in pilot-customer.md need (Seeta Sparrows FC Under-17 among them), each under its club;
    one renamed team (Mutundwe Meteors FC, created with `name_since` 2018-01-01 as "Mutundwe Weavers FC" and renamed
    from 2026-01-01) and one archived historical team
    (Kireka Kingfishers FC Reserves, archived "Folded into the first team"); ELSSC: under-16 boys' and girls' teams for
    its four schools. An independent team (no organisation) is a test case, not demo data. Rugby and basketball teams
    join the seed when S15 and S16 publish their definitions.
  - Idempotent by name as `registry.demo.people`; a tenant that isn't `ACTIVE` is skipped. Logos are generated
    placeholder images (`PlaceholderPng`), never a real club's. Every name is invented and listed in pilot-customer.md.
  - Tests: `RegistryTestData` gains organisations, teams and venues through the handlers; the multi-sport rule uses
    `TestSportDefinitions` (two test variants of a second sport enabled for one test tenant).
- **D23 — Screens.** Registry group: **Organisations**, **Teams**, **Venues** after People, each a directory (search,
  filters in the address, **Show more**), create and edit forms with the live duplicate warning (a panel listing the
  candidates with **Open** and **This is a different one**, which sends `duplicatesReviewed`), a detail page with tabs
  in `?tab=`, and status dialogs with a required reason:
  - organisation: Overview (fields, logo, parent, children) · Teams · History (status, parent links) · Names & IDs
    (aliases, external identifiers);
  - team: Overview (category, organisation, home venue, colours as swatches with their hex, code, motto, logo and
    badge) · History (status, former names) · Names & IDs. Players and Staff are S08's: the tabs aren't shown yet
    (non-negotiable 13);
  - venue: Overview (address, time zone, capacity, surfaces, supported sports, a map link when coordinates exist) ·
    Teams that call it home · History · Names & IDs.

  The team form's sport picker lists only enabled variants ("No sport is enabled for your organisation yet…"
  otherwise, as People's profiles); the venue form lists published variants. Labels from definitions (AR-29). Phone,
  tablet and desktop, light and dark.
- **D24 — Docs.** modules.md (Registry as built: organisations, teams, venues, the three directories), data-conventions.md
  (the lighter warnings, the external identifiers' entity types, slugs, the seeders), state-machines.md (the three
  machines as built), authorization.md (the actions and roles; `MAX_TEAMS` as built), api-conventions.md (public
  assets, `idOrSlug`), admin-app.md (Registry: Organisations, Teams, Venues as built), pilot-customer.md (the seeded
  organisations, teams and venues, ELSSC's venue, S07's test tenants), glossary.md (team category), roadmap.

## Out of scope

Team memberships, staff assignments and the team page's Players and Staff tabs (S08); competition entries, rosters and
the governing organisation of a competition (S09); fixtures at venues, venue clashes and pitches inside a complex
(S10); public organisation, team and venue pages (S14); merges of organisations, teams or venues (S25); rugby and
basketball demo teams (S15, S16); future-dated renames (scheduled jobs); bulk import (Data Exchange); a `MAX_ORGANISATIONS`
or venue quota; organisation contact people; maps and geocoding; branding and sponsor assets (S22).

## Tasks

### T1 — Venues (~2 PRs, about 1,100 lines)
Deliver:
- D2's per-aggregate conflict codes (people's tests unchanged).
- `registry.venues`, `venue_aliases`, `venue_status_changes`, external identifiers with `VENUE`; create (D13's
  warning, the check endpoint, the lock), update, status (D12), aliases, identifiers; the reads (D19);
  `IVenueDirectory`; isolation samples, table policies; the actions and roles (D20).
- Likely T1a (model, create with warnings, update, reads, D2) and T1b (lifecycle, aliases, identifiers, directory).

Acceptance:
- [x] A venue without a time zone, with an unknown zone, with only one coordinate or with an unpublished variant is
      refused with its field and code (tests).
- [x] A venue with the same name in the same city, or within 200 m, is warned as `LIKELY`; `duplicatesReviewed`
      creates it and the audit entry names the candidate (tests, edges at 0.7 and 200 m).
- [x] Every new table has RLS and an isolation sample; `RlsCoverageTests`' exception list is unchanged.
- [x] A conflict on a venue answers `VENUE_CONCURRENCY_CONFLICT`, on a person still `PERSON_CONCURRENCY_CONFLICT`.

### T2 — Organisations (~2 PRs, about 1,200 lines)
Deliver:
- `registry.organisations`, `organisation_aliases`, `organisation_status_changes`, `organisation_parent_links`,
  identifiers with `ORGANISATION`; create with warnings, update, status, the parent (D5) with the cycle check under
  the lock, aliases, identifiers; reads (D19) with children and parent links; `IOrganisationDirectory`.
- Likely T2a (model, create, update, reads) and T2b (lifecycle, parent links, aliases, identifiers).

Acceptance:
- [x] Setting a descendant as parent is refused with `ORGANISATION_PARENT_CYCLE`, also when two requests would close a
      cycle at once (test holding the lock); the parent history keeps both links with their dates.
- [x] Suspending or dissolving an organisation leaves its teams' statuses alone, and a team's update that keeps its
      dissolved organisation succeeds (tests, with T3's teams).
- [x] Unarchiving returns to the status before archiving; a dissolved organisation stays dissolved (test).
- [x] Duplicate warnings for organisations behave as D13 says (table-driven tests).

### T3 — Teams (~2 PRs, about 1,300 lines)
Deliver:
- `registry.teams`, `team_aliases` (with former names), `team_status_changes`, identifiers with `TEAM`; create (enabled
  variant, category, organisation and venue checks, slug, warning, `MAX_TEAMS` under the lock), update, rename (D8),
  status with the quota on reform and unarchive, aliases, identifiers; reads (D19); `ITeamDirectory` with the name on a
  date; the `TeamCount` counter.
- Likely T3a (model, create, quota, update, reads) and T3b (rename and aliases, lifecycle, identifiers, directory).

Acceptance:
- [x] A team can't be created for a variant the tenant hasn't enabled (`TEAM_SPORT_NOT_ENABLED`); one organisation
      runs teams in two sports (two test variants of two test sports) and in several categories of one (tests).
- [x] Renaming keeps the old name as a `FORMER_NAME` alias with its dates (from `name_since`); `ITeamDirectory`
      answers the old name on a date before the rename, the new one after and the first name before `name_since`
      (tests); an `effectiveOn` on or before the current name's start or after today is `TEAM_RENAME_DATE_INVALID`;
      `PUT` can't rename.
- [x] At `MAX_TEAMS` create, reform and unarchive are refused with "Your plan allows N teams."; disbanding frees a
      place; two creates at the limit don't both pass (tests).
- [x] A disabled variant keeps its teams, which can still be updated and change status (test).

### T4 — Logos, badges and public assets (~1 PR, about 700 lines; if larger, T4a Platform purposes and route, T4b Registry endpoints)
Deliver:
- The three purposes as `PUBLIC` assets (D15) with their limits and variants; the set/remove endpoints and actions;
  `STORAGE_GB` checked; the public asset route; signed URLs in admin reads.

Acceptance:
- [x] An SVG, a file over 2 MB or an image under 64 px is refused (`MEDIA_TYPE_NOT_ALLOWED`, `MEDIA_TOO_LARGE`,
      `MEDIA_IMAGE_TOO_SMALL`); a PNG with transparency
      keeps it in the WebP variants (tests).
- [x] The public asset route serves another tenant's asset, a `PRIVATE` asset (a person photo) or a removed one as
      `404` (tests); its response is cacheable as D15 says.
- [x] At the storage limit a logo upload is refused (test).

### T5 — Public reads (~1 PR, about 700 lines; if larger, T5a organisations and venues, T5b teams)
Deliver:
- D17's six endpoints with `idOrSlug`, the filters and caching; OpenAPI and the client.

Acceptance:
- [ ] Public reads answer only `PUBLIC` fields (the classification test covers the new DTOs); an archived record is
      `404`; another tenant's host never sees the record (tests).
- [ ] A team read shows its former names with dates, its organisation and home venue, and its logo URL on the
      tenant's host; an archived organisation or venue is embedded as `null` (tests).

### T6 — Screens (~4 PRs, about 2,200 lines)
Deliver:
- D23: Venues (T6a), Organisations (T6b), Teams with rename and images (T6c-1, T6c-2); navigation entries by
  capability; Vitest for forms, warnings and dialogs; screenshots.

Acceptance:
- [ ] The duplicate warning shows the candidates and lets the user open one or go on; a conflict shows "This record
      changed since you opened it" (Vitest).
- [ ] The team form lists only enabled sports; labels come from definitions; no sport word in web code (ESLint rule).
- [ ] A viewer sees the directories and details without any change control (Vitest).

### T7 — Demo data and slice close (~2 PRs, about 1,000 lines)
Deliver:
- D22's three DEMO seeders and their data; the slice demo e2e (`admin-registry-demo`) on two test tenants of its own;
  docs (D24); slice acceptance with its evidence.

Acceptance:
- [ ] `seed --category DEMO` twice gives KMSA its clubs, five venues and football teams, and ELSSC its schools, venue
      and teams, with no change on the second run (test).
- [ ] The slice demo runs in CI.

## Carried over from S06 (slice review 2026-11-21)

- `RegistryDbContext`'s single conflict code (#95 Q1): D2, S07-T1.
- D11 as built (a child bumps every loaded root it points at; untracked roots get no conflict): organisation parent
  links point at two organisations; T2's plan says whether a link moves the parent's version too (S08's memberships
  remain the item's trigger). Done in T2c-1: the new parent's version moves too, the earlier parent's doesn't (owner).
- The People directory's `limit` lacks `maximum`/`default` in OpenAPI (`DocumentsQueryInteger`): D19's new lists have
  the same need; T1a may add the maximum and default to `DocumentsQueryInteger` for every list (owner).

The rest of S06's tech debt has its own trigger in PROGRESS.md ("From S06").

## Slice acceptance

- [x] An organisation can run teams in several sports and categories; renaming a team keeps the old name as a dated
      alias; a team can't be created for a sport the tenant hasn't enabled.
- [ ] Lifecycles follow the machines in state-machines.md; an organisation's status never changes its teams'; nothing
      is deleted.
- [ ] Duplicate warnings for the three registries behave as D13 says; exact external identifiers are refused.
- [x] `MAX_TEAMS` is counted and enforced; logos and badges are stored through Platform media and count toward
      `STORAGE_GB`. (T3b-1 `TeamQuotaTests`; T4b `OrganisationLogoTests`, `TeamImageTests` at the storage limit.)
- [ ] Every new tenant table is covered by the isolation harness; the RLS exception list is unchanged; every new
      endpoint has allowed and denied authorization tests; public reads answer only `PUBLIC` fields.
- [ ] No sport code in generic backend or web code; the DEMO seed and the slice demo run.
- [ ] Docs updated (D24); the S06 carry-overs above are done or moved, and PROGRESS.md says where.

## Implementation notes

(The builder adds notes here as tasks merge.)

- **T1a (venues, first part):** T1 is three PRs (owner, plan 2026-11-21): T1a the model, create with the warning, the
  check and the read, and D2; T1b update, directory and lifecycle; T1c aliases, identifiers and `IVenueDirectory`.
  `VENUE_NAME_INVALID` (422) refuses a name with no letter or digit (it has no name key). The warning's 200 m rule
  filters a box around the point in SQL and measures the haversine distance in C# (`GeoPoint`); a box across the
  antimeridian filters on latitude only. "The same city" compares the normalised city (`city_key`). A sent slug that
  parses as a UUID or isn't well formed is `400 VALIDATION_INVALID_REQUEST` on `slug`; a taken one is
  `422 VENUE_SLUG_TAKEN`. The venue's country defaults to the tenant's through `TenantAccess.Country`.
- **T1b-1 (venues: update, lifecycle):** T1b is two PRs (owner, plan 2026-11-21): T1b-1 update, lifecycle and the status
  history; T1b-2 the directory with a `sort_key` column. `PUT /venues/{id}` (`venue.update`) replaces the fields in every
  status (a correction, as people's); `slug` left out keeps the venue's, `country` left out is the tenant's. The warning
  runs again only when the name key, the city key or the coordinates change, the venue itself left out; a change to one
  of them or to the slug holds `registry.venues:<tenantId>`. `POST /venues/{id}/status` (`venue.status.change`) with
  `{status, reason}`; `VENUE_STATUS_INVALID` (409); unarchive reads the status before archiving from the latest move to
  `ARCHIVED` (rows ordered by time, then ID). `GET /venues/{id}/status-changes` (`venue.read`), newest first. Create and
  update share `VenueChangeValidator<T>` (slug and duplicate reason). A PUT without `country` gives the venue the
  tenant's, so T6's venue form always sends the venue's current country (review #118 Q1). `ChangeStatus` throws when
  the history isn't loaded (review #118 R2).
- **T1b-2 (venues: the directory):** `GET /venues` (`venue.read`): `q`, `variant`, `city`, `status`, `limit` (1–100,
  25), `cursor`; sorted by `sort_key` and ID. `sort_key` (migration `AddVenueDirectory`) is
  `NameNormaliser.Normalise(name)`: word order kept, collation "C", index `(tenant_id, sort_key, id)`. `q` matches a
  prefix of the sort key or of one of its words, a prefix of the name key, or the name key by trigram similarity
  ≥ 0.3 (People's constant); text without a letter or digit matches nothing until T1c adds identifiers. `city`
  compares `city_key` (as D13's "same city"); `variant` is a code in `supported_variants`. Items: id, name, short
  name, slug, type, city, country, supported variants, status. The migration backfills existing rows in SQL (NFD,
  Latin modifier letters (U+02B0–U+02FF and the other Latin ranges) and combining marks U+0300–U+036F dropped,
  lower case, everything but `[:alnum:]` and whitespace (every space .NET counts, no-break ones included) dropped,
  whitespace collapsed, NFC; modifier letters of other scripts stay (review #119 R1);
  the name key if that is empty) through the new `migrationBuilder.UpdateTenantRowsAsOwner(schema, table, sql)`,
  which lifts `FORCE ROW LEVEL SECURITY` for that SQL only, in the migration's transaction (owner, plan and
  pre-review 2026-11-21; multi-tenancy.md and database.md name it); a test proves the rows changed in two tenants
  and FORCE is back. A `city` without a letter or digit matches no venue. `DocumentsQueryInteger(name, minimum, maximum, @default)`: People's and
  venues' `limit` declare `1, 100, 25`; the other lists are tech debt "From S07".
- **T1c (venues: aliases, identifiers, directory contract):** `registry.venue_aliases` (migration
  `AddVenueAliasesAndIdentifiers`, RLS, composite FK to the venue; names 1–120 as a venue's, `AliasKind` reused) and
  `ck_external_identifiers_entity_type` allowing `PERSON` and `VENUE` only (owner, plan 2026-11-21: each task adds its
  own type). `POST /venues/{id}/aliases`, `…/aliases/{aliasId}/remove`, `…/external-identifiers`,
  `…/external-identifiers/{identifierId}/end` (`venue.alias.add`, `venue.alias.remove`, `venue.external_id.add`,
  `venue.external_id.end`, granted with `venue.read`; `TENANT_OWNER`, `TENANT_ADMIN`, `REGISTRAR`) answer the venue
  with its `ETag`; `VENUE_ARCHIVED` and `VENUE_CLOSED` refuse new children, a temporarily unavailable venue takes them.
  The identifier add holds `registry.venues:<tenantId>`; a value is unique within authority and entity type, so a
  person's identifier never clashes with a venue's; no reserved authorities. `ExternalIdentifier.End` answers the
  codes of its entity type. The venue read gains `aliases` (current) and `externalIdentifiers` (`INTERNAL`). `GET
  /venues?q=` adds a current alias's name key (prefix of the key or of a word, similarity ≥ 0.3) and an identifier's
  exact value, ended ones included; text without a letter or digit finds identifiers only. Aliases don't feed the
  duplicate warning (owner, plan 2026-11-21: D13 read literally). `IVenueDirectory` with its own `VenueStatus` in
  Contracts, mapped from the domain's, as `IPeopleDirectory` (not moved, as the plan first said: People's pattern).
  One PR (owner).
- **T2a (organisations, first part):** T2 is four PRs (owner, plan 2026-11-22): T2a the model, create with the warning,
  the check and the read; T2b update, lifecycle, status history and the directory; T2c parent links; T2d aliases,
  identifiers and `IOrganisationDirectory`. `registry.organisations` keeps a key for each name (display, legal, short;
  null when a name has no letter or digit), `sort_key` and `city_key` (null without a city to compare) from the start,
  so the directory needs no backfill. D13 for organisations as built: only the same type or city key is compared;
  `LIKELY` when a name key of one is a name key of the other, `POSSIBLE` when display name keys are at least 0.7 similar
  (display names only, owner). `ORGANISATION_NAME_INVALID` (422) refuses a display name with no letter or digit; a
  founded year after the tenant's year is `400 VALIDATION_INVALID_REQUEST` on `foundedYear`. The primary venue is
  checked only when set or changed (D6's rule), untracked and before the lock (as venues' references); a temporarily
  unavailable venue is accepted. D4's `organisation_type` is the column `type` (as venues' `type` for D3's
  `venue_type`). `logo_asset_id` exists but stays null until T4.
- **T2b-1 (organisations: update, lifecycle, status history):** T2b is two PRs (owner, plan 2026-11-23): T2b-1 update,
  lifecycle and the status history; T2b-2 the directory. `PUT /organisations/{id}` (`organisation.update`) replaces the
  fields in every status, `ARCHIVED` and `DISSOLVED` included (a correction, as venues'); `slug` left out keeps the
  organisation's, `country` left out is the tenant's. The warning runs again only when a name key (display, legal or
  short), the type or the city key changes, the organisation itself left out; such a change, or a slug change, holds
  `registry.organisations:<tenantId>`. The primary venue is checked only when it changes (D6): a kept venue closed since
  stays. The organisation is loaded before the details' rules, so the venue check compares with the saved one.
  `POST /organisations/{id}/status` (`organisation.status.change`) with `{status, reason}`;
  `ORGANISATION_STATUS_INVALID` (409); `OrganisationTransitions` is D12's table; unarchive reads the status before
  archiving from the latest move to `ARCHIVED`, so a dissolved organisation stays dissolved (Q55). `GET
  /organisations/{id}/status-changes` (`organisation.read`), newest first. No migration (T2a's table has the columns).
- **T2b-2 (organisations: the directory):** `GET /organisations` (`organisation.read`): `q`, `type`, `status`, `limit`
  (1–100, 25), `cursor`; sorted by `sort_key` (the normalised display name) and ID; without `status`, archived ones are
  left out. `q` matches the display name as venues' name (a prefix of the sort key or of one of its words, a prefix of
  the name key, or trigram similarity ≥ 0.3) and the legal or short name by a prefix of its name key (owner, plan
  2026-11-23); text without a letter or digit matches nothing until T2d adds aliases and identifiers. Items: id,
  display name, short name, slug, type, city, country, status (all `PUBLIC`; email and phone left out). D19's
  `parentId` filter comes with T2c's parent links (owner, plan 2026-11-23). `VenueCursor` became the shared
  `DirectoryCursor` (`Features/`), decoded against the directory's sort key length (owner). No migration: T2a made the
  `(tenant_id, sort_key, id)` and `(tenant_id, type)` indexes.
- **T2c-1 (organisations: parent links):** T2c is two PRs (owner, plan 2026-11-23): T2c-1 the parent, the cycle check
  and the link history; T2c-2 `parent` and `children` on the read and the directory's `parentId`. `POST
  /organisations/{id}/parent` (`organisation.parent.set`, `REQUIRED_WITH_REASON`, `If-Match`) with `{parentId | null,
  reason}` answers the organisation with its `ETag`; `GET /organisations/{id}/parent-links` (`organisation.read`), newest
  first (by when set, then ID), each with the parent as id, display name, slug and status. Migration
  `AddOrganisationParentLinks`: `registry.organisation_parent_links` (RLS; composite foreign keys to both organisations;
  checks: not itself, `valid_to >= valid_from`; one open link per child). The parent's own checks and the cycle check
  (a recursive walk up the open links) run under `registry.organisations:<tenantId>`; clearing takes no lock. An unknown
  parent or another tenant's is `422 ORGANISATION_PARENT_UNAVAILABLE`, as a dissolved or archived one; the organisation
  itself or one below it `422 ORGANISATION_PARENT_CYCLE` (field `parentId`). A dissolved or archived child is `409
  ORGANISATION_DISSOLVED` / `ORGANISATION_ARCHIVED` for a new parent but may clear it
  (`Organisation.EnsureAcceptsNewChildren`, for T2d too). Setting the parent it already has (or none when it has none)
  is a no-op: no link, no new version, the audit entry still recorded (owner). A new link moves the child's version and
  the new parent's, not the earlier parent's (owner; the S06 carry-over). The reason for clearing a parent is in the
  audit trail only (D5's columns have one reason, the link's). The link's `parent_id` is `PUBLIC`, its dates and reason
  `INTERNAL` (owner, review #124 Q1).
- **T2c-2 (organisations: parent and children in the reads):** `GET /organisations/{id}` (and every endpoint that
  answers the organisation) gains `parent`, the current parent in any status (null without one), and `children`, the
  organisations whose open link points at it, archived ones left out, sorted by sort key and ID, not paged (owner, plan
  2026-11-23); both are `OrganisationSummaryResponse` (id, display name, slug, status; `PUBLIC`), which the parent-link
  history shares. The organisation's version doesn't move when a child leaves (T2c-1), so it is no freshness marker for
  `children`. `GET /organisations?parentId=` keeps the direct children (an open link to it), with the other filters, the
  default status filter and the cursor; an unknown ID or another tenant's keeps none; a value that isn't a UUID in the
  `D` form is `400 VALIDATION_INVALID_QUERY`. `DocumentsQueryUuid(name)` documents an ID query parameter as a `uuid`
  string (for T3's `organisationId` too). No migration: T2c-1's index on the open links' `(tenant_id, parent_id)` serves
  both.
- **T2d (organisations: aliases, identifiers, directory contract):** `registry.organisation_aliases` (migration
  `AddOrganisationAliasesAndIdentifiers`, RLS, composite foreign key to the organisation; names 1–120 as a display
  name, `AliasKind` reused) and `ck_external_identifiers_entity_type` with `ORGANISATION`. `POST /organisations/{id}/aliases`,
  `…/aliases/{aliasId}/remove`, `…/external-identifiers`, `…/external-identifiers/{identifierId}/end`
  (`organisation.alias.add`, `organisation.alias.remove`, `organisation.external_id.add`, `organisation.external_id.end`,
  granted with `organisation.read`; `TENANT_OWNER`, `TENANT_ADMIN`, `REGISTRAR`) answer the organisation with its
  `ETag`, as venues' (T1c). `ORGANISATION_DISSOLVED` and `ORGANISATION_ARCHIVED` refuse new children; removing and
  ending stay allowed. An alias whose key is the display, legal or short name's or a current alias's is
  `ORGANISATION_ALIAS_EXISTS`. The identifier add holds `registry.organisations:<tenantId>`. The read gains `aliases`
  (current) and `externalIdentifiers` (`INTERNAL`). `GET /organisations?q=` adds a current alias (as venues') and an
  identifier's exact value, ended ones included; text without a letter or digit finds identifiers only. The duplicate
  warning compares current aliases on both sides (owner, plan 2026-11-23): `SAME_NAME_KEY` when a name or alias key of
  one is a name or alias key of the other; an update brings the organisation's own aliases, a create and the live check
  have none. Adding an alias runs no warning (owner). `IOrganisationDirectory` with its own `OrganisationStatus` and
  `OrganisationType` in Contracts, mapped from the domain's. One PR (owner). The live check takes no `organisationId`: T6's edit forms
  (organisations, and venues and teams alike) don't call it; the save's `…_DUPLICATE_REVIEW_REQUIRED` shows the
  candidates (owner, review of #126, Q56).
- **T3a (teams, first part):** T3 is five PRs (owner, plan 2026-11-23): T3a the model, create with the warning and
  `MAX_TEAMS`, the check and the read; T3b-1 update, lifecycle (the quota on reform and unarchive) and the status
  history; T3b-2 the directory and the teams in the organisation read; T3c rename, aliases and the name on a date; T3d
  identifiers and `ITeamDirectory`. `registry.teams` and `team_status_changes` (migration `AddTeams`, RLS): D6's fields,
  D7's category as `gender_category`, `age_group`, `level` (Q53 as its default; `level` `FIRST` when left out), name
  and short-name keys, `sort_key`, `name_since` (`PUBLIC`: the names' dates, D16), `logo_asset_id` and `badge_asset_id`
  null until T4. The variant must be enabled (`IEnabledSports`; an unknown code is `TEAM_SPORT_NOT_ENABLED` too); it is
  checked before the lock, as `nameSince` (not after the tenant's today, not before 1800-01-01; `400` on its field).
  An unknown, other tenant's, dissolved or archived organisation is `422 TEAM_ORGANISATION_UNAVAILABLE` on
  `organisationId` (owner: D6's code, not D10's `409 ORGANISATION_*`); a suspended or inactive one is accepted. Colours
  are accepted in either case and stored upper case; the order rule answers on the first colour out of place
  (`secondaryColour` or `tertiaryColour`). The proposed slug is the name, the age group and gender codes, and the level
  unless `FIRST` (`seeta-sparrows-fc-u17-men`, `kireka-kingfishers-fc-senior-men-reserve`; owner); a long name is cut, never
  the category codes (review #127 R2). Under
  `registry.teams:<tenantId>`: the slug, then `MAX_TEAMS` (`TeamCount`: `ACTIVE`, `INACTIVE`, `SUSPENDED`; Q50 as its
  default; "Your plan allows N teams."), then the warning. D13 for teams as built (owner, Q57): only teams of the same
  variant **and category** are compared, for `POSSIBLE` too; `LIKELY` when the name or short name of one has a key of
  the other's; `POSSIBLE` when the names are at least 0.7 similar and the two share an organisation or either has none.
  The finder filters on `(tenant_id, variant)` and the category first. Candidates carry the team's variant, category,
  organisation ID and status (teams have no city). `TEAM_MANAGER` reads no team yet (S08's `TEAM` scope).
- **T3b-1 (teams: update, lifecycle, status history):** `PUT /teams/{id}` (`team.update`) replaces the details in every
  status, whatever the variant's state now; `slug` left out keeps the team's, `level` left out is `FIRST`. The variant
  can't be sent (an unknown key, `400`). `name` may be sent, but only as the current name (trimmed, exact): another one
  is `422 TEAM_RENAME_REQUIRED` on `name`, a blank one `400` (owner, plan 2026-11-23); the short name and the code change
  by `PUT`. The organisation and home venue are checked only when they change (D6): a kept dissolved organisation or
  closed venue stays. A change to the short name's key, the category or the organisation runs the warning again (the
  team left out); it, or a slug change, holds `registry.teams:<tenantId>`. `POST /teams/{id}/status`
  (`team.status.change`, names `MAX_TEAMS`; owner) with `{status, reason}`; `TEAM_STATUS_INVALID` (409);
  `TeamTransitions` is D12's table. Every move to a status that counts takes the lock before the team is read (review #128 R1:
  two moves of one team queue, so the second gets the version's conflict, not the quota); a move from a status that
  doesn't count (`DISBANDED`, `ARCHIVED`) to one that does checks the quota under it; other moves take no lock. `GET /teams/{id}/status-changes`
  (`team.read`), newest first. Duplicate candidates carry `organisation` (id, display name, slug; null when
  independent) instead of `organisationId` (owner, review #127 Q1 and plan). No migration.
- **T3b-2 (teams: the directory and the organisation's teams):** `GET /teams` (`team.read`): `q`, `variant`,
  `organisationId`, `genderCategory`, `ageGroup`, `level`, `status`, `limit` (1–100, 25), `cursor`; without `status`,
  archived teams are left out. Sorted by `sort_key`, then category, then ID. The category ranks by age group in declared
  order (`SENIOR`, `U6` … `U23`, `VETERANS`), then gender (`MEN`, `WOMEN`, `MIXED`), then level (`FIRST`, `RESERVE`,
  `DEVELOPMENT`, `ACADEMY`) (owner, plan 2026-11-24): the columns hold the codes as text, whose order isn't this one, so
  `TeamOrder.CategoryRank` computes age group × 100 + gender × 10 + level in SQL (a `CASE` per part) and the cursor
  (`TeamDirectoryCursor`: sort key, rank, ID) carries it; `DirectoryCursor`'s decoding is shared (`CursorText`). `q`
  matches the name as organisations' display name (prefix of the sort key or of a word, name-key prefix, similarity ≥
  0.3) and the short name by name-key prefix; aliases join with T3c and identifiers with T3d (text without a letter or
  digit matches nothing until then). An unknown variant or organisation, or another tenant's, keeps none; an
  `organisationId` that isn't a UUID in the `D` form is `400 VALIDATION_INVALID_QUERY`. Items: id, name, short name,
  slug, variant, the category codes, organisation (id, display name, slug, whatever its status; null when independent;
  one query for the page) and status, all `PUBLIC`. The organisation record (and every endpoint that answers it) gains
  `teams`: its teams but archived ones, in the same order, not paged (owner), each `OrganisationTeamResponse` (id, name,
  slug, variant, category codes, status). A team's changes don't move the organisation's version. No migration: the
  `(tenant_id, sort_key, id)` and `(tenant_id, organisation_id)` indexes serve both reads.
- **T3c (teams: rename, aliases, the name on a date):** `registry.team_aliases` (migration `AddTeamAliases`, RLS,
  composite foreign key to the team; names 1–120, `AliasKind` reused). A `FORMER_NAME` row also keeps the short name
  and code that went with the name, so the name on a date answers all three (D18); its `valid_from`/`valid_to` are set
  and it is never removed; nicknames and spellings have no dates or short name or code (check constraint). `POST
  /teams/{id}/rename` (`team.rename`, audit `REQUIRED`, `If-Match`): `name`, `effectiveOn`, optional `shortName` and
  `code` (left out keeps the current one, `null` clears it; owner, plan 2026-11-24), `duplicatesReviewed` with an
  optional reason. Order: a name without a letter or digit (`TEAM_NAME_INVALID`), the version, then `Team.Rename`:
  `TEAM_DISBANDED`/`TEAM_ARCHIVED` (409; owner), `TEAM_RENAME_DATE_INVALID` on `effectiveOn`, the same name exactly
  `TEAM_NAME_UNCHANGED` on `name`. When the name's or short name's key changes, the rename holds
  `registry.teams:<tenantId>` and runs the warning again for the new names (the team left out). The slug stays.
  Renaming back to an earlier name is allowed. `POST /teams/{id}/aliases` (`team.alias.add`: `NICKNAME` or `SPELLING`
  only, a `FORMER_NAME` is `400` on `kind`) and `…/aliases/{aliasId}/remove` (`team.alias.remove`,
  `REQUIRED_WITH_REASON`; a former name is `409 TEAM_ALIAS_FORMER_NAME`) as organisations'. Current aliases, former
  names included, feed the `LIKELY` warning on both sides (D13; owner, pre-review): the team being changed brings its
  saved aliases, so a rename compares those it had before. Both alias actions and the rename answer the team with its `ETag`. The read gains
  `formerNames` (`PUBLIC`, newest first: id, name, short name, code, `validFrom`, `validTo`) and `aliases` (current
  nicknames and spellings, `INTERNAL`). `GET /teams?q=` adds a current alias's key, former names included (prefix of
  the key or of a word, similarity ≥ 0.3). `Team.NameOn(date)`: the current names from `name_since`, the former name
  covering the date before it, the first name before the first start; T3d's `ITeamDirectory` calls it.
- **T3d (teams: identifiers, directory contract):** migration `AddTeamIdentifiers` allows `TEAM` in
  `ck_external_identifiers_entity_type` (no new table). `POST /teams/{id}/external-identifiers` and
  `…/external-identifiers/{identifierId}/end` (`team.external_id.add`, `team.external_id.end`, audit `REQUIRED`, granted
  with `team.read`; `TENANT_OWNER`, `TENANT_ADMIN`, `REGISTRAR`) answer the team with its `ETag`, as organisations'
  (T2d). `TEAM_DISBANDED` and `TEAM_ARCHIVED` refuse a new identifier; ending stays allowed. The add holds
  `registry.teams:<tenantId>`; a value any team has or had with the authority is `409 TEAM_EXTERNAL_ID_EXISTS` on
  `value`; a person's, venue's or organisation's identifier doesn't clash. `TEAM_EXTERNAL_ID_NOT_FOUND`, `…_ENDED`,
  `…_VALIDITY_INVALID` (on `validTo`) as people's. The read (and every endpoint answering the team) gains
  `externalIdentifiers` (`INTERNAL`; ended ones included, by authority). `GET /teams?q=` adds an identifier's exact
  value, ended ones included; text without a letter or digit finds identifiers only. `ITeamDirectory.GetSummariesAsync
  (ids, onDate?, …)` (D18) with its own `TeamStatus`, `GenderCategory`, `AgeGroup` and `TeamLevel` in Contracts, mapped
  from the domain's; `onDate` null is the current names, a date before the first name began is the first name
  (`Team.NameOn`); the aliases are loaded only when a date is asked for (owner, plan 2026-11-25). The identifier
  table already has an isolation sample (the person's); no new sample. One PR (owner).
- **T4a (public images, Platform):** T4 is two PRs (owner, plan 2026-11-25): T4a Platform, T4b the Registry endpoints.
  `MediaPurpose.OrgLogo`/`TeamLogo`/`TeamBadge` (`ORG_LOGO`, `TEAM_LOGO`, `TEAM_BADGE`); `MediaRules` gives each purpose its
  limit, types, variants (`VariantSizes`), shorter-side minimum and visibility; `ImageVariants.Create(bytes, purpose)`
  refuses a small image from the header (`MEDIA_IMAGE_TOO_SMALL`) and keeps the alpha channel.
  `ck_media_assets_public_purpose` ties `PUBLIC` to the three purposes. The route is
  `GET /api/public/v1/assets/{assetId}/{thumbnail|medium}` (lowercase segments; another value is `MEDIA_NOT_FOUND`); a
  site that isn't public is `403 TENANT_UNAVAILABLE`, as every public route. `GetPublicPath` throws for any other
  variant. T5's public reads link images through `GetPublicPath`; T4b's admin reads use signed URLs.
- **T4b (images, Registry):** one PR (owner, plan 2026-11-25). `PUT`/`DELETE …/organisations/{id}/logo`,
  `…/teams/{id}/logo`, `…/teams/{id}/badge` with `organisation.logo.set`/`.remove`, `team.logo.set`/`.remove`,
  `team.badge.set`/`.remove` (audit `REQUIRED`; the set actions name `STORAGE_GB`; granted with the area's read;
  `TENANT_OWNER`, `TENANT_ADMIN`, `REGISTRAR`). Uploads as person photos (one file in `file`, `If-Match`, `X-CSRF`, no
  lock: an image isn't a compared field, D21); the storage check, then Platform's checks on `file`, then the record names
  the asset and the replaced one is marked `REMOVED` after the save. Remove works in any status; without an image it is
  `404 ORGANISATION_LOGO_NOT_FOUND`, `TEAM_LOGO_NOT_FOUND` or `TEAM_BADGE_NOT_FOUND` (new). The organisation and team
  reads gain `logo` (and `badge`): `{ thumbnailUrl, mediumUrl, expiresAt }` (`INTERNAL`, signed), null without one or when
  Platform doesn't serve it (logged). Directories and summaries carry no URLs (D19). `PersonMedia` became `RegistryMedia`.
- **T5a (public reads: organisations and venues):** T5 is two PRs (owner, plan 2026-11-25): T5a organisations and
  venues, T5b teams. `GET /api/public/v1/organisations` (`q`, `type`, `status`, `limit`, `cursor`) and `…/venues` (`q`,
  `variant`, `status`, `limit`, `cursor`; no `city`, as D17 lists) and `…/{idOrSlug}`: `.RequirePublicTenant()`,
  `CacheableJson` 60 s. `{idOrSlug}` is an ID when it is a UUID in the `D` form, else a slug; a value that is neither
  finds nothing without a query (`IdOrSlug`). Directories list one status, `ACTIVE` when none is sent; `status=ARCHIVED`
  is `400 VALIDATION_INVALID_QUERY` and OpenAPI lists the other statuses (owner). `q` matches public text only (D16):
  an organisation's display name as the admin directory does and its short name by name-key prefix, a venue's name; never
  a legal name, an alias or an identifier; text without a letter or digit matches nothing. The organisation read embeds
  `primaryVenue` and `parent` only while they aren't archived (null otherwise); children and teams leave archived ones
  out, as the admin read. Images are `logo: { thumbnailUrl, mediumUrl }` (null without one; owner: both variants, not
  D15's single `logoUrl`) built by `PublicImages` through `GetPublicPath`, in the directory items too (owner: the paths
  cost no query). Public DTOs are separate from the admin ones (review #130 R2); the venue directory reuses
  `VenueListItem` (every field public). A public list's `page` is the new `PublicPageInfo` (BuildingBlocks; `PageInfo`
  members classified `PUBLIC`), which `PublicDtoClassificationTests` required. No migration, actions, errors or events.

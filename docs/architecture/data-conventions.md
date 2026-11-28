# Data conventions (the "Data Integrity Manager", as rules + tests)

These conventions replace the owner's "Data Integrity Manager" and the pack's spec 43. They are enforced by EF
Core conventions, migrations review, database constraints and tests — not by a runtime module.

## Schemas and naming

- One PostgreSQL schema per module (ADR-0001). Tables plural snake_case (`team_memberships`); columns
  snake_case; primary key `id`; foreign keys `<entity>_id`; booleans `is_`/`has_`; timestamps `_at`; dates `_on`
  or `_date` (pick `_date` and keep it).
- EF Core: `EFCore.NamingConventions` snake_case; each module sets `HasDefaultSchema("<schema>")` and its own
  migrations history table in that schema.
- Code vocabulary follows docs/product/glossary.md (`contest` not `match` in generic code; `person` not `player`
  for identity; `edition` not `season` in code).

## Identifiers

- Primary keys: `uuid`, generated in the application as UUIDv7 (`Guid.CreateVersion7()`), time-ordered for index
  locality.
- Human-readable codes/slugs where useful (`slug` unique per tenant for public entities; `code` unique globally
  for platform configuration).
- External identifiers in `<schema>.external_identifiers(tenant_id, entity_type, entity_id, authority, value,
  valid_from, valid_to)` with unique `(tenant_id, authority, entity_type, value)`. Authorities are codes like
  `FUFA`, `FIFA`, `SCHOOL_ADMISSION_NO`, `PROVIDER_X`.

## Standard columns

| Column | Where | Notes |
|---|---|---|
| `tenant_id uuid not null` | tenant-owned tables | AR-04; RLS policy required |
| `created_at timestamptz`, `created_by uuid` | all mutable tables | set by interceptor |
| `updated_at timestamptz`, `updated_by uuid` | all mutable tables | set by interceptor |
| `xmin` (system column) | mutable aggregates | mapped as concurrency token (`version` in DTOs); child rows move it too (below) |
| `status text` | lifecycle entities | values from docs/domain/state-machines.md; check constraint |
| `source text`, `import_job_id uuid null` | entities that can be imported | provenance (AR-22) |

## Deleting, archiving, history

- **No generic `is_deleted` soft-delete flag.** Lifecycle statuses (`ARCHIVED`, `MERGED`, `RETIRED`,
  `DISSOLVED`…) express what happened.
- Hard delete is allowed only for drafts never referenced elsewhere and for transient data (expired idempotency
  keys, temp uploads), via explicit policy.
- History patterns:
  - **Effective dating** for relationships (`start_date`, `end_date`): memberships, staff assignments, roster
    entries, sponsor campaigns.
  - **Versions** for things that change but must be reproducible: sport definitions, ruleset snapshots, schedule
    versions, result versions, brand versions.
  - **Status history** tables for lifecycle transitions with reason/actor: tenants, editions, contests,
    registrations, rosters.
  - **Append-only** for timeline events and audit.
- Merges keep the losing record with `status = MERGED` and `merged_into_id`; references are repointed by the
  owning module's merge command, and every repoint is audited.

## Time

- `timestamptz` for instants (stored UTC); `date` for birth dates and other date-only values.
- Scheduled starts are instants plus the venue's IANA time zone for display.
- Inject `TimeProvider`; tests use a fake clock. Never `DateTime.Now`/`UtcNow` in domain or handlers.
- A tenant's calendar days (subscription periods, S04) are `date`s in the tenant's time zone (settings): "today" is
  computed from the clock on every use (`TenantCalendar.Today`), never stored or cached.

## Money and units

- Money: `numeric(19,4)` + ISO 4217 currency code (fees, later).
- Height in centimetres, weight in kilograms, distances in metres, durations in seconds, times as ISO 8601
  durations in APIs where human-facing.

## Personal-data classification (ADR-0015)

Every column holding personal data is tagged in its EF configuration (e.g. `.HasAnnotation("pii", "PRIVATE")`)
so tests and exports can enforce it:

| Class | Examples | Allowed in |
|---|---|---|
| `PUBLIC` | display name, public photo, nationality (if public profile on), team, positions | public APIs/read models |
| `INTERNAL` | full legal name, gender, height/weight (unless sport policy makes them public) | admin APIs with `person.read` |
| `PRIVATE` | full date of birth, contacts, guardian details, identity documents metadata | admin APIs with `person.read_private`; never logs/analytics |
| `SENSITIVE` | national ID numbers (if ever stored) | encrypted column; explicit permission; never exported by default |

Rules: public DTO tests assert no non-`PUBLIC` fields; redaction tests assert logs/analytics/audit metadata don't
contain `PRIVATE`/`SENSITIVE` values; minors' public profiles follow ADR-0015.

Exception (owner, S03-T4b): a member's sign-in email (`identity.users`, `PRIVATE`) is shown in the member list and
detail (`/api/admin/v1/members`) to every holder of `membership.read`, without a `*.read_private` action: until
people have profiles it is the only way to tell members apart. It stays out of logs and audit metadata (the audit
names the membership by ID). The same holds for an invitation's email (`identity.invitations`, `PRIVATE`) in
`/api/admin/v1/invitations` (S03-T4c).

Properties of DTOs and audit metadata records are classified with `[PersonalData(PersonalDataClass.X)]`
(`SportsPlatform.BuildingBlocks.Privacy`; on a positional record, `[property: PersonalData(...)]`). Audit metadata
masks `PRIVATE` and `SENSITIVE` values as `"[REDACTED]"` (`AuditMetadata`). Unclassified members are written as they
are, so anything personal goes in a record with classified properties, never in an anonymous type or dictionary.

As built (S06-T1b): `PublicDtoClassificationTests` walks the response types of every `.AllowPublic()` endpoint,
nested types included, and fails on a property that isn't `[PersonalData(PersonalDataClass.Public)]`. A property
nobody classified fails too, so every public response DTO classifies every property, also where nothing is personal
(reference data, the site, health). The same test checks that the Registry's `person.read` responses classify every
property and none above `INTERNAL`. `PeopleReadTests` holds the redaction test: after the private read, a person's
date of birth is in no log event, span or audit entry. The directory's `q` is a name (`INTERNAL`) in the query
string; request logs and traces carry the path only.

As built (S06-T6a): `registry.guardian_consents` keeps the guardian's name, relationship and contact and the evidence
asset as `PRIVATE`, and the scopes, dates, status and withdrawal as `INTERNAL`. `person.read` shows each consent's
scopes, dates, status and whether it has evidence; `GET …/people/{id}/private` adds the guardian and a signed URL to
the evidence. Audit metadata names the consent, its scopes and the assets by ID, never the guardian.

## Serializing changes: advisory locks

When a rule spans rows that a single constraint can't check (the last owner of a tenant, non-overlapping subscription
periods, a quota), the command takes a PostgreSQL transaction-level advisory lock, then reads and writes in the same
transaction. Concurrent commands on the same key queue on the lock, and each reads what the earlier ones committed.

- Key: `<schema>.<purpose>:<id>`, hashed with `pg_advisory_xact_lock(hashtextextended(<key>, 0))`, e.g.
  `identity.tenant_owners:<tenantId>` (S03), `tenancy.subscription:<tenantId>`,
  `tenancy.entitlement_override:<tenantId>` and `identity.staff_users:<tenantId>` (S04), `tenancy.tenant_sports:<tenantId>`
  (S05: enable and disable), `registry.people:<tenantId>` (S06: create, an update that runs the check, and every new external identifier).
- Take the lock inside an explicit transaction (`BeginTransactionAsync`), before the reads the rule depends on; commit
  with `CancellationToken.None` after the save, so a client hanging up can't roll back an audited change.
- One key per rule and owner (usually the tenant); never hold two keys at once.

## Aggregate versions and child rows

An aggregate's version is its root row's `xmin`. A change to only a child row (an alias, a role assignment) doesn't
touch the root's row, so before every save `ModuleDbContext` follows each added, changed or deleted child's foreign
keys up to its aggregate root and marks a loaded, unchanged root as changed: the save then updates the root's
`updated_*` columns under its version check. Two concurrent edits to different children of one aggregate therefore
conflict (`<AREA>_CONCURRENCY_CONFLICT`). Rules:

- Commands load the root before they change its children. A child saved without its root loaded doesn't move the
  version.
- A row that belongs to an aggregate without a foreign key to it (`registry.external_identifiers`) needs
  `context.MarkChanged(root)` in the command.
- Every foreign key is followed, so a child moves the version of every loaded root it points at, not only its owner's.
  Load only the aggregate the command changes; check other aggregates with a query that doesn't track them.
- A child that moves to another parent moves the version of the root it left as well. A deleted child moves the
  version of the root it was stored under.
- A row two levels down reaches the root only through its parent row, so that parent must be loaded too.
- An append-only root has no version to move: a change below a loaded one throws (a bug, not an expected failure).

## Duplicate prevention

Registries (people, organisations, teams, venues, competitions) implement: normalised name fields, aliases,
external identifiers, a duplicate-check query, a candidate review workflow, and merge history. Never
auto-merge. Person rules are specified in docs/build/slices/S06-people-registry.md.

As built for people (S06-T1a):

- **Normalised name** (`NameNormaliser`, Registry Domain): trimmed, lower-cased, accents removed, everything that
  isn't a letter or a digit removed (apostrophes, hyphens, dots, other punctuation, invisible characters), inner
  whitespace collapsed. "Nänsubuga" → `nansubuga`, "Ng'ang'a" → `nganga`.
- **Name key:** the normalised tokens of first, middle, last and other names, sorted and joined with one space.
  "Ssekandi John" and "John Ssekandi" both give `john ssekandi`. `registry.persons.name_key`; each alias has its own
  (`registry.person_aliases.name_key`).
- **Similarity:** the `pg_trgm` extension (schema `public`, created by Registry's first migration as `sp_migrator`)
  with a GIN `gin_trgm_ops` index on both name keys; queries use `EF.Functions.TrigramsSimilarity`. These two
  indexes don't start with `tenant_id` (GIN can't without `btree_gin`); RLS and the query filter still apply.
- **External identifiers:** `registry.external_identifiers` has no foreign key to its entity (one table for every
  registry entity type; `entity_type` is `PERSON`, `VENUE` (S07-T1c), `ORGANISATION` (S07-T2d) or `TEAM` (S07-T3d)) and
  a unique index on `(tenant_id, authority,
  entity_type, value)` that includes ended rows. As built (S06-T3b): `person.external_id.add` refuses a value with the
  same authority that any person has or had, this one included (`PERSON_EXTERNAL_ID_EXISTS`); one person may hold two
  open identifiers of one authority. `person.external_id.end` sets `valid_to` (default the tenant's today; not after
  it, not before `valid_from`). Aliases are removed with a reason, never deleted; an alias whose name key is already
  the person's own or a current alias's is refused (`PERSON_ALIAS_EXISTS`).
  Venues (S07-T1c) follow the same rules under `venue.alias.*` and `venue.external_id.*` with `VENUE_…` codes: a
  value is unique within its authority and entity type, so a person and a venue may share one; the national-document
  authorities are a person rule only; an archived or closed venue takes no new alias or identifier. A venue's aliases
  are searched by the directory, not compared by the duplicate warning (owner, S07-T1c plan).
  Organisations (S07-T2d) follow them too under `organisation.alias.*` and `organisation.external_id.*` with
  `ORGANISATION_…` codes; a dissolved or archived organisation takes no new alias or identifier; an alias whose key is
  already the organisation's display, legal or short name key or a current alias's is `ORGANISATION_ALIAS_EXISTS`.
  Unlike venues', their current aliases feed the duplicate warning (D13).
  Teams (S07-T3c) follow them under `team.alias.*` with `TEAM_…` codes, but a `FORMER_NAME` comes only from
  `team.rename` and keeps the name's dates (`valid_from`, `valid_to`) and the short name and code it went with; it is
  never removed (`TEAM_ALIAS_FORMER_NAME`). Nicknames and spellings take no dates. A disbanded or archived team takes
  no new alias. As organisations', a team's current aliases (former names included) are searched by the directory
  and feed the duplicate warning (D13). Their identifiers (S07-T3d) follow the same rules under `team.external_id.*`
  with `TEAM_…` codes: a value any team has or had with that authority is `TEAM_EXTERNAL_ID_EXISTS`, checked under
  `registry.teams:<tenantId>`; a disbanded or archived team takes no new identifier; ending stays allowed.

As built for people (S06-T2a), the duplicate check (`DuplicateMatcher`, Registry Domain; `DuplicateFinder`):

- **Rules.** `EXACT_IDENTIFIER`: an external identifier with the same authority and value (ended ones included).
  `LIKELY`: the same date of birth and (the same name key, or similarity ≥ 0.8). `POSSIBLE`: similarity ≥ 0.6 and (a
  date of birth missing, or birth years within one year, or a nationality in common: primary or secondary on either
  side, Q44); or the same name key with another
  date of birth; or the same date of birth and the same normalised last name (twins and siblings, whose first names
  are unlike: "Mariam Nankya" and "Shamim Nankya" score 0.33). The thresholds are `DuplicateThresholds`, not tenant
  settings.
- **Names** are compared on the person's name key and every alias that isn't removed; the best counts. `pg_trgm`
  answers a 4-byte float, so scores are rounded to four decimals before they meet a threshold.
- **Evidence** is codes: `SAME_EXTERNAL_ID` (value: the authority), `SAME_DATE_OF_BIRTH`, `BIRTH_YEAR_WITHIN_ONE`,
  `SAME_NAME_KEY`, `NAME_SIMILARITY` (value: the whole percentage, rounded down), `SAME_LAST_NAME` (with the same date
  of birth, unless the name keys are equal), `ALIAS_MATCH`, `SAME_NATIONALITY`. The answer carries a birth year, never the other person's date of birth.
- **Who is read.** People of every status (an archived person is answered with `status` `ARCHIVED`). With `personId`
  (a person being changed), that person is left out, and so is everyone in `registry.duplicate_decisions` with them.
  A decision never hides an `EXACT_IDENTIFIER` match: it is about names and dates, not about a shared identifier.
  At most 20 are answered: by confidence, then similarity, then display name.
- **Decisions.** `registry.duplicate_decisions`: one row per pair, the lower ID first (`person_low_id <
  person_high_id`, as PostgreSQL orders UUIDs; unique per tenant), `decision` `NOT_SAME`, reason, actor and time;
  both IDs are composite foreign keys to `persons`. Written from S06-T2b on.
- **Not collected.** The authorities `NATIONAL_ID`, `NIN` and `PASSPORT` are refused
  (`PERSON_EXTERNAL_ID_AUTHORITY_RESERVED`, ADR-0015). A date of birth after the tenant's date is refused
  (`PERSON_DATE_OF_BIRTH_IN_FUTURE`, Q43). The check, create and (S06-T3) update share these rules
  (`PersonDetailsRules`) and one validator (`PersonDetailsValidator<T>`; nationalities are active countries).

As built for people (S06-T2b), creating one (`POST /api/admin/v1/people`, `CreatePersonHandler`):

- **The check token** (`DuplicateCheckTokens`): Data Protection, purpose `registry.duplicate-check`, holding the
  tenant, the actor, a SHA-256 of the normalised inputs (name key, last-name key, date of birth, both nationalities,
  the sorted identifiers, `personId`) and the IDs the check answered; it expires 30 minutes after the check (the
  expiry is in the token and read with `TimeProvider`). The token is opaque: no input is readable from it. A check
  with `personId` gives a token only an update of that person can use.
- **Order of refusals.** The details' rules; then the token (`422 PERSON_DUPLICATE_CHECK_REQUIRED` on `checkToken`
  when it is missing, expired, or for other inputs, another actor or another tenant). Then, in a transaction holding
  `pg_advisory_xact_lock(hashtextextended('registry.people:<tenantId>', 0))`, the check runs again: a shared
  identifier is `409 PERSON_EXTERNAL_ID_EXISTS` (on `externalIdentifiers[i].value`, whatever the reason); a person
  the token doesn't list is `409 PERSON_DUPLICATE_CHECK_STALE`; candidates without `overrideReason` are `422
  PERSON_DUPLICATE_OVERRIDE_REQUIRED`.
- **Saved together:** the person (`MANUAL`), its identifiers, and a `NOT_SAME` decision with the reason for each
  candidate. Two creates of one person queue on the lock and the second finds the first's person (stale), and so
  does a create repeated with the same token: no `Idempotency-Key`.
- **Audit:** entity `Person`, the reason, and metadata `{candidates: [{personId, confidence}]}` (IDs and codes only);
  refusals are audited with the same metadata once the check has run again.

As built for venues (S07-T1a, T1b-1), the lighter warning (S07 D13, Q54): no check token and no remembered decisions.
`VenueDuplicateFinder` finds `LIKELY` with the same name key and city key, or coordinates within 200 m (a bounding box
in SQL, the haversine distance in C#), and `POSSIBLE` with names at least 0.7 similar in the same city; archived venues
are found and marked; at most 10. Create, and an update that changes the name key, the city key or the coordinates (the
venue itself left out), run it under `registry.venues:<tenantId>` and answer `422 VENUE_DUPLICATE_REVIEW_REQUIRED` with
`candidates` until the body says `duplicatesReviewed`; the audit entry names the candidates and the reason. The slug
(unique per tenant, every status) is proposed on create and editable on update, where one left out is kept; an update
that changes it holds the same lock.

As built for organisations (S07-T2a, T2b-1): `OrganisationDuplicateFinder` compares only organisations of the same type or
with the same city key (a missing city never matches; `(tenant_id, type)` and `(tenant_id, city_key)` are indexed, so
the similarity is measured within them). `LIKELY`: a name key (display, legal or short name) of one equals a name key of
the other; `POSSIBLE`: display name keys at least 0.7 similar (display names only, owner, plan 2026-11-22). Evidence:
`SAME_NAME_KEY` or `NAME_SIMILARITY`, then `SAME_TYPE`, `SAME_CITY`. Create, and an update that changes a name key
(display, legal or short), the type or the city key (the organisation itself left out), run it under
`registry.organisations:<tenantId>` and answer `422 ORGANISATION_DUPLICATE_REVIEW_REQUIRED` with `candidates` until
`duplicatesReviewed`, as venues. Current aliases count as name keys on both sides (S07-T2d, owner, plan
2026-11-23): an organisation's aliases meet the other's names and aliases, and an update brings the organisation's
own aliases (a create and the live check have none). Adding an alias runs no warning; removed aliases are no evidence.

Organisation parents (S07-T2c): `registry.organisation_parent_links` (child, parent, `valid_from`, `valid_to` as the
tenant's dates, reason, who set it and when). At most one link of a child is open (`valid_to` null; partial unique
index). A new parent ends the open link on the tenant's today and opens one from that day; clearing only ends it; a link
opened and ended on the same day keeps both dates. Rows are never deleted. The cycle check walks up the open links from
the proposed parent under `registry.organisations:<tenantId>`, which every new parent holds. A link is a child of its
organisation and, when new, also moves the new parent's version (the parent is loaded tracked), so a parent dissolved
or archived at the same moment conflicts; the earlier parent's version doesn't move (owner, S07-T2c plan, the S06 D11
carry-over).

## Migrations

- Generated by EF Core per module; reviewed like code. Names: EF Core's `<yyyyMMddHHmmss>_<ShortDescription>`
  (keep the generated timestamp; don't rename files).
- **Never edit a migration that is on `main`** — add a new one (a hook blocks edits; see .claude/hooks).
- Every new tenant table: RLS enable + force + `tenant_isolation` policy in the same migration
  (`EnableTenantRls(schema, table)`). Foreign keys between tenant-owned tables are composite `(tenant_id, x_id)`
  (docs/architecture/multi-tenancy.md, "Uniqueness and references").
- Data migrations accompany semantic changes; reversible where practical; destructive steps need a reviewed
  backup/restore note in the PR.
- Production applies migrations with the CLI (`migrate`) as `sp_migrator`, never automatically at app start.
- **Post-migration steps** (`IPostMigrationStep`, registered with `services.AddPostMigrationStep<T>()`): data work a
  migration can't do because it needs configuration. `MigrationRunner` runs every step after all migrations, on each
  run, so a step must be idempotent; it writes as the owner through `OwnerContextFactory` (no tenant, so never
  tenant-owned rows). First one: `tenancy.platform-subdomains` gives each tenant without a domain
  `<slug>.<Platform:BaseDomain>` (S02-T3b).

## Table access

`01-roles.sh` gives `sp_app` and `sp_platform` SELECT, INSERT, UPDATE and DELETE on every table `sp_migrator`
creates (never TRUNCATE). Each table gets one policy, set in the migration that creates it:

| Policy | Runtime roles may | How |
|---|---|---|
| Read-write | SELECT, INSERT, UPDATE, DELETE | default, no call |
| Read-only (reference and configuration tables) | SELECT | `migrationBuilder.ReadOnlyForRuntimeRoles(schema, table)`; seeds run as `sp_migrator` |
| Append-only (audit, timeline) | SELECT, INSERT | `migrationBuilder.AppendOnly(schema, table)` + `IsAppendOnly()` in the EF configuration |
| Migrations history | nothing | revoked by the `migrate` runner after every run |

- Append-only tables also get triggers that reject UPDATE, DELETE and TRUNCATE for every role, the owner
  included (AR-19). They can be bypassed only by a superuser with `session_replication_role = replica` (the test
  reset; managed PostgreSQL in production gives the app no superuser) or by the owner changing the triggers in DDL,
  which happens only in a reviewed migration.
- Partitioned tables: grants and the TRUNCATE trigger on the parent don't reach the partitions, so apply the
  helper to each partition too (the grants test flags undeclared partitions).
- Read-only covers `sp_platform` too: it bypasses tenant isolation for cross-tenant work; it doesn't write
  reference data. A platform service that must write a global table is a declared exception (below).
- The grants test (`TableGrantsTests`) reads the privileges (table- and column-level) of every table, view,
  materialized view and foreign table and compares them with
  `TableAccessPolicies` in the integration tests. A new table fails until its policy is declared there;
  exceptions for one role are declared there with a reason.

## Seeding

- Seeders implement `ISeeder` (`SportsPlatform.BuildingBlocks.Persistence.Seeding`) and are registered with
  `services.AddSeeder<T>()` in the module. Each has a stable `Key` (`<module>.<category>.<set>`), a category, an
  `Order` within the category and a `Checksum` that changes whenever what it writes changes (data + seeder version).
- Categories: `REFERENCE` and `CONFIGURATION` write read-only global tables, so they run as the owner
  (`SeedContext.CreateOwnerContext<T>()`, on the `Migrator` connection) in every environment. `DEMO` and `TEST` go
  through application commands as `sp_app` (AR-22) and run only in Development, Staging and Testing. Running a
  category runs `REFERENCE`, then `CONFIGURATION`, then it.
- Runs are idempotent: a seeder upserts by its natural key and changes nothing when the data is already there. Seeded
  records get `DeterministicIds.Create("<module>.<entity>:<natural key>")` (UUIDv5), the same in every environment;
  never change a key once records exist. Seeders never delete: a record dropped from the data is deactivated.
- `platform.seed_runs` (append-only) records each completed run with its checksum, environment and counts. A seeder
  whose last run has the same checksum is skipped; a seeder that throws stops the run and records nothing.
- One seed run at a time per database: the runner holds a PostgreSQL advisory lock (`SeedRunner.LockKey`) for the whole
  run, and a second run is refused.
- `seed --reset` drops every module schema as the owner and applies the migrations again before seeding (append-only
  tables can't be truncated, even by the owner). It needs Development **and** a local `Migrator` host (localhost,
  loopback, a Unix socket or the compose service `postgres`); a non-local Development database must be named in
  `Seeding:AllowResetOf`.
- Reference data (Platform, S01-T7b): `platform.countries` (ISO 3166-1), `currencies` (ISO 4217), `languages`
  (ISO 639-1) and `time_zones` (IANA), read-only for the runtime roles, keyed by `code`, with `is_active`. The data
  files are embedded in the Platform module (`Seed/Reference/Data`, sources and licences in its README) and are
  regenerated by `backend/tools/reference-data/build-reference-data.cs`, never edited by hand. Names are English
  (CLDR) for now.
- Configuration data (Tenancy, S04-T1b): `tenancy.configuration.plans` writes `tenancy.plans` (code, name, rank,
  `is_active`) and `tenancy.plan_entitlements` (plan, entitlement code, value as `jsonb`), read-only for the runtime
  roles, from the plans declared in code (`TenancyPlans`, read through `IPlanCatalogue`); IDs `tenancy.plan:<code>`
  and `tenancy.plan_entitlement:<plan>:<entitlement>`. A value is `{ "enabled": bool }`, `{ "limit": n }` (`null` =
  unlimited) or `{ "level": code }` (`null` = none), the one JSON shape of `EntitlementValueJsonConverter`. A value
  changes by a reviewed change to `TenancyPlans`, then `seed --category CONFIGURATION`; a plan dropped from the code is
  deactivated, and a value for an entitlement no longer declared stays but is never read.
- Configuration data (Sports Configuration, S05-T1b1): `config.configuration.sports` writes `config.sport_definitions`
  (variant, sport, version, name, scoring family, shape, status `PUBLISHED`, content as `jsonb` written from its RFC
  8785 canonical text, content hash, published at; `jsonb` keeps the tree, not the text, so canonicalize again to
  re-hash), read-only for the runtime roles, from the definition files embedded in the module (`Definitions/Sports`);
  IDs `config.sport_definition:<variant>@<version>`. Unlike the upserting seeders, it only inserts: a published version
  is immutable (S05 D4), so a stored version whose file now hashes differently fails the run with
  `SPORT_DEFINITION_IMMUTABLE` (two files declaring one version: `SPORT_DEFINITION_DUPLICATE_VERSION`), and a file
  dropped from the module leaves its rows as they are (every row is `PUBLISHED`; there is nothing to deactivate).
  `config.configuration.formats` (S05-T3a; after the sports) does the same for `config.format_definitions` (code,
  version, name, status, content, hash, published at; IDs `config.format_definition:<code>@<version>`;
  `FORMAT_DEFINITION_IMMUTABLE`, `FORMAT_DEFINITION_DUPLICATE_VERSION`) from `Definitions/Formats`. Both share
  `DefinitionSeeder<TRow>`, which T4's ruleset templates reuse.
- Integration tests seed the real reference and configuration data once per run, and the per-test reset keeps it
  (`PostgresFixture.ReferenceTables`, `PostgresFixture.PlanTables`, `PostgresFixture.ConfigurationTables`); a test that
  changes it calls `RestoreSeedDataAsync`.
- DEMO and TEST seeders call application commands through `SeedContext.RunCommandAsync(action, tenantId, …)`: each
  command runs in a new scope under that tenant (set once, before any connection opens; `null` leaves it for a command
  that sets its own, like tenant creation), as the system actor, with its
  audit begun as source `SYSTEM`, so its first save commits the audit entry with the change. A seeder runs a command
  only for a change; one that saves nothing or fails throws and stops the run. `SeedContext.QueryAsTenantAsync` reads
  tenant-owned data to decide what to run (not audited). Seeded tenants: `tenancy.demo.tenants` (KMSA and ELSSC).
  Seeded invitations: `identity.demo.invitations` (S03 D18, after the tenants) invites the pilot people by email
  through `membership.invite`; they get no deterministic IDs (a person may be invited several times; the natural key
  is tenant + normalised email), and the emails go out when the API's outbox runs (the CLI doesn't send).
  Seeded sports and people (S06 D23, docs/product/pilot-customer.md): `tenancy.demo.sports` (order 6, after the
  subscriptions whose plan counts `MAX_ENABLED_SPORTS`) enables each variant through `tenant.sport.enable`, skipping one
  already enabled; `registry.demo.people` (order 20) creates people through the duplicate check and `person.create`
  (the file gives the reason where it expects candidates, and the run fails when the check disagrees), then their
  profiles, consents and evidence, photos and archiving through the Registry's commands, one command per change. Their
  data files are embedded JSON (`Seed/Demo/Data` in each module), not C#, because they name sport variants (AR-11,
  `SportLiteralTests`). People get no deterministic IDs (`person.create` makes its own): a person is found again by
  name key, and each step runs only when what it adds is missing. Birth and consent dates are relative to the
  tenant's date when the person is created, so the demo's minors stay minors; the second of a pair on one birth date
  (twins, the demo's decided pairs) takes the first's stored date (`sameBirthDateAs`), so a run that stopped between
  them and resumes another day still gives them one date. Unlike the upserting seeders, neither deactivates: a person or
  variant dropped from its file is left as it is (`seed --reset` to remove it). A tenant that isn't `ACTIVE` is
  skipped, as for the invitations, but the run is recorded, so it is seeded only when the seeder next runs (changed
  data or `seed --reset`). Photos and consent forms are generated placeholder PNGs
  (`PlaceholderPng`; SkiaSharp stays in Platform media).
  Seeded venues, organisations and teams (S07 D22): `registry.demo.venues` (30), `registry.demo.organisations` (31)
  and `registry.demo.teams` (32), in that order because organisations and teams name venues and organisations (by
  name in the files, resolved when created). Each create runs without `duplicatesReviewed`: the data expects no
  warning, so one stops the run with its candidates. Venues and organisations are found again by name key, teams by
  variant, category and a current or former name (so a rename made since doesn't create the team again); a run only
  adds a missing logo, badge, rename or archived status. Logos and badges are placeholder shields
  (`PlaceholderPng.Logo`). Neither deactivates; a tenant that isn't `ACTIVE` is skipped, as for people.

## CI checks (automate as they become relevant)

Tenant column + RLS presence · table grants match the declared access policy · forbidden cross-module references · public DTO field classification ·
stale generated catalogues (actions, errors, events, OpenAPI) · published configuration hash unchanged ·
no `IgnoreQueryFilters()` outside allow-listed platform services · no `DateTime.Now` in domain/handlers.

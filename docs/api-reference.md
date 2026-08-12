# Sports Platform API Documentation

## Overview

The Sports Platform API is a RESTful API built with ASP.NET Core 9 that provides comprehensive management of sports competitions, contests, participants, and real-time scoring. The API supports multi-tenancy, real-time updates via SignalR, and offline-capable mobile applications.

Tenant-owned protected resources are authorized from the authenticated
`tenant_id` claim. Supplying another tenant's identifier in a route, query, or
related-resource payload returns `403 Forbidden`; platform-owner access is the
explicit exception. The automated isolation matrix covers competitions,
contests, participants, branding, subscriptions, registration forms, and
registrations against both in-memory and PostgreSQL-backed API hosts.

## Base URL

```
Production: https://api.sportsplatform.example
Staging:    https://api-staging.sportsplatform.example
Development: http://localhost:8080
```

## Authentication

The API uses JWT (JSON Web Token) authentication. Include the token in the `Authorization` header:

```
Authorization: Bearer <your_jwt_token>
```

### Endpoints

#### POST /api/auth/login
Authenticate an active user and create a device session. `deviceId` should be a
stable installation identifier so the same device replaces its prior session.

**Request Body:**
```json
{
  "email": "user@example.com",
  "password": "your-password",
  "deviceId": "installation-uuid",
  "deviceName": "John's phone",
  "deviceType": "mobile"
}
```

**Response (200 OK):**
```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "refreshToken": "dGhpcyBpcyBhIHJlZnJlc2ggdG9rZW4...",
  "expiresAt": "2026-07-17T10:16:59Z",
  "user": {
    "id": "user-123",
    "email": "user@example.com",
    "firstName": "John",
    "lastName": "Doe",
    "roles": ["admin", "scorer"],
    "permissions": ["users.read"]
  }
}
```

#### POST /api/auth/register
Register a new user account.

**Request Body:**
```json
{
  "email": "newuser@example.com",
  "password": "SecurePassword123!",
  "firstName": "New",
  "lastName": "User",
  "tenantId": "00000000-0000-0000-0000-000000000002",
  "deviceId": "installation-uuid",
  "deviceName": "New user's phone",
  "deviceType": "mobile"
}
```

**Response (200 OK):** the same access-token, refresh-token, expiry, and safe
user envelope returned by login.

#### POST /api/auth/refresh
Refresh an expired JWT token.

**Request Body:**
```json
{
  "refreshToken": "dGhpcyBpcyBhIHJlZnJlc2ggdG9rZW4...",
  "deviceId": "installation-uuid"
}
```

**Response (200 OK):**
```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "refreshToken": "bmV3IHJlZnJlc2ggdG9rZW4...",
  "expiresAt": "2026-07-17T10:16:59Z"
}
```

#### POST /api/auth/logout
Revoke the supplied device session. Requires the access token and body
`{ "refreshToken": "...", "deviceId": "installation-uuid" }`.

**Response (200 OK)**

#### Password and email lifecycle

- `POST /api/auth/forgot-password` accepts `{ "email": "..." }` and always
  returns a generic response. A separate, expiring, single-use reset token is
  delivered by email.
- `POST /api/auth/reset-password` accepts `email`, `token`, and `newPassword`.
  Success revokes all device sessions.
- `POST /api/auth/request-email-verification` requires authentication and sends
  a separate verification token; `POST /api/auth/verify-email` consumes it.
- `POST /api/auth/change-password` requires authentication and also revokes all
  device sessions.

Password-reset and email-verification tokens are purpose-specific and stored
only as SHA-256 hashes. Password hashes use BCrypt.

#### Device sessions and account administration

- `GET /api/auth/sessions` lists the signed-in user's devices without exposing
  refresh credentials.
- `DELETE /api/auth/sessions/{sessionId}` revokes one owned session.
- `GET /api/auth/users` requires `users.read` and returns tenant users.
- `POST /api/auth/users/{userId}/suspend` and `/unsuspend` require
  `users.manage` and a reason. Suspension immediately rejects existing access
  tokens, revokes refresh sessions, and blocks login.

### Roles and capability permissions

The authoritative standard roles are Platform Owner, Platform Support, Tenant
Owner, Competition Administrator, Registration Officer, Fixture Manager, Head
Official, Scorer, Result Approver, Media Manager, and Viewer. The first two are
platform roles; the remaining roles are provisioned independently for every
tenant, including tenants created through the API.

Permissions use one lowercase `resource.action` catalogue. Each protected API
declares a permission from that catalogue, and successful login/refresh embeds
the user's current effective role and permission set in the access token.
Platform-only capabilities cannot be placed on tenant roles.

- `GET /api/roles` lists standard and custom roles in the caller's scope.
- `GET /api/roles/permissions` lists capabilities assignable in that scope.
- `POST /api/roles` and `PUT /api/roles/{id}` create or update a custom role.
- `DELETE /api/roles/{id}` deletes an unassigned custom role. Standard roles
  cannot be changed or deleted.
- `POST /api/roles/assign` and `/remove` change one tenant-safe user assignment.

Role reads, creation, updates, deletion, and assignment require `roles.read`,
`roles.create`, `roles.update`, `roles.delete`, and `roles.assign`
respectively. Every mutation is audited. Permission changes are reflected when
the affected user next logs in or refreshes their access token.

---

## Tenants

### GET /api/tenants
List tenants visible to the caller. Platform operators receive every tenant;
tenant identities receive only their own tenant. Requires `tenants.read`.

**Response (200 OK):**
```json
[
  {
    "id": "00000000-0000-0000-0000-000000000002",
    "name": "Premier League",
    "shortName": "PL",
    "slug": "premier-league",
    "status": "Active",
    "country": "United Kingdom",
    "timezone": "Europe/London",
    "currency": "GBP",
    "defaultLanguage": "en-GB"
  }
]
```

### GET /api/tenants/{id}
Get tenant details.

**Response (200 OK):**
```json
{
  "id": "00000000-0000-0000-0000-000000000002",
  "name": "Premier League",
  "shortName": "PL",
  "slug": "premier-league",
  "status": "Active",
  "country": "United Kingdom",
  "timezone": "Europe/London",
  "currency": "GBP",
  "defaultLanguage": "en-GB",
  "createdAt": "2025-01-15T00:00:00Z"
}
```

### POST /api/tenants
Create a new tenant (admin only).

**Request Body:**
```json
{
  "name": "New Sports League",
  "shortName": "NSL",
  "slug": "new-sports-league"
}
```

Creation is platform-only, requires `tenants.manage`, validates the immutable
public slug, and provisions the standard tenant-role catalogue.

### PUT /api/tenants/{id}
Update the tenant name, short name, and description. Requires
`tenants.update` and access to that tenant.

### POST /api/tenants/{id}/activate|suspend|archive
Platform-only lifecycle operations requiring `tenants.manage`. Archive is a
terminal state. Suspended and archived tenant identities can continue reading
their authorized resources but receive a structured `403` for protected
writes.

### GET|POST /api/tenants/{id}/contacts
List or create tenant contacts. Contact mutation requires `tenants.update`;
only one contact remains primary when a new primary is selected.

### PUT|DELETE /api/tenants/contacts/{contactId}
Update or delete a tenant-owned contact with resource-level tenant isolation.

### PUT /api/tenants/{id}/localization
Set country, timezone, currency, and default language. Requires
`tenants.update` and tenant access.

### DELETE /api/tenants/{id}
Platform-only compatibility operation that converts deletion to archive.

---

## Tenant branding

### GET /api/branding/tenant/{tenantId}
Returns the tenant-owned branding record to a caller with `branding.read` and
resource access to that tenant.

### PUT /api/branding/tenant/{tenantId}
Creates or updates tenant name, short name, logo URL, primary/secondary/accent
colors, immutable public slug, contact email/phone, and website. Requires
`branding.manage`; custom logo and color entitlements are enforced. Colors must
use `#RRGGBB`, URLs must be absolute HTTP(S) URLs without embedded credentials,
and contact email and slug formats are validated. Every update is audited.

### POST /api/branding/tenant/{tenantId}/logo
Uploads an entitled tenant logo as multipart form field `file`. Only files up
to 2 MB with genuine PNG, JPEG, or WebP signatures are accepted. The private
object is stored under a tenant-specific MinIO key and served through the
public immutable asset endpoint. Storage quota is checked and usage is metered;
replacing a managed logo removes the previous object and decrements its usage.

### GET /api/branding/assets/{tenantId}/{fileName}
Anonymous immutable delivery endpoint for validated tenant-owned logo objects.

Anonymous `GET /api/public/tenants/{slug}` and `/home` payloads include the
validated branding identity and contact fields consumed by public web and
Flutter. Sponsor placeholders are managed through the tenant-safe sponsor
placement endpoints and support public home, competition header, contest
header, contest sheet, sidebar, and footer positions.

---

## Sports

### GET /api/sports
Lists the complete platform catalogue for callers with `sports.read`.

```json
[{
  "id": "00000000-0000-0000-0000-000000000100",
  "code": "FOOTBALL",
  "name": "Football",
  "description": "Association football/soccer",
  "isActive": true,
  "icon": "⚽",
  "sportPackageVersion": 1,
  "supportedParticipantShapes": ["team"],
  "versions": [{ "id": "...", "versionNumber": 1, "name": "Football", "isPublished": true }]
}]
```

`GET /api/sports/{id}` and `GET /api/sports/by-code/{code}` return one item in
the same shape.

### POST /api/sports

Platform operators with `sports.create` create a catalogue item. Codes are
immutable, unique, and canonicalized to uppercase. Required request fields are
`code`, `name`, and a non-empty `supportedParticipantShapes` array; optional
fields are `description` and `icon`.

### PUT /api/sports/{id}

Platform operators with `sports.manage` update the name, description, icon,
participant shapes, and active state. A sport enabled by any tenant cannot be
deactivated until those selections are disabled. Catalogue mutations are
audited.

### GET /api/sports/tenant/{tenantId}

Returns `{ sport, enabled }` entries for the tenant. Tenant identities may read
only their own tenant; platform operators may select any tenant.

### PUT /api/sports/tenant/{tenantId}/{sportId}

Enables or disables a sport using `{ "enabled": true }`. Enabling requires an
active platform sport, the tenant's `sports.max_sports` entitlement, and room
under its `max_sports` limit. The selection is tenant isolated and audited.
Competition creation returns `409 SportNotEnabled` unless this selection is
enabled.

### Sport definition version lifecycle

Platform operators manage immutable definitions through these endpoints:

- `POST /api/sports/{sportId}/versions` with `{ "name": "2027 rules" }`
  creates an empty draft. The API derives `createdBy` from the authenticated
  identity and ignores client impersonation fields.
- `GET /api/sports/versions/{versionId}` returns every definition field.
- `PUT /api/sports/versions/{versionId}` configures an unpublished draft.
- `POST /api/sports/{sportId}/versions/{sourceVersionId}/clone` with a name
  copies every definition field into the sport's next numbered draft.
- `POST /api/sports/versions/{versionId}/publish` permanently publishes a
  complete draft. Repeating publication is idempotent.

The configuration request contains `contestShape`, `participantType`, and JSON
strings for `segmentDefinitions`, `eventDefinitions`, `metricDefinitions`,
`resultMethods`, `rankingRules`, `displayConfiguration`, and
`validationConfiguration`. The first five definition collections, result
methods, and ranking rules must be JSON arrays; display and validation must be
JSON objects. Invalid or incomplete definitions cannot be published, and any
attempt to edit a published version returns `409 Conflict`.

Version numbers are unique within a sport. New competitions automatically pin
the latest published version; the competition's `rulesetVersionId` cannot be
changed to another version after assignment. Every create, configure, clone,
and publish operation is audited.

### Metric framework

Metric definitions support `Integer`, `Decimal`, `Duration`, `ClockTime`,
`Distance`, `Weight`, `Boolean`, `Text`, `Enumeration`, and `CompositeScore`.
Each definition requires a code, name, and type. The API normalizes a
`validation` object containing the applicable precision, minimum/maximum,
maximum text length, regular-expression pattern, enumeration values, or ordered
composite components. Definitions may set a unit and `Ascending`, `Descending`,
or `None` ranking direction. Legacy `Percentage` definitions are treated as
decimal values with a percent unit.

- `GET /api/sports/metrics/types` returns the ten canonical type names.
- `POST /api/sports/metrics/validate` accepts `{ definition, value, unit }` and
  returns the normalized value with its data type, unit, precision, numeric/text
  representation, and composite parts.
- `POST /api/sports/metrics/rank` accepts `{ definition, values }`, where each
  value has `participantId`, `value`, and optional `unit`. It returns sorted
  competition ranks (`1, 1, 3` for a two-way first-place tie).

Numeric values use invariant formatting and preserve zeroes at the configured
precision. Duration values normalize to seconds, clock values validate a
24-hour clock, enumeration values use their canonical spelling, and composite
scores are normalized JSON objects in configured component order.

---

## Competitions

### GET /api/competitions
List competitions with filtering and pagination.

**Query Parameters:**
- `sportCode` (string): Filter by sport code
- `status` (string): Filter by status (Draft, Upcoming, Registration, Active, Completed, Cancelled)
- `format` (string): Filter by format (RoundRobin, Knockout, GroupPlusKnockout, etc.)
- `search` (string): Search by name
- `page` (int): Page number (default: 1)
- `pageSize` (int): Items per page (default: 20, max: 100)
- `sortBy` (string): Sort field (name, startDate, createdAt)
- `sortOrder` (string): Sort order (asc, desc)

**Response (200 OK):**
```json
{
  "items": [
    {
      "id": "comp-001",
      "name": "Premier League 2025-26",
      "sportCode": "football",
      "sportName": "Football",
      "format": "RoundRobin",
      "status": "Active",
      "edition": "2025/26",
      "startDate": "2025-08-15",
      "endDate": "2026-05-30",
      "participantCount": 20,
      "contestCount": 190,
      "tenantId": "tenant-001",
      "createdAt": "2025-01-01T00:00:00Z"
    }
  ],
  "totalCount": 12,
  "page": 1,
  "pageSize": 20
}
```

### GET /api/competitions/{id}
Get competition details.

**Response (200 OK):**
```json
{
  "id": "comp-001",
  "name": "Premier League 2025-26",
  "sportCode": "football",
  "sportName": "Football",
  "format": "RoundRobin",
  "status": "Active",
  "edition": "2025/26",
  "description": "The top professional football league in England",
  "startDate": "2025-08-15",
  "endDate": "2026-05-30",
  "venue": {
    "name": "Various",
    "city": "England",
    "country": "United Kingdom"
  },
  "participantCount": 20,
  "contestCount": 190,
  "stages": [
    {
      "id": "stage-001",
      "name": "Regular Season",
      "type": "League",
      "order": 1,
      "groupCount": 1,
      "roundCount": 38
    }
  ],
  "tenantId": "tenant-001",
  "createdAt": "2025-01-01T00:00:00Z",
  "updatedAt": "2026-07-15T00:00:00Z"
}
```

### GET /api/competitions/{id}/contests
Get contests for a competition.

**Query Parameters:**
- `status` (string): Filter by status
- `round` (int): Filter by round number
- `page` (int): Page number
- `pageSize` (int): Items per page

### Competition hierarchy lifecycle

All hierarchy routes require tenant access. Reads require `competitions.read`;
mutations require `competitions.manage`.

- `GET /api/competitions/{id}/hierarchy` returns every edition, ordered stage,
  group, round, and stage contest in one connected view.
- `POST /api/competitions/{id}/editions` accepts `name`, `startDate`, and an
  optional `endDate`. A competition may contain multiple editions.
- `POST /api/competitions/{id}/editions/{editionId}/stages` accepts `name`,
  `type`, and positive `sortOrder`.
- `POST /api/competitions/{id}/stages/{stageId}/groups` accepts `name`.
- `POST /api/competitions/{id}/stages/{stageId}/rounds` accepts `name` and
  positive `sortOrder`. A stage may contain groups and rounds simultaneously.
- `POST /api/competitions/{id}/stages/{stageId}/contests` manually creates a
  named contest with `contestShape`, positive `sortOrder`, optional `groupId`
  and `roundId`, and optional `allowedParticipantTypes`. Group and round IDs
  must belong to the selected stage. The contest inherits the competition's
  exact ruleset version and sport segments.

Generated contests continue to use the format-specific fixture endpoints such
as `/fixtures/round-robin`, `/fixtures/group-knockout`, athletics meets, golf
tournaments, cricket tournaments, and combat cards; all appear in the same
hierarchy read.

### POST /api/competitions
Create a new competition.

**Request Body:**
```json
{
  "name": "Championship 2026",
  "sportCode": "football",
  "format": "Knockout",
  "edition": "2026",
  "description": "Annual championship tournament",
  "startDate": "2026-06-01",
  "endDate": "2026-06-30",
  "venue": {
    "name": "National Stadium",
    "city": "London",
    "country": "United Kingdom"
  },
  "settings": {
    "participantLimit": 16,
    "allowTies": false,
    "thirdPlaceMatch": true
  }
}
```

### PUT /api/competitions/{id}
Update competition details.

### DELETE /api/competitions/{id}
Delete a competition.

### POST /api/competitions/{id}/publish
Publish a competition (change status from Draft to Upcoming).

### POST /api/competitions/{id}/fixtures/round-robin
Generate and persist round-robin fixtures from the competition's approved or
confirmed registrations. The caller needs `competitions.manage`. Generation
fails if fewer than two registrations are eligible or fixtures already exist.

**Request Body:**
```json
{
  "editionName": "2026/27",
  "stageName": "League",
  "startTimeUtc": "2026-09-01T15:00:00Z",
  "endDateUtc": "2027-05-30T23:59:59Z",
  "venue": "Main Stadium",
  "homeAndAway": true,
  "daysBetweenRounds": 7
}
```

The response includes the edition and stage IDs, eligible participant count,
fixture count, and every generated contest with its round, match number,
participants, and scheduled start time. Eight entrants with `homeAndAway: true`
produce 56 fixtures.

### POST /api/competitions/{id}/playoffs/top-four
Create a persisted seeded playoff from the competition's current league
standings. The caller needs `competitions.manage`. The competition must have at
least four ranked tenant participants and must not already have a bracket.

**Request Body:**
```json
{
  "bracketName": "Championship Playoffs",
  "stageName": "Championship Playoffs",
  "startTimeUtc": "2027-06-05T15:00:00Z",
  "venue": "Main Stadium",
  "daysUntilFinal": 7,
  "semifinalBestOf": 3,
  "finalBestOf": 1,
  "daysBetweenSeriesGames": 2
}
```

Seeds are paired 1-v-4 and 2-v-3. Series lengths must be positive odd numbers
and default to one. The response includes the persisted bracket, each
semifinal series, and pending final series. An approved game updates its series;
reaching the required wins cancels unnecessary games and automatically assigns
the winner to every final-series contest. Completing the final series stores
the bracket champion.

### GET /api/competitions/{id}/playoffs
Get the authenticated tenant view of a competition's persisted playoff bracket,
rounds, matches, contest links, participants, winners, and champion.

### GET /api/public/tenants/{slug}/playoffs?competitionId={id}
Get the anonymous public projection of a published competition's playoff
bracket. It returns named participants and winners for each ordered round and
match, best-of length and per-game series history, plus the completed champion.
Draft/archived competitions and inactive tenants are not exposed. The public
web playoff route accepts `tenant={slug}` to select the corresponding tenant
projection.

Basketball fixtures generated by the round-robin and playoff endpoints include
four required quarters and conditional overtime. Setting the live-scoring
session context to overtime is rejected until regulation has reached its final
quarter and both participant live-score totals are tied.

### POST /api/competitions/{id}/golf/tournament
Create the authoritative four-round golf contest from exactly 24 approved
competition registrations. Each round contains 18 persisted nested hole
segments.

```json
{
  "tournamentName": "National Open",
  "editionName": "2026",
  "stageName": "Championship",
  "startTimeUtc": "2026-10-01T08:00:00Z",
  "venue": "Championship Course",
  "expectedParticipants": 24,
  "totalRounds": 4,
  "holesPerRound": 18,
  "parPerRound": 72,
  "cutAfterRound": 2,
  "cutToParticipants": 12
}
```

### POST /api/competitions/{id}/golf/scorecards
Persist one validated score for every hole in a golfer's round. Scores contain
strokes and optional penalty strokes. Duplicate scorecards are rejected.
Rounds after the configured cut round are rejected until the cut is applied,
and golfers who missed the cut cannot receive later-round scores.

```json
{
  "participantId": "00000000-0000-0000-0000-000000000000",
  "roundNumber": 1,
  "holes": [
    { "holeNumber": 1, "strokes": 4, "penalties": 0 }
  ]
}
```

The `holes` array must contain all holes 1–18 exactly once.

### POST /api/competitions/{id}/golf/cut
Apply the configured cut after every active golfer has complete pre-cut
scorecards. The score at the requested cut position is inclusive, so all
golfers tied at the line advance.

### GET /api/public/tenants/{slug}/golf/leaderboard?competitionId={id}
Return the anonymous four-round leaderboard with participant names, round
scores, total strokes, score to par, tied positions, cut status, withdrawals,
and disqualifications. The public `/leaderboard` page accepts
`tenant={slug}&competitionId={id}`.

### POST /api/competitions/{id}/combat/card
Create the authoritative eight-bout MMA card from sixteen unique approved
athlete registrations. The ordered `bouts` array must include preliminary,
main, co-main, and main-event sections. Normal bouts have three rounds and the
single main event has five. Every bout requires one named referee and at least
three distinct named judges.

```json
{
  "editionName": "2026",
  "stageName": "Fight Card",
  "startTimeUtc": "2026-10-10T17:00:00Z",
  "venue": "Combat Arena",
  "minutesBetweenBouts": 30,
  "bouts": [
    {
      "fighter1Id": "00000000-0000-0000-0000-000000000001",
      "fighter2Id": "00000000-0000-0000-0000-000000000002",
      "weightClass": "Lightweight",
      "cardSection": "PreliminaryCard",
      "scheduledRounds": 3,
      "officials": [
        {
          "officialId": "00000000-0000-0000-0000-000000000101",
          "displayName": "Referee One",
          "role": "Referee"
        },
        {
          "officialId": "00000000-0000-0000-0000-000000000102",
          "displayName": "Judge One",
          "role": "Judge"
        }
      ]
    }
  ]
}
```

The example abbreviates `bouts` and `officials`; production requests require
all eight bouts and the full official assignment for each.

### POST /api/competitions/{id}/combat/bouts/{boutId}/rounds/{round}/{start|end}
Start or end an ordered persisted bout round. A round must be pending to start
and in progress to end.

### POST /api/competitions/{id}/combat/bouts/{boutId}/result
Complete a bout using knockout, technical knockout, submission, unanimous,
split, or majority decision, disqualification, draw, or no contest. Stoppages
require a valid round and time from 0–300 seconds. Decisions and draws require
exactly three scorecards from judges assigned to that bout; each scorecard must
cover every scheduled round with valid 10-point-must values.

```json
{
  "winnerId": "00000000-0000-0000-0000-000000000001",
  "resultMethod": "Unanimous Decision",
  "round": null,
  "timeInRoundSeconds": null,
  "scorecards": [
    {
      "officialId": "00000000-0000-0000-0000-000000000102",
      "rounds": [
        { "roundNumber": 1, "fighter1Score": 10, "fighter2Score": 9 },
        { "roundNumber": 2, "fighter1Score": 10, "fighter2Score": 9 },
        { "roundNumber": 3, "fighter1Score": 10, "fighter2Score": 9 }
      ]
    }
  ]
}
```

### GET /api/public/tenants/{slug}/combat/card?competitionId={id}
Return the anonymous ordered fight card for a published competition, including
fighter names/corners, weight classes, card sections, scheduled rounds, named
officials, winner, result method, stoppage round/time, and judge scorecards.
The public `/fight-card` page accepts
`tenant={slug}&competitionId={id}`.

### POST /api/competitions/{id}/athletics/meet
Create the authoritative men's 100m demo from approved athlete registrations.
The workflow creates two lane-assigned heat contests and stores configurable
qualification-by-place, fastest-remaining, and cutoff-tie behavior.

```json
{
  "eventName": "Men's 100m",
  "editionName": "2026",
  "stageName": "Track",
  "startTimeUtc": "2026-10-12T08:00:00Z",
  "venue": "Main Track",
  "distanceMeters": 100,
  "expectedParticipants": 16,
  "heatCount": 2,
  "lanesPerHeat": 8,
  "qualifyByPlace": 3,
  "qualifyByTime": 2,
  "advanceTies": true,
  "minutesBetweenHeats": 15
}
```

### POST /api/competitions/{id}/athletics/races/{raceId}/results
Submit exactly one millisecond finish time or explained disqualification for
every participant in a heat or final. Times must be from 8,000 through 60,000
milliseconds. A race can only be submitted once.

```json
{
  "results": [
    {
      "participantId": "00000000-0000-0000-0000-000000000001",
      "timeMilliseconds": 10123,
      "disqualified": false
    },
    {
      "participantId": "00000000-0000-0000-0000-000000000002",
      "timeMilliseconds": null,
      "disqualified": true,
      "disqualificationReason": "False start"
    }
  ]
}
```

### POST /api/competitions/{id}/athletics/qualify
Apply the stored rule after both heats are complete. Disqualified athletes are
excluded, place qualifiers are selected first, and the fastest remaining
athletes fill the configured slots. When enabled, athletes tied at the
fastest-remaining cutoff all advance. The operation stores auditable
qualification reasons and creates the final contest.

### GET /api/public/tenants/{slug}/athletics/meet?competitionId={id}
Return both ordered heats and the generated final with named athletes, lanes,
millisecond times, tied positions, qualification reasons, disqualifications,
and gold/silver/bronze medals. The public `/athletics` page accepts
`tenant={slug}&competitionId={id}`.

### POST /api/competitions/{id}/cricket/tournament
Create a single round-robin T20 group from exactly four approved team
registrations. The six generated matches each contain two innings with twenty
nested overs.

```json
{
  "editionName": "2026",
  "groupStageName": "Group stage",
  "venue": "National Cricket Ground",
  "startTimeUtc": "2026-11-01T08:00:00Z"
}
```

### POST /api/competitions/{id}/cricket/matches/{matchId}/innings
Record an innings as ordered legal deliveries. Each delivery is persisted as
an approved contest event and attributed to its over. Balls must be contiguous
from 1.1, an innings is limited to 120 legal balls and ten wickets, and an early
finish is accepted only for all-out innings or a completed chase.

```json
{
  "inningsNumber": 1,
  "battingTeamId": "00000000-0000-0000-0000-000000000001",
  "deliveries": [
    {
      "overNumber": 1,
      "ballNumber": 1,
      "runs": 4,
      "extraRuns": 0,
      "extraType": null,
      "isWicket": false
    }
  ]
}
```

The response includes runs, wickets, balls/overs, target, winner, run or wicket
margin, and the tied-match Super Over placeholder.

### POST /api/competitions/{id}/cricket/qualify
After all six group matches are complete, rank teams by points and simplified
net run rate, then create 1-v-4 and 2-v-3 semifinals plus a pending final.
Completing each semifinal automatically inserts its winner into the final.

```json
{
  "stageName": "Knockout",
  "venue": "National Cricket Ground",
  "startTimeUtc": "2026-11-08T08:00:00Z",
  "daysUntilFinal": 2
}
```

### GET /api/competitions/{id}/cricket/tournament
Return the authenticated tournament projection with standings, group matches,
innings scorecards, semifinals, final, and champion.

### GET /api/public/tenants/{slug}/cricket/tournament?competitionId={id}
Return the same read-only projection anonymously for a published competition.
The public `/cricket` page accepts `tenant={slug}&competitionId={id}`.

---

## Contests

### Universal contest model

The same contest aggregate stores head-to-head team, head-to-head individual,
multi-participant ranked, race, heat, best-of-series, and fight-card-bout
shapes. Existing sport labels such as `TeamVsTeam`, `MultiRoundLeaderboard`,
`CricketGroupMatch`, athletics finals, and `Bout` are classified into those
families without changing their sport-specific configuration.

- `GET /api/contests/shapes` returns the seven canonical shape names.
- `GET /api/contests/{id}/model` returns status, venue, start/end times,
  participants, segments, events, metrics, officials, and results in one
  tenant-authorized response.
- `POST /api/contests/{id}/metrics` validates and records a typed metric with
  its unit and precision plus optional participant and segment. When a contest
  pins a ruleset, the metric must exist in that ruleset and its definition is
  authoritative. Unpinned/manual contests may supply `value`, `dataType`, and
  `precision`; the legacy `numericValue` and `textValue` fields remain accepted.
  Referenced participants and segments must belong to the contest.
- `POST /api/contests/{id}/officials` idempotently attaches an official from
  the contest tenant with a role and optional display name.

```json
{
  "name": "finishTime",
  "numericValue": 10.42,
  "unit": "s",
  "dataType": "Duration",
  "precision": 2,
  "participantId": "00000000-0000-0000-0000-000000000001",
  "segmentId": "00000000-0000-0000-0000-000000000002"
}
```

### GET /api/contests
List contests with filtering.

**Query Parameters:**
- `competitionId` (string): Filter by competition
- `sportCode` (string): Filter by sport
- `status` (string): Filter by status (Scheduled, Live, Completed, Cancelled)
- `date` (string): Filter by date (YYYY-MM-DD)
- `page` (int): Page number
- `pageSize` (int): Items per page

### GET /api/contests/live
Get all live contests.

**Response (200 OK):**
```json
{
  "items": [
    {
      "id": "contest-001",
      "competitionId": "comp-001",
      "competitionName": "Premier League 2025-26",
      "name": "Arsenal vs Chelsea",
      "sportCode": "football",
      "status": "Live",
      "scheduledAt": "2026-07-15T15:00:00Z",
      "startedAt": "2026-07-15T15:02:00Z",
      "homeParticipant": {
        "id": "team-001",
        "name": "Arsenal",
        "score": 2
      },
      "awayParticipant": {
        "id": "team-002",
        "name": "Chelsea",
        "score": 1
      },
      "currentSegment": "2nd Half",
      "elapsedSeconds": 3600
    }
  ],
  "totalCount": 3
}
```

### GET /api/contests/upcoming
Get upcoming scheduled contests.

### GET /api/contests/{id}
Get contest details.

**Response (200 OK):**
```json
{
  "id": "contest-001",
  "competitionId": "comp-001",
  "competitionName": "Premier League 2025-26",
  "name": "Arsenal vs Chelsea",
  "sportCode": "football",
  "status": "Live",
  "scheduledAt": "2026-07-15T15:00:00Z",
  "startedAt": "2026-07-15T15:02:00Z",
  "venue": {
    "name": "Emirates Stadium",
    "city": "London"
  },
  "homeParticipant": {
    "id": "team-001",
    "name": "Arsenal",
    "shortName": "ARS",
    "score": 2,
    "statistics": {
      "possession": 58,
      "shots": 12,
      "shotsOnTarget": 6,
      "corners": 7,
      "fouls": 10
    }
  },
  "awayParticipant": {
    "id": "team-002",
    "name": "Chelsea",
    "shortName": "CHE",
    "score": 1,
    "statistics": {
      "possession": 42,
      "shots": 8,
      "shotsOnTarget": 3,
      "corners": 4,
      "fouls": 14
    }
  },
  "currentSegment": "2nd Half",
  "elapsedSeconds": 3600,
  "segments": [
    {
      "name": "1st Half",
      "type": "Half",
      "status": "Completed",
      "startTime": "2026-07-15T15:02:00Z",
      "endTime": "2026-07-15T15:47:00Z",
      "duration": 2700
    },
    {
      "name": "Halftime",
      "type": "Break",
      "status": "Completed",
      "startTime": "2026-07-15T15:47:00Z",
      "endTime": "2026-07-15T16:02:00Z",
      "duration": 900
    },
    {
      "name": "2nd Half",
      "type": "Half",
      "status": "InProgress",
      "startTime": "2026-07-15T16:02:00Z",
      "elapsedSeconds": 3600
    }
  ],
  "events": [
    {
      "id": "event-001",
      "type": "goal",
      "timestamp": "2026-07-15T15:30:00Z",
      "segmentId": "seg-001",
      "participantId": "team-001",
      "data": {
        "scorer": "Saka",
        "assist": "Ødegaard",
        "score": "1-0"
      }
    }
  ],
  "tenantId": "tenant-001"
}
```

### POST /api/contests
Create a new tenant-scoped contest. The optional `name` is persisted and
returned by contest reads.

**Request Body:**
```json
{
  "tenantId": "tenant-001",
  "competitionId": "comp-001",
  "sportId": "sport-001",
  "contestShape": "HeadToHead",
  "name": "Arsenal vs Chelsea",
  "allowedParticipantTypes": ["Team", "CompositeTeam"]
}
```

`allowedParticipantTypes` must contain one or more supported participant types.
When omitted, the contest accepts every supported type.

### POST /api/contests/{id}/participants
Attach an existing tenant participant to a contest. The operation requires the
`contests.manage` permission, is idempotent for an existing participant slot,
rejects participants from another tenant, rejects a supplied type that does
not match the stored profile, and enforces the contest's accepted types.

**Request Body:**
```json
{
  "participantId": "team-001",
  "participantType": "Team"
}
```

### PUT /api/contests/{id}
Update contest details.

### POST /api/contests/{id}/start
Start a contest (change status to Live).

### POST /api/contests/{id}/complete
Complete a contest.

### POST /api/contests/{id}/cancel
Cancel a contest.

### POST /api/contests/{id}/events
Submit a live event.

**Request Body:**
```json
{
  "eventType": "goal",
  "participantId": "team-001",
  "timestamp": "2026-07-15T15:30:00Z",
  "segmentId": "seg-003",
  "data": {
    "scorer": "Saka",
    "assist": "Ødegaard",
    "score": "1-0"
  }
}
```

**Response (201 Created):**
```json
{
  "id": "event-002",
  "contestId": "contest-001",
  "eventType": "goal",
  "participantId": "team-001",
  "timestamp": "2026-07-15T15:30:00Z",
  "submittedBy": "user-123",
  "data": {
    "scorer": "Saka",
    "assist": "Ødegaard",
    "score": "1-0"
  }
}
```

### POST /api/contests/{id}/events/{eventId}/correct
Submit a correction for an event.

**Request Body:**
```json
{
  "reason": "Incorrect scorer credited",
  "correctedData": {
    "scorer": "Martinelli",
    "assist": "Saka",
    "score": "1-0"
  }
}
```

### POST /api/contests/{id}/checkin
Check in a participant for a contest.

**Request Body:**
```json
{
  "participantId": "team-001"
}
```

---

## Participants

### GET /api/participants
List tenant participants. Supply `tenantId` when authorized and optionally
filter by `participantType`.

Supported types are `Team`, `Athlete`, `Pair`, `RelayTeam`, `Crew`, `Club`,
`School`, `Country`, and `CompositeTeam`.

### GET /api/participants/{id}
Get the shared participant profile and all sport-specific attribute profiles.

**Response (200 OK):**
```json
{
  "id": "team-001",
  "type": 0,
  "name": "Arsenal FC",
  "shortName": "ARS",
  "tenantId": "tenant-001",
  "sportProfiles": [{
    "sportId": "sport-001",
    "attributesJson": "{\"position\":\"Wing\"}"
  }]
}
```

Participant types are accepted by name in request/query fields. The current
participant entity response serializes the enum ordinal (`Team` is `0`,
`Athlete` is `1`, through `CompositeTeam` as `8`).

### POST /api/participants
Create any supported type through the shared participant model. The legacy
`/api/participants/teams` and `/api/participants/athletes` routes remain
available.

```json
{
  "tenantId": "tenant-001",
  "type": "Crew",
  "name": "Lake Victoria Eight",
  "shortName": "LV8"
}
```

### PUT /api/participants/{id}/sports/{sportId}/attributes
Create or replace one sport's JSON-object attributes without changing the
participant's other sport profiles.

```json
{
  "attributes": { "boatClass": "Eight", "seed": 3 }
}
```

---

## Registrations

### GET /api/registrations/competition/{competitionId}
List tenant-authorized registrations for a competition.

### GET /api/registrations/contest/{contestId}
List tenant-authorized registrations for a contest.

### GET /api/registrations/{id}
Get registration details.

### POST /api/registrations/competition/{competitionId}
Create an idempotent draft competition registration. The competition and
participant must belong to the same authorized tenant.

**Request Body:**
```json
{
  "participantId": "00000000-0000-0000-0000-000000000201",
  "type": 1
}
```

`type: 1` represents a team registration.

### POST /api/registrations
Create a draft contest registration with `contestId`, `participantId`, and
`type`.

### POST /api/registrations/{id}/submit
Submit registration for review.

### POST /api/registrations/{id}/approve
Approve registration.

### POST /api/registrations/{id}/reject
Reject registration.

**Request Body:**
```json
{
  "reason": "Incomplete documentation provided"
}
```

### POST /api/registrations/{id}/withdraw
Withdraw registration.

---

## Notifications

### GET /api/notifications
List notifications for current user.

**Query Parameters:**
- `isRead` (boolean): Filter by read status
- `page` (int): Page number
- `pageSize` (int): Items per page

### GET /api/notifications/{id}
Get notification details.

### PUT /api/notifications/{id}/read
Mark notification as read.

### PUT /api/notifications/read-all
Mark all notifications as read.

### DELETE /api/notifications/{id}
Delete a notification.

---

## Subscriptions

All subscription endpoints require authentication and the matching
`subscriptions.*` permission. Tenant-scoped callers can access only their own
tenant; platform administrators can manage any tenant.

### GET /api/subscriptions/active/{tenantId}

Return the tenant's latest non-draft lease, including grace-period, read-only,
or archived state.

### GET /api/subscriptions/history/{tenantId}

Return the tenant's lease history.

### POST /api/subscriptions

Create a draft lease from `tenantId`, `plan`, `startDate`, and `endDate`.

### POST /api/subscriptions/{id}/activate

Activate a draft lease.

### POST /api/subscriptions/{id}/suspend

Suspend a lease.

### POST /api/subscriptions/{id}/renew

Renew a lease and restore it to active status. `endDateUtc` must be in the
future.

```json
{
  "endDateUtc": "2027-07-31T00:00:00Z"
}
```

When a tenant is suspended, read-only, or archived, authenticated write
requests return `403 Forbidden` with `error` set to
`SubscriptionInReadOnlyMode`. Read endpoints and anonymous public routes remain
available.

---

## Health Check

### GET /health/live

Checks that the API process is alive. `/health` is a compatibility alias.

### GET /health/ready

Checks PostgreSQL and Redis readiness. `/health/dependencies` is a compatibility
alias. PostgreSQL failure returns `503 Service Unavailable`; Redis unavailability
is reported as degraded because PostgreSQL-backed authoritative operations remain
available.

**Response (200 OK):**
```json
{
  "status": "Healthy",
  "durationMilliseconds": 18.42,
  "checks": [
    {
      "name": "postgresql",
      "status": "Healthy",
      "description": "PostgreSQL is reachable.",
      "durationMilliseconds": 17.91
    },
    {
      "name": "redis",
      "status": "Healthy",
      "description": "Redis TCP endpoint is reachable.",
      "durationMilliseconds": 2.14
    }
  ]
}
```

Every response also returns `X-Correlation-Id` and `X-Request-Id`. See
[Observability operations](operations/observability.md).

---

## SignalR Hub

### Connection
Connect to the SignalR hub at `/hubs/contest`.

```
wss://api.sportsplatform.example/hubs/contest?access_token=<jwt_token>
```

### Client Methods (Server to Client)

#### ContestUpdated
Triggered when contest status changes.

```json
{
  "type": "ContestUpdated",
  "contestId": "contest-001",
  "status": "Live",
  "timestamp": "2026-07-15T15:02:00Z"
}
```

#### ScoreUpdated
Triggered when score changes.

```json
{
  "type": "ScoreUpdated",
  "contestId": "contest-001",
  "homeScore": 2,
  "awayScore": 1,
  "updatedBy": "user-123",
  "timestamp": "2026-07-15T16:30:00Z"
}
```

#### EventRecorded
Triggered when a new event is recorded.

```json
{
  "type": "EventRecorded",
  "contestId": "contest-001",
  "event": {
    "id": "event-002",
    "type": "goal",
    "participantId": "team-001",
    "timestamp": "2026-07-15T16:30:00Z",
    "data": {
      "scorer": "Saka",
      "score": "2-1"
    }
  }
}
```

#### SegmentChanged
Triggered when contest segment changes.

```json
{
  "type": "SegmentChanged",
  "contestId": "contest-001",
  "previousSegment": "1st Half",
  "currentSegment": "Halftime",
  "timestamp": "2026-07-15T15:47:00Z"
}
```

### Server Methods (Client to Server)

#### JoinContest
Subscribe to contest updates.

```csharp
await hubConnection.InvokeAsync("JoinContest", "contest-001");
```

#### LeaveContest
Unsubscribe from contest updates.

```csharp
await hubConnection.InvokeAsync("LeaveContest", "contest-001");
```

---

## Error Responses

All errors follow a standard format:

```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.1",
  "title": "Bad Request",
  "status": 400,
  "detail": "The request could not be understood due to malformed syntax.",
  "traceId": "00-abc123-def456-00",
  "errors": {
    "email": ["The Email field is not a valid e-mail address."]
  }
}
```

### Common Status Codes

| Code | Description |
|------|-------------|
| 400 | Bad Request - Invalid request data |
| 401 | Unauthorized - Missing or invalid authentication |
| 403 | Forbidden - Insufficient permissions |
| 404 | Not Found - Resource doesn't exist |
| 409 | Conflict - Resource conflict (e.g., duplicate) |
| 422 | Unprocessable Entity - Validation failed |
| 429 | Too Many Requests - Rate limit exceeded |
| 500 | Internal Server Error - Server error |
| 503 | Service Unavailable - Service temporarily unavailable |

---

## Rate Limiting

Rate limits are partitioned by authenticated user ID or client IP. The API
defaults to 300 requests/minute globally, 30 authentication requests/minute,
10 password-recovery requests/15 minutes, and 10 CSV uploads/minute. Values
can be changed under `Security:RateLimits`. Rejected requests return HTTP 429,
a JSON `RateLimitExceeded` error, and `Retry-After` when available.

## Security controls

- JSON authentication inputs are constrained for required values, lengths,
  email/phone formats, and password minimums.
- Responses include content-sniffing, framing, referrer, permissions, content
  security, cross-origin resource, and no-store protections. HSTS is added for
  HTTPS requests, including correctly forwarded HTTPS traffic.
- Browser access is restricted to configured `Security:AllowedOrigins` without
  credentialed cross-origin requests. Authentication uses bearer tokens in the
  `Authorization` header rather than ambient cookies, so cookie-based CSRF does
  not apply to these API routes.
- Refresh tokens are single-use: a successful refresh replaces the stored
  token and replay of the old value is rejected. Five failed password attempts
  lock the account for 15 minutes.
- TOTP MFA enrollment, verification, disablement, and login enforcement are
  available at `/api/auth/mfa/*`. Enrollment secrets are protected at rest with
  ASP.NET Data Protection; staging persists its key ring in a dedicated volume.
- Bulk CSV imports require authorization and participant-create permission,
  are limited to 2 MB and 10,000 rows, and validate safe filenames, MIME type,
  UTF-8 text, null bytes, row length, and required headers before parsing.
- JWT rotation uses `Jwt:Secret` for newly issued tokens and optionally accepts
  `Jwt:PreviousSecrets` during the overlap window. Set the former signing key as
  a previous secret, deploy the new current key, wait for old access tokens to
  expire, then remove the previous key. Every configured signing secret must be
  at least 32 UTF-8 bytes.

---

## Pagination

All list endpoints support pagination:

**Request:**
```
GET /api/competitions?page=2&pageSize=20
```

**Response:**
```json
{
  "items": [...],
  "totalCount": 150,
  "page": 2,
  "pageSize": 20,
  "totalPages": 8,
  "hasNextPage": true,
  "hasPreviousPage": true
}
```

---

## Reports

All report routes require authentication. Tenant owners need `reports.read` to
list/status reports and `reports.export` to queue, retry, or download them.
Every request and stored report is tenant-scoped.

### GET /api/reports/catalog

Returns the authoritative report catalogue and identifies report types that
are currently available. Registration List, Team Roster, Official Assignment,
Match Sheet, Fixture Schedule, Results, Standings, and Competition Summary are
implemented alongside Discipline, Athlete Performance, and Audit History. All
eleven catalogue families support genuine PDF, UTF-8 CSV, Excel `.xlsx`, and
print-ready HTML.
Registration reports contain tenant-owned competition/contest entries and
their workflow state; roster reports contain tenant-owned team and athlete
membership, jersey numbers, positions, and active state. Official-assignment
reports use the current contest-owned official records. Match-sheet reports
include sport, contest format, participants, segments, officials, schedule,
venue, and status; branded archival contest sheets remain a separate workflow.
Standings include the core table fields plus sport-specific metadata. Competition
summaries aggregate editions, stages, registrations, distinct participants,
contests, completed contests, result rows, and standing rows.
Discipline combines canonical card, foul, penalty, and disqualification events
with durable participant-disqualification history. Athlete Performance uses
athlete-only results and includes ranking, performance/statistics data, record
flags, and remarks. Audit History exports tenant-owned audit entries and resolves
an optional competition filter through its owned stages, contests, results,
registrations, and standings. XLSX cells are safely limited to Excel's maximum
cell length without truncating CSV or HTML exports. PDF output is rendered with
PDFsharp/MigraDoc as an A4 document, switches to landscape for wide reports,
repeats table headers across pages, includes page numbering, and uses bundled
system-font resolution consistently on Windows and Linux containers.

### GET /api/reports?tenantId={tenantId}&page=1&pageSize=20

Returns paginated report metadata without loading the generated file bytes.

### POST /api/reports

Queues a durable background generation job and returns HTTP 202. Optional
competition and inclusive UTC date filters are validated within the tenant.

```json
{
  "tenantId": "00000000-0000-0000-0000-000000000002",
  "name": "Premier fixture schedule",
  "type": "FixtureSchedule",
  "format": "Excel",
  "competitionId": "00000000-0000-0000-0000-000000000300",
  "fromUtc": null,
  "toUtc": null
}
```

### GET /api/reports/{id}

Returns `Pending`, `Processing`, `Completed`, or `Failed` status and safe error
metadata. Pending jobs are recovered when the API restarts.

### GET /api/reports/{id}/download

Downloads completed content with its exact media type and file name. Returns
HTTP 409 until generation completes.

### POST /api/reports/{id}/retry

Clears a completed or failed artifact and queues it again.

### Printable contest sheets

Printable contest sheets are separate immutable archives rather than an
additional general report family. They use the same durable background worker
and require `reports.read` for metadata or `reports.export` for creation,
retry, and download.

`POST /api/contest-sheets` accepts a tenant-owned `contestId` and optional
archive `name`, returns HTTP 202, and captures the contest data available at
generation time. The PDF applies the tenant name, short name, configured brand
colors and brand-asset reference; includes active contest- or
competition-linked sponsors; and renders participants, officials, results,
sign-off fields, and sport-specific sections. Templates cover football, rugby,
basketball, T20 cricket, golf, combat sports, and athletics, with a safe generic
contest fallback.

`GET /api/contest-sheets?tenantId={tenantId}&contestId={contestId}` lists the
tenant's archived snapshots. `GET /api/contest-sheets/{id}` returns generation
status, `POST /api/contest-sheets/{id}/retry` regenerates a failed archive, and
`GET /api/contest-sheets/{id}/download` returns the archived PDF. Cross-tenant
contest selection, listing, status, retry, and download are denied.

### Tenant data exports

`POST /api/tenant-exports` requires `reports.export`, accepts the authenticated
tenant ID and an optional archive name, and queues a durable background export.
The completed ZIP contains a versioned manifest plus JSON datasets for tenant
identity and branding, credential-safe users, referenced sports, participants,
competition structure, contests, results, registrations, standings, live-score
events, notifications, officials, brackets, leaderboards, subscriptions, report
archive metadata, linked sponsors/sponsorships, and tenant audit entries.
Password hashes, refresh tokens, and device-session credentials are explicitly
excluded.

`GET /api/tenant-exports?tenantId={tenantId}` and
`GET /api/tenant-exports/{id}` expose tenant-scoped archive metadata and status.
`POST /api/tenant-exports/{id}/retry` retries a completed or failed export, and
`GET /api/tenant-exports/{id}/download` returns `application/zip`. Creation,
completion, failure, retry, and every download are written to tenant-owned audit
history; request IP address and user-agent context are captured for creation and
download. A tenant administrator cannot list, create, inspect, retry, or download
another tenant's export.

### Audit history

`GET /api/audit` requires `audit.read` and returns append-only history for the
authenticated tenant. Platform operators may select `tenantId` or omit it to
search across tenants; tenant users cannot select another tenant. Supported
filters are `userId`, partial `action`, exact `entityType`, exact `entityId`,
free-text `search` across action/target/reason, `fromUtc`, `toUtc`, `page`, and
`pageSize` (maximum 200). Results are newest first and include the actor's safe
name/email projection, tenant, action, target, timestamp, reason, before/after
JSON, IP address, and user agent.

Authentication, tenant, role/permission, registration decision, scheduling,
live-score, correction, result approval, report/export, and subscription lease
mutations produce audit entries. Missing reasons are normalized to `Not
supplied`, so every entry has a reason even when the underlying operation does
not accept an operator note. The application rejects tracked updates and
deletes to audit entities, and staging/production PostgreSQL installs an
`AuditEntries` trigger that independently rejects SQL `UPDATE` and `DELETE`.

### Privacy controls

All privacy routes require authentication. `GET /api/privacy/me` returns only
the signed-in user's safe profile fields, current visibility/data-use settings,
versioned consent history, and data-subject requests. `PUT
/api/privacy/consents/{kind}` records a new grant or revocation event with its
policy version, source, timestamp, and request IP; earlier decisions are retained.
`PUT /api/privacy/settings` controls Public, Tenant-only, or Private profile
visibility plus activity, analytics, and marketing preferences. `PUT
/api/privacy/profile` performs a reasoned correction to the user's name or phone
number.

`POST /api/privacy/requests` accepts DataAccess, DataCorrection, or
AccountDeletion. Data-access requests immediately create a credential-safe JSON
package available only to that user at `GET
/api/privacy/requests/{id}/download`; password hashes, refresh tokens, and
device credentials are excluded. Deletion and correction requests enter a
review queue. Authorized tenant administrators use `GET
/api/privacy/admin/requests?tenantId=...`, the decision endpoint, and the
completion endpoint. Completing an approved deletion anonymizes direct account
identifiers, disables the account, and revokes its sessions while retaining
referential and audit integrity.

Tenant retention administration requires `privacy.read` or `privacy.manage`.
The `/api/privacy/admin/retention` routes create, list, update, delete, and
manually run tenant-scoped policies for old notifications, revoked/expired
device sessions, or completed privacy requests. Policies configure retention
days, deletion versus supported anonymization, active state, and run interval.
An hourly hosted sweep executes active due policies using the same audited
service as manual runs. Every consent, setting, correction, request decision,
download, deletion completion, policy mutation, and retention execution is
written to append-only audit history.

### Sponsor management

Sponsor administration requires `sponsorships.read` for reads and
`sponsorships.manage` for mutations. `GET /api/sponsors?tenantId={tenantId}`
returns tenant-owned sponsor profiles, their sponsorships, current visibility,
and the supported placement catalogue. `GET /api/sponsors/{id}` returns one
profile and rejects access from another tenant.

`POST /api/sponsors` creates a tenant-owned profile with its name, description,
logo URL, website, contact details, and tier. `PUT /api/sponsors/{id}` updates
the profile or activates/deactivates it. Logo and website values must be
absolute HTTP or HTTPS URLs, and every profile mutation is written to the
tenant audit history.

`POST /api/sponsors/{sponsorId}/sponsorships` creates a commercial placement;
`PUT /api/sponsors/sponsorships/{id}` updates it. A placement contains its
level, value and currency, active dates, benefits, contract reference, display
order, placement rule, and exactly one tenant-owned competition or contest
association. Supported rules are `PublicHome`, `CompetitionHeader`,
`ContestHeader`, `ContestSheet`, `Sidebar`, and `Footer`. Placement creation is
server-gated by the `sponsorship.placements` subscription entitlement.

`POST /api/sponsors/sponsorships/{id}/activate` publishes a proposed placement,
and `POST /api/sponsors/sponsorships/{id}/terminate` removes it from use. Public
home projection uses only `PublicHome` placements, while generated contest
sheets use only `ContestSheet` placements. Both surfaces require an active
sponsor and active sponsorship whose current time is inside its configured
date window. Sponsor and association tenant IDs are checked independently,
preventing a tenant asset from being attached to another tenant's competition
or contest.

---

## Filtering and Sorting

### Filter Examples

```
GET /api/competitions?sportCode=football&status=Active
GET /api/contests?status=Live&date=2026-07-15
GET /api/participants?type=Team&sportCode=basketball&search=LA
```

### Sort Examples

```
GET /api/competitions?sortBy=startDate&sortOrder=desc
GET /api/participants?sortBy=name&sortOrder=asc
```

---

## Webhooks

Configure webhooks to receive real-time notifications:

### Available Events

- `contest.started` - Contest has started
- `contest.completed` - Contest has completed
- `contest.cancelled` - Contest has been cancelled
- `score.updated` - Score has been updated
- `registration.submitted` - New registration submitted
- `registration.approved` - Registration approved
- `registration.rejected` - Registration rejected

### Webhook Payload

```json
{
  "event": "contest.completed",
  "timestamp": "2026-07-15T17:45:00Z",
  "data": {
    "contestId": "contest-001",
    "homeScore": 2,
    "awayScore": 1,
    "winnerId": "team-001"
  }
}
```

---

## SDKs and Libraries

### Official SDKs

- **.NET**: `SportsPlatform.SDK`
- **JavaScript/TypeScript**: `@sportsplatform/sdk`
- **Flutter**: `sports_platform_sdk`

### Community Libraries

- Python wrapper (community maintained)
- Go client (community maintained)

---

## Support

- **Documentation**: https://docs.sportsplatform.example
- **API Status**: https://status.sportsplatform.example
- **Support Email**: support@sportsplatform.example
- **Developer Forum**: https://developers.sportsplatform.example

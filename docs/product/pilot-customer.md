# Pilot customer persona — Kampala Metro Sports Association (fictional)

> **Everything here is fictional.** The association, its clubs, schools, venues, people and domain names were
> invented for demos, seed data, UX examples and acceptance tests. Any resemblance to real organisations or
> people is coincidental. Never use the names of real clubs, federations, leagues, venues or people in seeds or
> demos; if a name below turns out to belong to a real one, rename it here and in the seeds.

**Why this exists.** The owner has no customer yet (open question Q5, answered 2026-10-17). One consistent,
realistic customer keeps demos, seeds, acceptance scenarios and UX examples coherent, and it carries the owner's
sport order: **football → rugby → basketball → others**.

## At a glance

| | |
|---|---|
| Name | **Kampala Metro Sports Association (KMSA)** |
| What it is | A volunteer-run umbrella body for community and corporate club sport across greater Kampala (Kampala, Wakiso, Mukono) |
| Members | 34 clubs; about 2,400 registered players, about 600 of them under 18 |
| Tenant | slug `kmsa`; public site `kmsa.<platform-domain>`; later its own domain `www.kmsa.example` (PRO) |
| Settings | time zone `Africa/Kampala` · currency `UGX` · country `UG` · English |
| Plan | STANDARD (football, rugby and basketball = 3 sports) on the pilot offer: six months `COMPLIMENTARY`, then paid annually (docs/product/pricing-and-plans.md) |
| Upgrade trigger | Adds netball in 2028 (a 4th sport) and wants its own domain and sponsor campaigns → PRO |
| Tools today | WhatsApp groups, Excel fixture sheets, photos of paper score sheets, a Facebook page |

## People and their platform roles

| Person | Job at KMSA | Role (scope) |
|---|---|---|
| Grace Nakato | General Secretary; signs the contract | `TENANT_OWNER` (tenant) |
| Brian Ssemakula | Registrar: player and club registration | `REGISTRAR` (tenant) |
| Moses Okello | Football competitions chair | `COMPETITION_MANAGER` (each football competition) |
| Isaac Mugisha | Rugby convenor | `COMPETITION_MANAGER` (each rugby competition) |
| Sarah Achieng | Basketball convenor | `COMPETITION_MANAGER` (each basketball competition) |
| Esther Nabirye | Results and discipline officer | `RESULTS_OFFICER` (tenant) |
| Daniel Kato | Media and sponsorship volunteer | `TENANT_ADMIN` (tenant) until a narrower role exists |
| Ruth Atim | Volunteer scorer, university student, mid-range Android phone on prepaid data | `SCORER` (assigned contests) |
| Patrick Opio | Team manager, Ntinda Swifts FC Senior Men | `TEAM_MANAGER` (that team) |
| Joan Nansubuga | Team manager, Kisaasi Kestrels FC Senior Men | `TEAM_MANAGER` (that team) |

## What hurts today (jobs to be done)

1. Fixtures live in Excel and change over WhatsApp; clubs miss changes and turn up at the wrong ground.
2. Results arrive as photos of paper score sheets; the table is updated by hand and disputed (goal difference,
   forfeits, points deductions).
3. Fans and parents ask "what's the score?" in WhatsApp groups; there are no live updates.
4. Local sponsors want visible logos and numbers to justify their money.
5. Eligibility disputes: players registered with two clubs; over-age players in the under-17 cup.
6. Volunteers change every season, so scoring must be learnable in ten minutes on a phone.
7. Mobile data at grounds is patchy and costs money: pages must be light; offline scoring matters later.
8. Money is tight: KMSA pays once a year, after sponsor money arrives in February.

## Competitions (in the owner's sport order)

| Sport | Competition (2027) | Format | Builds on |
|---|---|---|---|
| Football | **Metro Football League** — Senior Men, 10 teams | Double round robin (18 rounds), Saturdays, February–November | M1 (S05–S14) |
| Football | Metro Women's League — 6 teams | Double round robin, March–July | M1 |
| Football | Metro Under-17 Cup — 8 teams (all players minors) | Single elimination with extra time and penalties | M1 + minors' consent (S06, S09) |
| Rugby | **Metro Rugby League** (15s) — 6 teams | Single round robin, then semi-finals and final; try and losing bonus points | S15 |
| Rugby | **Metro Sevens** — 8 teams, one festival day | Two pools of four, then Cup and Plate knockouts; sudden-death extra time | S15 |
| Basketball | **Metro Basketball League** — 8 men's teams, 4 women's teams | Double round robin; best-of-3 semi-finals, best-of-5 final (men) | S16 |
| Others (2028) | Metro Netball League; Metro Corporate T20 Cup (cricket) | League; T20 league | Phase 9 (netball), S17 (cricket) |

## Seed set (DEMO data)

Seeds are created through application commands (AR-22), never raw inserts.

**Clubs (organisations) and their teams**

| Club | Teams |
|---|---|
| Ntinda Swifts SC (multi-sport) | Ntinda Swifts FC Senior Men, Women, Under-17; Ntinda Swifts RFC (15s and 7s) |
| Kisaasi Kestrels SC (multi-sport) | Kisaasi Kestrels FC Senior Men, Women, Under-17; Kisaasi Kestrels basketball (men, women) |
| Kololo Kudus Club (multi-sport) | Kololo Kudus RFC (15s and 7s); Kololo Kudus basketball (men, women) |
| Naguru Nightjars Club (multi-sport) | Naguru Nightjars RFC (15s and 7s); Naguru Nightjars basketball (men) |
| Football clubs | Bukoto Comets FC, Kansanga Harriers FC, Nakawa Nomads FC, Kireka Kingfishers FC, Bunga Breakers FC, Seeta Sparrows FC, Kyebando Ravens FC, Mutundwe Meteors FC (with women's and under-17 teams where the competitions above need them) |
| Rugby clubs | Ggaba Gulls RFC, Muyenga Mambas RFC, Mutungo Marlins RFC (plus Bukoto Comets 7s and Seeta Sparrows 7s for the sevens) |
| Basketball clubs | Bugolobi Bison, Muyenga Monarchs (men, women), Kansanga Kites, Lubaga Lynx (men, women), Kira Ibis |

**Venues** (all `Africa/Kampala`): Bukoto Community Stadium (football, 3,000 seats) · Kisaasi Recreation Ground
(football, rugby) · Ggaba Lakeside Rugby Ground (rugby) · Metro Indoor Arena, Naguru (basketball, netball) ·
Seeta Hill Sports Field (football, cricket).

**People:** realistic Ugandan names (mix of Luganda, Luo, Runyankole, Swahili and English names), birth dates
that make every under-17 player a minor, a few deliberate near-duplicates to exercise duplicate warnings, and
guardian consents for some — not all — minors.

**Sponsors (fictional businesses):** Nile Crest Insurance, Metro Fresh Dairy, Mengo Hardware Supplies — logos
are generated placeholders.

**Demo sign-in (S03 D18).** The DEMO seed (`identity.demo.invitations`) invites, never creates passwords for:
`grace.nakato@kmsa.example` (`TENANT_OWNER`), `daniel.kato@kmsa.example` (`TENANT_ADMIN`),
`brian.ssemakula@kmsa.example` (`REGISTRAR`), `esther.nabirye@kmsa.example` (`RESULTS_OFFICER`) and
`agnes.namutebi@elssc.example` (`TENANT_OWNER`). The addresses are invented (`.example` never delivers). To use one:
`docker compose up -d`, `seed --category DEMO`, run the API (its outbox sends the emails), open Mailpit
(http://localhost:8025), take the token from the invitation's link and accept it with a password of your choosing:
on the admin app's accept page once S03-T6 builds it, until then `POST /api/account/v1/invitations/accept` (Scalar).
Invitations expire after 7 days; the seeder sends a new one when the data changes or after `seed --reset`. An
accepted invitation is never sent again, even after that member is removed: run `seed --reset` to get them back. The other
people above get roles scoped to competitions, teams or contests, which arrive with those modules.

**Demo subscriptions (S04 D18).** The DEMO seed (`tenancy.demo.subscriptions`, through `subscription.assign` as the
system) gives KMSA a STANDARD period, `COMPLIMENTARY`, for six months from the seed date (the pilot offer), and ELSSC a
STANDARD period, `PAID`, for 45 days (the event pass), with the override `MAX_ACTIVE_EDITIONS = 1` until the
period's last day (through `entitlement.override`, reason "Event pass: one active edition, the Inter-Schools Football
Festival"). Days are in `Africa/Kampala`. A tenant that already has a period (or, for the override, any override) is
left alone, so once a demo period has ended (the tenant is read-only), run `seed --reset` to start again.

**Demo sports (S06 D23).** The DEMO seed (`tenancy.demo.sports`, through `tenant.sport.enable` as the system) enables
football (`FOOTBALL_ASSOCIATION`) for KMSA and ELSSC; a variant already enabled is skipped. The variants are listed
in `Seed/Demo/Data/demo-sports.json` (Tenancy), not in code.

**Demo people (S06 D23).** The DEMO seed (`registry.demo.people`, from `Seed/Demo/Data/demo-people.json` in Registry)
creates each person through the duplicate check and `person.create` as the system, then their profiles, consents and
evidence, photos and lifecycle through the Registry's commands. Birth dates are ages on the seed date, so minors stay
minors whenever the demo is seeded; a twin, or the second of a decided pair, gets the first one's date. A tenant that
isn't `ACTIVE` when the seed runs is skipped until the next `seed --reset` (or a change to the data). Photos and consent forms are generated placeholder images, never photos of anyone;
guardians' contacts are `example.com` addresses. Clubs are noted in the file for S08's teams; no team exists yet.

- **KMSA (40):** 26 players of the Metro Football League clubs and the Ntinda Swifts women, among them
  **John Ssekandi** (Ntinda Swifts, striker; the demo's likely match), **Mariam Nankya** (Kisaasi Kestrels women;
  the demo adds her twin Shamim), **Peter Ochieng** and **Peter Ochieng Okot** (decided "not the same person"), the
  twins **Babirye Lillian Namaganda** and **Nakato Irene Namaganda** (one birth date, decided "Twin sister,
  different first name"), and **Ivan Mukasa**, archived ("Left the association"). **Denis Opoka** is a goalkeeper,
  assistant coach and referee (licence until 2027-06-30). Staff Charles Mwesigwa (head coach) and Florence Akurut
  (physiotherapist); officials Richard Ekwaro (referee) and Winnie Nambooze (assistant referee). Ten under-17
  players, four with a guardian's consent; **Brenda Akello** (15) has none, so the demo records it. Ten photos.
- **ELSSC (10):** pupils of Lakeshore High School, Mabira Hills College, Riverside Secondary School and Sunrise
  Academy, all under 16 (the festival's age) with athlete profiles; seven with a guardian's consent (four with a scanned form), three
  photos.

No public profile is on. Officials' licence dates are fixed (Denis Opoka's 2027-06-30, as the slice demo says), so
they show as expired once that day has passed. A person is found again by name: a run only adds what a seeded person
lacks (a profile, a consent, its form, a photo, the archived status). A person renamed, archived or marked deceased by
hand can't be completed that way (the run stops or the duplicate check disagrees): run `seed --reset`.

## Second tenant (isolation and multi-tenant demos)

**Eastern Lakes Schools Sports Council (ELSSC)** — slug `elssc`, fictional. Twelve secondary schools (for
example Lakeshore High School, Mabira Hills College, Riverside Secondary School, Sunrise Academy). Almost every
player is a minor. It buys an **event pass** for its two-day Inter-Schools Football Festival (under-16 boys and
girls, knockout). ELSSC proves that two tenants never see each other's data, brand or users, and it exercises
consent-first registration (tenant setting: guardian consent required before registration).

| Person | Job at ELSSC | Role (scope) |
|---|---|---|
| Agnes Namutebi | Council General Secretary; signs the event-pass contract | `TENANT_OWNER` (tenant) |

## Other tenants (tests only)

Tests that need more than two tenants (paging, status filters, a provisioning tenant) use these invented bodies,
named after trees and birds so they don't read as a real district, council or league: **Acacia Schools Netball
Circle** (`acacia-schools`), **Jacaranda Corporate Games** (`jacaranda-games`), **Mvule Youth Football League**
(`mvule-youth`), **Sunbird Junior Sports Club** (`sunbird-juniors`), **Acacia Road Runners** (`acacia-runners`),
**Weaver Bird Athletics Club** (`weaver-athletics`), **Kingfisher Masters Hockey Club** (`kingfisher-masters`),
**Hornbill Volleyball Club** (`hornbill-volleyball`), **Shoebill Community Sports Club** (`shoebill-sports`). Specs
that add members use one of their own rather than KMSA or ELSSC (S06-T7a): **Turaco Sports Club** (`turaco-sports`),
**Marabou Sports Club** (`marabou-sports`), **Papyrus Lakeside Sports Club** (`papyrus-lakeside`), **Pelican Football
Club** (`pelican-football`), **Mango Grove Sports Club** (`mango-grove`), **Heron Netball Club** (`heron-netball`),
**Fig Tree Schools Sports Circle** (`fig-tree-schools`), **Tamarind Junior Sports Club** (`tamarind-juniors`), **Ibis
Rugby Club** (`ibis-rugby`), **Lark Basketball Club** (`lark-basketball`), **Egret Hockey Club** (`egret-hockey`),
**Musizi Athletics Club** (`musizi-athletics`), **Cassia Community Games** (`cassia-games`), **Bee-eater Sports
Club** (`bee-eater-sports`), **Crane Netball Club** (`crane-netball`), **Hammerkop Sports Club**
(`hammerkop-sports`), **Wagtail Junior Sports Club** (`wagtail-juniors`), **Bulbul Sports Club**
(`bulbul-sports`), **Barbet Sports Club** (`barbet-sports`) and **Hoopoe Sports Club** (`hoopoe-sports`); the S06 slice demo uses **Coucal Sports Club** (`coucal-sports`) and
**Francolin Schools Sports Circle** (`francolin-schools`). They are never seeded; e2e runs make each slug and name run-unique.

## Privacy notes for this persona

- Under-17 players: public profiles off by default; display name, team and age group only when consent allows;
  photos only with the `PHOTO` scope (ADR-0015).
- KMSA's tenant setting: guardian consent **required before registration** for the under-17 cup.
- Adult players accept a privacy notice at registration that says where data is stored (ADR-0016).

## How the persona is used

- **M1 acceptance (S14):** KMSA runs the Metro Football League with four entrants, from provisioning to a
  corrected, re-approved result and an updated public table.
- **Seeds:** S02 (tenants), S06 (people), S07 (clubs, teams, venues), S09 (competitions) use the names above.
- **UX examples and docs** use KMSA names, never "Team A" or lorem ipsum.
- **Entitlement demos:** a sport quota refusal says the limit, not a plan: "Your plan allows N sports." Only football
  is published in S05, so the S05 demo shows it through the override `MAX_ENABLED_SPORTS = 0` (S05 D18); a 4th sport
  on STANDARD (after cricket, S17) is refused the same way. ELSSC's event pass allows one active edition.

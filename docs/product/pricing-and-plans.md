# Plans and pricing

**Status:** Adopted 2026-10-17 on the reviewer's recommendation. The owner delegated this decision (open
question Q2). Revisit after the first three paying customers.

Prices are commercial data, not code. The code holds **plans and entitlements**: the exact limits are in
docs/architecture/authorization.md, which is the source of truth for STANDARD and PRO values. Prices live here
and on invoices. v1 has no billing integration: platform admins record subscriptions (S04).

## What comparable products do (researched October 2026)

| Product | Free tier | Paid tiers | What paying adds |
|---|---|---|---|
| Tournify (tournaments) | Amateur: up to 8 teams, live website | World Class €40 per event (up to 60 teams); Legendary €120 per event (unlimited teams) | Custom branding, sponsor listing, publishing in the Android/iOS app |
| CricClubs (cricket leagues) | Basic/Community: core live scoring, one theme, mobile app; ads, no custom domain, few sponsor logos | Essentials ($1) and Premium ($3) per player or per team per month; Enterprise by quote | Premium: custom domain, no ads, more sponsor slots, advanced reports. Enterprise: dedicated mobile app, REST APIs, dedicated server |
| LeagueRepublic (leagues) | Free version with full functionality | Paid plans (price not published on the review site) | Removes ads, stronger branding |
| GameChanger (team scoring) | Everything free for coaches, staff and scorekeepers | Fans pay $9.99 per month or $39.99 per year | Live-stream viewing, highlight clips, box scores |
| Pitchero (club websites) | Free version | From £12 per user per month (Capterra) | Club website and management features |

**The pattern.** Live scoring and a platform-branded public site are the minimum everyone offers, often for
free. Organisers pay for: their own domain and brand, sponsor visibility without platform ads, scale (more teams
and events), better reports, data integrations (APIs) and white-label apps (enterprise). Free tiers work for
products that are ad-funded at large scale or that charge fans. We have neither yet.

## The plans

| Plan | For | In one line |
|---|---|---|
| **STANDARD** | A league, association, schools council or club running up to 3 sports | Everything needed to run competitions, score live and publish a public site on the platform's domain |
| **PRO** | Larger associations and federations | More sports and seasons, own domain and full branding, sponsorship campaigns, transfers, historical import, advanced reports, public notifications, API |
| **Event pass** | One tournament, festival or sports day (up to 45 days) | A STANDARD subscription with a short term and one active edition — not a separate plan in code |
| **Enterprise** | National federations, multi-country bodies | PRO plus overrides: white-label mobile apps, national registry, single sign-on, dedicated hosting or region, service-level agreement |

Two plans exist in code (`STANDARD`, `PRO`). The event pass is a STANDARD subscription period with an override
`MAX_ACTIVE_EDITIONS = 1`. Enterprise is PRO with per-tenant overrides until a third plan is justified.

### What each plan includes (plain words)

**STANDARD:** up to 3 sports (rugby 15s and 7s count as one sport), 8 seasons/tournaments running at once,
120 teams, 100 staff users (admins, managers, scorers), 10 GB of photos and files; live scoring (online and,
from Phase 8, offline); a public site at `<name>.<platform-domain>` with the tenant's logo and two colours and a
small "Powered by" footer; up to 5 sponsor logos on the site; importing people, teams and venues from
spreadsheet templates; entering past results by hand; CSV reports.

**PRO:** everything in STANDARD, plus unlimited sports, 60 seasons/tournaments at once, 2,000 teams,
1,000 staff users, 100 GB; a custom domain; full branding (brand versions, images, optional footer);
sponsorship campaigns and placements with impression reports; importing historical results and timelines;
transfers and loans; XLSX/PDF and scheduled reports; email and push alerts for the public; API access.

### Indicative prices (validate with the first customers)

| | STANDARD | PRO | Event pass | Enterprise |
|---|---|---|---|---|
| Uganda | UGX 150,000 per month, or UGX 1,500,000 per year | UGX 500,000 per month, or UGX 5,000,000 per year | UGX 200,000 per event | Quote (from about UGX 20 million per year) |
| Other countries | US$39 per month, or US$390 per year | US$129 per month, or US$1,290 per year | US$49 per event | Quote (from about US$5,000 per year) |

- **Annual = 10 × monthly** (two months free). Annual billing is the default offer; monthly exists for
  cash-flow reasons.
- **Why these numbers.** Hosting costs roughly US$125–300 per month in total — plan for about US$250
  (ADR-0016) — so 6–8 STANDARD customers cover it. The event pass sits next to Tournify's €40 per event. STANDARD per year costs about the
  same as eight event passes, which nudges regular organisers to subscribe.
- **Currency.** Price Ugandan customers in UGX and everyone else in USD. On 16 October 2026, US$1 ≈ UGX 4,130,
  and the shilling had fallen about 15% against the dollar over the previous year, while hosting is billed in
  USD. Review UGX prices every January.
- **Pilot offer.** The first three customers get six months free (a `COMPLIMENTARY` subscription period) in
  return for regular feedback and permission to publish a case study.
- **Trial.** 30 days on PRO (a `TRIAL` subscription period). When it ends, the normal expiry rules apply
  (docs/domain/state-machines.md): the public site stays online read-only and nothing is deleted.

### Upgrades, downgrades and lapses

- Upgrades take effect immediately; downgrades at the next renewal.
- After a downgrade, existing data stays. Quotas only block **new** items (for example a 4th sport or the
  121st team); nothing is hidden or deleted.
- Expiry: `EXPIRING` and `GRACE` show a banner; after that the tenant is `READ_ONLY` until renewal (live contests
  may finish, exports still work).

## Why there is no free plan at launch

- The real cost of a tenant is onboarding and support (training volunteers and scorers), not hosting. Free
  tenants would take support time without paying for it.
- Competitors' free tiers are paid for by advertising at very large scale or by fans. We have neither yet.
- Plans are data. A `FREE` plan (for example 1 sport, 1 active edition, 16 teams, platform branding) can be added
  without code once self-service onboarding exists (Phase 7). Until then, use trials, event passes and pilot
  offers.

## Collecting payment in v1

Manual: an invoice, then bank transfer or mobile money (MTN MoMo, Airtel Money) to the company's merchant
account. The platform admin records the subscription period. Automated billing needs its own ADR later.

## What to validate with the first customers

1. The price points — talk to about five organisations: two associations, a schools council, a club and a
   tournament organiser.
2. Whether "3 sports" is the right STANDARD limit. The pilot persona (docs/product/pilot-customer.md) uses
   exactly three, so a fourth sport is the natural reason to upgrade.
3. Whether sponsor logos on a STANDARD site bring in sponsor money that pays for the subscription.
4. Demand for event passes (schools, corporate tournaments).

## Sources

- [Tournify pricing](https://www.tournifyapp.com/en/pricing)
- [CricClubs plans (FitGap summary)](https://us.fitgap.com/products/032790/cricclubs)
- [LeagueRepublic review (Subscribed.fyi)](https://subscribed.fyi/leaguerepublic/reviews/)
- [GameChanger for teams and families](https://gc.com/home/all-other-sports)
- [Pitchero on Capterra](https://www.capterra.com/p/148390/Pitchero/)
- [USD/UGX rate history (Pound Sterling Live)](https://www.poundsterlinglive.com/data/currencies/usd-pairs/USDUGX-exchange-rate)

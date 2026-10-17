# Design system

One visual language across admin web, public web and (later) Flutter. Tokens live in `web/packages/tokens`
(CSS variables + a TypeScript export) and are mirrored into Flutter themes in Phase 8. Figma community files the
owner collected are **references only** — the product UI must be original.

## Tokens

| Group | Contents |
|---|---|
| Colour — brand | `brand.primary`, `brand.primary-contrast`, `brand.secondary` — **tenant-overridable** (Branding module) |
| Colour — neutral & surfaces | background, surface, surface-raised, border, text, text-muted (light + dark) |
| Colour — semantic | `live`, `success`, `warning`, `danger`, `info` — **not tenant-overridable** (meaning must stay consistent and accessible) |
| Typography | System font stack by default; tenant may choose from an allowed list of web fonts; scale: 12/14/16/18/20/24/30/36 |
| Spacing | 4 px base scale (4, 8, 12, 16, 20, 24, 32, 40, 48, 64) |
| Radius | 4, 8, 12, full |
| Elevation | 0–3 (shadows tuned for both themes) |
| Motion | 150/250 ms standard easing; respect `prefers-reduced-motion` |
| Breakpoints | phone < 768, tablet 768–1199, desktop ≥ 1200 |

## Contest status presentation

| Status / state | Treatment (always icon/label + colour, never colour alone) |
|---|---|
| Live | `live` colour, pulsing dot (static when reduced motion), phase + clock |
| Break (half-time, innings break) | `live` outline, phase label |
| Starting soon | `info` label with countdown |
| Not started | neutral, local time |
| Delayed / Interrupted / Suspended | `warning` label + reason where public |
| Postponed / Cancelled / Abandoned | `danger` label; struck time for postponed with "new date TBC" or new date |
| Ended (awaiting result) | neutral "Ended" |
| Final | muted "FT"/"Final" (terminology from definition) |
| Corrected result | small `info` note "Corrected" with date |

## Branding rules (Branding module, S22)

- Tenant colours map only to `brand.*` tokens. Publishing a brand version runs contrast checks (WCAG AA:
  4.5:1 for text, 3:1 for large text/UI) against both themes; failures block publish with suggestions.
- Logos: square and wide variants; generated fallbacks (initials on brand colour) when missing.
- No arbitrary CSS injection.

## Core components (`web/packages/ui`)

AppShell · NavRail · BottomNav · TenantSwitcher · DateStrip · FilterChips · ContestCard (renderer host) ·
ScoreSummary · StatusBadge · PhaseClock · Timeline · LineupView · StandingsTable · Bracket (later) ·
DataTable · FormField set · EntityPicker (search-first, shows duplicates) · DuplicateReview · Stepper ·
NextActionsChecklist · ReasonDialog · EmptyState · ErrorState (shows error code + correlation ID) · Skeleton ·
AuditDrawer · ConnectionBadge · ScorerPad (buttons) · DeliveryPad (cricket).

## Accessibility

WCAG 2.2 AA target: keyboard reachable, visible focus, labelled controls, `aria-live="polite"` for score
updates, touch targets ≥ 44–48 px, text resizes to 200% without loss, dark mode contrast verified.

## Long names and missing data

Design for long team names (truncate with full name on hover/long-press), missing badges/photos (fallbacks),
TBD participants ("Winner QF1"), and empty states that explain what to do next.

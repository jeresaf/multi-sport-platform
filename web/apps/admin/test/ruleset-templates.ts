// Test-only ruleset templates (S05 D17): invented codes and words, so a test passes only if the screen shows what the
// template says. Each is one the backend's template rules (RulesetTemplateRules, RulesetValueChecks) would accept
// against the test-only Ringball definition (test/sport-definitions.ts, version 2) and the catalogue entries it carries
// (review #91 R2): counts are never negative, and every choice fits the template's extra periods. `ringLeague` uses
// the definition's labels: a position and a position group, extra periods, an official role, and a parameter of each
// template type. `quickBout` changes nothing, has no positions, officials or description, and the one tiebreaker the
// schema requires at least (review #91 R4).
import type { RulesetTemplate } from "@/lib/rulesets";

export const ringLeague: RulesetTemplate = {
  code: "RING_LEAGUE",
  version: 2,
  name: "Ring league",
  description: "Two spells of ten minutes; a level bout goes to a golden spell, then a ring-off.",
  schemaVersion: 1,
  contentHash: "sha256:1d2e3f4a5b6c7d8e9f0a1b2c3d4e5f6a7b8c9d0e1f2a3b4c5d6e7f8a9b0c1d2e",
  // 21:30 UTC on 1 Nov is 2 Nov in Kampala.
  publishedAt: "2026-11-01T21:30:00Z",
  sport: { variant: "RINGBALL_SEVENS", version: 2 },
  formats: ["LADDER_CLIMB", "SHOWDOWN"],
  parameters: {
    "standings.points.win": { type: "NUMBER", label: "Points for a bout won", description: null, min: 2, max: 4, values: [], default: 3 },
    "standings.tiebreakers": {
      type: "LIST",
      label: "Tiebreakers",
      description: "Applied in this order when crews are level on points.",
      min: null,
      max: null,
      values: ["POINTS", "RING_DIFFERENCE", "FAR_RINGS", "COIN_TOSS"],
      default: ["POINTS", "RING_DIFFERENCE", "COIN_TOSS"],
    },
    "contest.coverage_default": {
      type: "CHOICE",
      label: "Coverage of a bout",
      description: null,
      min: null,
      max: null,
      values: ["STANDARD", "DETAILED"],
      default: "DETAILED",
    },
    "standings.h2h_reapply": {
      type: "FLAG",
      label: "Head-to-head again among crews still level",
      description: null,
      min: null,
      max: null,
      values: [],
      default: false,
    },
    "contest.period_seconds.FIRST_SPELL": {
      type: "NUMBER",
      label: "First spell (seconds)",
      description: null,
      min: 300,
      max: 900,
      values: [],
      default: 600,
    },
  },
  values: {
    contest: {
      periodSeconds: { FIRST_SPELL: 600, INTERVAL: 90, SECOND_SPELL: 630 },
      tieResolution: "EXTRA_PERIODS_SUDDEN_DEATH",
      extraPeriodSequence: ["GOLDEN_SPELL", "RING_OFF"],
      coverageDefault: "DETAILED",
    },
    lineup: { starters: 7, benchMax: 5, requiresApproval: true, positions: { HOOP_GUARD: { min: 1, max: 1 }, RINGERS: { min: 2, max: 6 } } },
    substitutions: { max: 6, windows: 4, extraInExtraTime: 1 },
    roster: { min: 10, max: 14, requiresApproval: false, lockPolicy: "FIRST_CONTEST" },
    standings: {
      points: { win: 3, draw: 1, loss: 0, noResult: 1, forfeitWin: 2, forfeitLoss: 0 },
      forfeitScore: { scored: 5, conceded: 0 },
      tiebreakers: ["POINTS", "RING_DIFFERENCE", "COIN_TOSS"],
      headToHeadReapply: false,
    },
    operations: {
      readiness: ["LINEUPS_APPROVED", "OFFICIALS_MINIMUM"],
      officialsMinimum: { RING_JUDGE: 2 },
      scorerCorrectionWindowMinutes: 45,
      startingSoonMinutes: 20,
    },
    eligibility: {
      membershipRequired: false,
      minAge: 14,
      maxAge: 17,
      ageReferenceDate: "2027-01-01",
      minorConsentRequired: "TENANT_DEFAULT",
    },
  },
  tiebreakers: [
    { code: "POINTS", label: "Points", description: "More points ranks higher." },
    { code: "RING_DIFFERENCE", label: "Ring difference", description: "Rings scored minus rings conceded." },
    { code: "FAR_RINGS", label: "Far rings", description: "More rings from beyond the far line." },
    { code: "COIN_TOSS", label: "Toss of a coin", description: "Decided by a coin toss in front of both crews." },
  ],
};

export const quickBout: RulesetTemplate = {
  ...ringLeague,
  code: "QUICK_BOUT",
  version: 1,
  name: "Quick bout",
  description: null,
  formats: ["SHOWDOWN"],
  parameters: {},
  values: {
    ...ringLeague.values,
    contest: { periodSeconds: { FIRST_SPELL: 300 }, tieResolution: "DRAW", extraPeriodSequence: [], coverageDefault: "RESULT_ONLY" },
    lineup: { starters: 7, benchMax: 0, requiresApproval: false, positions: {} },
    standings: { ...ringLeague.values.standings, tiebreakers: ["POINTS"], headToHeadReapply: false },
    operations: { readiness: [], officialsMinimum: {}, scorerCorrectionWindowMinutes: 0, startingSoonMinutes: 10 },
    eligibility: { membershipRequired: true, minAge: null, maxAge: null, ageReferenceDate: null, minorConsentRequired: "TENANT_DEFAULT" },
  },
  tiebreakers: [ringLeague.tiebreakers[0]!],
};

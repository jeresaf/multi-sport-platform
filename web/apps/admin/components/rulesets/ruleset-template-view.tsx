"use client";

// A ruleset template, read-only (S05 D17): what it is, its sport and formats, the parameters a competition may change,
// every rule in sections, and the tiebreakers it lists or allows. It takes the template (and, when they could be read,
// the pinned sport definition and the formats' names) rather than reading them, so the tenant workspace and the
// platform console (T5b2) show the same view over their own reads.
import Link from "next/link";
import type { ReactNode } from "react";
import { Facts, NoneOr, ViewerSection } from "@/components/viewer/facts";
import { ParametersTable } from "@/components/viewer/parameters-table";
import { formatDay } from "@/lib/entitlements";
import { numberRange } from "@/lib/parameters";
import {
  ageLabel,
  officialLabel,
  periodLabel,
  positionLabel,
  rulesetValueLabel,
  type RulesetTemplate,
} from "@/lib/rulesets";
import { codeLabel, formatSeconds, type SportDefinition } from "@/lib/sport-definition";
import { formatDate } from "@/lib/tenant-settings";

const linkClass = "inline-flex min-h-11 items-center text-primary underline-offset-4 hover:underline";

const yesNo = (value: boolean) => (value ? "Yes" : "No");

const minorConsentLabels: Record<string, string> = { TENANT_DEFAULT: "As the organisation's settings say" };

export function RulesetTemplateView({
  template,
  definition,
  formatNames = {},
  sportHref,
  formatHref,
  timeZone,
  back,
}: Readonly<{
  template: RulesetTemplate;
  /** The pinned sport definition version, for its period, position and official labels; codes as words without it. */
  definition?: SportDefinition;
  /** Format names by code, when the formats could be read. */
  formatNames?: Readonly<Record<string, string>>;
  /** Where the sport's and a format's pages are; no links when left out. */
  sportHref?: string;
  formatHref?: (code: string) => string;
  /** Where the publication date is shown: the tenant's zone in a workspace. */
  timeZone: string;
  /** A link back to the list, above the title. */
  back?: ReactNode;
}>) {
  const { contest, lineup, substitutions, roster, standings, operations, eligibility } = template.values;
  const editable = new Set(Object.keys(template.parameters));
  // A rule a competition may change is the template's default for it (its parameter above says within what). Every
  // rule names its paths as the files write them (`values` keys), so any rule a template makes a parameter is marked.
  const rule = (value: ReactNode, ...paths: string[]) =>
    paths.some((path) => editable.has(path)) ? (
      <>
        {value} <span className="text-sm text-muted-foreground">(default; a competition may change it)</span>
      </>
    ) : (
      value
    );
  const period = (code: string) => periodLabel(definition, code);
  const sportName = `${definition?.name ?? codeLabel(template.sport.variant)}, version ${template.sport.version}`;
  const formatName = (code: string) => formatNames[code] ?? codeLabel(code);
  const periods = Object.entries(contest.periodSeconds);
  const positions = Object.entries(lineup.positions);
  const officials = Object.entries(operations.officialsMinimum);

  return (
    // `minmax(0, 1fr)`: a table would otherwise widen the page on a phone.
    <section className="grid grid-cols-[minmax(0,1fr)] gap-6">
      <header className="grid gap-2">
        {back}
        <h1 className="text-2xl font-semibold">{template.name}</h1>
        <p className="text-muted-foreground">
          Version {template.version} · published {formatDate(template.publishedAt, timeZone)}
        </p>
        <p className="text-sm text-muted-foreground">
          Read-only: a template changes only by publishing a new version. Content hash{" "}
          <code className="font-mono text-xs break-all">{template.contentHash}</code>
        </p>
      </header>

      <ViewerSection title="Overview" level={2}>
        <Facts
          items={[
            // An empty description is none, as in the list (which leaves it out).
            ["Description", template.description || "None"],
            ["Sport", sportHref ? <Link href={sportHref} className={linkClass}>{sportName}</Link> : sportName],
            [
              "Formats",
              <NoneOr key="formats" empty={template.formats.length === 0}>
                <ul aria-label="Formats" className="grid">
                  {template.formats.map((code) => (
                    <li key={code}>
                      {formatHref ? (
                        <Link href={formatHref(code)} className={linkClass}>
                          {formatName(code)}
                        </Link>
                      ) : (
                        formatName(code)
                      )}
                    </li>
                  ))}
                </ul>
              </NoneOr>,
            ],
          ]}
        />
      </ViewerSection>

      <ViewerSection title="Parameters" level={2}>
        <p className="text-muted-foreground">
          A competition may change these when it uses the template; every other rule stays as below.
        </p>
        <ParametersTable parameters={template.parameters} valueLabel={(code) => rulesetValueLabel(template, code)} />
      </ViewerSection>

      <section className="grid gap-6">
        <h2 className="text-xl font-semibold">Rules</h2>

        <ViewerSection title="Contest">
          <Facts
            items={[
              [
                "Period lengths",
                <NoneOr key="periods" empty={periods.length === 0}>
                  <ul aria-label="Period lengths" className="grid gap-1">
                    {periods.map(([code, seconds]) => (
                      <li key={code}>
                        {period(code)}: {rule(formatSeconds(seconds), `contest.period_seconds.${code}`)}
                      </li>
                    ))}
                  </ul>
                </NoneOr>,
              ],
              ["When the scores are level", rule(codeLabel(contest.tieResolution), "contest.tie_resolution")],
              [
                "Extra periods",
                rule(
                  contest.extraPeriodSequence.length > 0 ? contest.extraPeriodSequence.map(period).join(", then ") : "None",
                  "contest.extra_period_sequence",
                ),
              ],
              ["Default coverage", rule(codeLabel(contest.coverageDefault), "contest.coverage_default")],
            ]}
          />
        </ViewerSection>

        <ViewerSection title="Lineup">
          <Facts
            items={[
              ["Starters", rule(lineup.starters, "lineup.starters")],
              ["Substitutes on the bench", rule(`At most ${lineup.benchMax}`, "lineup.bench_max")],
              ["Lineups need approval", rule(yesNo(lineup.requiresApproval), "lineup.requires_approval")],
              [
                "Positions",
                <NoneOr key="positions" empty={positions.length === 0}>
                  <ul aria-label="Positions" className="grid gap-1">
                    {positions.map(([code, range]) => (
                      <li key={code}>
                        {positionLabel(definition, code)}:{" "}
                        {rule(numberRange(range.min, range.max), `lineup.positions.${code}.min`, `lineup.positions.${code}.max`)}
                      </li>
                    ))}
                  </ul>
                </NoneOr>,
              ],
            ]}
          />
        </ViewerSection>

        <ViewerSection title="Substitutions">
          <Facts
            items={[
              ["Substitutions", rule(`At most ${substitutions.max}`, "substitutions.max")],
              ["Substitution windows", rule(substitutions.windows, "substitutions.windows")],
              ["Extra in extra periods", rule(substitutions.extraInExtraTime, "substitutions.extra_in_extra_time")],
            ]}
          />
        </ViewerSection>

        <ViewerSection title="Roster">
          <Facts
            items={[
              ["Roster size", rule(numberRange(roster.min, roster.max), "roster.min", "roster.max")],
              ["Rosters need approval", rule(yesNo(roster.requiresApproval), "roster.requires_approval")],
              ["Roster locks", rule(codeLabel(roster.lockPolicy), "roster.lock_policy")],
            ]}
          />
        </ViewerSection>

        <ViewerSection title="Standings">
          <Facts
            items={[
              ["Points for a win", rule(standings.points.win, "standings.points.win")],
              ["Points for a draw", rule(standings.points.draw, "standings.points.draw")],
              ["Points for a loss", rule(standings.points.loss, "standings.points.loss")],
              ["Points for no result", rule(standings.points.noResult, "standings.points.no_result")],
              ["Points for a forfeit win", rule(standings.points.forfeitWin, "standings.points.forfeit_win")],
              ["Points for a forfeit loss", rule(standings.points.forfeitLoss, "standings.points.forfeit_loss")],
              [
                "Score of a forfeit",
                rule(
                  `${standings.forfeitScore.scored}–${standings.forfeitScore.conceded}`,
                  "standings.forfeit_score.for",
                  "standings.forfeit_score.against",
                ),
              ],
              [
                "Tiebreakers",
                rule(
                  <NoneOr key="tiebreakers" empty={standings.tiebreakers.length === 0}>
                    <ol aria-label="Tiebreakers in order" className="grid list-decimal gap-1 ps-5">
                      {standings.tiebreakers.map((code) => (
                        <li key={code}>{rulesetValueLabel(template, code)}</li>
                      ))}
                    </ol>
                  </NoneOr>,
                  "standings.tiebreakers",
                ),
              ],
              [
                "Head-to-head applied again to those still level",
                rule(yesNo(standings.headToHeadReapply), "standings.h2h_reapply"),
              ],
            ]}
          />
        </ViewerSection>

        <ViewerSection title="Contest operations">
          <Facts
            items={[
              [
                "Ready to start when",
                rule(
                  <NoneOr key="readiness" empty={operations.readiness.length === 0}>
                    <ul aria-label="Readiness checks" className="grid gap-1">
                      {operations.readiness.map((check) => (
                        <li key={check}>{codeLabel(check)}</li>
                      ))}
                    </ul>
                  </NoneOr>,
                  "operations.readiness",
                ),
              ],
              [
                "Officials at least",
                <NoneOr key="officials" empty={officials.length === 0}>
                  <ul aria-label="Officials at least" className="grid gap-1">
                    {officials.map(([code, count]) => (
                      <li key={code}>
                        {officialLabel(definition, code)}: {rule(count, `operations.officials_minimum.${code}`)}
                      </li>
                    ))}
                  </ul>
                </NoneOr>,
              ],
              [
                "Scorer corrections",
                rule(
                  operations.scorerCorrectionWindowMinutes > 0
                    ? `Up to ${operations.scorerCorrectionWindowMinutes} min after the end`
                    : "Not after the end",
                  "operations.scorer_correction_window_minutes",
                ),
              ],
              ["Starting soon", rule(`${operations.startingSoonMinutes} min before the start`, "operations.starting_soon_minutes")],
            ]}
          />
        </ViewerSection>

        <ViewerSection title="Eligibility">
          <Facts
            items={[
              ["Membership required", rule(yesNo(eligibility.membershipRequired), "eligibility.membership_required")],
              ["Age", rule(ageLabel(eligibility.minAge, eligibility.maxAge), "eligibility.min_age", "eligibility.max_age")],
              [
                "Age worked out on",
                rule(
                  eligibility.ageReferenceDate ? formatDay(eligibility.ageReferenceDate) : "Not set",
                  "eligibility.age_reference_date",
                ),
              ],
              [
                "Consent for minors",
                rule(
                  minorConsentLabels[eligibility.minorConsentRequired] ?? codeLabel(eligibility.minorConsentRequired),
                  "eligibility.minor_consent_required",
                ),
              ],
            ]}
          />
        </ViewerSection>
      </section>

      <ViewerSection title="Tiebreakers" level={2}>
        <p className="text-muted-foreground">Every tiebreaker the template lists or allows.</p>
        <NoneOr empty={template.tiebreakers.length === 0}>
          <Facts items={template.tiebreakers.map((tiebreaker) => [tiebreaker.label, tiebreaker.description] as const)} />
        </NoneOr>
      </ViewerSection>
    </section>
  );
}

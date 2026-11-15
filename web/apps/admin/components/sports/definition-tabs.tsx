// The variant viewer's tabs other than Events and the scorer pad (S05 D17). Everything shown is the definition's own
// data; the words around it are generic.
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@msp/ui/components/table";
import {
  aggregationLabel,
  clockLabel,
  codeLabel,
  derivationLabel,
  formatSeconds,
  poolLabel,
  sideLabel,
  type SportDefinition,
} from "@/lib/sport-definition";
import { Facts, orNone, ViewerSection } from "./facts";

type Props = Readonly<{ definition: SportDefinition }>;

const yesNo = (value: boolean) => (value ? "Yes" : "No");

/** "1", "3–5", "at least 1", "at most 2", "any number". */
export function boundsLabel(min: number | null, max: number | null): string {
  if (min !== null && max !== null) return min === max ? `${min}` : `${min}–${max}`;
  if (min !== null) return `at least ${min}`;
  if (max !== null) return `at most ${max}`;
  return "any number";
}

export function OverviewTab({ definition }: Props) {
  const { terminology, lineup } = definition;
  return (
    <div className="grid gap-8">
      <ViewerSection title="Terminology">
        <Facts
          items={[
            ["Contest", `${terminology.contest} (${terminology.contestPlural})`],
            ["Period", terminology.period],
            ["Participant", terminology.participant],
            ["Athlete", terminology.athlete],
            ["Score unit", terminology.scoreUnit],
          ]}
        />
      </ViewerSection>
      <ViewerSection title="Contest">
        <Facts
          items={[
            ["Shape", codeLabel(definition.shape)],
            ["Scoring family", codeLabel(definition.scoringFamily)],
            ["Sides", orNone(definition.sides.map(codeLabel))],
            ["Participant types", orNone(definition.participantTypes.map(codeLabel))],
            ["Coverage levels", orNone(definition.coverageLevels.map(codeLabel))],
            ["Tie resolutions", orNone(definition.tieResolutions.map(codeLabel))],
          ]}
        />
      </ViewerSection>
      <ViewerSection title="Lineup">
        <Facts
          items={[
            ["Starters", lineup.starters],
            ["Bench (most)", lineup.benchMax],
            ["Fewest to continue", lineup.minimumPlayersToContinue ?? "Not set"],
            [
              "Position limits",
              lineup.positionConstraints.length > 0
                ? lineup.positionConstraints
                    .map(({ group, min, max }) => `${codeLabel(group)}: ${boundsLabel(min, max)}`)
                    .join(", ")
                : "None",
            ],
            ["Special roles", orNone(lineup.specialRoles.map(codeLabel))],
            ["Shirt numbers follow positions", yesNo(lineup.shirtNumbersFollowPositions)],
          ]}
        />
      </ViewerSection>
    </div>
  );
}

export function PeriodsTab({ definition }: Props) {
  return (
    <Table>
      <TableHeader>
        <TableRow>
          <TableHead scope="col">Period</TableHead>
          <TableHead scope="col">Short</TableHead>
          <TableHead scope="col">Kind</TableHead>
          <TableHead scope="col">Clock</TableHead>
          <TableHead scope="col">Length</TableHead>
          <TableHead scope="col">Notes</TableHead>
        </TableRow>
      </TableHeader>
      <TableBody>
        {definition.periods.map((period) => {
          const notes = [
            period.allowsAddedTime ? "Added time" : null,
            period.overtime ? "Overtime" : null,
            period.repeatable ? "Repeatable" : null,
            period.suddenDeath ? "Sudden death" : null,
            period.clockStartsAtSeconds ? `Clock starts at ${formatSeconds(period.clockStartsAtSeconds)}` : null,
          ].filter((note) => note !== null);
          return (
            <TableRow key={period.code}>
              <TableHead scope="row" className="font-medium">
                {period.label}
              </TableHead>
              <TableCell>{period.shortLabel}</TableCell>
              <TableCell>{codeLabel(period.kind)}</TableCell>
              <TableCell>{clockLabel(period.clock)}</TableCell>
              <TableCell>{period.defaultSeconds ? formatSeconds(period.defaultSeconds) : "—"}</TableCell>
              <TableCell className="whitespace-normal">{notes.length > 0 ? notes.join(", ") : "—"}</TableCell>
            </TableRow>
          );
        })}
      </TableBody>
    </Table>
  );
}

export function PositionsTab({ definition }: Props) {
  const groups = [...new Set(definition.positions.map((position) => position.group))];
  return (
    <div className="grid gap-8">
      <ViewerSection title="Positions">
        {groups.length === 0 ? <p className="text-muted-foreground">None</p> : null}
        <Facts
          items={groups.map((group) => [
            codeLabel(group),
            definition.positions
              .filter((position) => position.group === group)
              .map((position) => position.label)
              .join(", "),
          ])}
        />
      </ViewerSection>
      <ViewerSection title="Roles in events">
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead scope="col">Role</TableHead>
              <TableHead scope="col">Who</TableHead>
              <TableHead scope="col">Side</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {Object.entries(definition.actorRoles).map(([code, role]) => (
              <TableRow key={code}>
                <TableHead scope="row" className="font-medium">
                  {codeLabel(code)}
                </TableHead>
                <TableCell>{poolLabel(role.who)}</TableCell>
                <TableCell>{sideLabel(role.side)}</TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </ViewerSection>
      <ViewerSection title="Staff roles">
        <p>{orNone(definition.staffRoles.map((role) => role.label))}</p>
      </ViewerSection>
      <ViewerSection title="Officials">
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead scope="col">Official</TableHead>
              <TableHead scope="col">Minimum by default</TableHead>
              <TableHead scope="col">Most</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {definition.officialRoles.map((role) => (
              <TableRow key={role.code}>
                <TableHead scope="row" className="font-medium">
                  {role.label}
                </TableHead>
                <TableCell>{role.defaultMinimum}</TableCell>
                <TableCell>{role.max ?? "No limit"}</TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </ViewerSection>
    </div>
  );
}

export function StatisticsTab({ definition }: Props) {
  return (
    <Table>
      <TableHeader>
        <TableRow>
          <TableHead scope="col">Statistic</TableHead>
          <TableHead scope="col">How it&apos;s worked out</TableHead>
          <TableHead scope="col">Kept for</TableHead>
          <TableHead scope="col">Aggregation</TableHead>
          <TableHead scope="col">Better</TableHead>
          <TableHead scope="col">Coverage</TableHead>
        </TableRow>
      </TableHeader>
      <TableBody>
        {definition.statistics.map((statistic) => (
          <TableRow key={statistic.code}>
            <TableHead scope="row" className="font-medium">
              {statistic.label}
            </TableHead>
            <TableCell className="min-w-56 whitespace-normal">{derivationLabel(definition, statistic)}</TableCell>
            <TableCell>{statistic.scopes.map(codeLabel).join(", ")}</TableCell>
            <TableCell>{aggregationLabel(statistic.aggregation)}</TableCell>
            <TableCell>{statistic.higherIsBetter ? "Higher" : "Lower"}</TableCell>
            <TableCell>{codeLabel(statistic.coverage)}</TableCell>
          </TableRow>
        ))}
      </TableBody>
    </Table>
  );
}

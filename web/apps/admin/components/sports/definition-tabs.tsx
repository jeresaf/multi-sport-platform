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
import { Facts, NoneOr, orNone, RowName, ViewerSection, wide } from "./facts";

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
        <p className="text-sm text-muted-foreground">Defaults: a competition&apos;s rules can change the numbers.</p>
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
    <NoneOr empty={definition.periods.length === 0}>
      <Table>
        <TableHeader>
          <TableRow>
            <TableHead scope="col">Period</TableHead>
            <TableHead scope="col" className={wide}>
              Short
            </TableHead>
            <TableHead scope="col" className={wide}>
              Kind
            </TableHead>
            <TableHead scope="col" className={wide}>
              Clock
            </TableHead>
            <TableHead scope="col">Default length</TableHead>
            <TableHead scope="col" className={wide}>
              Notes
            </TableHead>
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
            const noteText = notes.length > 0 ? notes.join(", ") : "—";
            return (
              <TableRow key={period.code}>
                <TableHead scope="row" className="h-auto py-2 align-top whitespace-normal">
                  <RowName
                    name={period.label}
                    details={[
                      ["Short", period.shortLabel],
                      ["Kind", codeLabel(period.kind)],
                      ["Clock", clockLabel(period.clock)],
                      ...(notes.length > 0 ? [["Notes", noteText] as const] : []),
                    ]}
                  />
                </TableHead>
                <TableCell className={wide}>{period.shortLabel}</TableCell>
                <TableCell className={wide}>{codeLabel(period.kind)}</TableCell>
                <TableCell className={wide}>{clockLabel(period.clock)}</TableCell>
                <TableCell className="align-top">{period.defaultSeconds ? formatSeconds(period.defaultSeconds) : "—"}</TableCell>
                <TableCell className={`${wide} whitespace-normal`}>{noteText}</TableCell>
              </TableRow>
            );
          })}
        </TableBody>
      </Table>
    </NoneOr>
  );
}

export function PositionsTab({ definition }: Props) {
  const groups = [...new Set(definition.positions.map((position) => position.group))];
  const roles = Object.entries(definition.actorRoles);
  return (
    <div className="grid gap-8">
      <ViewerSection title="Positions">
        <NoneOr empty={groups.length === 0}>
          <Facts
            items={groups.map((group) => [
              codeLabel(group),
              definition.positions
                .filter((position) => position.group === group)
                .map((position) => position.label)
                .join(", "),
            ])}
          />
        </NoneOr>
      </ViewerSection>
      <ViewerSection title="Roles in events">
        <NoneOr empty={roles.length === 0}>
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead scope="col">Role</TableHead>
                <TableHead scope="col">Who</TableHead>
                <TableHead scope="col">Side</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {roles.map(([code, role]) => (
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
        </NoneOr>
      </ViewerSection>
      <ViewerSection title="Staff roles">
        <p>{orNone(definition.staffRoles.map((role) => role.label))}</p>
      </ViewerSection>
      <ViewerSection title="Officials">
        <NoneOr empty={definition.officialRoles.length === 0}>
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
        </NoneOr>
      </ViewerSection>
    </div>
  );
}

export function StatisticsTab({ definition }: Props) {
  return (
    <NoneOr empty={definition.statistics.length === 0}>
      <Table>
        <TableHeader>
          <TableRow>
            <TableHead scope="col">Statistic</TableHead>
            <TableHead scope="col">How it&apos;s worked out</TableHead>
            <TableHead scope="col" className={wide}>
              Kept for
            </TableHead>
            <TableHead scope="col" className={wide}>
              Aggregation
            </TableHead>
            <TableHead scope="col" className={wide}>
              Better
            </TableHead>
            <TableHead scope="col" className={wide}>
              Coverage
            </TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          {definition.statistics.map((statistic) => {
            const keptFor = statistic.scopes.map(codeLabel).join(", ");
            const better = statistic.higherIsBetter ? "Higher" : "Lower";
            return (
              <TableRow key={statistic.code}>
                <TableHead scope="row" className="h-auto py-2 align-top whitespace-normal">
                  <RowName
                    name={statistic.label}
                    details={[
                      ["Kept for", keptFor],
                      ["Aggregation", aggregationLabel(statistic.aggregation)],
                      ["Better", better],
                      ["Coverage", codeLabel(statistic.coverage)],
                    ]}
                  />
                </TableHead>
                <TableCell className="align-top whitespace-normal tablet:min-w-56">{derivationLabel(definition, statistic)}</TableCell>
                <TableCell className={wide}>{keptFor}</TableCell>
                <TableCell className={wide}>{aggregationLabel(statistic.aggregation)}</TableCell>
                <TableCell className={wide}>{better}</TableCell>
                <TableCell className={wide}>{codeLabel(statistic.coverage)}</TableCell>
              </TableRow>
            );
          })}
        </TableBody>
      </Table>
    </NoneOr>
  );
}

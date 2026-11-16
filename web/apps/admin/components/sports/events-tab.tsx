// The variant viewer's Events tab (S05 D17): every event the definition declares, with who takes part, when it may be
// recorded and what it does, then the events every sport shares. Labels are the definition's.
import { Card, CardContent, CardHeader, CardTitle } from "@msp/ui/components/card";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@msp/ui/components/table";
import { codeLabel, eventLabel, listLabel, phaseLabel, type SportDefinition, type SportEventType } from "@/lib/sport-definition";
import { Facts, ViewerSection } from "@/components/viewer/facts";

type Props = Readonly<{ definition: SportDefinition }>;

function actorsLabel(event: SportEventType): string {
  if (event.actors.length === 0) return "None";
  return event.actors
    .map((actor) => {
      const notes = [
        actor.optional
          ? "optional"
          : actor.requiredFrom
            ? `required from ${codeLabel(actor.requiredFrom).toLowerCase()} coverage`
            : "required",
        actor.distinctFrom ? `not the ${codeLabel(actor.distinctFrom).toLowerCase()}` : null,
      ].filter((note) => note !== null);
      return notes.length > 0 ? `${codeLabel(actor.role)} (${notes.join(", ")})` : codeLabel(actor.role);
    })
    .join("; ");
}

/**
 * Every period whose kind the event lists, in the definition's order: "First half, Half-time and Second half" (the
 * schema requires at least one kind; a kind without periods is written as words).
 */
function allowedDuring(definition: SportDefinition, event: SportEventType): string {
  const periods = definition.periods.filter((period) => event.phases.includes(period.kind)).map((period) => period.label);
  const missing = event.phases.filter((kind) => !definition.periods.some((period) => period.kind === kind));
  return listLabel([...periods, ...missing.map((kind) => phaseLabel(definition, kind))], "and");
}

/** A competition rule's path as words: "substitutions.max" → "Substitutions: max". */
function ruleLabel(path: string): string {
  const [first = "", ...rest] = path.split(".");
  return [codeLabel(first), ...rest.map((part) => codeLabel(part).toLowerCase())].join(": ");
}

function followUpLabel(definition: SportDefinition, event: SportEventType): string | null {
  const followUp = event.followUp;
  if (!followUp) return null;
  const conditions = [
    followUp.sameSide ? "for the same side" : null,
    followUp.samePeriod ? "in the same period" : null,
    followUp.unconsumed ? "not already followed up" : null,
  ].filter((condition) => condition !== null);
  const preceding = listLabel(followUp.requiresPreceding.map((code) => eventLabel(definition, code)));
  return `Needs an earlier ${preceding}${conditions.length > 0 ? ` ${conditions.join(", ")}` : ""}${followUp.consumes ? ", which it uses up" : ""}`;
}

function metadataLabel(event: SportEventType): string | null {
  const fields = Object.entries(event.metadata);
  if (fields.length === 0) return null;
  return fields
    .map(([code, field]) => {
      const detail =
        field.type === "CHOICE"
          ? `one of ${field.values.map(codeLabel).join(", ")}`
          : field.type === "NUMBER" && (field.min !== null || field.max !== null)
            ? `a number ${field.min ?? "…"}–${field.max ?? "…"}`
            : field.type === "FLAG"
              ? "yes or no"
              : field.type === "NUMBER"
                ? "a number"
                : "text";
      return `${codeLabel(code)}: ${detail}${field.optional ? " (optional)" : ""}`;
    })
    .join("; ");
}

function EventCard({ definition, event }: Props & Readonly<{ event: SportEventType }>) {
  const points =
    event.points === null
      ? null
      : event.points === 0
        ? "No points"
        : `${event.points} to the ${event.side === "OPPONENT" ? "opponent" : "event's side"}${
          event.component && event.component !== "main" ? ` (${codeLabel(event.component).toLowerCase()} score)` : ""
        }`;
  const facts: Array<readonly [string, string]> = [
    ["Category", codeLabel(event.category)],
    ["Importance", codeLabel(event.importance)],
    ["Recorded from", `${codeLabel(event.coverage)} coverage`],
    ["Allowed during", allowedDuring(definition, event)],
    ["Who", actorsLabel(event)],
  ];
  const optional: Array<readonly [string, string | null]> = [
    ["Points", points],
    ["Details", metadataLabel(event)],
    ["Effects", event.effects.length > 0 ? event.effects.map(codeLabel).join(", ") : null],
    ["Follow-up", followUpLabel(definition, event)],
    ["Limited by", event.limits.length > 0 ? event.limits.map(ruleLabel).join(", ") : null],
    ["Reversed by", event.reversibleBy.length > 0 ? listLabel(event.reversibleBy.map((code) => eventLabel(definition, code))) : null],
  ];
  return (
    <Card>
      <CardHeader>
        <CardTitle>
          <h3 className="font-semibold">{event.label}</h3>
        </CardTitle>
      </CardHeader>
      <CardContent>
        <Facts items={[...facts, ...optional.filter((item): item is readonly [string, string] => item[1] !== null)]} />
      </CardContent>
    </Card>
  );
}

export function EventsTab({ definition }: Props) {
  return (
    <div className="grid gap-8">
      <p className="text-sm text-muted-foreground">Points are defaults: a competition&apos;s rules can change them.</p>
      <div className="grid gap-4 desktop:grid-cols-2">
        {definition.eventTypes.map((event) => (
          <EventCard key={event.code} definition={definition} event={event} />
        ))}
      </div>
      <ViewerSection title="Events every sport has">
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead scope="col">Event</TableHead>
              <TableHead scope="col">Category</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {definition.genericEventTypes.map((event) => (
              <TableRow key={event.code}>
                <TableHead scope="row" className="font-medium">
                  {event.label}
                </TableHead>
                <TableCell>{codeLabel(event.category)}</TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </ViewerSection>
    </div>
  );
}

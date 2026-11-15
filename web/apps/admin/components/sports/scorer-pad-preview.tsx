import { useId } from "react";
import { codeLabel, eventLabel, listLabel, phaseLabel, type PhaseKind, type SportDefinition } from "@/lib/sport-definition";

/** The periods of the kinds a group shows in, in the definition's order: "First half or Second half". */
function shownDuring(definition: SportDefinition, phases: readonly PhaseKind[]): string {
  const periods = definition.periods.filter((period) => phases.includes(period.kind)).map((period) => period.label);
  const missing = phases.filter((kind) => !definition.periods.some((period) => period.kind === kind));
  return listLabel([...periods, ...missing.map((kind) => phaseLabel(definition, kind))]);
}

/**
 * The scorer pad as the definition lays it out (S05 D17, admin-app.md "Pad from metadata"): its groups in order, each
 * button labelled by its event. A preview, not a pad: nothing here records an event, so the buttons are shown as items,
 * not controls. A pad kind this preview can't draw says so instead of showing something made up (rule 13).
 */
export function ScorerPadPreview({ definition }: Readonly<{ definition: SportDefinition }>) {
  const pad = definition.scorerPad;
  const idPrefix = useId();

  if (pad.kind !== "BUTTONS") {
    return (
      <div className="grid gap-1 rounded-lg border border-dashed p-6 text-center">
        <p className="font-medium">Preview not available</p>
        <p className="text-sm text-muted-foreground">
          This sport&apos;s scorer pad ({codeLabel(pad.kind).toLowerCase()}) can&apos;t be previewed yet.
        </p>
      </div>
    );
  }

  return (
    <div className="grid gap-6">
      <p className="text-sm text-muted-foreground">
        What a scorer sees during a {definition.terminology.contest.toLowerCase()}, built from this definition. A
        preview only: nothing here records an event.
      </p>
      {pad.groups.map((group, index) => {
        const headingId = `${idPrefix}-group-${index}`;
        const notes = [
          group.phases.length > 0 ? `Only during ${shownDuring(definition, group.phases)}` : null,
          group.coverage ? `From ${codeLabel(group.coverage).toLowerCase()} coverage` : null,
        ].filter((note) => note !== null);
        return (
          <section key={headingId} aria-labelledby={headingId} className="grid gap-2">
            <div className="flex flex-wrap items-baseline gap-x-3 gap-y-1">
              <h3 id={headingId} className="font-semibold">
                {group.label}
              </h3>
              {notes.map((note) => (
                <p key={note} className="text-sm text-muted-foreground">
                  {note}
                </p>
              ))}
            </div>
            <ul className="grid grid-cols-2 gap-3 tablet:grid-cols-3 desktop:grid-cols-4">
              {group.buttons.map((button) => (
                <li
                  key={button}
                  className="flex min-h-14 items-center justify-center rounded-md border bg-card px-3 py-2 text-center font-medium shadow-sm"
                >
                  {eventLabel(definition, button)}
                </li>
              ))}
            </ul>
          </section>
        );
      })}
    </div>
  );
}

"use client";

// A format, read-only (S05 D17): what it is, how many participants it takes, its stages and the parameters a
// competition chooses. It takes the format rather than reading it, so the tenant workspace and the platform console
// (T5b2) show the same view over their own reads.
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@msp/ui/components/table";
import type { components } from "@msp/api-client";
import type { ReactNode } from "react";
import { Facts, NoneOr, RowName, ViewerSection, orNone, wide } from "@/components/viewer/facts";
import { numberRange, parameterAllowed, parameterTypeLabel, parameterValueLabel } from "@/lib/parameters";
import { codeLabel } from "@/lib/sport-definition";
import { formatDate } from "@/lib/tenant-settings";

export type FormatDefinition = components["schemas"]["FormatDefinitionResponse"];

/** "2 to 40 participants". */
export const participantsLabel = (format: FormatDefinition) =>
  `${numberRange(format.participants.min, format.participants.max)} participants`;

export const shapesLabel = (format: FormatDefinition) => orNone(format.compatibleShapes.map(codeLabel));

export function FormatView({
  format,
  timeZone,
  back,
}: Readonly<{
  format: FormatDefinition;
  /** Where the publication date is shown: the tenant's zone in a workspace. */
  timeZone: string;
  /** A link back to the list, above the title. */
  back?: ReactNode;
}>) {
  const parameters = Object.entries(format.parameters);
  return (
    // `minmax(0, 1fr)`: a table would otherwise widen the page on a phone.
    <section className="grid grid-cols-[minmax(0,1fr)] gap-6">
      <header className="grid gap-2">
        {back}
        <h1 className="text-2xl font-semibold">{format.name}</h1>
        <p className="text-muted-foreground">
          Version {format.version} · published {formatDate(format.publishedAt, timeZone)}
        </p>
        <p className="text-sm text-muted-foreground">
          Read-only: a format changes only by publishing a new version. Content hash{" "}
          <code className="font-mono text-xs break-all">{format.contentHash}</code>
        </p>
      </header>

      <ViewerSection title="Overview" level={2}>
        <Facts
          items={[
            // An empty description is none, as in the list (which leaves it out).
            ["Description", format.description || "None"],
            ["Participants", participantsLabel(format)],
            ["Contest shapes", shapesLabel(format)],
            [
              "Stages",
              <NoneOr key="stages" empty={format.stages.length === 0}>
                <ol aria-label="Stages" className="grid gap-1">
                  {format.stages.map((stage) => (
                    <li key={stage.code}>
                      {codeLabel(stage.code)}: {codeLabel(stage.type).toLowerCase()} stage
                      {/* A generator named as its stage type says nothing more ("knockout stage, drawn by the knockout generator"). */}
                      {stage.generator === stage.type ? null : `, drawn by the ${codeLabel(stage.generator).toLowerCase()} generator`}
                    </li>
                  ))}
                </ol>
              </NoneOr>,
            ],
          ]}
        />
      </ViewerSection>

      <ViewerSection title="Parameters" level={2}>
        <p className="text-muted-foreground">A competition chooses these when it uses the format.</p>
        <NoneOr empty={parameters.length === 0}>
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead scope="col">Parameter</TableHead>
                <TableHead scope="col" className={wide}>
                  Type
                </TableHead>
                <TableHead scope="col">Default</TableHead>
                <TableHead scope="col" className={wide}>
                  Allowed
                </TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {parameters.map(([code, parameter]) => (
                <TableRow key={code}>
                  <TableHead scope="row" className="h-auto py-2 align-top whitespace-normal">
                    <RowName
                      name={parameter.label}
                      details={[
                        ["Type", parameterTypeLabel(parameter.type)],
                        ["Allowed", parameterAllowed(parameter)],
                      ]}
                    />
                    {parameter.description ? (
                      <p className="mt-1 text-sm font-normal text-muted-foreground">{parameter.description}</p>
                    ) : null}
                  </TableHead>
                  <TableCell className={`${wide} align-top`}>{parameterTypeLabel(parameter.type)}</TableCell>
                  <TableCell className="align-top whitespace-normal">{parameterValueLabel(parameter.default)}</TableCell>
                  <TableCell className={`${wide} align-top whitespace-normal`}>{parameterAllowed(parameter)}</TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </NoneOr>
      </ViewerSection>
    </section>
  );
}

// The parameters a competition chooses or changes, as a table (S05 D17): label and description, type, default and
// what may be chosen. Shared by the format and ruleset template viewers; `valueLabel` words their choice and list
// values (a template labels its tiebreakers from the catalogue).
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@msp/ui/components/table";
import { NoneOr, RowName, wide } from "@/components/viewer/facts";
import { parameterAllowed, parameterTypeLabel, parameterValueLabel, type Parameter } from "@/lib/parameters";
import { codeLabel } from "@/lib/sport-definition";

export function ParametersTable({
  parameters,
  valueLabel = codeLabel,
}: Readonly<{ parameters: Readonly<Record<string, Parameter>>; valueLabel?: (code: string) => string }>) {
  const entries = Object.entries(parameters);
  return (
    <NoneOr empty={entries.length === 0}>
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
          {entries.map(([code, parameter]) => (
            <TableRow key={code}>
              <TableHead scope="row" className="h-auto py-2 align-top whitespace-normal">
                <RowName
                  name={parameter.label}
                  details={[
                    ["Type", parameterTypeLabel(parameter.type)],
                    ["Allowed", parameterAllowed(parameter, valueLabel)],
                  ]}
                />
                {parameter.description ? (
                  <p className="mt-1 text-sm font-normal text-muted-foreground">{parameter.description}</p>
                ) : null}
              </TableHead>
              <TableCell className={`${wide} align-top`}>{parameterTypeLabel(parameter.type)}</TableCell>
              <TableCell className="align-top whitespace-normal">{parameterValueLabel(parameter.default, valueLabel)}</TableCell>
              <TableCell className={`${wide} align-top whitespace-normal`}>{parameterAllowed(parameter, valueLabel)}</TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </NoneOr>
  );
}

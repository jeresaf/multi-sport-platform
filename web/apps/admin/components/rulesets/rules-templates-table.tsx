// The published ruleset templates as a table (S05 D17), each name opening the template. Shared by the tenant
// workspace's and the platform console's Rules templates pages, which read the lists themselves. The sport and format
// names come from their lists when the user may read them, else the codes are written as words.
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@msp/ui/components/table";
import Link from "next/link";
import { RowName, orNone, wide } from "@/components/viewer/facts";
import type { RulesetTemplate } from "@/lib/rulesets";
import { codeLabel } from "@/lib/sport-definition";

export function RulesTemplatesTable({
  items,
  sportNames,
  formatNames,
  href,
}: Readonly<{
  items: readonly RulesetTemplate[];
  /** Variant names by code, when the sports could be read. */
  sportNames: Readonly<Record<string, string>>;
  /** Format names by code, when the formats could be read. */
  formatNames: Readonly<Record<string, string>>;
  /** Where a template's page is. */
  href: (code: string) => string;
}>) {
  const sportOf = (variant: string) => sportNames[variant] ?? codeLabel(variant);
  const formatsOf = (codes: readonly string[]) => orNone(codes.map((code) => formatNames[code] ?? codeLabel(code)));
  return (
    <Table>
      <TableHeader>
        <TableRow>
          <TableHead scope="col">Name</TableHead>
          <TableHead scope="col" className={wide}>
            Sport
          </TableHead>
          <TableHead scope="col" className={wide}>
            Formats
          </TableHead>
          <TableHead scope="col" className={wide}>
            Latest version
          </TableHead>
        </TableRow>
      </TableHeader>
      <TableBody>
        {items.map((item) => (
          <TableRow key={item.code}>
            <TableHead scope="row" className="h-auto py-2 align-top whitespace-normal">
              <RowName
                name={
                  <Link
                    href={href(item.code)}
                    className="inline-flex min-h-11 items-center text-primary underline-offset-4 hover:underline"
                  >
                    {item.name}
                  </Link>
                }
                details={[
                  ["Sport", sportOf(item.sport.variant)],
                  ["Formats", formatsOf(item.formats)],
                  ["Latest version", item.version],
                ]}
              />
              {item.description ? (
                <p className="mt-1 text-sm font-normal text-muted-foreground">{item.description}</p>
              ) : null}
            </TableHead>
            <TableCell className={`${wide} align-top`}>{sportOf(item.sport.variant)}</TableCell>
            <TableCell className={`${wide} align-top whitespace-normal`}>{formatsOf(item.formats)}</TableCell>
            <TableCell className={`${wide} align-top`}>{item.version}</TableCell>
          </TableRow>
        ))}
      </TableBody>
    </Table>
  );
}

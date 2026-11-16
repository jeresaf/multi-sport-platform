// The published sport variants as a table (S05 D17), each name opening its definition. Shared by the tenant workspace's
// and the platform console's Sports pages, which read the list themselves.
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@msp/ui/components/table";
import Link from "next/link";
import { RowName, wide } from "@/components/viewer/facts";
import { codeLabel, type SportVariant } from "@/lib/sport-definition";

export function SportsTable({
  items,
  href,
}: Readonly<{
  items: readonly SportVariant[];
  /** Where a variant's page is. */
  href: (variant: string) => string;
}>) {
  return (
    <Table>
      <TableHeader>
        <TableRow>
          <TableHead scope="col">Name</TableHead>
          <TableHead scope="col">Sport</TableHead>
          <TableHead scope="col" className={wide}>
            Scoring family
          </TableHead>
          <TableHead scope="col" className={wide}>
            Shape
          </TableHead>
          <TableHead scope="col" className={wide}>
            Latest version
          </TableHead>
        </TableRow>
      </TableHeader>
      <TableBody>
        {items.map((item) => (
          <TableRow key={item.variant}>
            <TableHead scope="row" className="h-auto py-2 align-top whitespace-normal">
              <RowName
                name={
                  <Link
                    href={href(item.variant)}
                    className="inline-flex min-h-11 items-center text-primary underline-offset-4 hover:underline"
                  >
                    {item.name}
                  </Link>
                }
                details={[
                  ["Scoring family", codeLabel(item.scoringFamily)],
                  ["Shape", codeLabel(item.shape)],
                  ["Latest version", item.latestVersion],
                ]}
              />
            </TableHead>
            <TableCell className="align-top">{codeLabel(item.sport)}</TableCell>
            <TableCell className={wide}>{codeLabel(item.scoringFamily)}</TableCell>
            <TableCell className={wide}>{codeLabel(item.shape)}</TableCell>
            <TableCell className={wide}>{item.latestVersion}</TableCell>
          </TableRow>
        ))}
      </TableBody>
    </Table>
  );
}

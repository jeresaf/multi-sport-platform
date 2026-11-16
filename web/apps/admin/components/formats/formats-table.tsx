// The published formats as a table (S05 D17), each name opening the format. Shared by the tenant workspace's and the
// platform console's Formats pages, which read the list themselves.
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@msp/ui/components/table";
import Link from "next/link";
import { RowName, wide } from "@/components/viewer/facts";
import { participantsLabel, shapesLabel, type FormatDefinition } from "./format-view";

export function FormatsTable({
  items,
  href,
}: Readonly<{
  items: readonly FormatDefinition[];
  /** Where a format's page is. */
  href: (code: string) => string;
}>) {
  return (
    <Table>
      <TableHeader>
        <TableRow>
          <TableHead scope="col">Name</TableHead>
          <TableHead scope="col" className={wide}>
            Participants
          </TableHead>
          <TableHead scope="col" className={wide}>
            Contest shapes
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
                  ["Participants", participantsLabel(item)],
                  ["Contest shapes", shapesLabel(item)],
                  ["Latest version", item.version],
                ]}
              />
              {item.description ? (
                <p className="mt-1 text-sm font-normal text-muted-foreground">{item.description}</p>
              ) : null}
            </TableHead>
            <TableCell className={`${wide} align-top`}>{participantsLabel(item)}</TableCell>
            <TableCell className={`${wide} align-top`}>{shapesLabel(item)}</TableCell>
            <TableCell className={`${wide} align-top`}>{item.version}</TableCell>
          </TableRow>
        ))}
      </TableBody>
    </Table>
  );
}

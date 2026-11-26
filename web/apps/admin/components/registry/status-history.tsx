"use client";

// A registry record's status history (people, venues; organisations and teams in T6b, T6c): every move, newest first,
// with its time in the tenant's zone and its reason. On a phone the time and reason stack under the change.
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@msp/ui/components/table";
import { Loading } from "@/components/loading";
import { ProblemAlert } from "@/components/problem-alert";
import { RowName, wide } from "@/components/viewer/facts";
import { humanizeCode } from "@/lib/codes";
import { formatDateTime, useTenantTimeZone } from "@/lib/tenant-settings";

export type StatusChange = {
  id: string;
  fromStatus: string | null;
  toStatus: string;
  reason: string | null;
  occurredAt: string;
};

/** What a status-changes read gives: its items once read, or why not. */
type HistoryRead = {
  isPending: boolean;
  isError: boolean;
  isSuccess: boolean;
  error: unknown;
  data?: { items: readonly StatusChange[] } | undefined;
};

export function StatusHistory({ history }: Readonly<{ history: HistoryRead }>) {
  const timeZone = useTenantTimeZone();
  const changes = history.data?.items ?? [];
  return (
    <>
      {history.isPending ? <Loading /> : null}
      {history.isError ? <ProblemAlert error={history.error} /> : null}
      {history.isSuccess && changes.length === 0 ? <p className="text-muted-foreground">No changes recorded.</p> : null}
      {changes.length > 0 ? (
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Change</TableHead>
              <TableHead className={wide}>When</TableHead>
              <TableHead className={wide}>Reason</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {changes.map((change) => {
              const label = change.fromStatus
                ? `${humanizeCode(change.fromStatus)} → ${humanizeCode(change.toStatus)}`
                : `Recorded as ${humanizeCode(change.toStatus).toLowerCase()}`;
              const when = formatDateTime(change.occurredAt, timeZone);
              const reason = change.reason ?? "No reason given";
              return (
                <TableRow key={change.id}>
                  <TableHead scope="row" className="h-auto py-2 align-top whitespace-normal">
                    <RowName name={label} details={[["When", when], ["Reason", reason]]} />
                  </TableHead>
                  <TableCell className={wide}>{when}</TableCell>
                  <TableCell className={`${wide} max-w-96 break-words whitespace-normal`}>{reason}</TableCell>
                </TableRow>
              );
            })}
          </TableBody>
        </Table>
      ) : null}
    </>
  );
}

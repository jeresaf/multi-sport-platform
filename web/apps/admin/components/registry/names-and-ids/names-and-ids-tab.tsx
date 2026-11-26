"use client";

import { Button } from "@msp/ui/components/button";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@msp/ui/components/table";
import { Plus } from "lucide-react";
import { useRef, useState, type ReactNode } from "react";
import { UnavailableNote } from "@/components/unavailable-note";
import { NoneOr, RowName, ViewerSection, wide } from "@/components/viewer/facts";
import { formatDay } from "@/lib/entitlements";
import { todayIn, useTenantTimeZoneOnceRead } from "@/lib/tenant-settings";
import { useTenantWorkspace } from "@/lib/tenant-workspace";
import {
  AddAliasDialog,
  AddIdentifierDialog,
  EndIdentifierDialog,
  RemoveAliasDialog,
} from "./names-and-ids-dialogs";
import {
  aliasKindLabels,
  type NamedRecord,
  type NamesAndIdsApi,
  type NamesAndIdsConfig,
  type RecordAlias,
  type RecordIdentifier,
} from "./names-and-ids-data";

type OpenDialog =
  | { type: "add-identifier" }
  | { type: "end-identifier"; identifier: RecordIdentifier }
  | { type: "add-alias" }
  | { type: "remove-alias"; alias: RecordAlias };

const unavailableId = "names-unavailable";
const headings = { aliases: "aliases-heading", identifiers: "identifiers-heading" } as const;

/** "From 1 Jan 2026", "1 Jan 2026 to 30 Jun 2026 (ended)", "Current". */
export function validity(validFrom: string | null, validTo: string | null): string {
  if (validTo) {
    return `${validFrom ? `${formatDay(validFrom)} to ` : "Until "}${formatDay(validTo)} (ended)`;
  }
  return validFrom ? `From ${formatDay(validFrom)}` : "Current";
}

/**
 * Names & IDs of an organisation, a team or a venue (S07 D10, D23): the other names it is known by (removed ones are
 * not listed) and the identifiers other bodies gave it (ended ones included: a value is never given twice). With the
 * actions, an alias is added or removed and an identifier added or ended here; a record that takes no new one (archived,
 * closed…) still lets its aliases be removed and its identifiers ended (corrections, D10).
 */
export function NamesAndIdsTab<R extends NamedRecord>({
  record,
  config,
  api,
}: Readonly<{ record: R; config: NamesAndIdsConfig; api: NamesAndIdsApi<R> }>) {
  const { can, unavailable } = useTenantWorkspace();
  const zone = useTenantTimeZoneOnceRead();
  const today = zone.isLoading ? "" : todayIn(zone.timeZone);
  const [dialog, setDialog] = useState<OpenDialog | null>(null);
  const [announcement, setAnnouncement] = useState("");
  // The section to focus once the dialog has closed after a change (its heading).
  const focusAfter = useRef<string | null>(null);
  const { actions } = config;

  const offered = (action: string) => can(action) || unavailable(action) !== null;
  const disabled = (action: string) =>
    unavailable(action) !== null ? { disabled: true, "aria-describedby": unavailableId } : {};
  const refusal =
    Object.values(actions)
      .map((action) => unavailable(action))
      .find((reason) => reason !== null) ?? null;
  const closedNote = config.closedNote ? (
    <p className="text-sm text-muted-foreground">{config.closedNote}</p>
  ) : null;

  const saved = (message: string, heading: string) => {
    focusAfter.current = heading;
    setAnnouncement(message);
    setDialog(null);
  };
  const focusChanged = (event: Event) => {
    const id = focusAfter.current;
    focusAfter.current = null;
    const heading = id ? document.getElementById(id) : null;
    if (heading) {
      event.preventDefault();
      heading.focus();
    }
  };
  const close = (open: boolean) => !open && setDialog(null);
  const dialogProps = { record, config, api, onOpenChange: close, onCloseAutoFocus: focusChanged };

  /** An open identifier's End; one that starts after today can't be ended yet (the API refuses it, Q46). */
  const endButton = (identifier: RecordIdentifier): ReactNode => {
    if (identifier.validTo !== null || !offered(actions.endIdentifier)) {
      return null;
    }
    const notYet = today !== "" && identifier.validFrom !== null && identifier.validFrom > today;
    const readOnly = unavailable(actions.endIdentifier) !== null;
    // Both reasons are read when both apply (a read-only tenant, a start after today).
    const reasons = [readOnly ? unavailableId : null, notYet ? `starts-${identifier.id}` : null].filter(Boolean);
    return (
      <div className="grid gap-1">
        <Button
          variant="outline"
          aria-label={`End ${identifier.authority} ${identifier.value}`}
          onClick={() => setDialog({ type: "end-identifier", identifier })}
          disabled={reasons.length > 0}
          aria-describedby={reasons.length > 0 ? reasons.join(" ") : undefined}
        >
          End
        </Button>
        {notYet ? (
          <p id={`starts-${identifier.id}`} className="text-sm text-muted-foreground">
            Can be ended from {formatDay(identifier.validFrom!)}
          </p>
        ) : null}
      </div>
    );
  };

  const addButton = (action: string, label: string, open: OpenDialog): ReactNode =>
    offered(action) ? (
      closedNote ?? (
        <div className="flex flex-wrap gap-2">
          <Button variant="outline" onClick={() => setDialog(open)} {...disabled(action)}>
            <Plus aria-hidden="true" />
            {label}
          </Button>
        </div>
      )
    ) : null;

  const endsOffered = offered(actions.endIdentifier);
  const removesOffered = offered(actions.removeAlias);

  return (
    <>
      <p role="status" className="sr-only">
        {announcement}
      </p>
      {refusal ? <UnavailableNote id={unavailableId} reason={refusal} /> : null}

      <ViewerSection title="Aliases" headingId={headings.aliases}>
        {addButton(actions.addAlias, "Add alias", { type: "add-alias" })}
        <NoneOr empty={record.aliases.length === 0}>
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Name</TableHead>
                <TableHead className={wide}>Kind</TableHead>
                {removesOffered ? (
                  <TableHead>
                    <span className="sr-only">Actions</span>
                  </TableHead>
                ) : null}
              </TableRow>
            </TableHeader>
            <TableBody>
              {record.aliases.map((alias) => (
                <TableRow key={alias.id}>
                  <TableHead scope="row" className="h-auto py-2 align-top whitespace-normal">
                    <RowName
                      name={<span className="break-words">{alias.name}</span>}
                      details={[["Kind", aliasKindLabels[alias.kind]]]}
                    />
                  </TableHead>
                  <TableCell className={wide}>{aliasKindLabels[alias.kind]}</TableCell>
                  {removesOffered ? (
                    <TableCell className="align-top">
                      <Button
                        variant="outline"
                        aria-label={`Remove the alias ${alias.name}`}
                        onClick={() => setDialog({ type: "remove-alias", alias })}
                        {...disabled(actions.removeAlias)}
                      >
                        Remove
                      </Button>
                    </TableCell>
                  ) : null}
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </NoneOr>
      </ViewerSection>

      <ViewerSection title="External identifiers" headingId={headings.identifiers}>
        {addButton(actions.addIdentifier, "Add identifier", { type: "add-identifier" })}
        <NoneOr empty={record.externalIdentifiers.length === 0}>
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Identifier</TableHead>
                <TableHead className={wide}>Authority</TableHead>
                <TableHead className={wide}>Valid</TableHead>
                {endsOffered ? (
                  <TableHead>
                    <span className="sr-only">Actions</span>
                  </TableHead>
                ) : null}
              </TableRow>
            </TableHeader>
            <TableBody>
              {record.externalIdentifiers.map((identifier) => {
                const valid = validity(identifier.validFrom, identifier.validTo);
                return (
                  <TableRow key={identifier.id}>
                    <TableHead scope="row" className="h-auto py-2 align-top whitespace-normal">
                      <RowName
                        name={<span className="font-mono break-all">{identifier.value}</span>}
                        details={[
                          ["Authority", identifier.authority],
                          ["Valid", valid],
                        ]}
                      />
                    </TableHead>
                    <TableCell className={`${wide} break-all whitespace-normal`}>{identifier.authority}</TableCell>
                    <TableCell className={wide}>{valid}</TableCell>
                    {endsOffered ? (
                      <TableCell className="align-top whitespace-normal">{endButton(identifier)}</TableCell>
                    ) : null}
                  </TableRow>
                );
              })}
            </TableBody>
          </Table>
        </NoneOr>
      </ViewerSection>

      {dialog?.type === "add-identifier" ? (
        <AddIdentifierDialog {...dialogProps} onSaved={(message) => saved(message, headings.identifiers)} />
      ) : null}
      {dialog?.type === "end-identifier" ? (
        <EndIdentifierDialog
          {...dialogProps}
          identifier={dialog.identifier}
          onSaved={(message) => saved(message, headings.identifiers)}
        />
      ) : null}
      {dialog?.type === "add-alias" ? (
        <AddAliasDialog {...dialogProps} onSaved={(message) => saved(message, headings.aliases)} />
      ) : null}
      {dialog?.type === "remove-alias" ? (
        <RemoveAliasDialog
          {...dialogProps}
          alias={dialog.alias}
          onSaved={(message) => saved(message, headings.aliases)}
        />
      ) : null}
    </>
  );
}

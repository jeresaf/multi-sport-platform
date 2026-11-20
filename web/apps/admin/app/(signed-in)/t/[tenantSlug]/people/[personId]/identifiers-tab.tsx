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
import type { Person } from "../people-data";
import {
  AddAliasDialog,
  AddIdentifierDialog,
  aliasKindLabels,
  EndIdentifierDialog,
  RemoveAliasDialog,
} from "./identifier-dialogs";
import { identityActions } from "./identity-data";
import { isClosedPerson } from "./profile-data";

type Identifier = Person["externalIdentifiers"][number];
type Alias = Person["aliases"][number];
type OpenDialog =
  | { type: "add-identifier" }
  | { type: "end-identifier"; identifier: Identifier }
  | { type: "add-alias" }
  | { type: "remove-alias"; alias: Alias };

const unavailableId = "identifiers-unavailable";
const headings = { identifiers: "identifiers-heading", aliases: "aliases-heading" } as const;

/**
 * External IDs & aliases (S06 D10): the identifiers other bodies gave the person (ended ones included, an identifier
 * is never reused) and the other names they are known by (removed ones are not listed). With their actions, an
 * identifier is added or ended and an alias added or removed here (T7c-2a); an archived or deceased person takes no
 * new one, but their identifiers can still be ended and their aliases removed (corrections, D9).
 */
export function IdentifiersTab({ person }: Readonly<{ person: Person }>) {
  const { can, unavailable } = useTenantWorkspace();
  const zone = useTenantTimeZoneOnceRead();
  const today = zone.isLoading ? "" : todayIn(zone.timeZone);
  const [dialog, setDialog] = useState<OpenDialog | null>(null);
  const [announcement, setAnnouncement] = useState("");
  // The section to focus once the dialog has closed after a change (its heading).
  const focusAfter = useRef<string | null>(null);

  const offered = (action: string) => can(action) || unavailable(action) !== null;
  const disabled = (action: string) =>
    unavailable(action) !== null ? { disabled: true, "aria-describedby": unavailableId } : {};
  const refusal =
    Object.values(identityActions)
      .filter((action) => action !== identityActions.setPhoto && action !== identityActions.removePhoto)
      .map((action) => unavailable(action))
      .find((reason) => reason !== null) ?? null;
  const closed = isClosedPerson(person);
  const closedNote = closed ? (
    <p className="text-sm text-muted-foreground">
      {person.displayName} is {person.status === "DECEASED" ? "deceased" : "archived"}: no new one can be added.
    </p>
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

  /** An open identifier's End; one that starts after today can't be ended yet (the API refuses it, Q46). */
  const endButton = (identifier: Identifier): ReactNode => {
    if (identifier.validTo !== null || !offered(identityActions.endIdentifier)) {
      return null;
    }
    const notYet = today !== "" && identifier.validFrom !== null && identifier.validFrom > today;
    return (
      <div className="grid gap-1">
        <Button
          variant="outline"
          aria-label={`End ${identifier.authority} ${identifier.value}`}
          onClick={() => setDialog({ type: "end-identifier", identifier })}
          {...disabled(identityActions.endIdentifier)}
          {...(notYet ? { disabled: true, "aria-describedby": `starts-${identifier.id}` } : {})}
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

  const removeButton = (alias: Alias): ReactNode =>
    offered(identityActions.removeAlias) ? (
      <Button
        variant="outline"
        aria-label={`Remove the alias ${alias.name}`}
        onClick={() => setDialog({ type: "remove-alias", alias })}
        {...disabled(identityActions.removeAlias)}
      >
        Remove
      </Button>
    ) : null;

  const endsOffered = offered(identityActions.endIdentifier);
  const removesOffered = offered(identityActions.removeAlias);

  return (
    <>
      <p role="status" className="sr-only">
        {announcement}
      </p>
      {refusal ? <UnavailableNote id={unavailableId} reason={refusal} /> : null}

      <ViewerSection title="External identifiers" headingId={headings.identifiers}>
        {offered(identityActions.addIdentifier) ? (
          closed ? (
            closedNote
          ) : (
            <div className="flex flex-wrap gap-2">
              <Button
                variant="outline"
                onClick={() => setDialog({ type: "add-identifier" })}
                {...disabled(identityActions.addIdentifier)}
              >
                <Plus aria-hidden="true" />
                Add identifier
              </Button>
            </div>
          )
        ) : null}
        <NoneOr empty={person.externalIdentifiers.length === 0}>
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
              {person.externalIdentifiers.map((identifier) => {
                const valid = validity(identifier.validFrom, identifier.validTo);
                return (
                  <TableRow key={identifier.id}>
                    <TableHead scope="row" className="h-auto py-2 align-top whitespace-normal">
                      <RowName
                        name={<span className="font-mono break-all">{identifier.value}</span>}
                        details={[["Authority", identifier.authority], ["Valid", valid]]}
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

      <ViewerSection title="Aliases" headingId={headings.aliases}>
        {offered(identityActions.addAlias) ? (
          closed ? (
            closedNote
          ) : (
            <div className="flex flex-wrap gap-2">
              <Button
                variant="outline"
                onClick={() => setDialog({ type: "add-alias" })}
                {...disabled(identityActions.addAlias)}
              >
                <Plus aria-hidden="true" />
                Add alias
              </Button>
            </div>
          )
        ) : null}
        <NoneOr empty={person.aliases.length === 0}>
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
              {person.aliases.map((alias) => (
                <TableRow key={alias.id}>
                  <TableHead scope="row" className="h-auto py-2 align-top whitespace-normal">
                    <RowName
                      name={<span className="break-words">{alias.name}</span>}
                      details={[["Kind", aliasKindLabels[alias.kind]]]}
                    />
                  </TableHead>
                  <TableCell className={wide}>{aliasKindLabels[alias.kind]}</TableCell>
                  {removesOffered ? <TableCell className="align-top">{removeButton(alias)}</TableCell> : null}
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </NoneOr>
      </ViewerSection>

      {dialog?.type === "add-identifier" ? (
        <AddIdentifierDialog
          person={person}
          onOpenChange={close}
          onSaved={(message) => saved(message, headings.identifiers)}
          onCloseAutoFocus={focusChanged}
        />
      ) : null}
      {dialog?.type === "end-identifier" ? (
        <EndIdentifierDialog
          person={person}
          identifier={dialog.identifier}
          onOpenChange={close}
          onSaved={(message) => saved(message, headings.identifiers)}
          onCloseAutoFocus={focusChanged}
        />
      ) : null}
      {dialog?.type === "add-alias" ? (
        <AddAliasDialog
          person={person}
          onOpenChange={close}
          onSaved={(message) => saved(message, headings.aliases)}
          onCloseAutoFocus={focusChanged}
        />
      ) : null}
      {dialog?.type === "remove-alias" ? (
        <RemoveAliasDialog
          person={person}
          alias={dialog.alias}
          onOpenChange={close}
          onSaved={(message) => saved(message, headings.aliases)}
          onCloseAutoFocus={focusChanged}
        />
      ) : null}
    </>
  );
}

/** "From 1 Jan 2026", "1 Jan 2026 to 30 Jun 2026 (ended)", "Current". */
export function validity(validFrom: string | null, validTo: string | null): string {
  if (validTo) {
    return `${validFrom ? `${formatDay(validFrom)} to ` : "Until "}${formatDay(validTo)} (ended)`;
  }
  return validFrom ? `From ${formatDay(validFrom)}` : "Current";
}

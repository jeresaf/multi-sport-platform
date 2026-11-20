"use client";

import type { components } from "@msp/api-client";
import { Button } from "@msp/ui/components/button";
import { Download, FileUp, Plus } from "lucide-react";
import { useRef, useState, type ReactNode } from "react";
import { StatusBadge } from "@/components/status-badge";
import { UnavailableNote } from "@/components/unavailable-note";
import { Facts, ViewerSection } from "@/components/viewer/facts";
import { humanizeCode } from "@/lib/codes";
import { formatDay } from "@/lib/entitlements";
import { formatDateTime, useTenantTimeZone } from "@/lib/tenant-settings";
import { useTenantWorkspace } from "@/lib/tenant-workspace";
import type { Person, PersonPrivate } from "../people-data";
import { consentActions } from "./consent-data";
import { ConsentDialog } from "./consent-dialog";
import { EvidenceDialog } from "./evidence-dialog";
import { isClosedPerson } from "./profile-data";
import { WithdrawConsentDialog } from "./withdraw-consent-dialog";

type ConsentScope = components["schemas"]["ConsentScope"];
type Consent = Person["consents"][number];
type OpenDialog = { type: "record" } | { type: "evidence"; consent: Consent } | { type: "withdraw"; consent: Consent };

const unavailableId = "consent-unavailable";

export const scopeLabels: Readonly<Record<ConsentScope, string>> = {
  DATA_PROCESSING: "Data processing",
  PUBLIC_PROFILE: "Public profile",
  PHOTO: "Photo",
};

export const scopeList = (scopes: readonly ConsentScope[]) =>
  scopes.length > 0 ? scopes.map((scope) => scopeLabels[scope]).join(", ") : "None";

/** The Consent tab is shown for someone who isn't known to be an adult, or who has a consent on record (S06 T7). */
export function showsConsent(person: Person): boolean {
  return person.ageStatus !== "ADULT" || person.consents.length > 0;
}

/**
 * Consent: what the guardians have agreed to (S06 D14), active consents first. Everyone with `person.read` sees the
 * scopes, dates and status; the guardian (name, relationship, contact) and the evidence are `PRIVATE` and shown only
 * from the private read (`privateDetails`, `person.read_private`). With their actions, a consent is recorded (for
 * anyone not known to be an adult), its evidence uploaded or replaced and its scopes withdrawn here (T7c-1b).
 */
export function ConsentTab({ person, privateDetails }: Readonly<{ person: Person; privateDetails: PersonPrivate | undefined }>) {
  const timeZone = useTenantTimeZone();
  const { can, unavailable } = useTenantWorkspace();
  const [dialog, setDialog] = useState<OpenDialog | null>(null);
  const [announcement, setAnnouncement] = useState("");
  // The consent to focus once the dialog has closed after a change (its heading).
  const focusAfter = useRef<string | null>(null);
  const consents = [...person.consents].sort(
    (a, b) => Number(b.status === "ACTIVE") - Number(a.status === "ACTIVE") || b.recordedAt.localeCompare(a.recordedAt),
  );

  const offered = (action: string) => can(action) || unavailable(action) !== null;
  const disabled = (action: string) =>
    unavailable(action) !== null ? { disabled: true, "aria-describedby": unavailableId } : {};
  const refusal =
    [consentActions.record, consentActions.evidence, consentActions.withdraw]
      .map((action) => unavailable(action))
      .find((reason) => reason !== null) ?? null;
  const closed = isClosedPerson(person);
  // Consent is recorded only for someone who isn't known to be an adult (T6a: PERSON_CONSENT_NOT_NEEDED).
  const recordable = person.ageStatus !== "ADULT" && offered(consentActions.record);

  const saved = (message: string, consentId: string) => {
    focusAfter.current = consentId;
    setAnnouncement(message);
    setDialog(null);
  };
  const focusChanged = (event: Event) => {
    const id = focusAfter.current;
    focusAfter.current = null;
    const heading = id ? document.getElementById(`consent-${id}`) : null;
    if (heading) {
      event.preventDefault();
      heading.focus();
    }
  };

  /** An active consent's evidence and withdrawal; a withdrawn one is history. */
  const consentButtons = (consent: Consent) => {
    if (consent.status !== "ACTIVE") {
      return null;
    }
    const when = `the consent given on ${formatDay(consent.givenOn)}`;
    const buttons: ReactNode[] = [];
    // No new evidence on an archived or deceased person (T6a); withdrawing stays possible.
    if (offered(consentActions.evidence) && !closed) {
      const label = consent.hasEvidence ? "Replace evidence" : "Upload evidence";
      buttons.push(
        <Button
          key="evidence"
          variant="outline"
          aria-label={`${label}: ${when}`}
          onClick={() => setDialog({ type: "evidence", consent })}
          {...disabled(consentActions.evidence)}
        >
          <FileUp aria-hidden="true" />
          {label}
        </Button>,
      );
    }
    if (offered(consentActions.withdraw)) {
      buttons.push(
        <Button
          key="withdraw"
          variant="outline"
          aria-label={`Withdraw: ${when}`}
          onClick={() => setDialog({ type: "withdraw", consent })}
          {...disabled(consentActions.withdraw)}
        >
          Withdraw
        </Button>,
      );
    }
    return buttons.length > 0 ? <div className="flex w-full flex-wrap gap-2 tablet:ml-auto tablet:w-auto">{buttons}</div> : null;
  };

  return (
    <>
      <p role="status" className="sr-only">
        {announcement}
      </p>
      {refusal ? <UnavailableNote id={unavailableId} reason={refusal} /> : null}

      <ViewerSection title="What is agreed now">
        <p>{scopeList(person.activeConsentScopes)}</p>
        {person.ageStatus !== "ADULT" ? (
          <p className="text-sm text-muted-foreground">
            {person.ageStatus === "MINOR"
              ? "A minor's public profile and photo are shown only with their guardian's consent."
              : "Without a date of birth this person is protected as a minor: their public profile and photo are shown only with a guardian's consent."}
          </p>
        ) : null}
      </ViewerSection>

      <ViewerSection title="Consent records">
        {recordable && closed ? (
          <p className="text-sm text-muted-foreground">
            {person.displayName} is {person.status === "DECEASED" ? "deceased" : "archived"}: no new consent can be
            recorded.
          </p>
        ) : null}
        {recordable && !closed ? (
          <div className="flex flex-wrap gap-2">
            <Button variant="outline" onClick={() => setDialog({ type: "record" })} {...disabled(consentActions.record)}>
              <Plus aria-hidden="true" />
              Record consent
            </Button>
          </div>
        ) : null}
        {consents.length === 0 ? <p className="text-muted-foreground">No consent recorded yet.</p> : null}
        {consents.map((consent) => {
          const details = privateDetails?.consents.find((item) => item.id === consent.id);
          const evidence = details?.evidence ? (
            <a
              href={details.evidence.url}
              download
              className="inline-flex min-h-11 items-center gap-1 text-primary underline-offset-4 hover:underline"
            >
              <Download aria-hidden="true" className="size-4" />
              Download the signed form
            </a>
          ) : consent.hasEvidence ? (
            "On file"
          ) : (
            "None"
          );
          return (
            <article key={consent.id} className="grid gap-3 rounded-lg border p-4" aria-labelledby={`consent-${consent.id}`}>
              <header className="flex flex-wrap items-center gap-2">
                <h4
                  id={`consent-${consent.id}`}
                  tabIndex={-1}
                  className="font-semibold outline-none focus-visible:ring-2 focus-visible:ring-ring"
                >
                  Given on {formatDay(consent.givenOn)}
                </h4>
                <StatusBadge status={consent.status} />
                {consentButtons(consent)}
              </header>
              <Facts
                items={[
                  ["Agreed to", scopeList(consent.scopes)],
                  ...(details
                    ? ([
                        ["Guardian", details.guardianName],
                        ["Relationship", details.relationship ? humanizeCode(details.relationship) : "Not recorded"],
                        ["Contact", details.contact],
                      ] as const)
                    : []),
                  ["Evidence", evidence],
                  ["Recorded", formatDateTime(consent.recordedAt, timeZone)],
                  ...(consent.withdrawnAt
                    ? ([
                        ["Withdrawn", formatDateTime(consent.withdrawnAt, timeZone)],
                        ["Reason", consent.withdrawalReason ?? "No reason given"],
                      ] as const)
                    : []),
                ]}
              />
            </article>
          );
        })}
      </ViewerSection>

      {dialog?.type === "record" ? (
        <ConsentDialog
          person={person}
          onOpenChange={(open) => !open && setDialog(null)}
          onSaved={saved}
          onCloseAutoFocus={focusChanged}
        />
      ) : null}
      {dialog?.type === "evidence" ? (
        <EvidenceDialog
          person={person}
          consent={dialog.consent}
          onOpenChange={(open) => !open && setDialog(null)}
          onSaved={(message) => saved(message, dialog.consent.id)}
          onCloseAutoFocus={focusChanged}
        />
      ) : null}
      {dialog?.type === "withdraw" ? (
        <WithdrawConsentDialog
          person={person}
          consent={dialog.consent}
          onOpenChange={(open) => !open && setDialog(null)}
          onSaved={saved}
          onCloseAutoFocus={focusChanged}
        />
      ) : null}
    </>
  );
}

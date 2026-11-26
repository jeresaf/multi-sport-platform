"use client";

// The organisation's primary venue (S07 D4): optional, found by a search of the venue directory (`venue.read`). A
// closed venue is shown but can't be chosen (`ORGANISATION_PRIMARY_VENUE_UNAVAILABLE`); archived ones aren't listed.
// The venue the organisation already has stays chosen whatever its status (the update keeps it), until it is removed
// or another is chosen.
import { Button } from "@msp/ui/components/button";
import { Input } from "@msp/ui/components/input";
import { Label } from "@msp/ui/components/label";
import { X } from "lucide-react";
import { useEffect, useRef, useState } from "react";
import { Loading } from "@/components/loading";
import { ProblemAlert } from "@/components/problem-alert";
import { StatusBadge } from "@/components/status-badge";
import { humanizeCode } from "@/lib/codes";
import { useTenantWorkspace } from "@/lib/tenant-workspace";
import { useVenueChoices } from "./organisations-data";

const searchDelay = 300;
const id = "organisation-primary-venue";

/** Whether a venue may become an organisation's primary venue: not closed or archived (D4). */
const choosable = (status: string) => status !== "CLOSED" && status !== "ARCHIVED";

export function PrimaryVenueField({
  value,
  onChange,
  saved,
  error,
}: Readonly<{
  /** The chosen venue's ID ("" for none). */
  value: string;
  onChange: (venueId: string) => void;
  /** The venue the organisation has now (Edit), kept as a choice whatever its status. */
  saved?: { id: string; name: string } | null;
  error?: string;
}>) {
  const { can } = useTenantWorkspace();
  const canSearch = can("venue.read");
  const [typed, setTyped] = useState("");
  const [q, setQ] = useState("");
  const [chosenName, setChosenName] = useState<string | null>(null);
  // A choice removes the button that made it: the focus goes back to the search (or, without one, to the choice
  // shown), and the choice is announced, not the search it ended (pre-review 1, 2).
  const [said, setSaid] = useState("");
  const search = useRef<HTMLInputElement>(null);
  const shownChoice = useRef<HTMLDivElement>(null);
  const choose = (venueId: string, venueName: string | null, message: string) => {
    onChange(venueId);
    setChosenName(venueName);
    setTyped("");
    setQ("");
    setSaid(message);
    (search.current ?? shownChoice.current)?.focus();
  };
  useEffect(() => {
    const text = typed.trim();
    const timer = setTimeout(() => setQ(text), searchDelay);
    return () => clearTimeout(timer);
  }, [typed]);
  const choices = useVenueChoices(q);
  // The answer for the text shown: an earlier search's kept list is shown while the next is asked, but isn't
  // described as the answer to it.
  const found = q === "" ? [] : (choices.data?.items ?? []).filter((item) => item.id !== value);
  const searching = q !== "" && (choices.isPending || choices.isPlaceholderData);
  const answered = q !== "" && choices.isSuccess && !choices.isPlaceholderData;
  const announcement = answered ? (found.length === 0 ? "No venue found." : `${found.length} found.`) : said;
  const name = value === "" ? null : saved && saved.id === value ? saved.name : (chosenName ?? "The venue chosen");
  const errorId = error ? `${id}-error` : undefined;

  return (
    <fieldset className="grid gap-2" aria-describedby={[`${id}-hint`, errorId].filter(Boolean).join(" ")}>
      <legend className="mb-2 text-sm font-medium">Primary venue (optional)</legend>
      <p id={`${id}-hint`} className="text-sm text-muted-foreground">
        Where it is based or usually plays. Closed venues can&apos;t be chosen.
      </p>
      <div
        ref={shownChoice}
        tabIndex={-1}
        className="flex min-h-11 flex-wrap items-center gap-2 rounded-md outline-none focus-visible:ring-2 focus-visible:ring-ring"
      >
        {name ? (
          <>
            <span className="font-medium break-words">{name}</span>
            <Button type="button" variant="outline" onClick={() => choose("", null, "No primary venue.")}>
              <X aria-hidden="true" />
              No primary venue
            </Button>
          </>
        ) : (
          <span className="text-sm">None chosen.</span>
        )}
      </div>
      <p role="status" className="sr-only">
        {announcement}
      </p>
      {canSearch ? (
        <div className="grid gap-2">
          <Label htmlFor={`${id}-search`}>{name ? "Find another venue" : "Find a venue"}</Label>
          <Input
            ref={search}
            id={`${id}-search`}
            type="search"
            value={typed}
            onChange={(event) => {
              setTyped(event.target.value);
              setSaid("");
            }}
            placeholder="Name, alias or identifier"
            autoComplete="off"
          />
          {searching ? <Loading /> : null}
          {choices.isError ? <ProblemAlert error={choices.error} /> : null}
          {answered && found.length === 0 ? (
            <p className="text-sm text-muted-foreground">No venue found for “{q}”.</p>
          ) : null}
          {found.length > 0 ? (
            <ul className="grid gap-2" aria-label="Venues found">
              {found.map((item) => {
                const available = choosable(item.status);
                const detailId = `${id}-${item.id}-detail`;
                return (
                  <li key={item.id} className="flex min-h-11 flex-wrap items-center gap-x-3 gap-y-1">
                    <Button
                      type="button"
                      variant="outline"
                      disabled={!available}
                      aria-describedby={detailId}
                      onClick={() => choose(item.id, item.name, `${item.name} chosen as the primary venue.`)}
                    >
                      Choose {item.name}
                    </Button>
                    {item.status !== "ACTIVE" ? <StatusBadge status={item.status} /> : null}
                    <span id={detailId} className="text-sm text-muted-foreground">
                      {available
                        ? [humanizeCode(item.type), item.city].filter(Boolean).join(" · ")
                        : "Closed: it can't be a primary venue."}
                    </span>
                  </li>
                );
              })}
            </ul>
          ) : null}
        </div>
      ) : (
        <p className="text-sm text-muted-foreground">Choosing a venue needs access to the venues.</p>
      )}
      {error ? (
        // An alert: the server's refusal of the venue suppresses the form's alert, so it is read out here (pre-review 3).
        <p id={errorId} role="alert" className="text-sm text-destructive">
          {error}
        </p>
      ) : null}
    </fieldset>
  );
}

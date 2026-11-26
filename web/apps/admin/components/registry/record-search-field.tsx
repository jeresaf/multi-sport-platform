"use client";

// One registry record chosen by a search of its directory (S07 D4, D6, D23): an organisation's primary venue, a team's
// organisation or home venue. The record chosen is shown with a button that removes it; a search lists the first page
// of matches, each with **Choose**, and a record the server would refuse (a closed venue, a dissolved organisation) is
// shown but can't be chosen. The record saved before stays chosen whatever its status (the server keeps a reference
// it isn't asked to change, D6) until it is removed or another is chosen. Every word here is generic (AR-11).
import { Button } from "@msp/ui/components/button";
import { Input } from "@msp/ui/components/input";
import { Label } from "@msp/ui/components/label";
import type { UseQueryResult } from "@tanstack/react-query";
import { X } from "lucide-react";
import { useEffect, useRef, useState, type ReactNode } from "react";
import { Loading } from "@/components/loading";
import { ProblemAlert } from "@/components/problem-alert";
import { article } from "@/components/registry/duplicate-panel";
import { StatusBadge } from "@/components/status-badge";

/** How long the search waits after the last key, as the directories' search does. */
export const recordSearchDelay = 300;

type Found = { id: string; status: string };

export type RecordSearchFieldProps<T extends Found> = Readonly<{
  /** The field's element ID (the search, the hint, the error and each result's detail derive from it). */
  id: string;
  /** The record's noun ("venue"), for the search's words. */
  noun: string;
  /** The group's name ("Primary venue (optional)"). */
  legend: string;
  /** What the field is for and which records can't be chosen. */
  hint: ReactNode;
  /** The chosen record's ID ("" for none). */
  value: string;
  /** A choice or its removal, with the record chosen from the search (null when removed). */
  onChange: (id: string, chosen: T | null) => void;
  /** The record saved before (Edit), kept as a choice whatever its status. */
  saved?: { id: string; name: string } | null;
  error?: string;
  /** Whether the user may search the directory (its read action); otherwise `noAccess` is said instead. */
  canSearch: boolean;
  noAccess: string;
  /** The directory's first page for the text (asked only with text: "" asks nothing). */
  useChoices: (q: string) => UseQueryResult<{ items: readonly T[] }>;
  nameOf: (item: T) => string;
  /** Whether a found record may be chosen, and the words beside it (its facts, or why it can't be chosen). */
  describe: (item: T) => { available: boolean; detail: string };
  /** The removal button's words ("No primary venue"), also announced once it is pressed. */
  clearLabel: string;
  /** Announced when a record is chosen ("Bukoto Community Stadium chosen as the primary venue."). */
  chosenMessage: (name: string) => string;
  /** Said under the record chosen (the team's home venue notes a venue that doesn't list its sport). */
  note?: ReactNode;
}>;

export function RecordSearchField<T extends Found>({
  id,
  noun,
  legend,
  hint,
  value,
  onChange,
  saved,
  error,
  canSearch,
  noAccess,
  useChoices,
  nameOf,
  describe,
  clearLabel,
  chosenMessage,
  note,
}: RecordSearchFieldProps<T>) {
  const [typed, setTyped] = useState("");
  const [q, setQ] = useState("");
  const [chosenName, setChosenName] = useState<string | null>(null);
  // A choice removes the button that made it: the focus goes back to the search (or, without one, to the choice
  // shown), and the choice is announced, not the search it ended (#140 pre-review 1, 2).
  const [said, setSaid] = useState("");
  const search = useRef<HTMLInputElement>(null);
  const shownChoice = useRef<HTMLDivElement>(null);
  const choose = (chosen: T | null, message: string) => {
    onChange(chosen?.id ?? "", chosen);
    setChosenName(chosen ? nameOf(chosen) : null);
    setTyped("");
    setQ("");
    setSaid(message);
    (search.current ?? shownChoice.current)?.focus();
  };
  useEffect(() => {
    const text = typed.trim();
    const timer = setTimeout(() => setQ(text), recordSearchDelay);
    return () => clearTimeout(timer);
  }, [typed]);
  const choices = useChoices(q);
  // The answer for the text shown: an earlier search's kept list is shown while the next is asked, but isn't
  // described as the answer to it.
  const found = q === "" ? [] : (choices.data?.items ?? []).filter((item) => item.id !== value);
  const searching = q !== "" && (choices.isPending || choices.isPlaceholderData);
  const answered = q !== "" && choices.isSuccess && !choices.isPlaceholderData;
  const none = `No ${noun} found`;
  const announcement = answered ? (found.length === 0 ? `${none}.` : `${found.length} found.`) : said;
  const name = value === "" ? null : saved && saved.id === value ? saved.name : (chosenName ?? `The ${noun} chosen`);
  const errorId = error ? `${id}-error` : undefined;
  const plural = `${noun.charAt(0).toUpperCase()}${noun.slice(1)}s found`;

  return (
    <fieldset className="grid gap-2" aria-describedby={[`${id}-hint`, errorId].filter(Boolean).join(" ")}>
      <legend className="mb-2 text-sm font-medium">{legend}</legend>
      <p id={`${id}-hint`} className="text-sm text-muted-foreground">
        {hint}
      </p>
      <div
        ref={shownChoice}
        tabIndex={-1}
        className="flex min-h-11 flex-wrap items-center gap-2 rounded-md outline-none focus-visible:ring-2 focus-visible:ring-ring"
      >
        {name ? (
          <>
            <span className="font-medium break-words">{name}</span>
            <Button type="button" variant="outline" onClick={() => choose(null, `${clearLabel}.`)}>
              <X aria-hidden="true" />
              {clearLabel}
            </Button>
          </>
        ) : (
          <span className="text-sm">None chosen.</span>
        )}
      </div>
      {name && note ? <div className="text-sm text-muted-foreground">{note}</div> : null}
      <p role="status" className="sr-only">
        {announcement}
      </p>
      {canSearch ? (
        <div className="grid gap-2">
          <Label htmlFor={`${id}-search`}>{name ? `Find another ${noun}` : `Find ${article(noun)} ${noun}`}</Label>
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
            <p className="text-sm text-muted-foreground">
              {none} for “{q}”.
            </p>
          ) : null}
          {found.length > 0 ? (
            <ul className="grid gap-2" aria-label={plural}>
              {found.map((item) => {
                const { available: choosable, detail } = describe(item);
                // The saved record isn't checked again by the server, whatever its status now.
                const available = choosable || item.id === saved?.id;
                const detailId = `${id}-${item.id}-detail`;
                const itemName = nameOf(item);
                return (
                  <li key={item.id} className="flex min-h-11 flex-wrap items-center gap-x-3 gap-y-1">
                    <Button
                      type="button"
                      variant="outline"
                      disabled={!available}
                      aria-describedby={detailId}
                      // A long name wraps instead of widening the page on a phone (#140 R4).
                      className="h-auto min-h-11 max-w-full text-left whitespace-normal break-words"
                      onClick={() => choose(item, chosenMessage(itemName))}
                    >
                      Choose {itemName}
                    </Button>
                    {item.status !== "ACTIVE" ? <StatusBadge status={item.status} /> : null}
                    <span id={detailId} className="text-sm text-muted-foreground">
                      {detail}
                    </span>
                  </li>
                );
              })}
            </ul>
          ) : null}
        </div>
      ) : (
        <p className="text-sm text-muted-foreground">{noAccess}</p>
      )}
      {error ? (
        // An alert: the server's refusal of the record suppresses the form's alert, so it is read out here (#140 pre-review 3).
        <p id={errorId} role="alert" className="text-sm text-destructive">
          {error}
        </p>
      ) : null}
    </fieldset>
  );
}

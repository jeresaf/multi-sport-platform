// Names & IDs of a registry record (S07 D10, D23): an organisation's, a team's or a venue's aliases and external
// identifiers. Each is a child of its record, so every change sends the version its dialog opened on (`If-Match`) and
// answers the record, which replaces the one the page shows. The area supplies the calls (`NamesAndIdsApi`) and the
// words (`NamesAndIdsConfig`); the rules here are the API's (S06 D10, the same for every entity type).
import type { components } from "@msp/api-client";

export type AliasKind = components["schemas"]["AliasKind"];

export type RecordAlias = { id: string; name: string; kind: AliasKind };
export type RecordIdentifier = {
  id: string;
  authority: string;
  value: string;
  validFrom: string | null;
  validTo: string | null;
};

/** A record with Names & IDs: its current aliases, its identifiers (ended ones included) and its version. */
export type NamedRecord = {
  id: string;
  version: number;
  aliases: readonly RecordAlias[];
  externalIdentifiers: readonly RecordIdentifier[];
};

/** The record as a dialog first showed it: what a change is based on. */
export type OpenedRecord = { id: string; version: number };

export type NewIdentifier = { authority: string; value: string; validFrom: string | null };
export type IdentifierEnd = { validTo: string | null; reason: string | null };
export type NewAlias = { name: string; kind: AliasKind };

/** The area's calls, each answering the record as changed; `keep` puts it in the page's cache. */
export type NamesAndIdsApi<R extends NamedRecord> = {
  addIdentifier: (opened: OpenedRecord, body: NewIdentifier) => Promise<R>;
  endIdentifier: (opened: OpenedRecord, identifierId: string, body: IdentifierEnd) => Promise<R>;
  addAlias: (opened: OpenedRecord, body: NewAlias) => Promise<R>;
  removeAlias: (opened: OpenedRecord, aliasId: string, reason: string) => Promise<R>;
  keep: (record: R) => void;
  /** Reads the record again (after a concurrency conflict). */
  reload: () => unknown;
};

export type NamesAndIdsConfig = {
  /** The record's name, as titles say it ("Add an alias for Bukoto Community Stadium"). */
  name: string;
  /** What the record is, in words ("venue"). */
  noun: string;
  actions: { addIdentifier: string; endIdentifier: string; addAlias: string; removeAlias: string };
  /** Why the record takes no new alias or identifier (archived, closed…), or null when it takes them. */
  closedNote: string | null;
  /** The kinds the area's aliases take (a team's former names come only from a rename). */
  aliasKinds: readonly AliasKind[];
  /** The alias name's limit on the server (the record's own name's). */
  aliasNameMaxLength: number;
  /** Examples of an authority's code for this kind of record. */
  authorityExamples: string;
};

/** An issuer's code (S06 D10): 2–40 capital letters, digits or underscores, starting with a letter. */
export const authorityPattern = /^[A-Z][A-Z0-9_]{1,39}$/;
export const authorityMessage =
  "Use 2 to 40 capital letters, digits or underscores, starting with a letter, such as KMSA_GROUND_NO.";
export const identifierValueMaxLength = 100;
export const reasonMaxLength = 500;
/** Said for a start after the tenant's today: the identifier then couldn't be ended before that day (Q46). */
export const validFromLaterMessage = "An identifier can't start later than today.";

export const aliasKindLabels: Readonly<Record<AliasKind, string>> = {
  FORMER_NAME: "Former name",
  NICKNAME: "Nickname",
  SPELLING: "Another spelling",
};

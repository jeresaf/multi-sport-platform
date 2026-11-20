// Who may edit a person's details (owner, S06-T7c-2b plan): the update replaces the whole biodata, the date of birth
// included, which only the private read gives, and a change to the names runs the duplicate check. Every role that holds
// `person.update` holds the other two today (S06 D21, Q42).
export const editDetailsActions = ["person.update", "person.read_private", "person.duplicate_check"] as const;

/**
 * Whether Edit details is open to the user (`allowed`), or shown disabled with the reason it can't be used now (each
 * action held or only unavailable, a read-only tenant say); with neither, it isn't offered (pre-review m3).
 */
export function editDetailsAccess(
  can: (action: string) => boolean,
  unavailable: (action: string) => string | null,
): { allowed: boolean; reason: string | null } {
  if (editDetailsActions.every((action) => can(action))) {
    return { allowed: true, reason: null };
  }
  const offered = editDetailsActions.every((action) => can(action) || unavailable(action) !== null);
  const reason = offered ? editDetailsActions.map((action) => unavailable(action)).find((item) => item !== null) : null;
  return { allowed: false, reason: reason ?? null };
}
